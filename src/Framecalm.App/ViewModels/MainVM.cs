using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Framecalm.App.Controls;
using Framecalm.App.Services;
using Framecalm.Backups;
using Framecalm.Core;
using Framecalm.Windows;

namespace Framecalm.App.ViewModels;

public sealed class MainVM : Observable
{
    public static MainVM? Current { get; private set; }
    public AppServices S { get; }
    public bool IsDemo => S.IsDemo;
    public HardwareInfo Hw { get; private set; } = new() { Cpu = "" };
    public ObservableCollection<GameVM> Games { get; } = [];

    public LibraryVM Library { get; }
    public OptimizeVM OptimizePage { get; }
    public DashboardVM DashboardPage { get; }
    public AutoOptimizeVM AutoPage { get; }
    public StartupVM StartupPage { get; }
    public SessionVM SessionPage { get; }
    public HealthVM HealthPage { get; }
    public ProofVM ProofPage { get; }
    public HistoryVM HistoryPage { get; }
    public SettingsVM SettingsPage { get; }

    public MainVM(bool demo)
    {
        Current = this;
        S = new AppServices(demo);
        Motion.ForceReduced = S.Settings.ReduceMotion;
        Library = new(this);
        OptimizePage = new(this);
        DashboardPage = new(this);
        AutoPage = new(this);
        StartupPage = new(this);
        SessionPage = new(this);
        HealthPage = new(this);
        ProofPage = new(this);
        HistoryPage = new(this);
        SettingsPage = new(this);
        _page = demo || S.Settings.SeenIntro ? DashboardPage : new FirstRunVM(this);
        NavCmd = new Cmd(p => Go((string)p!));
        BackCmd = new Cmd(Back, () => _back.Count > 0);
        CloseDialogCmd = new Cmd(() => Dialog = null);
    }

    object _page;
    readonly Stack<(object Page, string Tab)> _back = new();
    public object Page { get => _page; private set { Set(ref _page, value); } }

    string _tab = "dashboard";
    public string Tab => _tab;
    public bool TabDashboard { get => _tab is "dashboard" or "auto"; set { if (value) Go("dashboard"); } }
    public bool TabOptimize { get => _tab == "optimize"; set { if (value) Go("optimize"); } }
    public bool TabStartup { get => _tab == "startup"; set { if (value) Go("startup"); } }

    // five places in the sidebar; related pages are tabs at the top of the content
    public bool GroupHome { get => _tab is "dashboard" or "auto"; set { if (value) Go("dashboard"); } }
    public bool GroupOptimize { get => _tab is "optimize" or "health" or "startup"; set { if (value && !GroupOptimize) Go("optimize"); } }
    public bool GroupGames { get => _tab is "library" or "session" or "proof"; set { if (value && !GroupGames) Go("library"); } }
    public bool ShowOptimizeTabs => ShowNav && GroupOptimize && Page is OptimizeVM or HealthVM or StartupVM;
    public bool ShowGamesTabs => ShowNav && GroupGames && Page is LibraryVM or SessionVM or ProofVM;

    // ---------- updates ----------
    string? _update, _updateUrl;
    Framecalm.App.Services.UpdateCheck.Result? _updateInfo;
    bool _installing;
    public string? UpdateText { get => _update; private set { Set(ref _update, value); Raise(nameof(HasUpdate)); } }
    public bool HasUpdate => _update != null;
    public string UpdateAction => _installing ? "" : "  Get it";
    public System.Windows.Input.ICommand GetUpdateCmd => new Cmd(() => _ = GetUpdateAsync());

    /// <summary>Installed copies download and install the update in place; the portable zip opens the download.</summary>
    async Task GetUpdateAsync()
    {
        if (_installing || _updateInfo is null) return;
        if (!Framecalm.App.Services.Updater.CanInstall) { OpenUri(_updateUrl); return; }
        _installing = true;
        Raise(nameof(UpdateAction));
        var latest = _updateInfo.Latest;
        try
        {
            UpdateText = L.T("Downloading update");
            var progress = new Progress<double>(f => UpdateText = L.F("Downloading update {0:0}%", f * 100));
            var file = await Framecalm.App.Services.Updater.DownloadAsync(_updateInfo, progress);
            UpdateText = L.T("Installing update");
            await Task.Delay(400);
            Framecalm.App.Services.Updater.RunInstaller(file);
            (System.Windows.Application.Current.MainWindow as MainWindow)?.QuitForUpdate();
        }
        catch (Exception ex)
        {
            _installing = false;
            Raise(nameof(UpdateAction));
            UpdateText = L.F("{0} is out", latest);
            ShowToast(L.T("Couldn't install the update automatically, so the download page is opening instead.") + " " + L.T(ex.Message), bad: true);
            OpenUri(_updateUrl);
        }
    }

    /// <summary>Asks GitHub for the newest release. Quiet unless there is one, or unless asked from Settings.</summary>
    public async Task CheckForUpdateAsync(bool manual = false)
    {
        if (IsDemo) { if (manual) ShowToast("Demo mode: no update check."); return; }
        var r = await Framecalm.App.Services.UpdateCheck.RunAsync();
        if (r is { Newer: true }) { _updateUrl = r.Url; _updateInfo = r; UpdateText = L.F("{0} is out", r.Latest); if (manual) ShowToast(L.F("Framecalm {0} is out. Press Get it at the top to install it.", r.Latest)); }
        else if (manual) ShowToast(r is null ? "Couldn't reach GitHub. Check your connection and try again." : "You're on the newest version.", r is null);
    }

    public string OptimizeCount => Loaded ? OptimizePage.NavCount : "";
    public void RaiseNav() => Raise(nameof(OptimizeCount));
    public bool TabLibrary { get => _tab == "library"; set { if (value) Go("library"); } }
    public bool TabSession { get => _tab == "session"; set { if (value) Go("session"); } }
    public bool TabHealth { get => _tab == "health"; set { if (value) Go("health"); } }
    public bool TabProof { get => _tab == "proof"; set { if (value) Go("proof"); } }
    public bool TabHistory { get => _tab == "history"; set { if (value) Go("history"); } }
    public bool TabSettings { get => _tab == "settings"; set { if (value) Go("settings"); } }
    public bool ShowNav => Page is not FirstRunVM;

    public ICommand NavCmd { get; }
    public ICommand BackCmd { get; }
    public ICommand CloseDialogCmd { get; }

    public void Go(string tab)
    {
        _back.Clear();
        object page = tab switch
        {
            "session" => SessionPage, "health" => HealthPage, "proof" => ProofPage,
            "history" => HistoryPage, "settings" => SettingsPage, "optimize" => OptimizePage,
            "library" => Library, "auto" => AutoPage, "startup" => StartupPage, _ => DashboardPage,
        };
        if (page is IRefresh r) r.Refresh();
        SetPage(page, tab);
    }

    public void Open(object page, bool remember = true)
    {
        if (remember && Page is not RunVM and not AutoTuneVM) _back.Push((Page, _tab));
        if (page is IRefresh r) r.Refresh();
        SetPage(page, _tab);
    }

    void Back()
    {
        if (_back.Count == 0) return;
        var (p, t) = _back.Pop();
        if (p is IRefresh r) r.Refresh();
        SetPage(p, t);
    }

    void SetPage(object page, string tab)
    {
        _tab = tab;
        Page = page;
        RaiseAll();
    }

    public string PcLine => Hw.Primary is { } g ? $"{g.ShortName}, {Hw.RefreshHz} Hz" : "";

    // ---------- toast + dialog ----------
    string? _toast;
    bool _toastBad;
    DispatcherTimer? _toastTimer;
    public string? Toast { get => _toast; private set => Set(ref _toast, value); }
    public bool ToastBad { get => _toastBad; private set => Set(ref _toastBad, value); }

    public void ShowToast(string text, bool bad = false)
    {
        ToastBad = bad;
        Toast = L.T(text);
        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(bad ? 7 : 4.5) };
        _toastTimer.Tick += (_, _) => { Toast = null; _toastTimer!.Stop(); };
        _toastTimer.Start();
    }

    public void ClearToast() { _toastTimer?.Stop(); Toast = null; }

    object? _dialog;
    public object? Dialog { get => _dialog; set => Set(ref _dialog, value); }

    // ---------- loading ----------
    bool _loaded;
    public bool Loaded { get => _loaded; private set => Set(ref _loaded, value); }

    Task? _loading;
    /// <summary>Loads once; later calls wait for the same load (the tray start and the window can both ask).</summary>
    public Task LoadAsync() => _loading ??= LoadOnceAsync();

    async Task LoadOnceAsync()
    {
        Hw = await Task.Run(() => S.HardwareProbe.Probe());
        var games = await Task.Run(() => S.Discovery.Discover(S.Catalog));
        S.Games = games.ToList();
        var notes = S.Tx.Recover();
        if (S.Session.Active != null && !IsDemo && !S.Games.Any(g => g.Id == S.Session.Active.GameId && S.IsRunning(g)))
        {
            S.Session.Stop();
            notes.Add("A Game Session from last time was still active, so it was ended and everything it changed was put back.");
        }
        Games.Clear();
        foreach (var g in S.Games) Games.Add(new GameVM(this, g));
        foreach (var g in Games) g.Refresh();
        Library.Refresh();
        OptimizePage.Refresh();
        var art = Task.WhenAll(Games.Select(g => g.LoadArtAsync()));
        if (IsDemo) await Task.WhenAny(art, Task.Delay(9000)); // demo and recordings wait briefly so tiles appear with their art
        Loaded = true;
        if (S.Settings.CheckUpdates) _ = CheckForUpdateAsync();
        if (Page is DashboardVM d) d.Refresh();
        RaiseAll();
        if (notes.Count > 0) ShowToast(string.Join(" ", notes.Select(L.T)));
    }

    public GameVM? Find(string id) => Games.FirstOrDefault(g => g.Id == id);

    /// <summary>Re-read hardware after a PC-wide change (the refresh rate may have moved).</summary>
    public async Task ReprobeAsync()
    {
        if (!IsDemo) Hw = await Task.Run(() => S.HardwareProbe.Probe());
        foreach (var g in Games) g.Refresh();
        RaiseAll();
    }

    public void ShowIntro() => SetPage(new FirstRunVM(this), "dashboard");

    public void SaveSettings() { if (!IsDemo) S.Settings.Save(S.Store); }

    public static void OpenUri(string? uri)
    {
        if (string.IsNullOrEmpty(uri)) return;
        if (Current?.IsDemo == true) { Current.ShowToast("Demo mode: links don't open."); return; }
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch { }
    }
}

public interface IRefresh { void Refresh(); }

// =====================================================================================
// first launch
// =====================================================================================
public sealed class FirstRunVM(MainVM m) : Observable
{
    string _stage = "intro";
    public string Stage { get => _stage; set => Set(ref _stage, value); }
    public string Found { get; private set; } = "";
    public string FoundDetail { get; private set; } = "";
    public ICommand StartCmd => new AsyncCmd(Start);
    public bool NotStore => !Framecalm.App.Services.Packaged.IsPackaged;
    public bool WantUpdates { get; set; } = !Framecalm.App.Services.Packaged.IsPackaged;
    public bool WantTray { get; set; } = true;

    async Task Start()
    {
        Stage = "scanning";
        var wait = Task.Delay(1600);
        if (!m.Loaded) await m.LoadAsync();
        await wait;
        var auto = m.Games.Count(g => g.G.Support == SupportLevel.Automatic);
        List<SystemFix> fixes = [];
        try { fixes = SystemFixes.Find(m.S.PcState(m.Hw)); } catch { }
        Found = L.T("Your plan is ready.");
        FoundDetail = L.F("{0} PC {0|fix|fixes} and {1} {1|game|games} found. Opening it now.", fixes.Count, m.Games.Count);
        Raise(nameof(Found)); Raise(nameof(FoundDetail));
        Stage = "found";
        m.S.Settings.SeenIntro = true;
        m.S.Settings.CheckUpdates = WantUpdates;
        m.S.Settings.CloseToTray = WantTray;
        if (WantUpdates) _ = m.CheckForUpdateAsync();
        m.SaveSettings();
        await Task.Delay(1400);
        m.Go("auto");
    }
}

// =====================================================================================
// library
// =====================================================================================
public sealed class LibraryVM(MainVM m) : Observable, IRefresh
{
    public MainVM M => m;
    string _filter = "all";
    public bool All { get => _filter == "all"; set { if (value) SetFilter("all"); } }
    public bool Auto { get => _filter == "auto"; set { if (value) SetFilter("auto"); } }
    public bool Guided { get => _filter == "guided"; set { if (value) SetFilter("guided"); } }
    void SetFilter(string f) { _filter = f; RaiseAll(); }

    public IEnumerable<GameVM> Shown => m.Games.Where(g => _filter == "all" || (_filter == "auto" ? g.G.Support == SupportLevel.Automatic : g.G.Support != SupportLevel.Automatic));
    public int Count => m.Games.Count;
    public string CountText => L.F("{0} {0|game|games}", m.Games.Count);
    public string Sources => string.Join(", ", m.Games.Select(g => L.T(g.LauncherName)).Distinct());
    public List<GameVM> WrongGpu => m.Games.Where(g => g.GpuWrong).ToList();
    public string WrongGpuText => WrongGpu.Count switch
    {
        0 => "",
        1 => L.F("{0} isn't pinned to your {1}. Windows may start it on the {2}, which is far slower.", WrongGpu[0].Name, m.Hw.Dedicated?.ShortName, m.Hw.Integrated?.ShortName),
        var n => L.F("{0} games aren't pinned to your {1}. Windows may start them on the {2}, which is far slower.", n, m.Hw.Dedicated?.ShortName, m.Hw.Integrated?.ShortName),
    };
    public ICommand FixAllGpuCmd => new AsyncCmd(async () => { foreach (var g in WrongGpu) await g.FixGpu(quiet: true); m.ShowToast("Done. Those games now ask for your dedicated GPU. Undo any time in History."); Refresh(); });

    public string CpuText => m.Hw.CpuShort;
    public string GpuText => m.Hw.Primary?.ShortName ?? L.T("Unknown");
    public string VramText => m.Hw.Primary is { VramBytes: > 0 } g ? L.F("{0:0} GB", g.VramBytes / (double)(1L << 30)) : "";
    public string RamText => L.F("{0:0} GB", m.Hw.RamBytes / (double)(1L << 30));
    public string ScreenText => L.F("{0} Hz", m.Hw.RefreshHz);
    public string FormText => L.T(m.Hw.IsLaptop ? "Laptop" : "Desktop");

    public void Refresh() { foreach (var g in m.Games) g.Refresh(); RaiseAll(); }
}

// =====================================================================================
// one game: tile + detail page
// =====================================================================================
public sealed class GameVM : Observable, IRefresh
{
    public MainVM M { get; }
    public GameInstall G { get; }
    public GameVM(MainVM m, GameInstall g)
    {
        M = m; G = g;
        Art = Covers.For(g.Id, g.Name, g.ArtPath);
        var guided = g.Definition?.Guided is { Count: > 0 } gs ? gs
            : g.Definition?.Settings.Where(s => s.Special is null && s.Targets.ContainsKey("performance")).OrderByDescending(s => s.TypicalGain).Take(6)
                .Select(s => new GuidedStep { Where = "Graphics settings > " + s.Label, Set = s.Show(s.Targets["performance"]), Why = s.Why, Load = s.Load }).ToList() ?? [];
        Steps = new(guided.Select((s, i) => new GuidedStepVM(i + 1, s)));
    }

    public string Id => G.Id;
    public string Name => G.Name;
    ImageSource _art = null!;
    public ImageSource Art { get => _art; private set => Set(ref _art, value); }

    /// <summary>Swaps the generated cover for official art once it is cached (or downloaded, if allowed).</summary>
    public async Task LoadArtAsync()
    {
        if (G.ArtPath != null && File.Exists(G.ArtPath)) return;
        if (!M.S.Settings.DownloadCovers) return;
        var file = await Services.CoverArt.EnsureAsync(Id, G.Cover ?? G.Definition?.Cover);
        if (file != null) Art = Covers.For(Id, Name, file);
    }
    public string LauncherName => G.Launcher switch { Launcher.BattleNet => "Battle.net", Launcher.Xbox => "Xbox app", Launcher.Minecraft => "Minecraft Launcher", Launcher.Other => "Other", var l => l.ToString() };
    public bool IsAuto => G.Support == SupportLevel.Automatic;
    public bool IsGuided => G.Support == SupportLevel.Guided;
    public bool IsBasic => G.Support == SupportLevel.Basic;
    public string SupportLabel => L.T(IsAuto && CanTune ? "Automatic" : IsAuto ? "In-game steps" : IsGuided ? "In-game steps" : "GPU and Session");
    public string? AntiCheat => G.Definition?.AntiCheat;
    public string? Notes => G.Definition?.Notes is { } n ? L.T(n) : null;
    public bool Strict => G.Definition?.StrictAntiCheat == true;
    public ObservableCollection<GuidedStepVM> Steps { get; }

    // ---- config state ----
    public ConfigCheck? Check { get; private set; }
    public bool CanTune => IsAuto && Check?.Ok == true;
    public bool ShowSteps => Steps.Count > 0 && !CanTune;
    public string CheckText => Check is null ? "" : Check.Ok ? (Check.Version is { } v ? L.F("{0} checked (format {1})", Path.GetFileName(G.ConfigPath), v) : L.F("{0} checked", Path.GetFileName(G.ConfigPath))) : L.T(Check.Reason);

    // ---- GPU ----
    public bool Hybrid => M.Hw.IsHybrid && G.ExePath != null;
    public bool GpuWrong { get; private set; }
    public string GpuText { get; private set; } = "";
    public ICommand FixGpuCmd => new AsyncCmd(() => FixGpu());

    public async Task FixGpu(bool quiet = false)
    {
        if (G.ExePath is null) return;
        var now = M.S.GpuPrefs.Read(G.ExePath);
        var rec = new ChangeRecord
        {
            Scope = Id, ScopeName = Name, Key = G.ExePath, Label = "Graphics chip",
            Old = now, New = GpuPreferenceTarget.HighPerformance,
            OldLabel = GpuPreferenceTarget.Describe(now), NewLabel = M.Hw.Dedicated?.ShortName ?? "High performance GPU",
        };
        var r = await Task.Run(() => M.S.Tx.Apply(M.S.GpuPrefs, [rec]));
        if (!quiet) M.ShowToast(r.Ok ? L.F("{0} will now use your {1}. Restart the game if it's open.", Name, M.Hw.Dedicated?.ShortName) : L.T(r.Message), !r.Ok);
        Refresh();
    }

    // ---- results ----
    public SavedRun? Last { get; private set; }
    public double HeroFps => Last?.AvgFps ?? 0;
    public bool HasRuns => Last != null;
    public string HeroCaption { get; private set; } = "";
    public double[]? BeforeTrace { get; private set; }
    public double[]? AfterTrace { get; private set; }
    public double OneLow => Last is { P99Ms: > 0 } l ? 1000 / l.P99Ms : 0;
    public double P99 => Last?.P99Ms ?? 0;
    public double GainFps { get; private set; }
    public bool HasGain => GainFps >= 1;
    public BottleneckResult? Bottleneck { get; private set; }
    public double TargetMs => 1000.0 / Math.Max(30, M.Hw.RefreshHz);
    public string TargetLabel => $"{M.Hw.RefreshHz} FPS";
    public bool DemoData => Last?.Source == CaptureSource.Demo;
    public int AppliedCount { get; private set; }
    public string ActiveText => L.F("{0} Framecalm {0|change is|changes are} active for this game.", AppliedCount);

    public string Status { get; private set; } = "";
    public string StatusKind { get; private set; } = "muted";

    public void Refresh()
    {
        if (IsAuto) Check = M.S.ReadConfig(G).Check;
        if (Hybrid)
        {
            var pref = M.S.GpuPrefs.Read(G.ExePath!);
            GpuWrong = pref?.Contains("GpuPreference=2") != true;
            GpuText = GpuWrong
                ? L.F("Not pinned. Windows may run it on the {0}.", M.Hw.Integrated!.ShortName)
                : L.F("Runs on {0}", M.Hw.Dedicated!.ShortName);
        }
        else GpuText = M.Hw.Primary is { } p ? L.F("Runs on {0}", p.ShortName) : "";

        var runs = M.S.Captures(Id);
        Last = runs.LastOrDefault();
        var before = runs.LastOrDefault(r => r.Label.StartsWith("before"));
        var after = runs.LastOrDefault(r => r.Label.StartsWith("after"));
        BeforeTrace = before?.Trace;
        AfterTrace = after?.Trace ?? Last?.Trace;
        if (before != null && after != null && after.Taken > before.Taken)
        {
            var b = runs.Where(r => r.Label.StartsWith("before")).TakeLast(3).Average(r => r.AvgFps);
            var a = runs.Where(r => r.Label.StartsWith("after")).TakeLast(3).Average(r => r.AvgFps);
            GainFps = a - b;
            HeroCaption = L.F("AVG FPS · WAS {0:0}", b);
        }
        else if (runs.LastOrDefault(r => r.Label == "round-0") is { } r0 && runs.LastOrDefault(r => r.Label.StartsWith("round-") && r.Label != "round-0" && r.Taken > r0.Taken) is { } rn)
        {
            GainFps = rn.AvgFps - r0.AvgFps;
            BeforeTrace = r0.Trace;
            HeroCaption = L.F("AVG FPS · WAS {0:0} · AUTO-TUNE", r0.AvgFps);
        }
        else { GainFps = 0; HeroCaption = Last is null ? "" : L.T("AVG FPS · LAST RUN"); }
        if (Last != null && Last.GpuMs > 0)
            Bottleneck = BottleneckAnalyzer.Analyze(Enumerable.Repeat(new FrameSample(Last.FrameMs, Last.GpuMs, 0), 40).ToList(), M.Hw.RefreshHz);

        AppliedCount = M.S.Tx.History().Count(c => c.Scope == Id && c.Status == ChangeStatus.Applied);
        (Status, StatusKind) = this switch
        {
            { GpuWrong: true } => (L.T("Wrong GPU risk"), "warn"),
            { HasGain: true } => (L.F(DemoData ? "+{0:0} FPS · demo" : "+{0:0} FPS measured", GainFps), "accent"),
            { AppliedCount: > 0 } => (L.F("{0} {0|change|changes} applied", AppliedCount), "good"),
            { IsAuto: true, CanTune: true } => (L.T("Ready to tune"), "muted"),
            { IsAuto: true } => (L.T("In-game steps"), "muted"),
            { IsGuided: true } => (L.T(Strict ? "In-game steps, strict anti-cheat" : "In-game steps"), "muted"),
            _ => (L.T("GPU fix and Session"), "muted"),
        };
        RaiseAll();
    }

    public ICommand OpenCmd => new Cmd(() => M.Open(this));
    public ICommand TuneCmd => new Cmd(() => M.Open(new TuneVM(M, this, autotune: false)), () => CanTune);
    public ICommand AutoTuneCmd => new Cmd(() => M.Open(new TuneVM(M, this, autotune: true)), () => CanTune);
    public ICommand MeasureCmd => new Cmd(() => { M.Go("proof"); M.ProofPage.Select(this); });
    public ICommand UndoGameCmd => new AsyncCmd(async () =>
    {
        var ids = M.S.Tx.History().Where(c => c.Scope == Id && c.Status == ChangeStatus.Applied).Select(c => c.Id).ToList();
        await M.HistoryPage.UndoAsync(ids, L.F("everything Framecalm changed for {0}", Name));
        Refresh();
    }, () => AppliedCount > 0);
}

public sealed class GuidedStepVM(int n, GuidedStep s)
{
    public string Index => n.ToString("00");
    public string Where => s.Where;
    public string Set => L.T(s.Set);
    public string Why => L.T(s.Why);
    public string LoadText => s.Load switch { Load.Cpu => "CPU", Load.Gpu => "GPU", _ => "CPU + GPU" };
}

/// <summary>Decorative traces behind the first-launch screen (no axis, no numbers: illustration only).</summary>
public static class Intro
{
    public static readonly double[] Spiky = Framecalm.Demo.DemoPc.Frames(58, 0.9, 40, 3, 3.5).Select(f => f.FrameTime).ToArray();
    public static readonly double[] Calm = Framecalm.Demo.DemoPc.Frames(128, 0.7, 40, 5, 0.3).Select(f => f.FrameTime).ToArray();
}
