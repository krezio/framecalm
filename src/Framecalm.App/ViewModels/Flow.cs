using System.Collections.ObjectModel;
using System.Windows.Input;
using Framecalm.App.Services;
using Framecalm.Backups;
using Framecalm.Core;

namespace Framecalm.App.ViewModels;

public sealed class ProfileOption(Profile p, string name, string blurb, int level, Action<ProfileOption> pick) : Observable
{
    public Profile Profile => p;
    public string Name => name;
    public string Blurb => blurb;
    public bool L1 => level >= 1; public bool L2 => level >= 2; public bool L3 => level >= 3; public bool L4 => level >= 4;
    bool _sel;
    public bool IsSelected { get => _sel; set { if (Set(ref _sel, value) && value) pick(this); } }

    // exactly what this profile would change in this game's settings file, worked out before anything is written
    public List<PreviewLine> Preview { get; set; } = [];
    public string CountText { get; set; } = "";
    public string MoreText { get; set; } = "";
    public bool HasMore => MoreText.Length > 0;
}

public sealed record PreviewLine(string Label, string Old, string New);

public sealed class TargetOption(int fps, string name, string sub, Action<TargetOption> pick) : Observable
{
    public int Fps => fps;
    public string FpsText => fps.ToString();
    public string Name => name;
    public string Sub => sub;
    bool _sel;
    public bool IsSelected { get => _sel; set { if (Set(ref _sel, value) && value) pick(this); } }
}

public sealed class ChangeRowVM : Observable
{
    public ChangeRowVM(int i, ProposedChange c, double maxGain)
    {
        Change = c;
        Index = i.ToString("00");
        Bar = maxGain <= 0 ? 0 : c.Setting.TypicalGain / maxGain;
    }
    public ProposedChange Change { get; }
    public string Index { get; }
    public string Label => Change.Setting.Label;
    public string Old => Change.OldLabel;
    public string New => Change.NewLabel;
    public string Why => Change.Setting.Why;
    public string Tradeoff => Change.Setting.Tradeoff;
    public string LoadText => Change.Setting.Load switch { Load.Cpu => "CPU", Load.Gpu => "GPU", _ => "CPU + GPU" };
    public double Bar { get; }
    public double BarWidth => 4 + Bar * 76;
    public string Gain => Change.Setting.Special == "refresh" ? "Uncaps" : Change.Setting.TypicalGain >= 1 ? $"+{Change.Setting.TypicalGain:0}%" : "Small";
    bool _include = true, _open;
    public bool Include { get => _include; set => Set(ref _include, value); }
    public bool Open { get => _open; set => Set(ref _open, value); }
    public ICommand ToggleCmd => new Cmd(() => Open = !Open);
}

/// <summary>Profile & target picker, then the review list.</summary>
public sealed class TuneVM : Observable
{
    readonly MainVM _m;
    public GameVM Game { get; }
    public TuneVM(MainVM m, GameVM game, bool autotune)
    {
        _m = m; Game = game;
        _mode = autotune ? "target" : "profile";
        Profiles =
        [
            new(Profile.Light, "Light", "Cuts what you won't miss: blur, cinematic effects, wrong frame caps.", 1, Pick),
            new(Profile.Balanced, "Balanced", "The heavy hitters at sensible levels. Looks almost the same.", 2, Pick),
            new(Profile.Performance, "Performance", "Clearly faster. You'll notice simpler shadows and effects.", 3, Pick),
            new(Profile.MaxFps, "Max FPS", "Everything that buys frames. For competitive play and weak GPUs.", 4, Pick),
        ];
        var hz = m.Hw.RefreshHz;
        Targets =
        [
            new(hz, $"Match my {hz} Hz screen", "Every refresh gets a new frame", PickT),
            new(60, "Steady 60", "Smooth and quiet, good for laptops", PickT),
            new(hz >= 165 ? 144 : 120, hz >= 165 ? "144" : "120", "A middle ground", PickT),
            new(240, "240", "Competitive, needs a fast screen", PickT),
        ];
        BuildPreviews();
        Profiles[1].IsSelected = true;
        (m.S.Settings.TargetFps is > 0 and var t ? Targets.FirstOrDefault(x => x.Fps == t) ?? Targets[0] : Targets[0]).IsSelected = true;
        Measure = m.S.Bench.Available;
    }

    public ObservableCollection<ProfileOption> Profiles { get; }

    void BuildPreviews()
    {
        (ConfigCheck check, string? text) cfg;
        try { cfg = _m.S.ReadConfig(Game.G); } catch { cfg = (ConfigCheck.Fail("unreadable"), null); }
        foreach (var o in Profiles)
        {
            if (!cfg.check.Ok || cfg.text is null || Game.G.Definition is null)
            {
                o.CountText = "Can't read the settings file yet";
                continue;
            }
            var text = cfg.text;
            var props = Recommender.Propose(Game.G.Definition, s => _m.S.CurrentValue(Game.G, text, s), _m.Hw, o.Profile)
                .OrderByDescending(p => p.Setting.Special == "refresh" ? 999 : p.Setting.TypicalGain).ToList();
            if (props.Count == 0) { o.CountText = "Nothing to change: already at or below this"; continue; }
            var est = AutoTunePlanner.Estimate(100, props, Core.Bottleneck.Mixed) - 100;
            var gain = est >= 1 ? $"typically +{est:0}% FPS" : props.Any(p => p.Setting.Special == "refresh") ? "removes a frame cap" : "small gain";
            o.CountText = $"{props.Count} setting{(props.Count == 1 ? "" : "s")} change, {gain}";
            o.Preview = props.Take(4).Select(p => new PreviewLine(p.Setting.Label, p.OldLabel, p.NewLabel)).ToList();
            o.MoreText = props.Count > 4 ? $"and {props.Count - 4} more" : "";
        }
    }
    public ObservableCollection<TargetOption> Targets { get; }
    Profile _profile = Profile.Balanced;
    int _target;
    void Pick(ProfileOption o) { _profile = o.Profile; foreach (var p in Profiles) if (p != o) p.IsSelected = false; }
    void PickT(TargetOption o) { _target = o.Fps; foreach (var t in Targets) if (t != o) t.IsSelected = false; }

    string _mode;
    public bool ModeProfile { get => _mode == "profile"; set { if (value) { _mode = "profile"; RaiseAll(); } } }
    public bool ModeTarget { get => _mode == "target"; set { if (value) { _mode = "target"; RaiseAll(); } } }

    bool _measure;
    public bool Measure { get => _measure; set => Set(ref _measure, value); }
    public bool CanMeasure => _m.S.Bench.Available;
    public string MeasureNote => _m.S.Bench.Available
        ? "3 runs of 30 seconds before and after, in the same spot in the game. Takes about 4 minutes."
        : _m.S.Bench.UnavailableReason ?? "";

    string _stage = "pick";
    public string Stage { get => _stage; set => Set(ref _stage, value); }
    public ObservableCollection<ChangeRowVM> Rows { get; } = [];
    public string Summary { get; private set; } = "";
    public string Empty { get; private set; } = "";
    public string RestartNote => Game.G.Definition?.RestartNote ?? "";
    public string ProfileName => Profiles.First(p => p.Profile == _profile).Name;

    public ICommand ReviewCmd => new Cmd(Review);
    public ICommand BackCmd => new Cmd(() => Stage = "pick");
    public ICommand ApplyCmd => new Cmd(() =>
    {
        var picked = Rows.Where(r => r.Include).Select(r => r.Change).ToList();
        _m.Open(new RunVM(_m, Game, picked, Measure, ProfileName), remember: false);
    }, () => Rows.Any(r => r.Include));
    public ICommand AutoTuneCmd => new Cmd(() =>
    {
        _m.S.Settings.TargetFps = _target;
        _m.SaveSettings();
        _m.Open(new AutoTuneVM(_m, Game, _target), remember: false);
    });

    void Review()
    {
        var (check, text) = _m.S.ReadConfig(Game.G);
        Rows.Clear();
        if (!check.Ok || text is null)
        {
            Empty = $"Framecalm can't change this file safely: {check.Reason} Nothing was touched. The game page has the steps to do it by hand.";
        }
        else
        {
            var props = Recommender.Propose(Game.G.Definition!, s => _m.S.CurrentValue(Game.G, text, s), _m.Hw, _profile);
            var max = props.Count == 0 ? 0 : props.Max(p => p.Setting.TypicalGain);
            var i = 1;
            foreach (var p in props.OrderByDescending(p => p.Setting.Special == "refresh" ? 999 : p.Setting.TypicalGain)) Rows.Add(new ChangeRowVM(i++, p, max));
            Empty = Rows.Count == 0
                ? $"Nothing needs to change for {ProfileName}. Your {Game.Name} settings are already at or below it, and Framecalm never makes anything heavier. Try a stronger profile, or auto-tune to a target."
                : "";
            var est = AutoTunePlanner.Estimate(100, props, Core.Bottleneck.Mixed) - 100;
            Summary = $"{Rows.Count} change{(Rows.Count == 1 ? "" : "s")} · typically +{est:0}% FPS on PCs like yours · measure to see yours";
        }
        Raise(nameof(Empty)); Raise(nameof(Summary)); Raise(nameof(HasRows));
        Stage = "review";
    }

    public bool HasRows => Rows.Count > 0;
}

public sealed class StepVM : Observable
{
    public StepVM(string title) => Title = title;
    public string Title { get; }
    string _state = "wait", _detail = "";
    double _progress;
    public string State { get => _state; set { Set(ref _state, value); Raise(nameof(Glyph)); } }
    public string Detail { get => _detail; set => Set(ref _detail, value); }
    public double Progress { get => _progress; set => Set(ref _progress, value); }
    public string Glyph => State switch { "done" => "", "fail" => "", "run" => "", "skip" => "", _ => "" };
    public string StateText => State switch { "done" => "Done", "fail" => "Failed", "run" => "Now", "skip" => "Skipped", _ => "" };
}

/// <summary>Shared by apply and auto-tune: pauses for the player (start/close the game) in real mode, runs straight through in demo mode.</summary>
public abstract class FlowVM(MainVM m, GameVM game) : Observable
{
    protected readonly MainVM M = m;
    public GameVM Game => game;
    string _stage = "running";
    public string Stage { get => _stage; protected set => Set(ref _stage, value); }
    public ObservableCollection<StepVM> Steps { get; } = [];
    public bool IsDemoData => M.IsDemo;

    string? _promptTitle, _promptBody, _promptButton;
    public string? PromptTitle { get => _promptTitle; set => Set(ref _promptTitle, value); }
    public string? PromptBody { get => _promptBody; set => Set(ref _promptBody, value); }
    public string? PromptButton { get => _promptButton; set => Set(ref _promptButton, value); }
    TaskCompletionSource<bool>? _wait;
    public ICommand ContinueCmd => new Cmd(() => _wait?.TrySetResult(true));
    public ICommand CancelCmd => new Cmd(() => { _wait?.TrySetResult(false); _cts.Cancel(); });
    protected readonly CancellationTokenSource _cts = new();

    protected async Task<bool> Ask(string title, string body, string button, Func<bool>? autoWhen = null)
    {
        if (M.IsDemo) { await Task.Delay(400); return true; }
        PromptTitle = title; PromptBody = body; PromptButton = button;
        var before = Stage;
        Stage = "prompt";
        _wait = new TaskCompletionSource<bool>();
        if (autoWhen != null)
            _ = Task.Run(async () => { while (!_wait.Task.IsCompleted) { if (autoWhen()) _wait.TrySetResult(true); await Task.Delay(1000); } });
        var ok = await _wait.Task;
        Stage = before;
        return ok;
    }

    protected async Task<SavedRun?> CaptureAsync(StepVM step, string label, int seconds = 30)
    {
        step.State = "run";
        var p = new Progress<double>(v => { step.Progress = v; step.Detail = $"{v * seconds:0} / {seconds} s"; });
        try
        {
            var cap = await M.S.Bench.CaptureAsync(Game.G.ProcessName ?? "", seconds, p, _cts.Token);
            if (cap.Frames.Count < 100) throw new InvalidOperationException("Too few frames captured. Was the game in the foreground?");
            var run = SavedRun.From(cap, label);
            M.S.SaveCapture(Game.Id, run);
            step.State = "done";
            step.Detail = $"{run.AvgFps:0} FPS avg · 1% low {1000 / run.P99Ms:0}{(cap.Source == CaptureSource.Demo ? " · demo" : "")}";
            return run;
        }
        catch (OperationCanceledException) { step.State = "skip"; step.Detail = "Cancelled"; return null; }
        catch (Exception ex) { step.State = "fail"; step.Detail = ex.Message; return null; }
    }

    protected Task<bool> WaitClosed() => M.S.IsRunning(Game.G)
        ? Ask($"Close {Game.Name}", $"{Game.Name} keeps its settings in memory and writes them back when it closes, which would undo the changes. Close it, and Framecalm continues by itself.", "It's closed", () => !M.S.IsRunning(Game.G))
        : Task.FromResult(true);

    protected Task<bool> WaitRunning(string why) => Ask($"Start {Game.Name}", why, "Start measuring", null);

    public ICommand DoneCmd => new Cmd(() => { Game.Refresh(); M.Open(Game, remember: false); });
}

/// <summary>Applying a set of changes, optionally with the before/after proof.</summary>
public sealed class RunVM : FlowVM
{
    readonly List<ProposedChange> _changes;
    readonly bool _measure;
    readonly string _profile;
    public RunVM(MainVM m, GameVM game, List<ProposedChange> changes, bool measure, string profile) : base(m, game)
    {
        _changes = changes; _measure = measure; _profile = profile;
        if (measure) for (int i = 1; i <= 3; i++) Steps.Add(new($"Measure before · run {i} of 3"));
        Steps.Add(new("Back up the original settings"));
        Steps.Add(new($"Write {changes.Count} change{(changes.Count == 1 ? "" : "s")}"));
        Steps.Add(new("Read back and verify"));
        if (measure) for (int i = 1; i <= 3; i++) Steps.Add(new($"Measure after · run {i} of 3"));
        Applied = new(changes.Select((c, i) => new ChangeRowVM(i + 1, c, 1)));
        _ = RunAsync();
    }

    public string Title => $"Tuning {Game.Name}";
    public ObservableCollection<ChangeRowVM> Applied { get; }
    public string? Message { get; private set; }
    public Comparison? Cmp { get; private set; }
    public bool Measured => Cmp != null;
    public double BeforeFps => Cmp?.BeforeFps ?? 0;
    public double AfterFps => Cmp?.AfterFps ?? 0;
    public double Delta => Cmp?.FpsDelta ?? 0;
    public string DeltaText => Cmp is null ? "" : $"{(Delta >= 0 ? "+" : "")}{Delta:0} FPS  ·  {Cmp.FpsDeltaPct:+0;-0}%";
    public double BeforeLow => Cmp is null ? 0 : 1000 / Cmp.BeforeP99;
    public double AfterLow => Cmp is null ? 0 : 1000 / Cmp.AfterP99;
    public double BeforeP99 => Cmp?.BeforeP99 ?? 0;
    public double AfterP99 => Cmp?.AfterP99 ?? 0;
    public double BeforeMedian => Cmp?.BeforeMedian ?? 0;
    public double AfterMedian => Cmp?.AfterMedian ?? 0;
    public string Verdict => Cmp?.Verdict() ?? "";
    public bool Within => Cmp is { IsReal: false };
    public string NoiseText => Cmp is null ? "" : $"RUN-TO-RUN NOISE {Cmp.RunToRunNoisePct:0.0}%";
    public double[]? BeforeTrace { get; private set; }
    public double[]? AfterTrace { get; private set; }
    public double TargetMs => Game.TargetMs;
    public string TargetLabel => Game.TargetLabel;
    public string HeadlineDone => Measured ? (Within ? "No measurable change" : Delta > 0 ? "Faster. Measured." : "Slower than before") : "Applied and verified";

    async Task RunAsync()
    {
        await Task.Delay(350);
        var i = 0;
        var before = new List<SavedRun>();
        var after = new List<SavedRun>();
        if (_measure)
        {
            if (!await WaitRunning($"Load into a spot you can return to exactly (same map, same view), then press Start. Framecalm records 3 runs of 30 seconds. Keep playing normally or stand still; just do the same thing each run.")) { Cancelled(); return; }
            for (; i < 3; i++) { var r = await CaptureAsync(Steps[i], "before"); if (r is null) { Fail("A measurement didn't finish, so nothing was changed."); return; } before.Add(r); }
        }
        if (!await WaitClosed()) { Cancelled(); return; }

        var target = M.S.ConfigTarget(Game.G);
        if (target is null) { Fail("This game's settings file isn't available any more. Nothing was changed."); return; }
        var records = _changes.Select(c => new ChangeRecord
        {
            Scope = Game.Id, ScopeName = Game.Name, Key = c.Setting.Key, Label = c.Setting.Label,
            Old = c.Old, New = c.New, OldLabel = c.OldLabel, NewLabel = c.NewLabel,
        }).ToList();
        var bi = i;
        Steps[bi].State = "run";
        var progress = new Progress<ApplyStep>(s =>
        {
            var idx = bi + (int)s;
            for (int k = bi; k < Math.Min(idx, bi + 3); k++) Steps[k].State = "done";
            if (idx < bi + 3) Steps[idx].State = "run";
        });
        await Task.Delay(M.IsDemo ? 500 : 0);
        var result = await Task.Run(() => M.S.Tx.Apply(target, records, progress));
        await Task.Delay(M.IsDemo ? 500 : 0);
        if (!result.Ok)
        {
            for (int k = bi; k < bi + 3; k++) if (Steps[k].State != "done") Steps[k].State = "fail";
            Fail(result.Message);
            return;
        }
        for (int k = bi; k < bi + 3; k++) { Steps[k].State = "done"; }
        Steps[bi].Detail = "Original saved";
        Steps[bi + 1].Detail = $"{_changes.Count} settings";
        Steps[bi + 2].Detail = "Every value reads back correctly";
        i = bi + 3;
        Game.Refresh();

        if (_measure)
        {
            if (!await WaitRunning($"Start {Game.Name} again and go back to the same spot as before. Then press Start for the 3 after-runs.")) { Partial(); return; }
            for (int k = 0; k < 3; k++, i++) { var r = await CaptureAsync(Steps[i], "after"); if (r is null) { Partial(); return; } after.Add(r); }
            Cmp = new Comparison { Before = before.Select(r => r.Stats).ToList(), After = after.Select(r => r.Stats).ToList() };
            BeforeTrace = before.Last().Trace;
            AfterTrace = after.Last().Trace;
        }
        Message = Measured ? null : $"{_changes.Count} change{(_changes.Count == 1 ? "" : "s")} applied ({_profile}). Want proof? Run a benchmark from the game page; numbers you see in the meantime are typical, not yours.";
        Game.Refresh();
        RaiseAll();
        Stage = "results";
    }

    void Fail(string msg) { Message = msg; Raise(nameof(Message)); Stage = "failed"; }
    void Cancelled() { Message = "Stopped. Nothing was changed."; Raise(nameof(Message)); Stage = "failed"; }
    void Partial()
    {
        Message = "Your changes are applied and verified, but the after-runs didn't finish, so there's no proof chart yet. You can measure any time from the game page.";
        Raise(nameof(Message));
        Stage = "partial";
    }

    public ICommand UndoCmd => new AsyncCmd(async () =>
    {
        var ids = M.S.Tx.History().Where(c => c.Scope == Game.Id && c.Status == ChangeStatus.Applied && _changes.Any(x => x.Setting.Key == c.Key)).Select(c => c.Id).ToList();
        await M.HistoryPage.UndoAsync(ids, "these changes");
        Game.Refresh();
        M.Open(Game, remember: false);
    });
}

public sealed class RoundVM : Observable
{
    public string Title { get; init; } = "";
    public string Changes { get; init; } = "";
    string _state = "wait", _result = "";
    public string State { get => _state; set => Set(ref _state, value); }
    public string Result { get => _result; set => Set(ref _result, value); }
}

/// <summary>Auto-tune: measure, find the bottleneck, lower the best-value settings in rounds until the target is met.</summary>
public sealed class AutoTuneVM : FlowVM
{
    public AutoTuneVM(MainVM m, GameVM game, int target) : base(m, game)
    {
        Target = target;
        _ = RunAsync();
    }

    public int Target { get; }
    double _now;
    public double Now { get => _now; set { Set(ref _now, value); Raise(nameof(NowText)); } }
    public string NowText => Now <= 0 ? "--" : $"{Now:0}";
    public double GaugeMax => Math.Max(Target * 1.35, 90);
    public ObservableCollection<RoundVM> Rounds { get; } = [];
    string _status = "Measuring where you start";
    public string Status { get => _status; set => Set(ref _status, value); }
    string _detail = "";
    public string Detail { get => _detail; set => Set(ref _detail, value); }
    public string? Summary { get; private set; }
    public BottleneckResult? Bottleneck { get; private set; }
    readonly StepVM _probe = new("probe");
    public double[]? FirstTrace { get; private set; }
    public double[]? LastTrace { get; private set; }
    public double TargetMs => 1000.0 / Math.Max(1, Target);
    public string TargetLabel => $"{Target} FPS";

    async Task RunAsync()
    {
        var baseRound = new RoundVM { Title = "Baseline", Changes = "Your current settings, one 30-second run" };
        Rounds.Add(baseRound);
        if (!await WaitRunning($"Load into a spot you can return to (same map, same view). Auto-tune measures 30 seconds, then asks you to close the game while it changes settings, then measures again. Usually 2 or 3 rounds.")) { Stage = "failed"; Summary = "Stopped. Nothing was changed."; Raise(nameof(Summary)); return; }
        baseRound.State = "run";
        var b = await CaptureAsync(_probe, "round-0");
        if (b is null) { baseRound.State = "fail"; Summary = "The baseline run didn't finish, so nothing was changed. " + _probe.Detail; Raise(nameof(Summary)); Stage = "failed"; return; }
        Now = b.AvgFps;
        baseRound.State = "done";
        baseRound.Result = $"{b.AvgFps:0} FPS";
        FirstTrace = b.Trace; LastTrace = null; Raise(nameof(FirstTrace));
        var demoFrames = Enumerable.Repeat(new FrameSample(b.FrameMs, b.GpuMs, 0), 60).ToList();
        Bottleneck = BottleneckAnalyzer.Analyze(demoFrames, M.Hw.RefreshHz);
        Raise(nameof(Bottleneck));
        Detail = Bottleneck.Headline + ". " + Bottleneck.Detail;

        if (Now >= Target * 0.98)
        {
            Summary = $"Already there. You're at {Now:0} FPS against a target of {Target}, so nothing needed to change.";
            Raise(nameof(Summary));
            Stage = "done";
            return;
        }

        var (check, text) = M.S.ReadConfig(Game.G);
        if (!check.Ok || text is null) { Summary = check.Reason; Raise(nameof(Summary)); Stage = "failed"; return; }
        var candidates = Recommender.Propose(Game.G.Definition!, s => M.S.CurrentValue(Game.G, text, s), M.Hw, Profile.MaxFps);
        var ordered = AutoTunePlanner.Order(candidates, Bottleneck.Kind);
        var plan = AutoTunePlanner.Rounds(ordered, Now, Target, Bottleneck.Kind);
        if (plan.Count == 0)
        {
            Summary = $"Nothing sensible left to lower. You're at {Now:0} FPS and every setting that helps is already at its fastest. The rest is hardware: see Health for heat, power and drivers.";
            Raise(nameof(Summary));
            Stage = "done";
            return;
        }

        var target = M.S.ConfigTarget(Game.G)!;
        var n = 1;
        foreach (var round in plan)
        {
            var r = new RoundVM { Title = $"Round {n}", Changes = string.Join("  ·  ", round.Select(c => $"{c.Setting.Label} {c.OldLabel} → {c.NewLabel}")) };
            Rounds.Add(r);
            r.State = "run";
            Status = $"Round {n}: changing {round.Count} setting{(round.Count == 1 ? "" : "s")}";
            if (!await WaitClosed()) { r.State = "skip"; break; }
            var recs = round.Select(c => new ChangeRecord { Scope = Game.Id, ScopeName = Game.Name, Key = c.Setting.Key, Label = c.Setting.Label, Old = c.Old, New = c.New, OldLabel = c.OldLabel, NewLabel = c.NewLabel }).ToList();
            // values may have moved since planning (previous rounds); re-read old values from the file
            foreach (var rec in recs) rec.Old = target.Read(rec.Key);
            var res = await Task.Run(() => M.S.Tx.Apply(target, recs));
            if (!res.Ok) { r.State = "fail"; r.Result = "Not applied"; Summary = res.Message; Raise(nameof(Summary)); Stage = "failed"; return; }
            if (!await WaitRunning($"Start {Game.Name} and go back to the same spot. Then press Start.")) { r.State = "skip"; break; }
            Status = $"Round {n}: measuring";
            var m = await CaptureAsync(_probe, $"round-{n}");
            if (m is null) { r.State = "fail"; break; }
            r.State = "done";
            r.Result = $"{m.AvgFps:0} FPS  ({m.AvgFps - Now:+0;-0})";
            LastTrace = m.Trace; Raise(nameof(LastTrace));
            Now = m.AvgFps;
            n++;
            if (Now >= Target * 0.98) break;
        }
        Game.Refresh();
        Summary = Now >= Target * 0.98
            ? $"Target reached: {Now:0} FPS against {Target}, measured. Each round's number is a real measurement; the order came from what limits your PC."
            : $"Got to {Now:0} FPS (from {b.AvgFps:0}). Going further would mean settings that cost more looks than they buy, so auto-tune stopped here. Undo any round in History.";
        Raise(nameof(Summary));
        Status = "Finished";
        Stage = "done";
    }
}
