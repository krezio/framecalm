namespace Framecalm.Core;

public enum Bottleneck { Unknown, Cpu, Gpu, Capped, Mixed }

/// <summary>One frame from a capture (PresentMon) or a demo. Times in milliseconds.</summary>
public readonly record struct FrameSample(double FrameTime, double GpuBusy, double CpuBusy, double GpuClock = 0, double GpuTemp = 0, double CpuClock = 0);

public sealed class Capture
{
    public required IReadOnlyList<FrameSample> Frames { get; init; }
    public CaptureSource Source { get; init; } = CaptureSource.Measured;
    public string Process { get; init; } = "";
    public DateTime Taken { get; init; } = DateTime.Now;
    public double Seconds => Frames.Sum(f => f.FrameTime) / 1000.0;
    public RunStats Stats => RunStats.From(Frames.Select(f => f.FrameTime).ToArray());
}

public sealed record RunStats(double AvgFps, double MedianMs, double P95Ms, double P99Ms, int Frames)
{
    public double OnePercentLowFps => P99Ms > 0 ? 1000 / P99Ms : 0;

    public static RunStats From(double[] ft)
    {
        if (ft.Length == 0) return new(0, 0, 0, 0, 0);
        var sorted = ft.Where(x => x > 0 && double.IsFinite(x)).OrderBy(x => x).ToArray();
        if (sorted.Length == 0) return new(0, 0, 0, 0, 0);
        // average FPS = frames / total time (not the mean of per-frame FPS, which overstates it)
        var avg = sorted.Length / (sorted.Sum() / 1000.0);
        return new(avg, Pct(sorted, 50), Pct(sorted, 95), Pct(sorted, 99), sorted.Length);
    }

    public static double Pct(double[] sorted, double p)
    {
        if (sorted.Length == 1) return sorted[0];
        var rank = p / 100.0 * (sorted.Length - 1);
        var lo = (int)Math.Floor(rank);
        var hi = Math.Min(lo + 1, sorted.Length - 1);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (rank - lo);
    }
}

/// <summary>Three matched runs before vs three after. Honest about noise.</summary>
public sealed class Comparison
{
    public required IReadOnlyList<RunStats> Before { get; init; }
    public required IReadOnlyList<RunStats> After { get; init; }

    public double BeforeFps => Before.Average(r => r.AvgFps);
    public double AfterFps => After.Average(r => r.AvgFps);
    public double BeforeP99 => Before.Average(r => r.P99Ms);
    public double AfterP99 => After.Average(r => r.P99Ms);
    public double BeforeMedian => Before.Average(r => r.MedianMs);
    public double AfterMedian => After.Average(r => r.MedianMs);
    public double FpsDelta => AfterFps - BeforeFps;
    public double FpsDeltaPct => BeforeFps > 0 ? FpsDelta / BeforeFps * 100 : 0;

    static double Sd(IEnumerable<double> xs)
    {
        var a = xs.ToArray();
        if (a.Length < 2) return 0;
        var m = a.Average();
        return Math.Sqrt(a.Sum(x => (x - m) * (x - m)) / (a.Length - 1));
    }

    /// <summary>Spread between runs, as a share of the average. Above ~5% the scene wasn't repeatable enough.</summary>
    public double RunToRunNoisePct => Math.Max(Sd(Before.Select(r => r.AvgFps)) / Math.Max(1, BeforeFps), Sd(After.Select(r => r.AvgFps)) / Math.Max(1, AfterFps)) * 100;

    /// <summary>A change counts only if it beats 2× the run-to-run spread and 3%.</summary>
    public bool IsReal => Math.Abs(FpsDeltaPct) > Math.Max(3, 2 * RunToRunNoisePct);

    public string Verdict()
    {
        if (!IsReal)
            return L.F("Within noise. The difference ({0:+0.0;-0.0}%) is smaller than how much your runs varied ({1:0.0}%). Nothing measurable changed.", FpsDeltaPct, RunToRunNoisePct);
        var smooth = AfterP99 < BeforeP99 * 0.9 ? " " + L.F("Stutter dropped too: your slowest 1% of frames went from {0:0.0} ms to {1:0.0} ms.", BeforeP99, AfterP99) : "";
        return FpsDelta > 0
            ? L.F("Faster by {0:0} FPS on average ({1:+0}%), measured over 3 runs each.", FpsDelta, FpsDeltaPct) + smooth
            : L.F("Slower by {0:0} FPS ({1:0}%). Undo is one click, and we'd suggest it.", -FpsDelta, FpsDeltaPct);
    }
}

public sealed record BottleneckResult(Bottleneck Kind, double GpuBusyShare, double FrameMs, double GpuMs, double CpuMs, string Headline, string Detail);

public static class BottleneckAnalyzer
{
    public static BottleneckResult Analyze(IReadOnlyList<FrameSample> frames, int refreshHz, bool capLikely = false)
    {
        var withGpu = frames.Where(f => f.GpuBusy > 0 && f.FrameTime > 0).ToArray();
        if (withGpu.Length < 30)
            return new(Bottleneck.Unknown, 0, 0, 0, 0, "Not enough data",
                "The capture didn't include GPU timing, so we can't tell what limits this game yet. Try a longer capture in the same scene.");

        var ft = RunStats.Pct(withGpu.Select(f => f.FrameTime).OrderBy(x => x).ToArray(), 50);
        var gpu = RunStats.Pct(withGpu.Select(f => f.GpuBusy).OrderBy(x => x).ToArray(), 50);
        var cpu = withGpu.Any(f => f.CpuBusy > 0) ? RunStats.Pct(withGpu.Select(f => f.CpuBusy).OrderBy(x => x).ToArray(), 50) : 0;
        var share = Math.Clamp(gpu / ft, 0, 1);
        var fps = 1000 / ft;

        if (capLikely || (Math.Abs(fps - refreshHz) < 2 && share < 0.8))
            return new(Bottleneck.Capped, share, ft, gpu, cpu, "Held back by a frame cap",
                L.F("You're sitting right at {0} FPS with the GPU only busy {1:P0} of each frame. A frame cap or V-Sync is the limit, not your hardware.", Math.Round(fps), share));
        if (share >= 0.85)
            return new(Bottleneck.Gpu, share, ft, gpu, cpu, "Your graphics card sets the pace",
                L.F("The GPU is busy {0:P0} of every frame. Shadows, effects, anti-aliasing and render scale matter most here; view distance and simulation barely move it.", share));
        if (share <= 0.65)
            return new(Bottleneck.Cpu, share, ft, gpu, cpu, "Your CPU sets the pace",
                L.F("The GPU is only busy {0:P0} of each frame and waits on the CPU the rest. Lowering shadows won't help much: reduce view/render distance, simulation and crowd settings instead.", share));
        return new(Bottleneck.Mixed, share, ft, gpu, cpu, "CPU and GPU are close",
            L.F("The GPU is busy {0:P0} of each frame, so both sides matter. Expect smaller gains from any single setting.", share));
    }

    /// <summary>Clock or temperature sagging through a capture while load stays the same suggests throttling.</summary>
    public static string? Throttling(IReadOnlyList<FrameSample> frames)
    {
        if (frames.Count < 300) return null;
        var third = frames.Count / 3;
        double Avg(IEnumerable<FrameSample> s, Func<FrameSample, double> f) { var a = s.Select(f).Where(x => x > 0).ToArray(); return a.Length == 0 ? 0 : a.Average(); }
        var first = frames.Take(third).ToArray();
        var last = frames.Skip(frames.Count - third).ToArray();
        var g0 = Avg(first, f => f.GpuClock); var g1 = Avg(last, f => f.GpuClock);
        var c0 = Avg(first, f => f.CpuClock); var c1 = Avg(last, f => f.CpuClock);
        var t1 = Avg(last, f => f.GpuTemp);
        if (g0 > 0 && g1 < g0 * 0.85)
            return t1 > 0 ? L.F("GPU clock fell {0:P0} during the run while it reached {1:0}°C. That's thermal or power throttling: clean the vents, raise the laptop's back, or use a cooling pad.", 1 - g1 / g0, t1)
                          : L.F("GPU clock fell {0:P0} during the run. That's thermal or power throttling: clean the vents, raise the laptop's back, or use a cooling pad.", 1 - g1 / g0);
        if (c0 > 0 && c1 < c0 * 0.85)
            return L.F("CPU clock fell {0:P0} during the run. Likely heat or a power limit. Plugging in a laptop and keeping vents clear usually fixes it.", 1 - c1 / c0);
        return null;
    }
}
