using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Text.Json;
using Framecalm.Core;
using Microsoft.Win32;

namespace Framecalm.Windows;

public sealed class SessionOptions
{
    public bool PowerPlan { get; set; } = true;
    public bool HighPriority { get; set; }          // opt-in: small gain, and it can starve voice chat
    public List<string> CloseApps { get; set; } = []; // process names the user approved
}

/// <summary>What a Game Session changed, saved to disk so a crash still gets reverted on next start.</summary>
public sealed class SessionState
{
    public string GameId { get; set; } = "";
    public string Process { get; set; } = "";
    public Guid? PreviousPlan { get; set; }
    public Guid? SessionPlan { get; set; }
    public int? Pid { get; set; }
    public string? PreviousPriority { get; set; }
    public List<string> Closed { get; set; } = [];
    public DateTime Started { get; set; } = DateTime.Now;
}

public interface ISessionHost
{
    SessionState? Active { get; }
    List<string> Start(GameInstall game, SessionOptions options);
    List<string> Stop();
}

public sealed class GameSession(string stateFile) : ISessionHost
{
    public SessionState? Active { get; private set; } = Load(stateFile);

    static SessionState? Load(string f)
    {
        try { return File.Exists(f) ? JsonSerializer.Deserialize<SessionState>(File.ReadAllText(f), Json.Options) : null; }
        catch { return null; }
    }

    void Save()
    {
        if (Active is null) { if (File.Exists(stateFile)) File.Delete(stateFile); return; }
        Directory.CreateDirectory(Path.GetDirectoryName(stateFile)!);
        File.WriteAllText(stateFile, JsonSerializer.Serialize(Active, Json.Options));
    }

    public List<string> Start(GameInstall game, SessionOptions o)
    {
        var log = new List<string>();
        var s = new SessionState { GameId = game.Id, Process = game.ProcessName ?? "" };
        Active = s;
        Save();

        if (o.PowerPlan)
        {
            var best = PowerPlans.BestForGaming();
            var now = PowerPlans.Active();
            if (best is { } b && now != b.Id)
            {
                s.PreviousPlan = now;
                if (PowerPlans.Set(b.Id)) { s.SessionPlan = b.Id; log.Add(L.F("Power plan: {0}", b.Name)); }
            }
            else if (best is null) log.Add("Power plan: this PC only has Balanced. Set Power mode to Best performance in Settings.");
            else log.Add("Power plan: already on the fastest plan.");
        }

        if (o.HighPriority && game.ProcessName is { Length: > 0 } pn)
        {
            if (game.Definition?.StrictAntiCheat == true) log.Add("Priority: skipped. This game's anti-cheat doesn't allow other apps to touch it.");
            else
            {
                var p = Process.GetProcessesByName(pn).FirstOrDefault();
                if (p is null) log.Add("Priority: set when the game starts.");
                else
                {
                    try
                    {
                        s.Pid = p.Id;
                        s.PreviousPriority = p.PriorityClass.ToString();
                        p.PriorityClass = ProcessPriorityClass.High; // never RealTime
                        log.Add("Priority: High");
                    }
                    catch { log.Add("Priority: Windows didn't allow it for this game, left as is."); }
                }
            }
        }

        foreach (var app in o.CloseApps)
        {
            var n = Processes.CloseGracefully(app);
            if (n > 0) { s.Closed.Add(app); log.Add(L.F("Asked {0} to close", app)); }
        }
        Save();
        return log;
    }

    public List<string> Stop()
    {
        var log = new List<string>();
        var s = Active;
        if (s is null) return log;
        if (s.PreviousPlan is { } prev && PowerPlans.Active() == s.SessionPlan)
        {
            if (PowerPlans.Set(prev)) log.Add("Power plan restored");
        }
        if (s.Pid is { } pid && s.PreviousPriority is { } pp)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                p.PriorityClass = Enum.Parse<ProcessPriorityClass>(pp);
                log.Add("Priority restored");
            }
            catch { /* game already closed: nothing to restore */ }
        }
        if (s.Closed.Count > 0) log.Add(L.F("Closed apps ({0}) can be reopened any time; we don't restart apps for you.", (object)string.Join(", ", s.Closed)));
        Active = null;
        Save();
        return log;
    }
}

public enum HealthState { Good, Attention, Info }

public sealed record HealthCheck(string Id, string Title, HealthState State, string Finding, string? Action, string? ActionUri);

/// <summary>Read-only checks. Every fix is either a reversible change we make or a link to the right Windows page.</summary>
public static class Health
{
    public static List<HealthCheck> Run(HardwareInfo hw)
    {
        var list = new List<HealthCheck>();

        var gm = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\GameBar", "AutoGameModeEnabled", null);
        list.Add(gm is int i && i == 0
            ? new("gamemode", "Game Mode", HealthState.Attention, "Game Mode is off. It stops Windows Update from installing and restarting during games.", "Open Game Mode settings", "ms-settings:gaming-gamemode")
            : new("gamemode", "Game Mode", HealthState.Good, "On. It mostly prevents background updates and notifications during play; don't expect extra FPS from it.", null, null));

        var capture = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\GameDVR", "HistoricalCaptureEnabled", null);
        list.Add(capture is int c && c == 1
            ? new("recording", "Background recording", HealthState.Attention, "Windows is recording the last few minutes of every game. That costs some GPU time and disk writes.", "Open Captures settings", "ms-settings:gaming-gamedvr")
            : new("recording", "Background recording", HealthState.Good, "Off. Nothing is recording in the background.", null, null));

        var hags = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", null);
        list.Add(new("hags", "Hardware-accelerated GPU scheduling", HealthState.Info,
            hags is int h && h == 2 ? "On. Needed for DLSS Frame Generation; otherwise the effect on FPS is small either way." : "Off. Turn it on only if you use DLSS Frame Generation; otherwise the difference is small.",
            "Open Graphics settings", "ms-settings:display-advancedgraphics"));

        var gpu = hw.Primary;
        if (gpu?.DriverDate is { } d)
        {
            var months = (DateTime.Now - d).TotalDays / 30.4;
            var link = gpu.Vendor switch { "NVIDIA" => "https://www.nvidia.com/Download/index.aspx", "AMD" => "https://www.amd.com/en/support/download/drivers.html", "Intel" => "https://www.intel.com/content/www/us/en/download-center/home.html", _ => null };
            list.Add(months > 6
                ? new("driver", "Graphics driver", HealthState.Attention, L.F("Your {0} driver is from {1:MMM yyyy}, about {2:0} {2|month|months} old. New games often get fixes in newer drivers.", gpu.ShortName, d, months), L.F("Get it from {0}", gpu.Vendor), link)
                : new("driver", "Graphics driver", HealthState.Good, L.F("{0} driver from {1:MMM yyyy}. Recent enough.", gpu.ShortName, d), null, null));
        }

        if (hw.IsHybrid)
            list.Add(new("hybrid", "Two graphics chips", HealthState.Info,
                L.F("This PC has {0} and {1}. Games sometimes start on the weaker one. Check each game's page: Framecalm can pin it to {1}.", hw.Integrated!.ShortName, hw.Dedicated!.ShortName), null, null));

        var sys = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory)!);
        var free = (double)sys.AvailableFreeSpace / sys.TotalSize;
        list.Add(free < 0.1
            ? new("disk", "Drive space", HealthState.Attention, $"Only {sys.AvailableFreeSpace / 1e9:0} GB free on {sys.Name.TrimEnd('\\')}. Games stutter when Windows runs low on space for its page file and shader caches. Roomy (free, same maker) can clear it.", "Get Roomy", "https://roomy.krezio.xyz")
            : new("disk", "Drive space", HealthState.Good, $"{sys.AvailableFreeSpace / 1e9:0} GB free on {sys.Name.TrimEnd('\\')}.", null, null));

        if (hw.IsLaptop)
        {
            list.Add(OnBattery()
                ? new("power", "Plugged in", HealthState.Attention, "You're on battery. Laptops cut GPU power a lot on battery; plug in to play.", null, null)
                : new("power", "Plugged in", HealthState.Good, "Plugged in, so the GPU can use its full power.", null, null));
        }

        var net = NetworkInterface.GetAllNetworkInterfaces().FirstOrDefault(n =>
            n.OperationalStatus == OperationalStatus.Up &&
            (n.NetworkInterfaceType is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211) &&
            n.GetIPProperties().GatewayAddresses.Count > 0);
        if (net != null)
        {
            var wifi = net.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
            var mbps = net.Speed > 0 ? net.Speed / 1_000_000 : 0;
            list.Add(wifi
                ? new("network", "Network", HealthState.Info, mbps > 0 ? L.F("Connected over Wi-Fi at {0} Mbps. A wired connection has steadier latency; worth trying for competitive games.", mbps) : L.T("Connected over Wi-Fi. A wired connection has steadier latency; worth trying for competitive games."), null, null)
                : new("network", "Network", HealthState.Good, mbps > 0 ? L.F("Connected by cable at {0} Mbps. The steadiest option for online games.", mbps) : L.T("Connected by cable. The steadiest option for online games."), null, null));
        }
        return list;
    }

    public static bool OnBattery() => GetSystemPowerStatus(out var s) && s.ACLineStatus == 0;

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    struct SYSTEM_POWER_STATUS { public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag; public int BatteryLifeTime, BatteryFullLifeTime; }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);
}
