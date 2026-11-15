using System.Globalization;

namespace Framecalm.Core;

/// <summary>Turns a profile + this PC's hardware into proposed changes. Never makes anything prettier/heavier,
/// never touches opt-in settings (resolution, sensitivity, keybinds, audio) unless asked.</summary>
public static class Recommender
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string ProfileKey(Profile p) => p switch
    {
        Profile.Light => "light",
        Profile.Balanced => "balanced",
        Profile.Performance => "performance",
        Profile.MaxFps => "maxfps",
        _ => "balanced",
    };

    /// <summary>Strong hardware gets a gentler version of the profile, weak hardware a stronger one.</summary>
    public static Profile Effective(Profile p, HardwareInfo hw, SettingDef s)
    {
        if (p is Profile.Custom or Profile.MaxFps) return p;
        var tier = s.Load == Load.Cpu ? hw.CpuTier : s.Load == Load.Gpu ? hw.GpuTier : (Tier)Math.Min((int)hw.CpuTier, (int)hw.GpuTier);
        var i = (int)p + (tier == Tier.High ? -1 : tier == Tier.Low ? 1 : 0);
        return (Profile)Math.Clamp(i, 0, (int)Profile.MaxFps);
    }

    public static List<ProposedChange> Propose(GameDefinition def, Func<SettingDef, string?> current, HardwareInfo hw, Profile profile,
        ISet<string>? optIns = null, IDictionary<string, string>? custom = null)
    {
        var list = new List<ProposedChange>();
        foreach (var s in def.Settings)
        {
            if (s.OptIn && optIns?.Contains(s.Key) != true) continue;
            var cur = current(s);
            if (cur is null) continue;
            string? target;
            if (s.Special == "refresh")
            {
                target = CapFor(s, hw.RefreshHz, cur);
            }
            else if (profile == Profile.Custom)
            {
                if (custom == null || !custom.TryGetValue(s.Key, out target)) continue;
            }
            else
            {
                var eff = Effective(profile, hw, s);
                if (!s.Targets.TryGetValue(ProfileKey(eff), out target)) continue;
            }
            if (target is null || Same(s, cur, target)) continue;
            if (s.Special != "refresh" && !IsFaster(s, target, cur)) continue; // never raise a setting
            list.Add(new ProposedChange(s, cur, target));
        }
        return list;
    }

    /// <summary>Frame caps: match the screen. Raising a 60 cap on a 144 Hz screen is a real fix, so this one may go up.
    /// "Unlimited" values are left alone (they are already uncapped).</summary>
    static string? CapFor(SettingDef s, int hz, string cur)
    {
        if (!double.TryParse(cur, NumberStyles.Float, Inv, out var c)) return null;
        if (s.Targets.TryGetValue("unlimited", out var unl) && double.TryParse(unl, NumberStyles.Float, Inv, out var u) && c >= u) return null;
        if (c == 0) return null; // 0 = unlimited in many games
        var want = (double)Math.Max(60, hz);
        if (Math.Abs(c - want) < 0.5) return null;
        if (c > want) return null; // a higher cap than the screen is the user's choice, don't lower it
        return s.Kind == SettingKind.Float ? want.ToString("0.000000", Inv) : want.ToString("0", Inv);
    }

    public static bool Same(SettingDef s, string a, string b) =>
        s.Kind switch
        {
            SettingKind.Int or SettingKind.Float when double.TryParse(a, NumberStyles.Float, Inv, out var x) && double.TryParse(b, NumberStyles.Float, Inv, out var y) => Math.Abs(x - y) < 1e-6,
            _ => string.Equals(a.Trim('"'), b.Trim('"'), StringComparison.OrdinalIgnoreCase),
        };

    /// <summary>True if <paramref name="candidate"/> is strictly cheaper to render than <paramref name="current"/>.</summary>
    public static bool IsFaster(SettingDef s, string candidate, string current)
    {
        switch (s.Faster)
        {
            case "lower" or "higher":
                if (!double.TryParse(candidate, NumberStyles.Float, Inv, out var a) || !double.TryParse(current, NumberStyles.Float, Inv, out var b)) return false;
                return s.Faster == "lower" ? a < b : a > b;
            case "false" or "true":
                return string.Equals(candidate, s.Faster, StringComparison.OrdinalIgnoreCase) && !string.Equals(current, s.Faster, StringComparison.OrdinalIgnoreCase);
            case "order":
                if (s.Order is null) return false;
                var ci = Array.FindIndex(s.Order, o => string.Equals(o, candidate.Trim('"'), StringComparison.OrdinalIgnoreCase));
                var ui = Array.FindIndex(s.Order, o => string.Equals(o, current.Trim('"'), StringComparison.OrdinalIgnoreCase));
                return ci >= 0 && ui >= 0 && ci < ui;
            default:
                return false;
        }
    }
}

/// <summary>Plans auto-tune: biggest FPS gain per visual cost first, weighted by what limits this PC.</summary>
public static class AutoTunePlanner
{
    public static double Relevance(SettingDef s, Bottleneck b) => (b, s.Load) switch
    {
        (Bottleneck.Cpu, Load.Cpu) => 1.0,
        (Bottleneck.Cpu, Load.Both) => 0.8,
        (Bottleneck.Cpu, Load.Gpu) => 0.15,
        (Bottleneck.Gpu, Load.Gpu) => 1.0,
        (Bottleneck.Gpu, Load.Both) => 0.8,
        (Bottleneck.Gpu, Load.Cpu) => 0.2,
        _ => 0.7,
    };

    public static double Score(SettingDef s, Bottleneck b) => s.TypicalGain * Relevance(s, b) / Math.Max(1, s.VisualCost);

    /// <summary>Candidate changes (from the Max FPS profile) ordered by score. Frame-cap fixes always go first.</summary>
    public static List<ProposedChange> Order(IEnumerable<ProposedChange> candidates, Bottleneck b) =>
        candidates.OrderByDescending(c => c.Setting.Special == "refresh" ? double.MaxValue : Score(c.Setting, b)).ToList();

    /// <summary>Typical FPS after a set of changes (multiplicative, damped). Used only as an estimate between measured runs.</summary>
    public static double Estimate(double fps, IEnumerable<ProposedChange> applied, Bottleneck b) =>
        applied.Aggregate(fps, (f, c) => f * (1 + c.Setting.TypicalGain / 100.0 * Relevance(c.Setting, b) * 0.85));

    /// <summary>Splits ordered changes into up to <paramref name="maxRounds"/> rounds. Each round closes an even share of the
    /// remaining gap (by estimate), so the cheapest changes get measured first and later rounds only happen if they're needed.</summary>
    public static List<List<ProposedChange>> Rounds(List<ProposedChange> ordered, double fps, double target, Bottleneck b, int maxRounds = 3)
    {
        var rounds = new List<List<ProposedChange>>();
        var i = 0;
        var est = fps;
        while (i < ordered.Count && rounds.Count < maxRounds)
        {
            var left = maxRounds - rounds.Count;
            var goal = left == 1 ? target : est + (target - est) / left;
            var round = new List<ProposedChange>();
            while (i < ordered.Count)
            {
                round.Add(ordered[i]);
                est = Estimate(est, [ordered[i]], b);
                i++;
                if (est >= goal) break;
            }
            rounds.Add(round);
            if (est >= target) break;
        }
        return rounds;
    }
}
