using System.Diagnostics;
using System.Globalization;
using Framecalm.Core;

namespace Framecalm.Benchmarks;

/// <summary>
/// Frame capture with Intel's open-source PresentMon (ETW based: it reads Windows' own presentation events,
/// nothing is injected into the game). The exact version is pinned in tools/presentmon.json and checked by hash.
/// Capturing needs admin, so it runs through Framecalm.ElevatedHelper.
/// </summary>
public sealed class PresentMonRunner : IBenchmarkRunner
{
    public const string PinnedVersion = "2.6.0";
    public static string CaptureDir => Path.Combine(Paths.State, "captures");

    readonly string _appDir = AppContext.BaseDirectory;

    public string? ToolPath => new[] { Path.Combine(_appDir, "PresentMon.exe"), Path.Combine(_appDir, "tools", "PresentMon.exe") }.FirstOrDefault(File.Exists);
    string? HelperPath => new[] { Path.Combine(_appDir, "Framecalm.ElevatedHelper.exe") }.FirstOrDefault(File.Exists);

    public bool Available => ToolPath != null && HelperPath != null;
    public string? UnavailableReason => ToolPath is null ? "PresentMon isn't next to Framecalm. The installer and portable zip include it." : HelperPath is null ? "Framecalm.ElevatedHelper.exe is missing." : null;

    public async Task<Capture> CaptureAsync(string processName, int seconds, IProgress<double>? progress, CancellationToken ct)
    {
        if (!Available) throw new InvalidOperationException(UnavailableReason);
        Directory.CreateDirectory(CaptureDir);
        var csv = Path.Combine(CaptureDir, $"{DateTime.Now:yyyyMMdd-HHmmss}-{Sanitize(processName)}.csv");
        var psi = new ProcessStartInfo(HelperPath!, $"capture --process \"{processName}\" --seconds {seconds} --out \"{csv}\"")
        {
            UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden,
        };
        using var p = Process.Start(psi) ?? throw new InvalidOperationException("Couldn't start the capture helper.");
        var sw = Stopwatch.StartNew();
        while (!p.HasExited)
        {
            ct.ThrowIfCancellationRequested();
            progress?.Report(Math.Min(0.99, sw.Elapsed.TotalSeconds / seconds));
            await Task.Delay(200, ct);
        }
        if (p.ExitCode != 0 || !File.Exists(csv)) throw new InvalidOperationException($"Capture didn't finish (code {p.ExitCode}). Is {processName} running?");
        progress?.Report(1);
        return new Capture { Frames = Parse(File.ReadAllLines(csv)), Source = CaptureSource.Measured, Process = processName };
    }

    static string Sanitize(string s) => new(s.Where(char.IsLetterOrDigit).ToArray());

    /// <summary>Reads PresentMon CSV (1.x "MsBetweenPresents" or 2.x "FrameTime"/"MsBetweenDisplayChange" columns).</summary>
    public static List<FrameSample> Parse(IReadOnlyList<string> lines)
    {
        var list = new List<FrameSample>();
        if (lines.Count < 2) return list;
        var head = lines[0].Split(',').Select(h => h.Trim()).ToList();
        int Col(params string[] names) { foreach (var n in names) { var i = head.FindIndex(h => h.Equals(n, StringComparison.OrdinalIgnoreCase)); if (i >= 0) return i; } return -1; }
        var ft = Col("FrameTime", "MsBetweenPresents", "msBetweenPresents");
        var gpu = Col("GPUBusy", "MsGPUBusy", "msGPUActive", "MsGPUActive");
        var cpu = Col("CPUBusy", "MsCPUBusy");
        var gclk = Col("GPUFrequency", "GPUFrequency(MHz)");
        var gtmp = Col("GPUTemperature", "GPUTemperature(C)");
        var cclk = Col("CPUFrequency", "CPUFrequency(MHz)");
        var dropped = Col("Dropped");
        if (ft < 0) throw new InvalidDataException("This CSV has no frame time column.");
        double D(string[] c, int i) => i >= 0 && i < c.Length && double.TryParse(c[i], NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var c = line.Split(',');
            var t = D(c, ft);
            if (t <= 0 || t > 5000) continue;
            list.Add(new FrameSample(t, D(c, gpu), D(c, cpu), D(c, gclk), D(c, gtmp), D(c, cclk)));
        }
        return list;
    }
}
