using System.Diagnostics;
using System.Runtime.InteropServices;
using Framecalm.Core;
using Microsoft.Win32;

namespace Framecalm.Windows;

/// <summary>Per-user GPU preference: the same value Settings > System > Display > Graphics writes.</summary>
public sealed class GpuPreferenceTarget : IChangeTarget
{
    public const string KeyPath = @"Software\Microsoft\DirectX\UserGpuPreferences";
    public TargetKind Kind => TargetKind.Registry;
    public string Path => @"HKCU\" + KeyPath;
    public bool IsFile => false;

    public string? Read(string exePath)
    {
        using var k = Registry.CurrentUser.OpenSubKey(KeyPath);
        return k?.GetValue(exePath) as string;
    }

    public void Write(IReadOnlyList<(string Key, string? Value)> values)
    {
        using var k = Registry.CurrentUser.CreateSubKey(KeyPath, true);
        foreach (var (exe, v) in values)
            if (v is null) k.DeleteValue(exe, false);
            else k.SetValue(exe, v, RegistryValueKind.String);
    }

    public const string HighPerformance = "GpuPreference=2;";

    public static string Describe(string? v) => v switch
    {
        null => "Let Windows decide",
        _ when v.Contains("GpuPreference=2") => "High performance GPU",
        _ when v.Contains("GpuPreference=1") => "Power saving GPU",
        _ => "Let Windows decide",
    };
}

/// <summary>Startup apps, enabled/disabled the way Task Manager does it (StartupApproved), so Task Manager shows the same state.</summary>
public sealed class StartupTarget : IChangeTarget
{
    const string Run = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ApprovedRun = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";
    const string ApprovedFolder = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\StartupFolder";

    public TargetKind Kind => TargetKind.StartupItem;
    public string Path => @"HKCU\" + ApprovedRun;
    public bool IsFile => false;

    public sealed record Item(string Name, string Command, bool Enabled, bool FromFolder, string? Exe);

    public static List<Item> List()
    {
        var list = new List<Item>();
        using (var run = Registry.CurrentUser.OpenSubKey(Run))
            if (run != null)
                foreach (var n in run.GetValueNames())
                {
                    var cmd = run.GetValue(n)?.ToString() ?? "";
                    list.Add(new Item(n, cmd, IsEnabled(ApprovedRun, n), false, ExeOf(cmd)));
                }
        var folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
        if (Directory.Exists(folder))
            foreach (var f in Directory.GetFiles(folder).Where(f => !f.EndsWith("desktop.ini", StringComparison.OrdinalIgnoreCase)))
                list.Add(new Item(System.IO.Path.GetFileName(f), f, IsEnabled(ApprovedFolder, System.IO.Path.GetFileName(f)), true, null));
        return list;
    }

    static string? ExeOf(string cmd)
    {
        cmd = cmd.Trim();
        if (cmd.StartsWith('"')) { var e = cmd.IndexOf('"', 1); return e > 1 ? cmd[1..e] : null; }
        var i = cmd.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        return i > 0 ? cmd[..(i + 4)] : null;
    }

    static bool IsEnabled(string key, string name)
    {
        using var k = Registry.CurrentUser.OpenSubKey(key);
        return k?.GetValue(name) is not byte[] b || b.Length == 0 || (b[0] & 1) == 0; // 02/06 = enabled, 03/07 = disabled
    }

    // key format "run:<name>" or "folder:<name>"; values "enabled"/"disabled"
    public string? Read(string key)
    {
        var (sub, name) = Split(key);
        return IsEnabled(sub, name) ? "enabled" : "disabled";
    }

    public void Write(IReadOnlyList<(string Key, string? Value)> values)
    {
        foreach (var (key, v) in values)
        {
            var (sub, name) = Split(key);
            using var k = Registry.CurrentUser.CreateSubKey(sub, true);
            var data = new byte[12];
            data[0] = v == "disabled" ? (byte)3 : (byte)2;
            if (v == "disabled") BitConverter.GetBytes(DateTime.UtcNow.ToFileTimeUtc()).CopyTo(data, 4);
            k.SetValue(name, data, RegistryValueKind.Binary);
        }
    }

    static (string, string) Split(string key) =>
        key.StartsWith("folder:") ? (ApprovedFolder, key[7..]) : (ApprovedRun, key.StartsWith("run:") ? key[4..] : key);
}

/// <summary>Power plans through the documented powrprof API. Only switches between plans that already exist; never creates or edits plans.</summary>
public static class PowerPlans
{
    public static readonly Guid HighPerformance = new("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
    public static readonly Guid Ultimate = new("e9a42b02-d5df-448d-aa00-03f14749eb61");
    public static readonly Guid Balanced = new("381b4222-f694-41f0-9685-ff5bb260df2e");

    public static Guid? Active()
    {
        if (PowerGetActiveScheme(IntPtr.Zero, out var p) != 0) return null;
        var g = Marshal.PtrToStructure<Guid>(p);
        LocalFree(p);
        return g;
    }

    public static List<(Guid Id, string Name)> All()
    {
        var list = new List<(Guid, string)>();
        var buf = new byte[16];
        for (uint i = 0; ; i++)
        {
            uint size = 16;
            if (PowerEnumerate(IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, 16 /*ACCESS_SCHEME*/, i, buf, ref size) != 0) break;
            var g = new Guid(buf);
            list.Add((g, Name(g)));
        }
        return list;
    }

    static string Name(Guid g)
    {
        uint size = 0;
        PowerReadFriendlyName(IntPtr.Zero, ref g, IntPtr.Zero, IntPtr.Zero, null, ref size);
        var b = new byte[size];
        if (PowerReadFriendlyName(IntPtr.Zero, ref g, IntPtr.Zero, IntPtr.Zero, b, ref size) != 0) return g.ToString();
        return System.Text.Encoding.Unicode.GetString(b).TrimEnd('\0');
    }

    /// <summary>The best existing plan for gaming, or null if this PC only has Balanced (common on modern laptops; then we guide to the power mode slider).</summary>
    public static (Guid Id, string Name)? BestForGaming()
    {
        var all = All();
        return all.Where(p => p.Id == Ultimate).Concat(all.Where(p => p.Id == HighPerformance)).Cast<(Guid, string)?>().FirstOrDefault();
    }

    public static bool Set(Guid g) => PowerSetActiveScheme(IntPtr.Zero, ref g) == 0;

    [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr guid);
    [DllImport("powrprof.dll")] static extern uint PowerSetActiveScheme(IntPtr root, ref Guid guid);
    [DllImport("powrprof.dll")] static extern uint PowerEnumerate(IntPtr root, IntPtr sub, IntPtr setting, uint access, uint index, byte[] buffer, ref uint size);
    [DllImport("powrprof.dll")] static extern uint PowerReadFriendlyName(IntPtr root, ref Guid scheme, IntPtr sub, IntPtr setting, byte[]? buffer, ref uint size);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);
}

public sealed record ProcessUse(int Pid, string Name, string Title, double CpuPercent, long MemoryBytes, bool HasWindow);

public static class Processes
{
    /// <summary>What's using CPU and memory right now (two samples, 600 ms apart).</summary>
    public static async Task<List<ProcessUse>> SnapshotAsync()
    {
        var first = new Dictionary<int, TimeSpan>();
        foreach (var p in Process.GetProcesses()) { try { first[p.Id] = p.TotalProcessorTime; } catch { } p.Dispose(); }
        var sw = Stopwatch.StartNew();
        await Task.Delay(600);
        var list = new List<ProcessUse>();
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                if (p.Id is 0 or 4 || !first.TryGetValue(p.Id, out var t0)) continue;
                var cpu = (p.TotalProcessorTime - t0).TotalMilliseconds / sw.Elapsed.TotalMilliseconds / Environment.ProcessorCount * 100;
                list.Add(new ProcessUse(p.Id, p.ProcessName, p.MainWindowTitle, cpu, p.WorkingSet64, p.MainWindowHandle != IntPtr.Zero));
            }
            catch { }
            finally { p.Dispose(); }
        }
        // group helpers like chrome/msedge into one row per app
        return list.GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ProcessUse(g.First(x => x.HasWindow || true).Pid, g.Key, g.FirstOrDefault(x => x.Title.Length > 0)?.Title ?? "", g.Sum(x => x.CpuPercent), g.Sum(x => x.MemoryBytes), g.Any(x => x.HasWindow)))
            .OrderByDescending(x => x.CpuPercent * 50 + x.MemoryBytes / (1 << 20))
            .ToList();
    }

    /// <summary>Asks an app to close the same way clicking its X does. Never force-kills: the app can still ask to save.</summary>
    public static int CloseGracefully(string processName)
    {
        var asked = 0;
        foreach (var p in Process.GetProcessesByName(processName))
        {
            try { if (p.MainWindowHandle != IntPtr.Zero && p.CloseMainWindow()) asked++; }
            catch { }
            finally { p.Dispose(); }
        }
        return asked;
    }
}
