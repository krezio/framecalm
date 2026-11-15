namespace Framecalm.Core;

/// <summary>Named capabilities of the app. Everything is included today; this is the one place
/// a future edition check would go, so features never need to be rewired to gate them.</summary>
public enum Feature { GameTuning, AutoTune, Benchmarks, GameSession, SystemFixes, StartupApps, History }

public static class Features
{
    /// <summary>Decides whether a feature is available. Replace to add editions later.</summary>
    public static Func<Feature, bool> Policy { get; set; } = _ => true;
    public static bool Has(Feature f) => Policy(f);
}
