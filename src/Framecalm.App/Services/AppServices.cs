using System.Text.Json;
using Framecalm.Backups;
using Framecalm.Benchmarks;
using Framecalm.Core;
using Framecalm.Demo;
using Framecalm.Discovery;
using Framecalm.GameAdapters;
using Framecalm.Windows;

namespace Framecalm.App.Services;

public sealed record StartupRow(string Key, string Name, string Detail, bool Enabled, string Impact);

/// <summary>Everything the UI talks to. Real mode uses the PC; demo mode swaps in DemoPc and memory-only stores.</summary>
public sealed class AppServices
{
    public bool IsDemo { get; }
    public IReadOnlyList<GameDefinition> Catalog { get; } = Framecalm.GameAdapters.Catalog.All;
    public IHardwareProbe HardwareProbe { get; }
    public IGameDiscovery Discovery { get; }
    public IBenchmarkRunner Bench { get; }
    public IStateStore Store { get; }
    public TransactionManager Tx { get; }
    public IChangeTarget GpuPrefs { get; }
    public IChangeTarget Startup { get; }
    public TweakTarget Tweaks { get; }
    public ISessionHost Session { get; }
    public DemoPc? Demo { get; }
    public List<GameInstall> Games { get; set; } = [];
    public Settings Settings { get; }

    public AppServices(bool demo)
    {
        IsDemo = demo;
        if (demo)
        {
            Demo = new DemoPc();
            HardwareProbe = Demo;
            Discovery = Demo;
            Store = Demo.Store;
            GpuPrefs = Demo.GpuPrefs;
            Startup = Demo.Startup;
            Bench = new DemoPc.Bench(Demo, p => Games.FirstOrDefault(g => g.ProcessName == p)) { Speed = 12 };
            Session = new DemoSession();
            Settings = new Settings();
            var mem = new MemoryTweakBackend();
            foreach (var id in new[] { "hags-on", "ad-id-off", "svc-fax", "menu-delay-off" })
                foreach (var a in TweakCatalog.Find(id)!.Atoms(mem.Nics)) mem.Values[a.Key] = a.Value;
            Tweaks = new TweakTarget(mem);
        }
        else
        {
            HardwareProbe = new HardwareProbe();
            Discovery = new GameDiscovery();
            Store = new FolderStateStore(Paths.State);
            GpuPrefs = new GpuPreferenceTarget();
            Startup = new StartupTarget();
            Bench = new PresentMonRunner();
            Session = new GameSession(Path.Combine(Paths.State, "session.json"));
            Settings = Settings.Load(Store);
            Tweaks = new TweakTarget(new RegistryTweakBackend(Path.Combine(AppContext.BaseDirectory, "Framecalm.ElevatedHelper.exe"), Path.Combine(Paths.State, "requests")));
        }
        Tx = new TransactionManager(Store, Resolve);
    }

    IChangeTarget? Resolve(Journal j)
    {
        if (j.Kind == TargetKind.ConfigFile)
            return Games.FirstOrDefault(g => string.Equals(g.ConfigPath, j.Target, StringComparison.OrdinalIgnoreCase)) is { } g ? ConfigTarget(g) : null;
        var sys = _system ??= BuildSystem(null);
        if (j.Kind == TargetKind.Tweak) return Tweaks;
        var targets = new List<IChangeTarget> { GpuPrefs, Startup, sys.Registry, sys.Display, sys.Power };
        if (sys.Wireless != null) targets.Add(sys.Wireless);
        return targets.FirstOrDefault(t => t.Path == j.Target);
    }

    SystemState? _system;

    /// <summary>The PC-wide settings Framecalm can fix. Demo mode gets a made-up state kept in memory.</summary>
    public SystemState PcState(HardwareInfo hw) => _system = BuildSystem(hw);

    SystemState BuildSystem(HardwareInfo? hw)
    {
        if (Demo is null) return SystemFixes.Real(hw ?? HardwareProbe.Probe());
        if (_system != null) return _system;
        var reg = new MemoryRegistryTarget(TargetKind.Registry, UserRegistryTarget.TargetPath);
        reg.Values[SystemFixes.RecordingKey] = "dword:1";
        reg.Values[SystemFixes.GameModeKey] = "dword:1";
        var display = new MemoryRegistryTarget(TargetKind.Display, DisplayTarget.TargetPath);
        display.Values["refresh"] = "60";
        var power = new MemoryRegistryTarget(TargetKind.PowerPlan, PowerPlanTarget.TargetPath);
        power.Values["plan"] = PowerPlans.Balanced.ToString();
        var wireless = new MemoryRegistryTarget(TargetKind.PowerPlan, WirelessPowerTarget.TargetPath);
        wireless.Values["mode"] = "3";
        return new SystemState(reg, display, power, 144, (PowerPlans.HighPerformance.ToString(), "High performance"), "Balanced", IsLaptop: true, OnBattery: false, IsWindows11: true,
            Wireless: wireless, HasWifi: true);
    }

    public IChangeTarget? ConfigTarget(GameInstall g)
    {
        if (g.Definition is not { Support: SupportLevel.Automatic } def || def.Adapter is null) return null;
        if (Demo != null) return Demo.Configs.TryGetValue(g.Id, out var m) ? m : null;
        return g.ConfigPath is null ? null : new ConfigFileTarget(g.ConfigPath, Framecalm.GameAdapters.Catalog.Adapters[def.Adapter], def);
    }

    /// <summary>Checks the config before anything is proposed. Unknown version or format = guided mode (fail closed).</summary>
    public (ConfigCheck Check, string? Text) ReadConfig(GameInstall g)
    {
        var def = g.Definition;
        if (def?.Adapter is null) return (ConfigCheck.Fail("No adapter"), null);
        var adapter = Framecalm.GameAdapters.Catalog.Adapters[def.Adapter];
        string? text;
        if (Demo != null) text = Demo.Configs.TryGetValue(g.Id, out var m) ? m.Text : null;
        else
        {
            if (g.ConfigPath is null || !File.Exists(g.ConfigPath)) return (ConfigCheck.Fail(L.F("Settings file not found. Start {0} once so it creates it.", g.Name)), null);
            try { text = File.ReadAllText(g.ConfigPath); }
            catch (Exception ex) { return (ConfigCheck.Fail("Couldn't read the settings file: " + ex.Message), null); }
        }
        if (text is null) return (ConfigCheck.Fail("Settings file not found."), null);
        return (adapter.Check(text, def), text);
    }

    public string? CurrentValue(GameInstall g, string text, SettingDef s) =>
        g.Definition?.Adapter is { } a ? Framecalm.GameAdapters.Catalog.Adapters[a].Get(text, s) : null;

    public bool IsRunning(GameInstall g)
    {
        if (Demo != null || g.ProcessName is null) return false;
        var ps = System.Diagnostics.Process.GetProcessesByName(g.ProcessName);
        foreach (var p in ps) p.Dispose();
        return ps.Length > 0;
    }

    public List<HealthCheck> Health(HardwareInfo hw) => Demo != null ? DemoHealth(hw) : Framecalm.Windows.Health.Run(hw);

    static List<HealthCheck> DemoHealth(HardwareInfo hw) =>
    [
        new("thermal", "Heat", HealthState.Attention, "In your last Fortnite capture the GPU clock fell 18% after four minutes while it reached 87°C. That's thermal throttling: clear the vents and raise the back of the laptop.", null, null),
        new("hybrid", "Two graphics chips", HealthState.Info, L.F("This PC has {0} and {1}. Games sometimes start on the weaker one. Two of your games do right now.", hw.Integrated!.ShortName, hw.Dedicated!.ShortName), null, null),
        new("driver", "Graphics driver", HealthState.Attention, L.F("Your {0} driver is about 9 months old. New games often get fixes in newer drivers.", hw.Dedicated.ShortName), "Get it from NVIDIA", "https://www.nvidia.com/Download/index.aspx"),
        new("recording", "Background recording", HealthState.Attention, "Windows is recording the last few minutes of every game. That costs some GPU time and disk writes.", "Open Captures settings", "ms-settings:gaming-gamedvr"),
        new("gamemode", "Game Mode", HealthState.Good, "On. It mostly stops background updates and notifications during play; don't expect extra FPS from it.", null, null),
        new("hags", "Hardware-accelerated GPU scheduling", HealthState.Info, "On. Needed for DLSS Frame Generation; otherwise the effect on FPS is small either way.", "Open Graphics settings", "ms-settings:display-advancedgraphics"),
        new("disk", "Drive space", HealthState.Good, "212 GB free on C:.", null, null),
        new("network", "Network", HealthState.Info, "Connected over Wi-Fi at 300 Mbps. A wired connection has steadier latency; worth trying for competitive games.", null, null),
    ];

    public async Task<List<ProcessUse>> ProcessesAsync()
    {
        if (Demo is null) return (await Processes.SnapshotAsync()).Take(12).ToList();
        await Task.Delay(300);
        return
        [
            new(1, "chrome", "38 tabs", 9.4, 2_310L << 20, true), new(2, "Discord", "Discord", 3.1, 610L << 20, true),
            new(3, "OneDrive", "", 2.6, 140L << 20, false), new(4, "RazerSynapse", "Razer Synapse", 1.9, 520L << 20, true),
            new(5, "Spotify", "Spotify Premium", 1.2, 330L << 20, true), new(6, "EpicGamesLauncher", "Epic Games Launcher", 0.9, 390L << 20, true),
            new(7, "MsMpEng", "", 0.6, 210L << 20, false),
        ];
    }

    public List<StartupRow> StartupItems()
    {
        if (Demo != null)
            return DemoPc.StartupApps.Select(a => new StartupRow("run:" + a.Name, a.Name, a.Publisher, Demo.Startup.Read("run:" + a.Name) != "disabled",
                L.F("Uses {0:0.0}% CPU and {1} MB right now", a.CpuAtBoot, a.MemMb))).ToList();
        var running = System.Diagnostics.Process.GetProcesses().GroupBy(p => p.ProcessName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Sum(p => { try { return p.WorkingSet64; } catch { return 0; } }), StringComparer.OrdinalIgnoreCase);
        return StartupTarget.List().Select(i =>
        {
            var exe = i.Exe is null ? null : Path.GetFileNameWithoutExtension(i.Exe);
            var impact = exe != null && running.TryGetValue(exe, out var mem) ? L.F("Running now, {0:0} MB of memory", mem / 1e6) : L.T("Not running right now");
            return new StartupRow((i.FromFolder ? "folder:" : "run:") + i.Name, i.Name, i.Command, i.Enabled, impact);
        }).ToList();
    }

    public int CloseApp(string name) => Demo != null ? 1 : Processes.CloseGracefully(name);

    // ---------- saved captures ----------
    public void SaveCapture(string gameId, SavedRun run)
    {
        var list = Captures(gameId);
        list.Add(run);
        if (list.Count > 60) list.RemoveRange(0, list.Count - 60);
        Store.WriteText($"captures/{gameId}.json", JsonSerializer.Serialize(list, Json.Options));
    }

    public List<SavedRun> Captures(string gameId)
    {
        var t = Store.ReadText($"captures/{gameId}.json");
        try { return t is null ? [] : JsonSerializer.Deserialize<List<SavedRun>>(t, Json.Options) ?? []; } catch { return []; }
    }
}

public sealed class SavedRun
{
    public DateTime Taken { get; set; }
    public CaptureSource Source { get; set; }
    public string Label { get; set; } = "";      // before / after / single / round-1 ...
    public double AvgFps { get; set; }
    public double MedianMs { get; set; }
    public double P95Ms { get; set; }
    public double P99Ms { get; set; }
    public double FrameMs { get; set; }
    public double GpuMs { get; set; }
    public double[] Trace { get; set; } = [];

    public static SavedRun From(Capture c, string label)
    {
        var s = c.Stats;
        var ft = c.Frames.Select(f => f.FrameTime).ToArray();
        var step = Math.Max(1, ft.Length / 1500);
        var gpu = c.Frames.Where(f => f.GpuBusy > 0).Select(f => f.GpuBusy).OrderBy(x => x).ToArray();
        return new SavedRun
        {
            Taken = c.Taken, Source = c.Source, Label = label, AvgFps = s.AvgFps, MedianMs = s.MedianMs, P95Ms = s.P95Ms, P99Ms = s.P99Ms,
            FrameMs = s.MedianMs, GpuMs = gpu.Length > 0 ? gpu[gpu.Length / 2] : 0,
            Trace = Enumerable.Range(0, ft.Length / step).Select(i => Math.Round(ft.Skip(i * step).Take(step).Max(), 2)).ToArray(),
        };
    }

    public RunStats Stats => new(AvgFps, MedianMs, P95Ms, P99Ms, Trace.Length);
}

public sealed class Settings
{
    public bool ReduceMotion { get; set; }
    public bool CheckUpdates { get; set; }
    public bool CloseToTray { get; set; } = true;
    public bool AutoSession { get; set; } = true;
    public bool SeenIntro { get; set; }
    public bool DownloadCovers { get; set; } = true;
    public int TargetFps { get; set; }
    /// <summary>"auto" (follow Windows), "en" or "ru". Applied when Framecalm starts.</summary>
    public string Language { get; set; } = "auto";
    public SessionOptions Session { get; set; } = new();

    public static Settings Load(IStateStore s)
    {
        try { return s.ReadText("settings.json") is { } t ? JsonSerializer.Deserialize<Settings>(t, Json.Options) ?? new() : new(); }
        catch { return new(); }
    }

    public void Save(IStateStore s) => s.WriteText("settings.json", JsonSerializer.Serialize(this, Json.Options));
}

/// <summary>Game Session in demo mode: reports what it would do, changes nothing.</summary>
sealed class DemoSession : ISessionHost
{
    public SessionState? Active { get; private set; }
    public List<string> Start(GameInstall game, SessionOptions o)
    {
        Active = new SessionState { GameId = game.Id, Process = game.ProcessName ?? "" };
        var log = new List<string> { "Power plan: High performance" };
        if (o.HighPriority) log.Add("Priority: High");
        foreach (var a in o.CloseApps) log.Add(L.F("Asked {0} to close", a));
        return log;
    }
    public List<string> Stop() { Active = null; return ["Power plan restored", "Priority restored"]; }
}
