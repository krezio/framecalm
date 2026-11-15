using Framecalm.Backups;
using Framecalm.Core;
using Framecalm.GameAdapters;

namespace Framecalm.Demo;

/// <summary>
/// A made-up mid-range gaming laptop for demos, screenshots and recordings. Every write goes to memory;
/// nothing on the real PC is read or changed. All numbers here are invented and labelled "demo" in the UI.
/// </summary>
public sealed class DemoPc : IHardwareProbe, IGameDiscovery
{
    public HardwareInfo Hardware { get; } = new()
    {
        Cpu = "AMD Ryzen 5 7535HS with Radeon Graphics",
        Threads = 12,
        RamBytes = 16L << 30,
        Gpus =
        [
            new GpuInfo("NVIDIA GeForce RTX 4050 Laptop GPU", "NVIDIA", 6L << 30, GpuKind.Dedicated, "32.0.15.6094", DateTime.Today.AddMonths(-9)),
            new GpuInfo("AMD Radeon(TM) 660M", "AMD", 512L << 20, GpuKind.Integrated, "31.0.21912.14", DateTime.Today.AddMonths(-5)),
        ],
        IsLaptop = true,
        RefreshHz = 144,
        ScreenWidth = 1920,
        ScreenHeight = 1080,
    };

    public HardwareInfo Probe() => Hardware;

    public readonly Dictionary<string, MemoryConfigTarget> Configs = new();
    public readonly MemoryRegistryTarget GpuPrefs = new(TargetKind.Registry, @"HKCU\Software\Microsoft\DirectX\UserGpuPreferences");
    public readonly MemoryRegistryTarget Startup = new(TargetKind.StartupItem, @"HKCU\...\StartupApproved\Run");
    public readonly MemoryStateStore Store = new();

    /// <summary>Base FPS (demo) on the right GPU with the original settings, and how GPU-bound each game is.</summary>
    static readonly Dictionary<string, (double Fps, double GpuShare)> Base = new()
    {
        ["minecraft-java"] = (68, 0.55),
        ["fortnite"] = (80, 0.93),
        ["rocket-league"] = (118, 0.9),
        ["valorant"] = (212, 0.5),
        ["roblox"] = (60, 0.6),
        ["cs2"] = (164, 0.7),
        ["apex-legends"] = (88, 0.88),
        ["stardew"] = (144, 0.3),
    };

    public DemoPc()
    {
        var defs = Catalog.All.ToDictionary(d => d.Id);
        Configs["minecraft-java"] = new(defs["minecraft-java"], new MinecraftOptionsAdapter(), MinecraftOptions, @"C:\Users\Player\AppData\Roaming\.minecraft\options.txt");
        Configs["fortnite"] = new(defs["fortnite"], Catalog.Adapters["unreal-ini"], FortniteIni, @"C:\Users\Player\AppData\Local\FortniteGame\Saved\Config\WindowsClient\GameUserSettings.ini");
        Configs["rocket-league"] = new(defs["rocket-league"], Catalog.Adapters["unreal-ini"], RocketLeagueIni, @"C:\Users\Player\Documents\My Games\Rocket League\TAGame\Config\TASystemSettings.ini");
        Startup.Values["run:Discord"] = "enabled";
        Startup.Values["run:Spotify"] = "enabled";
        Startup.Values["run:OneDrive"] = "enabled";
        Startup.Values["run:EpicGamesLauncher"] = "enabled";
        Startup.Values["run:Steam"] = "enabled";
        Startup.Values["run:RazerSynapse"] = "enabled";
        foreach (var c in Configs.Values) c.Baseline = c.Text;
        foreach (var exe in PrePinned) GpuPrefs.Values[exe] = "GpuPreference=2;";
    }

    public static readonly (string Name, string Publisher, double CpuAtBoot, int MemMb)[] StartupApps =
    [
        ("Discord", "Discord Inc.", 1.9, 410), ("Spotify", "Spotify AB", 1.1, 260), ("OneDrive", "Microsoft", 0.8, 120),
        ("EpicGamesLauncher", "Epic Games", 2.4, 380), ("Steam", "Valve", 1.6, 300), ("RazerSynapse", "Razer", 2.9, 520),
    ];

    static readonly string[] PrePinned =
    [
        @"C:\Program Files\Epic Games\rocketleague\Binaries\Win64\RocketLeague.exe",
        @"C:\Riot Games\VALORANT\live\ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe",
        @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe",
        @"C:\Program Files (x86)\Steam\steamapps\common\Apex Legends\r5apex.exe",
        @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Stardew Valley.exe",
    ];

    public IReadOnlyList<GameInstall> Discover(IReadOnlyList<GameDefinition> catalog)
    {
        GameDefinition D(string id) => catalog.First(d => d.Id == id);
        GameInstall G(string id, Launcher l, string exe) => new()
        {
            Id = id, Name = D(id).Name, Launcher = l, Support = D(id).Support, Definition = D(id),
            ProcessName = D(id).ProcessNames.FirstOrDefault(), ExePath = exe, ConfigPath = Configs.TryGetValue(id, out var c) ? c.Path : null, Cover = D(id).Cover,
        };
        return
        [
            G("minecraft-java", Launcher.Minecraft, @"C:\Users\Player\AppData\Local\Packages\Microsoft.4297127D64EC6_8wekyb3d8bbwe\LocalCache\Local\runtime\java-runtime-delta\windows-x64\java-runtime-delta\bin\javaw.exe"),
            G("fortnite", Launcher.Epic, @"C:\Program Files\Epic Games\Fortnite\FortniteGame\Binaries\Win64\FortniteClient-Win64-Shipping.exe"),
            G("rocket-league", Launcher.Epic, @"C:\Program Files\Epic Games\rocketleague\Binaries\Win64\RocketLeague.exe"),
            G("roblox", Launcher.Roblox, @"C:\Users\Player\AppData\Local\Roblox\Versions\version-demo\RobloxPlayerBeta.exe"),
            G("valorant", Launcher.Riot, @"C:\Riot Games\VALORANT\live\ShooterGame\Binaries\Win64\VALORANT-Win64-Shipping.exe"),
            G("cs2", Launcher.Steam, @"C:\Program Files (x86)\Steam\steamapps\common\Counter-Strike Global Offensive\game\bin\win64\cs2.exe"),
            G("apex-legends", Launcher.Steam, @"C:\Program Files (x86)\Steam\steamapps\common\Apex Legends\r5apex.exe"),
            new GameInstall { Id = "stardew", Name = "Stardew Valley", Launcher = Launcher.Steam, Cover = "steam:413150", Support = SupportLevel.Basic, ProcessName = "Stardew Valley", ExePath = @"C:\Program Files (x86)\Steam\steamapps\common\Stardew Valley\Stardew Valley.exe" },
        ];
    }

    /// <summary>Demo FPS for a game in its current (in-memory) state, so before/after charts react to what was applied.</summary>
    public (double Fps, double GpuShare) Model(GameInstall g)
    {
        var (fps, share) = Base.TryGetValue(g.Id, out var b) ? b : (90, 0.7);
        // the "wrong GPU" story: Fortnite and Minecraft start on the integrated chip on this laptop
        var onDedicated = g.ExePath is null || g.Id is not ("fortnite" or "minecraft-java") || (GpuPrefs.Read(g.ExePath)?.Contains("=2") ?? false);
        if (!onDedicated) fps *= 0.42;
        if (Configs.TryGetValue(g.Id, out var cfg) && g.Definition is { } def)
        {
            var bottleneck = share >= 0.85 ? Bottleneck.Gpu : share <= 0.65 ? Bottleneck.Cpu : Bottleneck.Mixed;
            double cap = 0;
            foreach (var s in def.Settings)
            {
                var before = cfg.Adapter.Get(cfg.Baseline, s);
                var now = cfg.Adapter.Get(cfg.Text, s);
                if (s.Special == "refresh")
                {
                    // the game's own frame cap: holds FPS down until it's fixed
                    if (now != null && double.TryParse(now.Trim('"'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var c) && c > 0 && c < 250) cap = c;
                    continue;
                }
                if (before is null || now is null || Recommender.Same(s, before, now)) continue;
                if (Recommender.IsFaster(s, now, before))
                {
                    fps *= 1 + s.TypicalGain / 100.0 * AutoTunePlanner.Relevance(s, bottleneck) * 0.85;
                    share = Math.Max(0.45, share - 0.02);
                }
            }
            if (cap > 0) fps = Math.Min(fps, cap - 0.6);
        }
        return (fps, share);
    }

    /// <summary>Synthetic frames around a mean FPS, with realistic jitter and occasional spikes. Deterministic per seed.</summary>
    public static List<FrameSample> Frames(double fps, double gpuShare, int seconds, int seed, double spikiness = 1)
    {
        var rng = new Random(seed);
        var list = new List<FrameSample>();
        var mean = 1000 / fps;
        double t = 0;
        while (t < seconds * 1000)
        {
            var n = (rng.NextDouble() + rng.NextDouble() + rng.NextDouble() - 1.5) * 0.18;
            var ft = mean * (1 + n);
            if (rng.NextDouble() < 0.012 * spikiness) ft *= 1.8 + rng.NextDouble() * 1.6;
            var gpu = Math.Min(ft, mean * gpuShare * (1 + n * 0.5));
            list.Add(new FrameSample(ft, gpu, ft * (1 - gpuShare * 0.6), 2100 - rng.Next(40), 71 + rng.Next(4), 4200 - rng.Next(60)));
            t += ft;
        }
        return list;
    }

    public sealed class Bench(DemoPc pc, Func<string, GameInstall?> find) : IBenchmarkRunner
    {
        int _seed = 7;
        public bool Available => true;
        public string? UnavailableReason => null;
        public double Speed { get; set; } = 1; // recordings speed captures up

        public async Task<Capture> CaptureAsync(string processName, int seconds, IProgress<double>? progress, CancellationToken ct)
        {
            var g = find(processName);
            var (fps, share) = g is null ? (90, 0.7) : pc.Model(g);
            var steps = (int)Math.Max(8, seconds * 10 / Speed);
            for (int i = 1; i <= steps; i++) { await Task.Delay(100, ct); progress?.Report((double)i / steps); }
            var run = fps * (1 + (new Random(_seed).NextDouble() - 0.5) * 0.03);
            return new Capture { Frames = Frames(run, share, seconds, _seed++, Math.Clamp(90 / fps, 0.35, 2.2)), Source = CaptureSource.Demo, Process = processName };
        }
    }

    const string MinecraftOptions = """
version:4189
ao:true
biomeBlendRadius:2
enableVsync:true
entityDistanceScaling:1.0
entityShadows:true
forceUnicodeFont:false
fov:0.0
fovEffectScale:1.0
gamma:0.5
graphicsMode:1
guiScale:0
maxFps:60
mipmapLevels:4
particles:0
renderClouds:"true"
renderDistance:16
simulationDistance:12
fullscreen:false
mouseSensitivity:0.5
soundCategory_master:1.0
key_key.forward:key.keyboard.w
""";

    const string FortniteIni = """
[/Script/FortniteGame.FortGameUserSettings]
FrameRateLimit=0.000000
bUseVSync=True
bMotionBlur=True
ResolutionSizeX=1920
ResolutionSizeY=1080
bShowGrass=True

[ScalabilityGroups]
sg.ResolutionQuality=100.000000
sg.ViewDistanceQuality=3
sg.AntiAliasingQuality=3
sg.ShadowQuality=3
sg.GlobalIlluminationQuality=3
sg.ReflectionQuality=3
sg.PostProcessQuality=3
sg.TextureQuality=3
sg.EffectsQuality=3
sg.FoliageQuality=3
sg.ShadingQuality=3
""";

    const string RocketLeagueIni = """
[SystemSettings]
DynamicLights=True
DynamicShadows=True
LightEnvironmentShadows=True
MotionBlur=False
DepthOfField=True
AmbientOcclusion=True
Bloom=True
LightShafts=True
LensFlares=True
DetailMode=2
MaxAnisotropy=8
UseVsync=False
ResX=1920
ResY=1080
""";
}

/// <summary>A config "file" that only exists in memory (demo mode).</summary>
public sealed class MemoryConfigTarget(GameDefinition def, IConfigAdapter adapter, string text, string path) : IChangeTarget
{
    public GameDefinition Definition => def;
    public IConfigAdapter Adapter => adapter;
    public string Text { get; set; } = text;
    public string Baseline { get; set; } = text;
    public TargetKind Kind => TargetKind.ConfigFile;
    public string Path => path;
    public bool IsFile => false; // in-memory: the journal keeps old values instead of a file backup
    public string? Read(string key) => adapter.Get(Text, def.Settings.First(s => s.Key == key));
    public void Write(IReadOnlyList<(string Key, string? Value)> values)
    {
        var t = Text;
        foreach (var (k, v) in values) t = adapter.Set(t, def.Settings.First(s => s.Key == k), v!);
        Text = t;
    }
}

public sealed class MemoryRegistryTarget(TargetKind kind, string path) : IChangeTarget
{
    public readonly Dictionary<string, string> Values = new(StringComparer.OrdinalIgnoreCase);
    public TargetKind Kind => kind;
    public string Path => path;
    public bool IsFile => false;
    public string? Read(string key) => Values.TryGetValue(key, out var v) ? v : null;
    public void Write(IReadOnlyList<(string Key, string? Value)> values)
    {
        foreach (var (k, v) in values) { if (v is null) Values.Remove(k); else Values[k] = v; }
    }
}
