using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text.Json;
using System.Text.RegularExpressions;
using Framecalm.Core;
using Microsoft.Win32;

namespace Framecalm.Windows;

public enum TweakTier { Safe, Advanced, Extreme }

public enum TweakState { Off, On, Partial, Unavailable }

/// <summary>One registry value or power setting a tweak sets. Key is "reg|HKLM\path|Name|dword", "reg|HKCU\path|Name|sz" or "pwr|subgroup|setting".</summary>
public sealed record TweakAtom(string Key, string Value, string? Default, bool RequireKey);

public sealed record TweakCategory(string Id, string Name, string About);
public sealed record TweakPreset(string Id, string Name, string About);
public sealed record LeftOut(string Name, string Why);

public sealed record Tweak(string Id, string Category, TweakTier Tier, string Impact, string Name, string What, string Tradeoff,
    bool Restart, IReadOnlyList<string> Presets, IReadOnlyList<TweakAtom> Templates)
{
    /// <summary>Concrete atoms for this PC: "{nic}" expands to each active network adapter.</summary>
    public List<TweakAtom> Atoms(IReadOnlyList<string> nics) =>
        Templates.SelectMany(a => a.Key.Contains("{nic}") ? nics.Select(n => a with { Key = a.Key.Replace("{nic}", n) }) : [a]).ToList();

    public bool NeedsAdmin => Templates.Any(a => TweakKeys.NeedsAdmin(a.Key));
}

/// <summary>The tweak list, loaded from the embedded Data/tweaks.json. The elevated helper loads the same file and refuses any key not in it.</summary>
public static class TweakCatalog
{
    static readonly Lazy<(List<TweakCategory> C, List<TweakPreset> P, List<Tweak> T, List<LeftOut> L)> _data = new(Load);
    public static IReadOnlyList<TweakCategory> Categories => _data.Value.C;
    public static IReadOnlyList<TweakPreset> Presets => _data.Value.P;
    public static IReadOnlyList<Tweak> All => _data.Value.T;
    public static IReadOnlyList<LeftOut> LeftOut => _data.Value.L;
    public static Tweak? Find(string id) => All.FirstOrDefault(t => t.Id == id);

    static readonly Dictionary<string, (string Start, string Value)> StartValues = new()
    {
        ["disabled"] = ("4", "Disabled"), ["manual"] = ("3", "Manual"), ["automatic"] = ("2", "Automatic"), ["delayed"] = ("2", "Automatic (delayed)"),
    };

    static (List<TweakCategory>, List<TweakPreset>, List<Tweak>, List<LeftOut>) Load()
    {
        var asm = typeof(TweakCatalog).Assembly;
        var name = asm.GetManifestResourceNames().First(n => n.EndsWith("tweaks.json"));
        using var s = asm.GetManifestResourceStream(name)!;
        using var doc = JsonDocument.Parse(s);
        var root = doc.RootElement;
        var cats = root.GetProperty("categories").EnumerateArray().Select(c => new TweakCategory(c.Str("id")!, c.Str("name")!, c.Str("about")!)).ToList();
        var presets = root.GetProperty("presets").EnumerateArray().Select(c => new TweakPreset(c.Str("id")!, c.Str("name")!, c.Str("about")!)).ToList();
        var left = root.GetProperty("leftOut").EnumerateArray().Select(c => new LeftOut(c.Str("name")!, c.Str("why")!)).ToList();
        var tweaks = new List<Tweak>();
        foreach (var t in root.GetProperty("tweaks").EnumerateArray())
        {
            var atoms = t.GetProperty("atoms").EnumerateArray().Select(ParseAtom).ToList();
            var tier = Enum.Parse<TweakTier>(t.Str("tier")!, ignoreCase: true);
            tweaks.Add(new Tweak(t.Str("id")!, t.Str("category")!, tier, t.Str("impact")!, t.Str("name")!, t.Str("what")!, t.Str("tradeoff")!,
                t.TryGetProperty("restart", out var r) && r.GetBoolean(),
                t.GetProperty("presets").EnumerateArray().Select(p => p.GetString()!).ToList(), atoms));
        }
        var ids = tweaks.Select(t => t.Id).ToList();
        if (ids.Distinct().Count() != ids.Count) throw new InvalidDataException("Duplicate tweak id in tweaks.json");
        if (tweaks.Any(t => cats.All(c => c.Id != t.Category))) throw new InvalidDataException("Tweak with unknown category in tweaks.json");
        return (cats, presets, tweaks, left);
    }

    static TweakAtom ParseAtom(JsonElement a)
    {
        string? Def(JsonElement d) => d.ValueKind switch { JsonValueKind.Null => null, JsonValueKind.Number => d.GetInt64().ToString(CultureInfo.InvariantCulture), _ => d.GetString() };
        var def = a.TryGetProperty("default", out var d) ? Def(d) : null;
        if (a.TryGetProperty("service", out var svc))
        {
            var start = StartValues[a.Str("start")!].Start;
            var dflt = def is null ? null : StartValues[def].Start;
            return new TweakAtom($@"reg|HKLM\SYSTEM\CurrentControlSet\Services\{svc.GetString()}|Start|dword", start, dflt, RequireKey: true);
        }
        if (a.TryGetProperty("power", out var pw))
        {
            var parts = pw.GetString()!.Split('/');
            if (a.Str("alias") is { } alias) Aliases[$"pwr|{parts[0]}|{parts[1]}"] = alias;
            return new TweakAtom($"pwr|{parts[0]}|{parts[1]}", a.GetProperty("value").GetInt64().ToString(CultureInfo.InvariantCulture), def, false);
        }
        var path = a.Str("reg")!;
        var vname = a.Str("name")!;
        if (a.TryGetProperty("dword", out var dw))
            return new TweakAtom($"reg|{path}|{vname}|dword", dw.GetInt64().ToString(CultureInfo.InvariantCulture), def, false);
        return new TweakAtom($"reg|{path}|{vname}|sz", a.Str("sz")!, def, false);
    }

    /// <summary>Readable powercfg names for power settings, by atom key.</summary>
    public static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase);

    static string? Str(this JsonElement e, string n) => e.TryGetProperty(n, out var v) ? v.GetString() : null;

    /// <summary>Used by the elevated helper: is this exact key one the catalog can touch?</summary>
    public static bool IsAllowedKey(string key)
    {
        foreach (var t in All)
            foreach (var a in t.Templates)
            {
                if (!a.Key.Contains("{nic}")) { if (string.Equals(a.Key, key, StringComparison.OrdinalIgnoreCase)) return true; continue; }
                var rx = "^" + Regex.Escape(a.Key).Replace(Regex.Escape("{nic}"), @"\{[0-9A-Fa-f\-]{36}\}") + "$";
                if (Regex.IsMatch(key, rx, RegexOptions.IgnoreCase)) return true;
            }
        return false;
    }
}

public static class TweakKeys
{
    public static bool NeedsAdmin(string key) =>
        key.StartsWith("pwr|", StringComparison.Ordinal) ||
        key.StartsWith(@"reg|HKLM\", StringComparison.OrdinalIgnoreCase) ||
        key.StartsWith(@"reg|HKCU\Software\Policies\", StringComparison.OrdinalIgnoreCase);

    public static (string Hive, string Path, string Name, string Type) Reg(string key)
    {
        var p = key.Split('|');
        var i = p[1].IndexOf('\\');
        return (p[1][..i].ToUpperInvariant(), p[1][(i + 1)..], p[2], p[3]);
    }

    public static string Describe(string key, string? v)
    {
        if (v is null) return "Not set";
        if (key.EndsWith(@"|Start|dword", StringComparison.OrdinalIgnoreCase))
            return v switch { "2" => "Automatic", "3" => "Manual", "4" => "Disabled", _ => v };
        return v == "4294967295" ? "0xFFFFFFFF" : v;
    }

    public static IReadOnlyList<string> ActiveNics()
    {
        try
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211)
                .Select(n => n.Id).Where(id => Regex.IsMatch(id, @"^\{[0-9A-Fa-f\-]{36}\}$")).ToList();
        }
        catch { return []; }
    }
}

/// <summary>Where tweak values are read from and written to. Real = registry and power settings; demo = memory.</summary>
public interface ITweakBackend
{
    string? Read(string key);
    bool KeyExists(string key);
    void Write(IReadOnlyList<(string Key, string? Value)> values);
    IReadOnlyList<string> Nics { get; }
}

/// <summary>All tweaks go through one change target, so the transaction manager journals, verifies and undoes them like any other change.</summary>
public sealed class TweakTarget(ITweakBackend backend) : IChangeTarget
{
    public const string TargetPath = "windows:tweaks";
    public TargetKind Kind => TargetKind.Tweak;
    public string Path => TargetPath;
    public bool IsFile => false;
    public ITweakBackend Backend => backend;
    public string? Read(string key) => backend.Read(key);
    public void Write(IReadOnlyList<(string Key, string? Value)> values) =>
        backend.Write(values.Where(v => !Same(backend.Read(v.Key), v.Value)).ToList());

    static bool Same(string? a, string? b) => a is null || b is null ? a is null && b is null : string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    public TweakState State(Tweak t)
    {
        var atoms = t.Atoms(backend.Nics);
        if (atoms.Count == 0) return TweakState.Unavailable;
        if (atoms.Any(a => a.RequireKey && !backend.KeyExists(a.Key))) return TweakState.Unavailable;
        var on = atoms.Count(a => Same(backend.Read(a.Key), a.Value));
        return on == atoms.Count ? TweakState.On : on == 0 ? TweakState.Off : TweakState.Partial;
    }
}

public sealed class MemoryTweakBackend : ITweakBackend
{
    public readonly Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyList<string> Nics { get; } = ["{6C1D2F0A-4B7E-4A11-9C3D-2B5E8F7A9D10}"];
    public MemoryTweakBackend()
    {
        foreach (var t in TweakCatalog.All)
            foreach (var a in t.Atoms(Nics))
                if (a.Default != null) Values[a.Key] = a.Default;
    }
    public string? Read(string key) => Values.TryGetValue(key, out var v) ? v : null;
    public bool KeyExists(string key) => !key.Contains(@"\Services\Fax|", StringComparison.OrdinalIgnoreCase);
    public void Write(IReadOnlyList<(string Key, string? Value)> values)
    {
        foreach (var (k, v) in values) { if (v is null) Values.Remove(k); else Values[k] = v; }
    }
}

/// <summary>Request the main app hands to the elevated helper.</summary>
public sealed class TweakRequest
{
    public string UserSid { get; set; } = "";
    public bool RestorePoint { get; set; }
    public List<TweakWrite> Writes { get; set; } = [];
}

public sealed class TweakWrite
{
    public string Key { get; set; } = "";
    public string? Value { get; set; }
}

/// <summary>Reads and writes registry values and power settings. Writing HKLM, policies and power settings needs admin,
/// so the real backend hands those to Framecalm.ElevatedHelper (one prompt per apply), which calls <see cref="ApplyDirect"/>.</summary>
public sealed class RegistryTweakBackend(string helperPath, string requestDir) : ITweakBackend
{
    public IReadOnlyList<string> Nics { get; } = TweakKeys.ActiveNics();
    public bool RestorePointNext { get; set; } = true;
    public string? LastNote { get; private set; }

    public string? Read(string key) => TweakIo.Read(key, userSid: null);
    public bool KeyExists(string key) => TweakIo.KeyExists(key);

    public void Write(IReadOnlyList<(string Key, string? Value)> values)
    {
        LastNote = null;
        var direct = values.Where(v => !TweakKeys.NeedsAdmin(v.Key)).ToList();
        var elevated = values.Where(v => TweakKeys.NeedsAdmin(v.Key)).ToList();
        if (elevated.Count > 0) RunElevated(elevated);
        TweakIo.ApplyDirect(direct.Select(v => new TweakWrite { Key = v.Key, Value = v.Value }).ToList(), userSid: null);
    }

    void RunElevated(List<(string Key, string? Value)> writes)
    {
        if (!File.Exists(helperPath)) throw new InvalidOperationException("Framecalm.ElevatedHelper.exe is missing, so changes that need admin can't be made.");
        Directory.CreateDirectory(requestDir);
        var id = Guid.NewGuid().ToString("N");
        var req = System.IO.Path.Combine(requestDir, id + ".json");
        var res = System.IO.Path.Combine(requestDir, id + ".result.txt");
        var body = new TweakRequest
        {
            UserSid = WindowsIdentity.GetCurrent().User?.Value ?? "",
            RestorePoint = RestorePointNext,
            Writes = writes.Select(w => new TweakWrite { Key = w.Key, Value = w.Value }).ToList(),
        };
        File.WriteAllText(req, JsonSerializer.Serialize(body));
        try
        {
            var psi = new ProcessStartInfo(helperPath, $"tweak --in \"{req}\"") { UseShellExecute = true, Verb = "runas", WindowStyle = ProcessWindowStyle.Hidden };
            using var p = Process.Start(psi)!;
            if (!p.WaitForExit(120_000)) { try { p.Kill(); } catch { } throw new InvalidOperationException("The admin helper didn't finish in time."); }
            var result = File.Exists(res) ? File.ReadAllText(res).Trim() : "";
            if (p.ExitCode != 0) throw new InvalidOperationException(result.Length > 0 ? result : "The admin helper refused the change.");
            if (RestorePointNext) RestorePointNext = false;
            LastNote = result.Length > 0 ? result : null;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new InvalidOperationException("You said no to the admin prompt.");
        }
        finally
        {
            try { File.Delete(req); File.Delete(res); } catch { }
        }
    }
}

/// <summary>Low-level read/write shared by the app (per-user values) and the elevated helper (everything else).</summary>
public static class TweakIo
{
    static RegistryKey Root(string hive, string? userSid) => hive switch
    {
        "HKLM" => RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
        "HKCU" when !string.IsNullOrEmpty(userSid) => RegistryKey.OpenBaseKey(RegistryHive.Users, RegistryView.Registry64).OpenSubKey(userSid, true)
                                                      ?? throw new InvalidOperationException("User profile not loaded."),
        "HKCU" => RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64),
        _ => throw new InvalidOperationException("Unknown hive " + hive),
    };

    public static bool KeyExists(string key)
    {
        if (!key.StartsWith("reg|")) return true;
        var (hive, path, _, _) = TweakKeys.Reg(key);
        using var root = Root(hive, null);
        using var k = root.OpenSubKey(path);
        return k != null;
    }

    public static string? Read(string key, string? userSid)
    {
        if (key.StartsWith("pwr|")) return Power.Read(key);
        var (hive, path, name, type) = TweakKeys.Reg(key);
        using var root = Root(hive, userSid);
        using var k = root.OpenSubKey(path);
        var v = k?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
        return v switch
        {
            null => null,
            int i => unchecked((uint)i).ToString(CultureInfo.InvariantCulture),
            long l => l.ToString(CultureInfo.InvariantCulture),
            byte[] => "(binary)",
            _ => v.ToString(),
        };
    }

    public static void ApplyDirect(IReadOnlyList<TweakWrite> writes, string? userSid)
    {
        foreach (var w in writes)
        {
            if (w.Key.StartsWith("pwr|")) { Power.Write(w.Key, w.Value); continue; }
            var (hive, path, name, type) = TweakKeys.Reg(w.Key);
            using var root = Root(hive, userSid);
            if (w.Value is null)
            {
                using var k = root.OpenSubKey(path, writable: true);
                k?.DeleteValue(name, throwOnMissingValue: false);
                continue;
            }
            using var key = root.CreateSubKey(path, writable: true) ?? throw new InvalidOperationException("Couldn't open " + path);
            if (type == "dword") key.SetValue(name, unchecked((int)uint.Parse(w.Value, CultureInfo.InvariantCulture)), RegistryValueKind.DWord);
            else key.SetValue(name, w.Value, RegistryValueKind.String);
        }
    }

    /// <summary>Checks a write before the helper performs it: the key must be in the catalog and the value must fit its type.</summary>
    public static string? Validate(TweakWrite w)
    {
        if (!TweakCatalog.IsAllowedKey(w.Key)) return "not in the tweak catalog: " + w.Key;
        if (w.Value is null) return w.Key.StartsWith("pwr|") ? "power settings can't be removed" : null;
        if (w.Key.StartsWith("pwr|") || w.Key.EndsWith("|dword"))
            return uint.TryParse(w.Value, NumberStyles.None, CultureInfo.InvariantCulture, out _) ? null : "not a number: " + w.Value;
        return w.Value.Length <= 64 && !w.Value.Contains('\0') ? null : "value too long";
    }

    static class Power
    {
        [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr guid);
        [DllImport("powrprof.dll")] static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
        [DllImport("powrprof.dll")] static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint value);
        [DllImport("powrprof.dll")] static extern uint PowerWriteACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, uint value);
        [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);

        static Guid Active()
        {
            if (PowerGetActiveScheme(IntPtr.Zero, out var p) != 0) throw new InvalidOperationException("Couldn't read the active power plan.");
            try { return Marshal.PtrToStructure<Guid>(p); } finally { LocalFree(p); }
        }

        public static string? Read(string key)
        {
            var parts = key.Split('|');
            Guid scheme = Active(), sub = Guid.Parse(parts[1]), set = Guid.Parse(parts[2]);
            return PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref set, out var v) == 0 ? v.ToString(CultureInfo.InvariantCulture) : null;
        }

        public static void Write(string key, string? value)
        {
            if (value is null) return;
            var parts = key.Split('|');
            Guid scheme = Active(), sub = Guid.Parse(parts[1]), set = Guid.Parse(parts[2]);
            var r = PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref set, uint.Parse(value, CultureInfo.InvariantCulture));
            if (r != 0) throw new InvalidOperationException($"Windows refused the power setting (error {r}).");
            PowerSetActiveScheme(IntPtr.Zero, ref scheme);
        }
    }
}
