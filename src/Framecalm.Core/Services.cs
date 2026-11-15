using System.Text.Json;
using System.Text.Json.Serialization;

namespace Framecalm.Core;

public interface IHardwareProbe { HardwareInfo Probe(); }

public interface IGameDiscovery { IReadOnlyList<GameInstall> Discover(IReadOnlyList<GameDefinition> catalog); }

/// <summary>Result of checking a config file before touching it. Anything but Ok means guided mode (fail closed).</summary>
public sealed record ConfigCheck(bool Ok, string Reason, string? Version = null)
{
    public static ConfigCheck Fine(string? version) => new(true, "Recognised format", version);
    public static ConfigCheck Fail(string reason) => new(false, reason);
}

/// <summary>Reads and edits one config format, line by line, keeping everything it doesn't own exactly as it was.</summary>
public interface IConfigAdapter
{
    string Id { get; }
    ConfigCheck Check(string text, GameDefinition def);
    string? Get(string text, SettingDef s);
    string Set(string text, SettingDef s, string value);
}

/// <summary>Something a change can be applied to and read back from: a config file, a registry key, a startup entry.</summary>
public interface IChangeTarget
{
    TargetKind Kind { get; }
    string Path { get; }
    bool IsFile { get; }
    string? Read(string key);
    void Write(IReadOnlyList<(string Key, string? Value)> values);
}

public interface IBenchmarkRunner
{
    bool Available { get; }
    string? UnavailableReason { get; }
    Task<Capture> CaptureAsync(string processName, int seconds, IProgress<double>? progress, CancellationToken ct);
}

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };
}

public static class Paths
{
    public static string Expand(string p) => p
        .Replace("%LOCALAPPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), StringComparison.OrdinalIgnoreCase)
        .Replace("%APPDATA%", Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), StringComparison.OrdinalIgnoreCase)
        .Replace("%DOCUMENTS%", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), StringComparison.OrdinalIgnoreCase)
        .Replace("%USERPROFILE%", Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase);

    public static string State { get; } = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Framecalm");
}
