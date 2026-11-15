using System.Text.Json.Serialization;

namespace Framecalm.Core;

public enum Launcher { Steam, Epic, Riot, BattleNet, Xbox, Minecraft, Roblox, Other }

/// <summary>Automatic = Framecalm edits the game's config. Guided = exact in-game steps only. Basic = GPU preference and Game Session only.</summary>
public enum SupportLevel { Automatic, Guided, Basic }

public enum Profile { Light, Balanced, Performance, MaxFps, Custom }

public enum Tier { Low, Mid, High }

public enum GpuKind { Integrated, Dedicated, Unknown }

public sealed record GameInstall
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required Launcher Launcher { get; init; }
    public string? InstallDir { get; init; }
    public string? ExePath { get; init; }
    public string? ProcessName { get; init; }
    public string? ArtPath { get; init; }
    /// <summary>Where official cover art can be fetched from: "steam:&lt;appid&gt;" or "store:&lt;Microsoft Store id&gt;".</summary>
    public string? Cover { get; init; }
    public string? ConfigPath { get; init; }
    public SupportLevel Support { get; init; } = SupportLevel.Basic;
    public GameDefinition? Definition { get; init; }
}

public sealed record GpuInfo(string Name, string Vendor, long VramBytes, GpuKind Kind, string? DriverVersion, DateTime? DriverDate)
{
    public string ShortName => Name.Replace("NVIDIA ", "").Replace("GeForce ", "").Replace("AMD ", "").Replace("Radeon(TM) ", "Radeon ")
        .Replace("Intel(R) ", "Intel ").Replace(" GPU", "").Trim();
}

public sealed record HardwareInfo
{
    public required string Cpu { get; init; }
    public int Threads { get; init; }
    public long RamBytes { get; init; }
    public IReadOnlyList<GpuInfo> Gpus { get; init; } = [];
    public bool IsLaptop { get; init; }
    public int RefreshHz { get; init; } = 60;
    public int ScreenWidth { get; init; }
    public int ScreenHeight { get; init; }

    public GpuInfo? Dedicated => Gpus.FirstOrDefault(g => g.Kind == GpuKind.Dedicated);
    public GpuInfo? Integrated => Gpus.FirstOrDefault(g => g.Kind == GpuKind.Integrated);
    public GpuInfo? Primary => Dedicated ?? Gpus.FirstOrDefault();
    public bool IsHybrid => Dedicated != null && Integrated != null;

    public Tier GpuTier => Primary switch
    {
        null => Tier.Low,
        { Kind: GpuKind.Integrated } => Tier.Low,
        { VramBytes: >= 10L << 30 } => Tier.High,
        { VramBytes: >= 6L << 30 } => Tier.Mid,
        _ => Tier.Low,
    };

    public Tier CpuTier => Threads >= 16 ? Tier.High : Threads >= 8 ? Tier.Mid : Tier.Low;

    public string CpuShort => Cpu.Replace("(R)", "").Replace("(TM)", "").Replace(" with Radeon Graphics", "")
        .Replace("AMD ", "").Replace("Intel ", "").Replace("Core ", "").Replace(" Processor", "")
        .Replace("  ", " ").Split('@')[0].Trim();
}

// ---------------- game definitions (loaded from validated JSON) ----------------

public enum SettingKind { Int, Float, Bool, Enum }

/// <summary>Which hardware a setting mostly costs: helps the bottleneck check say what matters.</summary>
public enum Load { Cpu, Gpu, Both }

public sealed class SettingDef
{
    public required string Key { get; init; }
    public string? Section { get; init; }
    public required string Label { get; init; }
    public SettingKind Kind { get; init; }
    public string? Unit { get; init; }
    /// <summary>For Enum: values ordered from fastest to prettiest. For Bool: "false" or "true" is the faster value via Faster.</summary>
    public string[]? Order { get; init; }
    public Dictionary<string, string>? ValueLabels { get; init; }
    /// <summary>"lower", "higher", "false", "true" or "order" (use Order).</summary>
    public string Faster { get; init; } = "lower";
    /// <summary>"refresh" = a frame cap that should match the monitor (may go up, that's the point).</summary>
    public string? Special { get; init; }
    public Dictionary<string, string> Targets { get; init; } = new();
    public Load Load { get; init; } = Load.Gpu;
    /// <summary>1 (barely visible) .. 5 (big visual change).</summary>
    public int VisualCost { get; init; } = 2;
    /// <summary>Typical FPS gain in percent, from public testing. Always shown as "typical", never as measured.</summary>
    public double TypicalGain { get; init; }
    public required string Why { get; init; }
    public required string Tradeoff { get; init; }
    /// <summary>Opt-in only categories: resolution, sensitivity, keybinds, audio.</summary>
    public bool OptIn { get; init; }
    /// <summary>Only applied to the config when the value already exists in the file (never inserts unknown keys).</summary>
    public bool RequireExisting { get; init; } = true;
}

public sealed class VersionRule
{
    public string? Key { get; init; }
    public string? Section { get; init; }
    public long Min { get; init; }
    public long Max { get; init; }
    public string[]? RequiredKeys { get; init; }
    public string? RequiredSection { get; init; }
}

public sealed class GuidedStep
{
    public required string Where { get; init; }
    public required string Set { get; init; }
    public required string Why { get; init; }
    public Load Load { get; init; } = Load.Gpu;
}

public sealed class GameDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public SupportLevel Support { get; init; }
    public string? Adapter { get; init; }
    public string? Cover { get; init; }
    public string[] ProcessNames { get; init; } = [];
    public string[] SteamIds { get; init; } = [];
    public string[] EpicIds { get; init; } = [];
    public string[] RiotIds { get; init; } = [];
    public string[] NameMatches { get; init; } = [];
    /// <summary>Config path with %LOCALAPPDATA%, %APPDATA%, %DOCUMENTS% tokens.</summary>
    public string? ConfigPath { get; init; }
    public VersionRule? Version { get; init; }
    public List<SettingDef> Settings { get; init; } = [];
    public List<GuidedStep> Guided { get; init; } = [];
    /// <summary>Strict kernel anti-cheat: never touch the process (no priority changes either).</summary>
    public bool StrictAntiCheat { get; init; }
    public string? AntiCheat { get; init; }
    public string? Notes { get; init; }
    public string? RestartNote { get; init; }
}

// ---------------- proposals ----------------

public sealed record ProposedChange(SettingDef Setting, string Old, string New)
{
    public string OldLabel => Setting.Show(Old);
    public string NewLabel => Setting.Show(New);
}

public static class SettingText
{
    public static string Show(this SettingDef s, string value)
    {
        value = value.Trim().Trim('"');
        if (s.ValueLabels != null && s.ValueLabels.TryGetValue(value, out var l)) return l;
        if (s.Kind == SettingKind.Bool) return value.Equals("true", StringComparison.OrdinalIgnoreCase) ? "On" : "Off";
        if (s.Kind == SettingKind.Float && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var d))
            value = d.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
        if (s.Unit is not { Length: > 0 } u) return value;
        // "1 blocks" reads wrong: singular unit for exactly one
        if (value == "1" && u.EndsWith('s') && !u.EndsWith("ss")) u = u[..^1];
        return $"{value} {u}";
    }
}

// ---------------- change history ----------------

public enum TargetKind { ConfigFile, Registry, StartupItem, PowerPlan, Display, Tweak }

public enum ChangeStatus { Applied, Undone, Kept }

public sealed class ChangeRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    public string BatchId { get; set; } = "";
    public string Scope { get; set; } = "";        // game id or "windows"
    public string ScopeName { get; set; } = "";
    public TargetKind Kind { get; set; }
    public string Target { get; set; } = "";       // file path, registry key, startup item name
    public string Key { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Old { get; set; }
    public string? New { get; set; }
    public string? OldLabel { get; set; }
    public string? NewLabel { get; set; }
    public DateTime AppliedAt { get; set; } = DateTime.Now;
    public ChangeStatus Status { get; set; }
    public DateTime? UndoneAt { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CaptureSource { Measured, Demo }
