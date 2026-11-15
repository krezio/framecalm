using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Framecalm.Core;

namespace Framecalm.GameAdapters;

/// <summary>Minecraft Java "options.txt": one "key:value" per line, with a "version" line holding the data version.</summary>
public sealed class MinecraftOptionsAdapter : IConfigAdapter
{
    public string Id => "minecraft-options";

    static int Find(string[] lines, string key) => Array.FindIndex(lines, l => l.StartsWith(key + ":", StringComparison.Ordinal));

    static string[] Lines(string text) => text.Split('\n');

    public ConfigCheck Check(string text, GameDefinition def)
    {
        var lines = Lines(text);
        if (lines.Count(l => l.Contains(':')) < 10) return ConfigCheck.Fail("This doesn't look like a Minecraft options.txt.");
        var v = def.Version;
        string? version = null;
        if (v?.Key != null)
        {
            var i = Find(lines, v.Key);
            if (i < 0) return ConfigCheck.Fail("options.txt has no version line.");
            version = lines[i][(v.Key.Length + 1)..].Trim();
            if (!long.TryParse(version, out var n)) return ConfigCheck.Fail($"Unreadable version '{version}'.");
            if (n < v.Min || n > v.Max) return ConfigCheck.Fail($"Minecraft data version {n} isn't one we've tested ({v.Min}–{v.Max}).");
        }
        foreach (var k in v?.RequiredKeys ?? [])
            if (Find(lines, k) < 0) return ConfigCheck.Fail($"Expected setting '{k}' is missing.");
        return ConfigCheck.Fine(version);
    }

    public string? Get(string text, SettingDef s)
    {
        var lines = Lines(text);
        var i = Find(lines, s.Key);
        return i < 0 ? null : lines[i][(s.Key.Length + 1)..].TrimEnd('\r').Trim();
    }

    public string Set(string text, SettingDef s, string value)
    {
        var lines = Lines(text);
        var i = Find(lines, s.Key);
        if (i < 0) throw new InvalidOperationException($"'{s.Key}' isn't in options.txt.");
        var old = lines[i][(s.Key.Length + 1)..];
        var cr = old.EndsWith('\r') ? "\r" : "";
        var quoted = old.TrimEnd('\r').Trim().StartsWith('"');
        var v = value.Trim('"');
        lines[i] = $"{s.Key}:{(quoted ? $"\"{v}\"" : v)}{cr}";
        return string.Join('\n', lines);
    }
}

/// <summary>Unreal-style INI ("[Section]" + "Key=Value"). Edits values in place; never adds sections or keys.</summary>
public sealed class IniAdapter(string id) : IConfigAdapter
{
    public string Id => id;

    static (int start, int end) Section(string[] lines, string section)
    {
        var start = Array.FindIndex(lines, l => l.TrimEnd('\r').Trim().Equals($"[{section}]", StringComparison.OrdinalIgnoreCase));
        if (start < 0) return (-1, -1);
        var end = start + 1;
        while (end < lines.Length && !lines[end].TrimStart().StartsWith('[')) end++;
        return (start, end);
    }

    static int Find(string[] lines, string? section, string key)
    {
        var (s, e) = section is null ? (-1, lines.Length) : Section(lines, section);
        if (section != null && s < 0) return -1;
        for (int i = s + 1; i < e; i++)
        {
            var l = lines[i];
            var eq = l.IndexOf('=');
            if (eq > 0 && l[..eq].Trim().Equals(key, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    public ConfigCheck Check(string text, GameDefinition def)
    {
        var lines = text.Split('\n');
        var v = def.Version;
        if (v?.RequiredSection != null && Section(lines, v.RequiredSection).start < 0)
            return ConfigCheck.Fail($"Section [{v.RequiredSection}] is missing, so this isn't a layout we know.");
        string? version = null;
        if (v?.Key != null)
        {
            var i = Find(lines, v.Section, v.Key);
            if (i < 0) return ConfigCheck.Fail($"No {v.Key} entry.");
            version = lines[i][(lines[i].IndexOf('=') + 1)..].Trim();
            if (!long.TryParse(version, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) || n < v.Min || n > v.Max)
                return ConfigCheck.Fail($"Config version {version} isn't one we've tested.");
        }
        foreach (var k in v?.RequiredKeys ?? [])
        {
            var parts = k.Split('|');
            var (sec, key) = parts.Length == 2 ? (parts[0], parts[1]) : (null, parts[0]);
            if (Find(lines, sec, key) < 0) return ConfigCheck.Fail($"Expected '{key}' is missing.");
        }
        return ConfigCheck.Fine(version);
    }

    public string? Get(string text, SettingDef s)
    {
        var lines = text.Split('\n');
        var i = Find(lines, s.Section, s.Key);
        return i < 0 ? null : lines[i][(lines[i].IndexOf('=') + 1)..].TrimEnd('\r').Trim();
    }

    public string Set(string text, SettingDef s, string value)
    {
        var lines = text.Split('\n');
        var i = Find(lines, s.Section, s.Key);
        if (i < 0) throw new InvalidOperationException($"'{s.Key}' isn't in the file.");
        var l = lines[i];
        var eq = l.IndexOf('=');
        var cr = l.EndsWith('\r') ? "\r" : "";
        var old = l[(eq + 1)..].TrimEnd('\r').Trim();
        // keep the game's own spelling: True/False vs true/false, 60.000000 vs 60
        var v = value;
        if (old is "True" or "False" && bool.TryParse(value, out var b)) v = b ? "True" : "False";
        lines[i] = l[..(eq + 1)] + v + cr;
        return string.Join('\n', lines);
    }
}

public static class Catalog
{
    public static readonly IReadOnlyDictionary<string, IConfigAdapter> Adapters = new Dictionary<string, IConfigAdapter>
    {
        ["minecraft-options"] = new MinecraftOptionsAdapter(),
        ["unreal-ini"] = new IniAdapter("unreal-ini"),
    };

    static IReadOnlyList<GameDefinition>? _all;

    /// <summary>All game definitions shipped in Data/*.json, validated. Throws on any invalid file (caught by tests).</summary>
    public static IReadOnlyList<GameDefinition> All => _all ??= Load();

    static List<GameDefinition> Load()
    {
        var asm = Assembly.GetExecutingAssembly();
        var list = new List<GameDefinition>();
        foreach (var name in asm.GetManifestResourceNames().Where(n => n.StartsWith("Framecalm.Data.") && n.EndsWith(".json")).Order())
        {
            using var s = asm.GetManifestResourceStream(name)!;
            using var doc = JsonDocument.Parse(s, new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });
            var items = doc.RootElement.ValueKind == JsonValueKind.Array ? doc.RootElement.EnumerateArray().ToList() : [doc.RootElement];
            foreach (var e in items)
            {
                var def = e.Deserialize<GameDefinition>(Json.Options) ?? throw new InvalidDataException($"{name}: empty");
                var errors = Validate(def);
                if (errors.Count > 0) throw new InvalidDataException($"{name} ({def.Id}): {string.Join("; ", errors)}");
                list.Add(def);
            }
        }
        if (list.Select(d => d.Id).Distinct().Count() != list.Count) throw new InvalidDataException("Duplicate game ids in Data/*.json");
        return list;
    }

    static readonly string[] FasterValues = ["lower", "higher", "false", "true", "order"];
    static readonly string[] ProfileKeys = ["light", "balanced", "performance", "maxfps", "unlimited"];
    static readonly Regex IdRx = new("^[a-z0-9-]+$");

    public static List<string> Validate(GameDefinition d)
    {
        var e = new List<string>();
        if (!IdRx.IsMatch(d.Id)) e.Add("id must be lowercase-dashed");
        if (d.Support == SupportLevel.Automatic)
        {
            if (d.Adapter is null || !Adapters.ContainsKey(d.Adapter)) e.Add($"unknown adapter '{d.Adapter}'");
            if (d.ConfigPath is null) e.Add("automatic games need a configPath");
            if (d.Version is null) e.Add("automatic games need a version/format rule");
            if (d.Settings.Count == 0) e.Add("automatic games need settings");
        }
        if (d.Support == SupportLevel.Guided && d.Guided.Count == 0) e.Add("guided games need steps");
        if (d.Settings.Select(s => s.Key).Distinct().Count() != d.Settings.Count) e.Add("duplicate setting keys");
        foreach (var s in d.Settings)
        {
            if (!FasterValues.Contains(s.Faster)) e.Add($"{s.Key}: faster must be one of {string.Join('/', FasterValues)}");
            if (s.Faster == "order" && (s.Order is null || s.Order.Length < 2)) e.Add($"{s.Key}: order needed");
            if (s.VisualCost is < 1 or > 5) e.Add($"{s.Key}: visualCost 1-5");
            if (s.TypicalGain is < 0 or > 60) e.Add($"{s.Key}: typicalGain 0-60");
            if (s.Special is not null and not "refresh") e.Add($"{s.Key}: unknown special");
            foreach (var (k, v) in s.Targets)
            {
                if (!ProfileKeys.Contains(k)) e.Add($"{s.Key}: unknown profile '{k}'");
                if (s.Kind is SettingKind.Int or SettingKind.Float && !double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) e.Add($"{s.Key}: '{v}' isn't a number");
                if (s.Kind == SettingKind.Bool && !bool.TryParse(v, out _)) e.Add($"{s.Key}: '{v}' isn't true/false");
                if (s.Kind == SettingKind.Enum && s.Order?.Contains(v) != true) e.Add($"{s.Key}: '{v}' not in order");
            }
            // targets must get cheaper (or stay) as profiles get stronger
            if (s.Special is null && s.Faster != "order")
            {
                string? prev = null;
                foreach (var k in ProfileKeys.Take(4))
                {
                    if (!s.Targets.TryGetValue(k, out var v)) continue;
                    if (prev != null && Recommender.IsFaster(s, prev, v)) e.Add($"{s.Key}: '{k}' is heavier than a lighter profile");
                    prev = v;
                }
            }
        }
        return e;
    }

    public static string? ResolveConfig(GameDefinition d) => d.ConfigPath is null ? null : Paths.Expand(d.ConfigPath);
}
