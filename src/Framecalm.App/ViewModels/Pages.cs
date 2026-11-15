using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;
using Framecalm.App.Controls;
using Framecalm.App.Services;
using Framecalm.Backups;
using Framecalm.Core;
using Framecalm.Windows;

namespace Framecalm.App.ViewModels;

// =====================================================================================
// Game Session
// =====================================================================================
public sealed class ProcRowVM(SessionVM page, ProcessUse p, bool approved) : Observable
{
    public string Name => p.Name;
    public string Title => p.Title.Length > 0 ? p.Title : p.HasWindow ? p.Name : "Background";
    public string Cpu => $"{p.CpuPercent:0.0}%";
    public string Mem => p.MemoryBytes >= 1L << 30 ? $"{p.MemoryBytes / (double)(1L << 30):0.0} GB" : $"{p.MemoryBytes >> 20} MB";
    public double CpuBar => Math.Clamp(p.CpuPercent / 12.0, 0.02, 1) * 60;
    public bool CanClose => p.HasWindow;
    bool _approved = approved;
    public bool Approved { get => _approved; set { if (Set(ref _approved, value)) page.SetApproved(Name, value); } }
    public ICommand CloseCmd => new Cmd(() => page.Close(Name));
}

public sealed class SessionVM : Observable, IRefresh
{
    readonly MainVM _m;
    readonly DispatcherTimer _watch = new() { Interval = TimeSpan.FromSeconds(3) };
    public SessionVM(MainVM m)
    {
        _m = m;
        _watch.Tick += (_, _) => Watch();
        if (!m.IsDemo) _watch.Start();
    }

    public SessionOptions Options => _m.S.Settings.Session;
    public bool PowerPlan { get => Options.PowerPlan; set { Options.PowerPlan = value; _m.SaveSettings(); Raise(); } }
    public bool HighPriority { get => Options.HighPriority; set { Options.HighPriority = value; _m.SaveSettings(); Raise(); } }
    public bool AutoSession { get => _m.S.Settings.AutoSession; set { _m.S.Settings.AutoSession = value; _m.SaveSettings(); Raise(); } }

    public bool Active => _m.S.Session.Active != null;
    public string StateText => Active ? "ON" : "OFF";
    public string ActiveGame => _m.S.Session.Active is { } a ? _m.Find(a.GameId)?.Name ?? a.GameId : "";
    public string Headline => Active ? $"Session running for {ActiveGame}" : "Session is off";
    public string Sub => Active
        ? "Everything below goes back the moment the game closes. Closed apps stay closed; reopen them when you like."
        : AutoSession ? "It starts by itself when one of your games launches, and undoes itself when the game closes." : "Start it before you play. It undoes itself when the game closes.";
    public ObservableCollection<string> Log { get; } = [];
    public ObservableCollection<ProcRowVM> Procs { get; } = [];
    public ObservableCollection<GameVM> Games { get; } = [];
    GameVM? _game;
    public GameVM? Game { get => _game; set => Set(ref _game, value); }
    public string PlanText { get; private set; } = "";

    public ICommand StartCmd => new Cmd(() => Start(Game), () => !Active && Game != null);
    public ICommand StopCmd => new Cmd(Stop, () => Active);
    public ICommand RefreshCmd => new AsyncCmd(LoadProcs);
    public ICommand FocusCmd => new Cmd(() => MainVM.OpenUri("ms-settings:quiethours"));
    public ICommand PowerModeCmd => new Cmd(() => MainVM.OpenUri("ms-settings:powersleep"));

    public void Refresh()
    {
        Games.Clear();
        foreach (var g in _m.Games.Where(g => g.G.ProcessName != null)) Games.Add(g);
        Game ??= Games.FirstOrDefault();
        PlanText = _m.IsDemo ? "Switches to High performance (this PC has it)" :
            PowerPlans.BestForGaming() is { } b ? $"Switches to {b.Name}, then back" : "Only Balanced exists on this PC. Use Power mode: Best performance instead";
        RaiseAll();
        _ = LoadProcs();
    }

    async Task LoadProcs()
    {
        var list = await _m.S.ProcessesAsync();
        Procs.Clear();
        foreach (var p in list.Where(p => p.Name is not ("Idle" or "System" or "Framecalm" or "dwm" or "csrss" or "explorer")).Take(8))
            Procs.Add(new ProcRowVM(this, p, Options.CloseApps.Contains(p.Name, StringComparer.OrdinalIgnoreCase)));
    }

    public void SetApproved(string name, bool on)
    {
        Options.CloseApps.RemoveAll(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (on) Options.CloseApps.Add(name);
        _m.SaveSettings();
    }

    public void Close(string name)
    {
        var n = _m.S.CloseApp(name);
        _m.ShowToast(n > 0 ? $"Asked {name} to close. If it has unsaved work, it will ask you first." : $"{name} has no window to close. Close it from its tray icon.");
        _ = LoadProcs();
    }

    void Start(GameVM? g)
    {
        if (g is null) return;
        Log.Clear();
        foreach (var l in _m.S.Session.Start(g.G, Options)) Log.Add(l);
        RaiseAll();
    }

    void Stop()
    {
        foreach (var l in _m.S.Session.Stop()) Log.Add(l);
        RaiseAll();
    }

    void Watch()
    {
        if (!AutoSession) return;
        var active = _m.S.Session.Active;
        if (active != null)
        {
            var g = _m.Find(active.GameId);
            if (g != null && !_m.S.IsRunning(g.G)) { Stop(); _m.ShowToast($"{g.Name} closed. Game Session ended and everything was put back."); }
            return;
        }
        var running = _m.Games.FirstOrDefault(g => g.AppliedCount > 0 && _m.S.IsRunning(g.G));
        if (running != null) { Start(running); _m.ShowToast($"{running.Name} started. Game Session is on."); }
    }
}

// =====================================================================================
// Health checks + startup apps
// =====================================================================================
public sealed class HealthRowVM(HealthCheck c)
{
    public string Title => c.Title;
    public string Finding => c.Finding;
    public string StateText => c.State switch { HealthState.Good => "Good", HealthState.Attention => "Check", _ => "Info" };
    public string State => c.State.ToString();
    public string? Action => c.Action;
    public ICommand ActionCmd => new Cmd(() => MainVM.OpenUri(c.ActionUri));
}

public sealed class StartupRowVM(HealthVM page, StartupRow r) : Observable
{
    public string Name => r.Name;
    public string Initial => string.IsNullOrEmpty(r.Name) ? "?" : char.ToUpperInvariant(r.Name[0]).ToString();
    public string Detail => r.Detail;
    public string Impact => r.Impact;
    bool _on = r.Enabled;
    public bool Enabled { get => _on; set { if (_on == value) return; _on = value; Raise(); page.Toggle(r, value); } }
}

public sealed class FixRowVM(SystemFix? fix, string title, string now, string after, string why, string tradeoff, bool on) : Observable
{
    public SystemFix? Fix => fix;
    public string Title => title;
    public string Now => now;
    public string After => after;
    public string Why => why;
    public string Tradeoff => tradeoff;
    bool _include = on;
    public bool Include { get => _include; set { if (Set(ref _include, value)) Changed?.Invoke(); } }
    public Action? Changed;
}

public sealed class HealthVM(MainVM m) : Observable, IRefresh
{
    public ObservableCollection<HealthRowVM> Checks { get; } = [];
    public ObservableCollection<StartupRowVM> Startup { get; } = [];
    public ObservableCollection<FixRowVM> Fixes { get; } = [];
    public int Attention { get; private set; }
    public string Summary => Attention == 0 ? "Nothing else needs your attention." : $"{Attention} more thing{(Attention == 1 ? "" : "s")} worth a look.";
    public int FixCount => Fixes.Count(f => f.Include);
    public bool HasFixes => Fixes.Count > 0;
    public string FixHeadline => Fixes.Count == 0 ? "This PC is set up well" : Fixes.Count == 1 ? "1 fix ready for this PC" : $"{Fixes.Count} fixes ready for this PC";
    public string FixSub => Fixes.Count == 0
        ? "Refresh rate, power plan, Wi-Fi power saving, background recording and windowed-game optimizations all check out. Framecalm keeps watching these."
        : "Display, power and network settings that hold the whole PC back, not just one game. Each one is a setting Windows offers itself, changed the same way Settings changes it, and undoable in History.";
    public string ApplyText => FixCount == 1 ? "Apply 1 fix" : $"Apply {FixCount} fixes";

    public ICommand ApplyFixesCmd => new AsyncCmd(ApplyFixes, () => FixCount > 0);

    public Task ApplyFixesAsync() => ApplyFixes();

    async Task ApplyFixes()
    {
        var picked = Fixes.Where(f => f.Include).ToList();
        var done = 0;
        var problems = new List<string>();
        foreach (var row in picked)
        {
            if (row.Fix is not { } f) { foreach (var g in m.Library.WrongGpu) await g.FixGpu(quiet: true); done++; continue; }
            var rec = new ChangeRecord { Scope = "windows", ScopeName = "This PC", Key = f.Key, Label = f.Title, Old = f.Old, New = f.New, OldLabel = f.NowLabel, NewLabel = f.NewLabel };
            var r = await Task.Run(() => m.S.Tx.Apply(f.Target, [rec]));
            if (r.Ok) done++; else problems.Add($"{f.Title}: {r.Message}");
        }
        await m.ReprobeAsync();
        Refresh();
        m.ShowToast(problems.Count == 0
            ? $"{done} fix{(done == 1 ? "" : "es")} applied and verified. Undo any of them in History."
            : $"{done} applied. {string.Join(" ", problems)}", problems.Count > 0);
    }

    public void Refresh()
    {
        Fixes.Clear();
        List<SystemFix> fixes = [];
        try { fixes = SystemFixes.Find(m.S.PcState(m.Hw)); } catch { }
        foreach (var f in fixes) Fixes.Add(new FixRowVM(f, f.Title, f.NowLabel, f.NewLabel, f.Why, f.Tradeoff, f.DefaultOn));
        var wrong = m.Library.WrongGpu;
        if (wrong.Count > 0)
            Fixes.Add(new FixRowVM(null, "Games on the wrong graphics chip", $"{wrong.Count} not pinned", m.Hw.Dedicated?.ShortName ?? "Fast GPU",
                $"{string.Join(", ", wrong.Take(3).Select(g => g.Name))}{(wrong.Count > 3 ? " and more" : "")} can start on the slow built-in graphics. Pinning uses the same per-game setting as Windows' Graphics page.",
                "None. Undo per game in History.", true));
        foreach (var f in Fixes) f.Changed = () => { Raise(nameof(FixCount)); Raise(nameof(ApplyText)); };
        var fixIds = fixes.Select(f => f.Id).Append(wrong.Count > 0 ? "hybrid" : "").ToHashSet();

        Checks.Clear();
        var list = m.S.Health(m.Hw).Where(c => !fixIds.Contains(c.Id)).ToList();
        foreach (var c in list.OrderBy(c => c.State == HealthState.Attention ? 0 : c.State == HealthState.Info ? 1 : 2)) Checks.Add(new HealthRowVM(c));
        Attention = list.Count(c => c.State == HealthState.Attention);
        Startup.Clear();
        try { foreach (var s in m.S.StartupItems()) Startup.Add(new StartupRowVM(this, s)); } catch { }
        RaiseAll();
    }

    public void Toggle(StartupRow r, bool on)
    {
        var now = m.S.Startup.Read(r.Key);
        var rec = new ChangeRecord
        {
            Scope = "windows", ScopeName = "Startup apps", Key = r.Key, Label = $"{r.Name} at startup",
            Old = now, New = on ? "enabled" : "disabled", OldLabel = now == "disabled" ? "Off" : "On", NewLabel = on ? "On" : "Off",
        };
        var res = m.S.Tx.Apply(m.S.Startup, [rec]);
        m.ShowToast(res.Ok ? $"{r.Name} {(on ? "will start" : "won't start")} with Windows. Same switch as Task Manager; undo in History." : res.Message, !res.Ok);
    }
}

// =====================================================================================
// Benchmarks ("Proof")
// =====================================================================================
public sealed class RunRowVM(GameVM g, SavedRun r)
{
    public string Game => g.Name;
    public string When => r.Taken.ToString("d MMM, HH:mm");
    public string Label => r.Label.StartsWith("round") ? "Auto-tune" : char.ToUpper(r.Label[0]) + r.Label[1..];
    public string Fps => $"{r.AvgFps:0}";
    public string Low => $"{1000 / Math.Max(0.1, r.P99Ms):0}";
    public string P99 => $"{r.P99Ms:0.0}";
    public string Source => r.Source == CaptureSource.Demo ? "Demo" : "Measured";
    public DateTime Taken => r.Taken;
}

public sealed class ProofVM(MainVM m) : Observable, IRefresh
{
    public ObservableCollection<GameVM> Games { get; } = [];
    public ObservableCollection<RunRowVM> Runs { get; } = [];
    GameVM? _game;
    public GameVM? Game { get => _game; set { Set(ref _game, value); Raise(nameof(CanCapture)); } }
    public bool Available => m.S.Bench.Available;
    public string ToolText => m.IsDemo ? "Demo mode: runs are simulated and labelled DEMO." : Available ? "PresentMon 2 (open source, by Intel) reads frame timing from Windows itself. Nothing touches the game." : m.S.Bench.UnavailableReason ?? "";
    public bool CanCapture => Available && Game != null && !Busy;
    bool _busy;
    public bool Busy { get => _busy; set { Set(ref _busy, value); Raise(nameof(CanCapture)); } }
    double _progress;
    public double Progress { get => _progress; set => Set(ref _progress, value); }
    public SavedRun? Result { get; private set; }
    public BottleneckResult? Bottleneck { get; private set; }
    public string? ResultText { get; private set; }
    public string? Throttle { get; private set; }

    public void Select(GameVM g) { Refresh(); Game = Games.FirstOrDefault(x => x == g) ?? g; }

    public void Refresh()
    {
        Games.Clear();
        foreach (var g in m.Games.Where(g => g.G.ProcessName != null)) Games.Add(g);
        Game ??= Games.FirstOrDefault();
        Runs.Clear();
        foreach (var r in m.Games.SelectMany(g => m.S.Captures(g.Id).Select(r => new RunRowVM(g, r))).OrderByDescending(r => r.Taken).Take(30)) Runs.Add(r);
        RaiseAll();
    }

    public ICommand CaptureCmd => new AsyncCmd(Capture, () => CanCapture);

    async Task Capture()
    {
        if (Game is null) return;
        Busy = true;
        Result = null; ResultText = null; Throttle = null;
        RaiseAll();
        try
        {
            if (!m.IsDemo && !m.S.IsRunning(Game.G)) { m.ShowToast($"Start {Game.Name} first, then capture while you play.", bad: true); return; }
            var cap = await m.S.Bench.CaptureAsync(Game.G.ProcessName!, 30, new Progress<double>(v => Progress = v), CancellationToken.None);
            if (cap.Frames.Count < 100) { m.ShowToast("Too few frames. Keep the game in the foreground while capturing.", bad: true); return; }
            Result = SavedRun.From(cap, "single");
            m.S.SaveCapture(Game.Id, Result);
            Bottleneck = BottleneckAnalyzer.Analyze(cap.Frames, m.Hw.RefreshHz);
            Throttle = BottleneckAnalyzer.Throttling(cap.Frames);
            ResultText = $"{Result.AvgFps:0} FPS average, slowest 1% of frames at {Result.P99Ms:0.0} ms ({1000 / Result.P99Ms:0} FPS).";
            Game.Refresh();
        }
        catch (Exception ex) { m.ShowToast(ex.Message, bad: true); }
        finally { Busy = false; Refresh(); }
    }
}

// =====================================================================================
// History & undo
// =====================================================================================
public sealed class HistRowVM(HistoryVM page, ChangeRecord c)
{
    public string Label => c.Label;
    public string Old => c.OldLabel ?? c.Old ?? "not set";
    public string New => c.NewLabel ?? c.New ?? "";
    public string StatusText => c.Status switch { ChangeStatus.Applied => "Active", ChangeStatus.Undone => "Undone", _ => "Kept yours" };
    public bool IsActive => c.Status == ChangeStatus.Applied;
    public ICommand UndoCmd => new AsyncCmd(() => page.UndoAsync([c.Id], c.Label));
}

public sealed class BatchVM(HistoryVM page, List<ChangeRecord> items)
{
    public string Scope => items[0].ScopeName;
    public string When => items[0].AppliedAt.ToString("ddd d MMM · HH:mm");
    public string Where => items[0].Kind switch { TargetKind.ConfigFile => Path.GetFileName(items[0].Target), TargetKind.Registry => "Windows graphics preference", TargetKind.StartupItem => "Startup apps (Task Manager)", _ => "" };
    public List<HistRowVM> Rows { get; } = items.Select(i => new HistRowVM(page, i)).ToList();
    public bool AnyActive => items.Any(i => i.Status == ChangeStatus.Applied);
    public ICommand UndoCmd => new AsyncCmd(() => page.UndoAsync(items.Where(i => i.Status == ChangeStatus.Applied).Select(i => i.Id).ToList(), $"this {Scope} change set"));
}

public sealed class ConflictVM(List<Conflict> conflicts, Func<HashSet<string>, HashSet<string>, Task> resolve) : Observable
{
    public string Title => conflicts.Count == 1 ? "You changed this after Framecalm did" : $"You changed {conflicts.Count} of these after Framecalm did";
    public List<ConflictRow> Rows { get; } = conflicts.Select(c => new ConflictRow(c)).ToList();
    public ICommand KeepCmd => new AsyncCmd(() => resolve([], conflicts.Select(c => c.Change.Id).ToHashSet()));
    public ICommand RestoreCmd => new AsyncCmd(() => resolve(conflicts.Select(c => c.Change.Id).ToHashSet(), []));
}

public sealed record ConflictRow(Conflict C)
{
    public string Label => C.Change.Label;
    public string Ours => C.Change.NewLabel ?? C.Change.New ?? "";
    public string Now => C.CurrentValue ?? "removed";
    public string Original => C.Change.OldLabel ?? C.Change.Old ?? "";
}

public sealed class HistoryVM(MainVM m) : Observable, IRefresh
{
    public ObservableCollection<BatchVM> Batches { get; } = [];
    public int ActiveCount { get; private set; }
    public string Summary => ActiveCount == 0 ? "Nothing Framecalm changed is active right now." : $"{ActiveCount} change{(ActiveCount == 1 ? "" : "s")} active. Every one can be undone, alone or all together.";

    public void Refresh()
    {
        var h = m.S.Tx.History();
        Batches.Clear();
        foreach (var b in h.GroupBy(c => c.BatchId).OrderByDescending(g => g.First().AppliedAt)) Batches.Add(new BatchVM(this, b.ToList()));
        ActiveCount = h.Count(c => c.Status == ChangeStatus.Applied);
        RaiseAll();
    }

    public ICommand UndoAllCmd => new AsyncCmd(() => UndoAsync(m.S.Tx.History().Where(c => c.Status == ChangeStatus.Applied).Select(c => c.Id).ToList(), "everything"), () => ActiveCount > 0);

    public async Task UndoAsync(List<string> ids, string what)
    {
        if (ids.Count == 0) return;
        var r = await Task.Run(() => m.S.Tx.Undo(ids));
        Report(r, what);
        if (r.Conflicts.Count > 0)
        {
            m.Dialog = new ConflictVM(r.Conflicts.ToList(), async (force, keep) =>
            {
                m.Dialog = null;
                var r2 = await Task.Run(() => m.S.Tx.Undo(force.Concat(keep), force, keep));
                m.ShowToast(force.Count > 0 ? "Put back the original values." : "Kept your values. Framecalm won't touch them.");
                Refresh();
                foreach (var g in m.Games) g.Refresh();
            });
        }
        Refresh();
        foreach (var g in m.Games) g.Refresh();
    }

    void Report(UndoResult r, string what)
    {
        if (r.Errors.Count > 0) m.ShowToast($"Undid {r.Undone}. {string.Join(" ", r.Errors)}", bad: true);
        else if (r.Conflicts.Count == 0) m.ShowToast($"Undone: {what}. Original values are back and verified.");
        else if (r.Undone > 0) m.ShowToast($"Undid {r.Undone}. {r.Conflicts.Count} need your call.");
    }
}

// =====================================================================================
// Settings
// =====================================================================================
public sealed class SettingsVM(MainVM m) : Observable, IRefresh
{
    public bool ReduceMotion { get => m.S.Settings.ReduceMotion; set { m.S.Settings.ReduceMotion = value; Motion.ForceReduced = value; m.SaveSettings(); Raise(); } }
    public bool CheckUpdates { get => m.S.Settings.CheckUpdates; set { m.S.Settings.CheckUpdates = value; m.SaveSettings(); Raise(); } }
    public bool CloseToTray { get => m.S.Settings.CloseToTray; set { m.S.Settings.CloseToTray = value; m.SaveSettings(); Raise(); } }
    public bool LaunchFramecalm { get => m.StartupPage.LaunchFramecalm; set { m.StartupPage.LaunchFramecalm = value; Raise(); } }
    public System.Windows.Input.ICommand CheckNowCmd => new AsyncCmd(() => m.CheckForUpdateAsync(manual: true));
    public bool AutoSession { get => m.S.Settings.AutoSession; set { m.S.Settings.AutoSession = value; m.SaveSettings(); Raise(); } }
    public bool DownloadCovers { get => m.S.Settings.DownloadCovers; set { m.S.Settings.DownloadCovers = value; m.SaveSettings(); Raise(); if (value) foreach (var g in m.Games) _ = g.LoadArtAsync(); } }
    public string Version => "Version " + (typeof(SettingsVM).Assembly.GetName().Version?.ToString(3) ?? "0.1.0");
    public string DataPath => m.IsDemo ? "Demo mode keeps everything in memory." : Paths.State;
    public ICommand OpenDataCmd => new Cmd(() => MainVM.OpenUri(Paths.State));
    public ICommand SourceCmd => new Cmd(() => MainVM.OpenUri("https://github.com/krezio/framecalm-releases/issues"));
    public ICommand SiteCmd => new Cmd(() => MainVM.OpenUri("https://framecalm.krezio.xyz"));
    public ICommand UndoEverythingCmd => m.HistoryPage.UndoAllCmd;
    public void Refresh() => RaiseAll();
}
