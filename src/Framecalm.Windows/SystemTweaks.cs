using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using Framecalm.Core;
using Microsoft.Win32;

namespace Framecalm.Windows;

/// <summary>Per-user registry values that Windows' own Settings pages write (Game Mode, captures, graphics defaults).
/// Key format "sub\key|ValueName"; values "dword:1", "sz:text" or null (not set).</summary>
public sealed class UserRegistryTarget : IChangeTarget
{
    public const string TargetPath = @"HKCU (Windows gaming settings)";
    public TargetKind Kind => TargetKind.Registry;
    public string Path => TargetPath;
    public bool IsFile => false;

    public string? Read(string key)
    {
        var (sub, name) = Split(key);
        using var k = Registry.CurrentUser.OpenSubKey(sub);
        return k?.GetValue(name) switch { int i => "dword:" + i, string s => "sz:" + s, _ => null };
    }

    public void Write(IReadOnlyList<(string Key, string? Value)> values)
    {
        foreach (var (key, v) in values)
        {
            var (sub, name) = Split(key);
            using var k = Registry.CurrentUser.CreateSubKey(sub, true);
            if (v is null) k.DeleteValue(name, false);
            else if (v.StartsWith("dword:")) k.SetValue(name, int.Parse(v[6..]), RegistryValueKind.DWord);
            else if (v.StartsWith("sz:")) k.SetValue(name, v[3..], RegistryValueKind.String);
            else throw new InvalidOperationException("Unknown value format.");
        }
    }

    static (string, string) Split(string key) { var i = key.LastIndexOf('|'); return (key[..i], key[(i + 1)..]); }
}

/// <summary>The main screen's refresh rate, through the documented display-settings API.</summary>
public sealed class DisplayTarget : IChangeTarget
{
    public const string TargetPath = "display:primary";
    public TargetKind Kind => TargetKind.Display;
    public string Path => TargetPath;
    public bool IsFile => false;

    public string? Read(string key)
    {
        var dm = New();
        return EnumDisplaySettings(null, -1, ref dm) ? dm.dmDisplayFrequency.ToString() : null;
    }

    public void Write(IReadOnlyList<(string Key, string? Value)> values)
    {
        foreach (var (_, v) in values)
        {
            var dm = New();
            if (!EnumDisplaySettings(null, -1, ref dm)) throw new InvalidOperationException("Couldn't read the display mode.");
            dm.dmDisplayFrequency = int.Parse(v!);
            dm.dmFields = 0x400000 /*DM_DISPLAYFREQUENCY*/ | 0x80000 /*PELSWIDTH*/ | 0x100000 /*PELSHEIGHT*/;
            if (ChangeDisplaySettingsEx(null, ref dm, IntPtr.Zero, 2 /*CDS_TEST*/, IntPtr.Zero) != 0) throw new InvalidOperationException($"This screen doesn't accept {v} Hz at the current resolution.");
            if (ChangeDisplaySettingsEx(null, ref dm, IntPtr.Zero, 1 /*CDS_UPDATEREGISTRY*/, IntPtr.Zero) != 0) throw new InvalidOperationException("Windows refused the refresh rate change.");
        }
    }

    /// <summary>Highest refresh rate the screen offers at the resolution it's using now.</summary>
    public static int MaxRefresh()
    {
        var cur = New();
        if (!EnumDisplaySettings(null, -1, ref cur)) return 0;
        var max = cur.dmDisplayFrequency;
        for (int i = 0; ; i++)
        {
            var dm = New();
            if (!EnumDisplaySettings(null, i, ref dm)) break;
            if (dm.dmPelsWidth == cur.dmPelsWidth && dm.dmPelsHeight == cur.dmPelsHeight && dm.dmBitsPerPel == cur.dmBitsPerPel)
                max = Math.Max(max, dm.dmDisplayFrequency);
        }
        return max;
    }

    static DEVMODE New() => new() { dmSize = (short)Marshal.SizeOf<DEVMODE>() };

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettings(string? device, int mode, ref DEVMODE dm);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int ChangeDisplaySettingsEx(string? device, ref DEVMODE dm, IntPtr hwnd, int flags, IntPtr param);
}

/// <summary>The active power plan (only ever switches between plans that already exist).</summary>
public sealed class PowerPlanTarget : IChangeTarget
{
    public const string TargetPath = "power:active-plan";
    public TargetKind Kind => TargetKind.PowerPlan;
    public string Path => TargetPath;
    public bool IsFile => false;
    public string? Read(string key) => PowerPlans.Active()?.ToString();
    public void Write(IReadOnlyList<(string Key, string? Value)> values)
    {
        foreach (var (_, v) in values)
            if (!PowerPlans.Set(Guid.Parse(v!))) throw new InvalidOperationException("Windows refused the power plan change.");
    }
}

/// <summary>
/// The Wi-Fi adapter's power saving mode, part of the active power plan (Device Manager > wireless adapter >
/// Power Management, same setting as powercfg's "Wireless Adapter Settings"). Windows lets the radio idle down
/// to save battery, which adds latency; "Maximum Performance" turns that off. Reversible, documented, per-plan.
/// </summary>
public sealed class WirelessPowerTarget : IChangeTarget
{
    public const string TargetPath = "power:wireless-power-saving";
    public TargetKind Kind => TargetKind.PowerPlan;
    public string Path => TargetPath;
    public bool IsFile => false;

    static readonly Guid SubWireless = new("19cbb8fa-5279-450e-9fac-8a3d5fedd0c1");
    static readonly Guid SettingPowerSaving = new("12bbebe6-58d6-4636-95bb-3217ef867c1a");

    public static readonly string[] Labels = ["Maximum performance", "Low power saving", "Medium power saving", "Maximum power saving"];

    public string? Read(string key)
    {
        if (PowerGetActiveScheme(IntPtr.Zero, out var p) != 0) return null;
        var scheme = Marshal.PtrToStructure<Guid>(p);
        LocalFree(p);
        var sub = SubWireless; var setting = SettingPowerSaving;
        return PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, out var value) != 0 ? null : value.ToString();
    }

    public void Write(IReadOnlyList<(string Key, string? Value)> values)
    {
        if (PowerGetActiveScheme(IntPtr.Zero, out var p) != 0) throw new InvalidOperationException("Couldn't read the active power plan.");
        var scheme = Marshal.PtrToStructure<Guid>(p);
        LocalFree(p);
        var sub = SubWireless; var setting = SettingPowerSaving;
        foreach (var (_, v) in values)
        {
            var val = uint.Parse(v!);
            if (PowerWriteACValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, val) != 0) throw new InvalidOperationException("Windows refused the wireless power setting.");
            PowerWriteDCValueIndex(IntPtr.Zero, ref scheme, ref sub, ref setting, val);
        }
        PowerSetActiveScheme(IntPtr.Zero, ref scheme); // re-applies the active plan so the change takes effect immediately
    }

    [DllImport("powrprof.dll")] static extern uint PowerGetActiveScheme(IntPtr root, out IntPtr guid);
    [DllImport("powrprof.dll")] static extern uint PowerSetActiveScheme(IntPtr root, ref Guid scheme);
    [DllImport("powrprof.dll")] static extern uint PowerReadACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, out uint value);
    [DllImport("powrprof.dll")] static extern uint PowerWriteACValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, uint value);
    [DllImport("powrprof.dll")] static extern uint PowerWriteDCValueIndex(IntPtr root, ref Guid scheme, ref Guid sub, ref Guid setting, uint value);
    [DllImport("kernel32.dll")] static extern IntPtr LocalFree(IntPtr p);
}

/// <summary>One PC-wide fix: a single reversible change with a plain reason.</summary>
public sealed record SystemFix(string Id, string Title, string NowLabel, string NewLabel, string Why, string Tradeoff, bool DefaultOn,
    IChangeTarget Target, string Key, string? Old, string New);

/// <summary>What the PC looks like right now, so the same rules work on a real PC and in demo mode.</summary>
public sealed record SystemState(IChangeTarget Registry, IChangeTarget Display, IChangeTarget Power,
    int MaxRefreshHz, (string Id, string Name)? BestPlan, string ActivePlanName, bool IsLaptop, bool OnBattery, bool IsWindows11,
    IChangeTarget? Wireless = null, bool HasWifi = false);

/// <summary>PC-wide optimisations. Each one is a setting Windows itself exposes, changed the same way Settings changes it.
/// No service disabling, no timer or boot tweaks, nothing that weakens security.</summary>
public static class SystemFixes
{
    public const string RecordingKey = @"Software\Microsoft\Windows\CurrentVersion\GameDVR|HistoricalCaptureEnabled";
    public const string GameModeKey = @"Software\Microsoft\GameBar|AutoGameModeEnabled";
    public const string GraphicsKey = @"Software\Microsoft\DirectX\UserGpuPreferences|DirectXUserGlobalSettings";

    public static List<SystemFix> Find(SystemState s)
    {
        var list = new List<SystemFix>();

        if (int.TryParse(s.Display.Read("refresh"), out var hz) && s.MaxRefreshHz >= hz + 10)
            list.Add(new("refresh", "Screen refresh rate", $"{hz} Hz", $"{s.MaxRefreshHz} Hz",
                $"Your screen can show {s.MaxRefreshHz} frames a second but Windows is driving it at {hz}. Every game is limited to what the screen shows, so this is the biggest free upgrade on the list.",
                "None. A laptop on battery uses slightly more power.", true, s.Display, "refresh", hz.ToString(), s.MaxRefreshHz.ToString()));

        if (s.Registry.Read(RecordingKey) == "dword:1")
            list.Add(new("recording", "Background recording", "On", "Off",
                "Windows is recording the last few minutes of every game in the background. That takes GPU encoder time and constant disk writes.",
                "The \"record what happened\" shortcut (Win+Alt+G) stops working. Normal recording still works.", true, s.Registry, RecordingKey, "dword:1", "dword:0"));

        if (s.Registry.Read(GameModeKey) == "dword:0")
            list.Add(new("gamemode", "Game Mode", "Off", "On",
                "Game Mode stops Windows Update from installing drivers or asking to restart while you play, and gives the game priority over background work.",
                "None.", true, s.Registry, GameModeKey, "dword:0", "dword:1"));

        if (s.IsWindows11)
        {
            var cur = s.Registry.Read(GraphicsKey);
            var text = cur?.StartsWith("sz:") == true ? cur[3..] : "";
            if (!text.Contains("SwapEffectUpgradeEnable=1"))
            {
                var cleaned = string.Join(";", text.Split(';', StringSplitOptions.RemoveEmptyEntries).Where(p => !p.StartsWith("SwapEffectUpgradeEnable")));
                var next = (cleaned.Length > 0 ? cleaned + ";" : "") + "SwapEffectUpgradeEnable=1;";
                list.Add(new("windowed", "Optimizations for windowed games", "Off", "On",
                    "Lets older DirectX 10 and 11 games in windowed or borderless mode use the modern, faster way of putting frames on screen. Lower input delay, and Auto HDR and variable refresh start working there.",
                    "Rarely, an old game shows a glitch. Undo, or turn it off for that one game in Graphics settings.", true, s.Registry, GraphicsKey, cur, "sz:" + next));
            }
        }

        if (s.BestPlan is { } best && !s.OnBattery && !string.Equals(s.Power.Read("plan"), best.Id, StringComparison.OrdinalIgnoreCase))
            list.Add(new("power", "Power plan", s.ActivePlanName, best.Name,
                "The CPU stops parking itself and slowing down between frames, which mostly helps the worst stutters rather than the average.",
                s.IsLaptop ? "More heat and fan noise all the time, not just in games. Game Session does this only while you play, which suits laptops better." : "A few more watts when the PC is idle.",
                !s.IsLaptop, s.Power, "plan", s.Power.Read("plan"), best.Id));

        if (s.HasWifi && s.Wireless is { } wifi && wifi.Read("mode") is { } mode && mode != "0")
            list.Add(new("wireless-power", "Wi-Fi adapter power saving", Label(mode), Label("0"),
                "Windows is allowed to slow your Wi-Fi radio down to save power. That adds latency and can cause small stutters and spikes in online games.",
                "A little more battery use on a laptop. Only matters if you play over Wi-Fi; wired connections are unaffected.", true, wifi, "mode", mode, "0"));

        return list;

        static string Label(string v) => int.TryParse(v, out var i) && i >= 0 && i < WirelessPowerTarget.Labels.Length ? WirelessPowerTarget.Labels[i] : v;
    }

    public static SystemState Real(HardwareInfo hw)
    {
        var best = PowerPlans.BestForGaming();
        var active = PowerPlans.Active();
        var name = active is { } a ? PowerPlans.All().FirstOrDefault(p => p.Id == a).Name ?? "Unknown" : "Unknown";
        var hasWifi = NetworkInterface.GetAllNetworkInterfaces().Any(n => n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && n.OperationalStatus == OperationalStatus.Up);
        return new SystemState(new UserRegistryTarget(), new DisplayTarget(), new PowerPlanTarget(), DisplayTarget.MaxRefresh(),
            best is { } b ? (b.Id.ToString(), b.Name) : null, name, hw.IsLaptop, Health.OnBattery(), Environment.OSVersion.Version.Build >= 22000,
            hasWifi ? new WirelessPowerTarget() : null, hasWifi);
    }
}
