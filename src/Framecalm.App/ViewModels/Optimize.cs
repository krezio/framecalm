using System.Collections.ObjectModel;
using System.Windows.Input;
using Framecalm.Backups;
using Framecalm.Core;
using Framecalm.Windows;

namespace Framecalm.App.ViewModels;

// =====================================================================================
// Optimize: the tweak list. Each switch shows what's true on the PC now; flipping it stages a change,
// and the bar at the bottom applies everything staged in one go (one admin prompt, restore point first).
// =====================================================================================
public sealed class OptimizeVM : Observable, IRefresh
{
    readonly MainVM m;
    public OptimizeVM(MainVM main)
    {
        m = main;
        Categories.Add(new CategoryVM(this, null, "All tweaks", "Every tweak Framecalm can make, grouped by what it touches."));
        foreach (var c in TweakCatalog.Categories) Categories.Add(new CategoryVM(this, c.Id, c.Name, c.About));
        foreach (var t in TweakCatalog.All) _all.Add(new TweakRowVM(this, t));
        _category = Categories[0];
    }

    readonly List<TweakRowVM> _all = [];
    public ObservableCollection<CategoryVM> Categories { get; } = [];
    public ObservableCollection<TweakRowVM> Rows { get; } = [];
    public IReadOnlyList<TweakRowVM> All => _all;

    CategoryVM _category;
    public CategoryVM Category { get => _category; set { if (Set(ref _category, value)) { foreach (var c in Categories) c.Update(); Filter(); } } }

    string _tier = "all";
    public bool TierAll { get => _tier == "all"; set { if (value) SetTier("all"); } }
    public bool TierSafe { get => _tier == "safe"; set { if (value) SetTier("safe"); } }
    public bool TierAdvanced { get => _tier == "advanced"; set { if (value) SetTier("advanced"); } }
    public bool TierExtreme { get => _tier == "extreme"; set { if (value) SetTier("extreme"); } }
    void SetTier(string t) { _tier = t; Raise(nameof(TierAll)); Raise(nameof(TierSafe)); Raise(nameof(TierAdvanced)); Raise(nameof(TierExtreme)); Filter(); }

    string _search = "";
    public string Search { get => _search; set { if (Set(ref _search, value)) Filter(); } }

    void Filter()
    {
        Rows.Clear();
        foreach (var r in _all.Where(r =>
                     (_category.Id is null || r.T.Category == _category.Id) &&
                     (_tier == "all" || r.T.Tier.ToString().Equals(_tier, StringComparison.OrdinalIgnoreCase)) &&
                     (_search.Length == 0 || r.Name.Contains(_search, StringComparison.OrdinalIgnoreCase) || r.What.Contains(_search, StringComparison.OrdinalIgnoreCase))))
            Rows.Add(r);
        Raise(nameof(Empty));
        Raise(nameof(CategoryTitle));
        Raise(nameof(CategoryAbout));
    }

    public bool Empty => Rows.Count == 0;
    public string CategoryTitle => _category.Name;
    public string CategoryAbout => _category.About;

    // ---------- counts ----------
    public int OnCount => _all.Count(r => r.IsOn);
    public int Available => _all.Count(r => r.Available);
    public string Headline => $"{OnCount} of {Available} on";
    public double Share => Available == 0 ? 0 : (double)OnCount / Available;
    public string NavCount => $"{OnCount}/{Available}";
    public int SafeLeft => _all.Count(r => r.Available && !r.IsOn && r.T.Presets.Contains("recommended"));

    // ---------- staged changes ----------
    public List<TweakRowVM> Staged => _all.Where(r => r.Pending).ToList();
    public bool HasStaged => _all.Any(r => r.Pending);
    public string StagedText
    {
        get
        {
            var s = Staged;
            int on = s.Count(r => r.Desired), off = s.Count - on;
            var parts = new List<string>();
            if (on > 0) parts.Add($"{on} to turn on");
            if (off > 0) parts.Add($"{off} to turn off");
            return string.Join(", ", parts);
        }
    }
    public string StagedNote
    {
        get
        {
            var s = Staged;
            var notes = new List<string>();
            if (s.Any(r => r.T.NeedsAdmin)) notes.Add(m.IsDemo ? "Windows asks for admin once" : "Windows will ask for admin once");
            if (s.Any(r => r.Desired && r.T.Tier == TweakTier.Extreme)) notes.Add("includes Extreme changes");
            if (s.Any(r => r.T.Restart)) notes.Add("some need a restart");
            return notes.Count == 0 ? "Takes effect right away." : string.Join(" ", notes.Select(n => char.ToUpper(n[0]) + n[1..] + "."));
        }
    }
    public string ApplyText => Staged.Count == 1 ? "Apply 1 change" : $"Apply {Staged.Count} changes";

    internal void StagedChanged()
    {
        Raise(nameof(HasStaged)); Raise(nameof(StagedText)); Raise(nameof(StagedNote)); Raise(nameof(ApplyText));
    }

    public ICommand DiscardCmd => new Cmd(() => { foreach (var r in _all) r.Reset(); StagedChanged(); });
    public ICommand ApplyCmd => new AsyncCmd(Confirm, () => HasStaged);
    public ICommand PresetCmd => new Cmd(p => Preset((string)p!));
    public ICommand UndoAllCmd => new AsyncCmd(UndoAll, () => OnCount > 0);

    void Preset(string id)
    {
        var n = 0;
        foreach (var r in _all.Where(r => r.Available && r.T.Presets.Contains(id) && !r.Desired)) { r.Stage(true); n++; }
        StagedChanged();
        var name = TweakCatalog.Presets.First(p => p.Id == id).Name;
        m.ShowToast(n == 0 ? $"Everything in {name} is already on." : $"{name}: {n} tweak{(n == 1 ? "" : "s")} staged. Review them, then apply.");
        if (n > 0) { Category = Categories[0]; SetTier("all"); }
    }

    Task Confirm()
    {
        var extreme = Staged.Where(r => r.Desired && r.T.Tier == TweakTier.Extreme).ToList();
        if (extreme.Count == 0) return Apply();
        m.Dialog = new ConfirmVM(
            "Extreme changes lower Windows' defenses",
            "These can give real gains, but they switch off protections or features. Every one can be undone from History.",
            extreme.Select(r => $"{r.Name}: {r.Tradeoff}").ToList(),
            "Apply anyway", async () => { m.Dialog = null; await Apply(); });
        return Task.CompletedTask;
    }

    bool _busy;
    public bool Busy { get => _busy; private set => Set(ref _busy, value); }

    public Task ApplyStagedAsync() => Apply();

    async Task Apply()
    {
        var staged = Staged;
        if (staged.Count == 0) return;
        Busy = true;
        var target = m.S.Tweaks;
        var nics = target.Backend.Nics;
        var history = m.S.Tx.History();
        var records = new List<ChangeRecord>();
        var undoIds = new List<string>();
        foreach (var r in staged)
        {
            if (r.Desired)
            {
                foreach (var a in r.T.Atoms(nics))
                {
                    var now = target.Read(a.Key);
                    if (TransactionManager.Same(now, a.Value)) continue;
                    records.Add(Record(r, a.Key, now, a.Value));
                }
                continue;
            }
            // turning off: undo what Framecalm set if it can, otherwise put back the Windows default
            var mine = history.Where(c => c.Scope == "tweak:" + r.T.Id && c.Status == ChangeStatus.Applied).Select(c => c.Id).ToList();
            if (mine.Count > 0) { undoIds.AddRange(mine); continue; }
            foreach (var a in r.T.Atoms(nics))
            {
                var now = target.Read(a.Key);
                if (TransactionManager.Same(now, a.Default)) continue;
                records.Add(Record(r, a.Key, now, a.Default));
            }
        }

        var problems = new List<string>();
        string? note = null;
        if (records.Count > 0)
        {
            var res = await Task.Run(() => m.S.Tx.Apply(target, records));
            if (!res.Ok) problems.Add(res.Message);
            note = (target.Backend as RegistryTweakBackend)?.LastNote;
        }
        if (undoIds.Count > 0)
        {
            var u = await Task.Run(() => m.S.Tx.Undo(undoIds, force: undoIds.ToHashSet()));
            problems.AddRange(u.Errors);
        }
        Busy = false;
        Refresh();
        m.RaiseNav();
        var restart = staged.Any(r => r.T.Restart);
        if (problems.Count == 0)
            m.ShowToast($"{staged.Count} change{(staged.Count == 1 ? "" : "s")} applied and verified.{(note is { Length: > 0 } ? " " + note : "")}{(restart ? " Restart Windows to finish." : "")} Undo any of it here or in History.");
        else
            m.ShowToast(problems[0].StartsWith("You said no") || problems[0].Contains("said no")
                ? "Nothing was changed: the admin prompt was declined."
                : "Nothing was changed. " + problems[0], bad: true);
    }

    static ChangeRecord Record(TweakRowVM r, string key, string? now, string? value) => new()
    {
        Scope = "tweak:" + r.T.Id, ScopeName = "Optimize", Key = key, Label = r.Name,
        Old = now, New = value, OldLabel = TweakKeys.Describe(key, now), NewLabel = TweakKeys.Describe(key, value),
    };

    async Task UndoAll()
    {
        var ids = m.S.Tx.History().Where(c => c.Scope.StartsWith("tweak:") && c.Status == ChangeStatus.Applied).Select(c => c.Id).ToList();
        if (ids.Count == 0) { m.ShowToast("Framecalm hasn't changed any tweaks yet. Ones that were already on were set by Windows or another app."); return; }
        var u = await Task.Run(() => m.S.Tx.Undo(ids, force: ids.ToHashSet()));
        Refresh();
        m.RaiseNav();
        m.ShowToast(u.Errors.Count == 0 ? $"Put back {u.Undone} value{(u.Undone == 1 ? "" : "s")}. Everything Framecalm tweaked is back to how it was." : "Some values couldn't be put back: " + u.Errors[0], u.Errors.Count > 0);
    }

    public void Refresh()
    {
        foreach (var r in _all) r.Load();
        foreach (var c in Categories) c.Update();
        Filter();
        RaiseAll();
    }
}

public sealed class CategoryVM(OptimizeVM o, string? id, string name, string about) : Observable
{
    public string? Id => id;
    public string Name => name;
    public string About => about;
    IEnumerable<TweakRowVM> Mine => o.All.Where(r => id is null || r.T.Category == id);
    public string Count => $"{Mine.Count(r => r.IsOn)}/{Mine.Count(r => r.Available)}";
    public bool Selected { get => o.Category == this; set { if (value) o.Category = this; } }
    public void Update() { Raise(nameof(Count)); Raise(nameof(Selected)); }
}

public sealed class TweakRowVM(OptimizeVM o, Tweak t) : Observable
{
    public Tweak T => t;
    public string Name => t.Name;
    public string What => t.What;
    public string Tradeoff => t.Tradeoff;
    public string Tier => t.Tier.ToString();
    public int TierLevel => (int)t.Tier + 1;
    public string Impact => t.Impact switch
    {
        "high" => "Big difference",
        "medium" => "Noticeable",
        "low" => "Small gain",
        _ => "Fixes specific problems",
    };
    public bool Restart => t.Restart;
    public bool Admin => t.NeedsAdmin;

    TweakState _state;
    public TweakState State { get => _state; private set => Set(ref _state, value); }
    public bool Available => _state != TweakState.Unavailable;
    public bool IsOn => _state == TweakState.On;
    public bool Partial => _state == TweakState.Partial;

    bool _desired;
    public bool Desired
    {
        get => _desired;
        set { if (Set(ref _desired, value)) { Raise(nameof(Pending)); Raise(nameof(StatusText)); o.StagedChanged(); } }
    }
    public bool Pending => Available && _desired != IsOn;

    public string StatusText => !Available ? "Not on this PC"
        : Pending ? (_desired ? "Will turn on" : "Will turn off")
        : Partial ? "Partly on" : "";

    bool _open;
    public bool Open { get => _open; set => Set(ref _open, value); }
    public ICommand ToggleOpenCmd => new Cmd(() => Open = !Open);

    public List<AtomRow> Atoms { get; private set; } = [];

    internal void Stage(bool on) => Desired = on;
    internal void Reset() { _desired = IsOn; Raise(nameof(Desired)); Raise(nameof(Pending)); Raise(nameof(StatusText)); }

    internal void Load()
    {
        var target = MainVM.Current!.S.Tweaks;
        try { State = target.State(t); } catch { State = TweakState.Unavailable; }
        _desired = IsOn;
        Atoms = t.Atoms(target.Backend.Nics).Select(a =>
        {
            string? now = null;
            try { now = target.Read(a.Key); } catch { }
            return new AtomRow(Pretty(a.Key), TweakKeys.Describe(a.Key, now), TweakKeys.Describe(a.Key, a.Value));
        }).ToList();
        RaiseAll();
    }

    static string Pretty(string key)
    {
        var p = key.Split('|');
        if (p[0] == "pwr") return $"powercfg {(TweakCatalog.Aliases.TryGetValue(key, out var al) ? al : p[2])} (active plan, plugged in)";
        var name = p[2];
        return $"{p[1]}\\{name}";
    }
}

public sealed record AtomRow(string Where, string Now, string Set);

public sealed class ConfirmVM(string title, string body, List<string> items, string confirm, Func<Task> ok) : Observable
{
    public string Title => title;
    public string Body => body;
    public List<string> Items => items;
    public string ConfirmText => confirm;
    public ICommand OkCmd => new AsyncCmd(ok);
    public ICommand CancelCmd => new Cmd(() => MainVM.Current!.Dialog = null);
}

// =====================================================================================
// Dashboard: where this PC stands, in one screen.
// =====================================================================================
public sealed class DashboardVM(MainVM m) : Observable, IRefresh
{
    public OptimizeVM Opt => m.OptimizePage;
    public string Cpu => m.Hw.Cpu;
    public string Gpu => m.Hw.Dedicated?.Name ?? m.Hw.Primary?.Name ?? "Unknown";
    public string Memory => m.Hw.RamBytes > 0 ? $"{Math.Round(m.Hw.RamBytes / 1073741824.0)} GB" : "Unknown";
    public string Screen => m.Hw.ScreenWidth > 0 ? $"{m.Hw.ScreenWidth}×{m.Hw.ScreenHeight}, {m.Hw.RefreshHz} Hz" : $"{m.Hw.RefreshHz} Hz";
    public string Machine => $"{(m.Hw.IsLaptop ? "Laptop" : "Desktop")} with {m.Hw.Primary?.ShortName ?? "unknown graphics"}, {Memory} memory, {m.Hw.RefreshHz} Hz screen";
    public string TweaksSub => $"of {Opt.Available} tweaks on";
    public string FixesSub => Fixes == 1 ? "PC fix ready" : "PC fixes ready";
    public string AttentionText => Attention.Count == 0 ? "" : Attention.Count == 1 ? $"Also worth a look: {Attention[0].Title.ToLowerInvariant()}" : $"Also worth a look: {Attention[0].Title.ToLowerInvariant()} and {Attention.Count - 1} more";
    public bool HasAttentionText => Attention.Count > 0;
    public double PlanTarget => Opt.OnCount + Opt.SafeLeft;
    public string PlanTargetText => Opt.SafeLeft == 0 ? "Everyday plan: all on" : $"Your plan takes it to {Opt.OnCount + Opt.SafeLeft}";
    public List<GameVM> TopGames => m.Games.Take(8).ToList();
    public bool HasGames => m.Games.Count > 0;
    public int ActiveChanges { get; private set; }
    public List<PlanRowLite> UpNext { get; private set; } = [];
    public bool HasUpNext => UpNext.Count > 0;
    bool _busy;
    public bool Busy { get => _busy; private set { _busy = value; RaiseAll(); } }
    int Left => m.HealthPage.Fixes.Count + Opt.SafeLeft;
    public string GoIcon => Busy ? "\uE895" : Left == 0 ? "\uE73E" : "\uE945";
    public string GoText => Busy ? "Working" : Left == 0 ? "All set" : "Optimize";
    public string GoSub => Busy ? "restore point first" : Left == 0 ? "go further" : $"{Left} change{(Left == 1 ? "" : "s")}";
    public string StatusText => Busy
        ? "Making a restore point, then applying and checking every change."
        : Left == 0
            ? "The everyday plan is on. Click for the Competitive plan, which adds lower-latency changes."
            : $"One click applies {Left} safe change{(Left == 1 ? "" : "s")} picked for this {(m.Hw.IsLaptop ? "laptop" : "desktop")}. Restore point first, and you can undo any of it.";
    public ICommand OptimizeCmd => new AsyncCmd(async () =>
    {
        if (Left == 0) { m.AutoPage.ModeCompetitive = true; m.Go("auto"); return; }
        Busy = true;
        try { await m.AutoPage.RunEverydayAsync(); }
        finally { Busy = false; Refresh(); }
    }, () => !Busy);
    public string GamesSub => Games == 1 ? "game found" : "games found";
    public string Greeting => DateTime.Now.Hour switch { < 5 => "Late session.", < 12 => "Good morning.", < 18 => "Good afternoon.", _ => "Good evening." };

    public List<TierStat> Tiers { get; private set; } = [];
    public List<CatStat> Cats { get; private set; } = [];
    public int Fixes { get; private set; }
    public string FixText => Fixes == 0 ? "Display, power and Wi-Fi settings all check out." : $"{Fixes} PC fix{(Fixes == 1 ? "" : "es")} ready: display, power or network settings holding this PC back.";
    public List<HealthRowVM> Attention { get; private set; } = [];
    public bool HasAttention => Attention.Count > 0;
    public List<RecentRow> Recent { get; private set; } = [];
    public bool HasRecent => Recent.Count > 0;
    public int Games => m.Games.Count;
    public string GamesText => Games == 0 ? "No games found yet." : $"{Games} game{(Games == 1 ? "" : "s")} found across your launchers.";

    public ICommand GoCmd => new Cmd(p => m.Go((string)p!));
    public string AutoText
    {
        get
        {
            int f = m.HealthPage.Fixes.Count, t = Opt.SafeLeft;
            var parts = new List<string>();
            if (f > 0) parts.Add($"{f} PC fix{(f == 1 ? "" : "es")}");
            if (t > 0) parts.Add($"{t} safe tweak{(t == 1 ? "" : "s")}");
            var pc = m.Hw.IsLaptop ? "laptop" : "desktop";
            return parts.Count == 0
                ? "Everything in the everyday plan is already on. Build a Competitive plan for more."
                : $"{string.Join(" and ", parts)} picked for this {pc}. You see the list before anything changes.";
        }
    }
    public string RecommendedText => Opt.SafeLeft == 0 ? "Recommended is all on" : $"Stage Recommended ({Opt.SafeLeft} left)";
    public bool CanRecommend => Opt.SafeLeft > 0;
    public ICommand RecommendedCmd => new Cmd(() => { m.Go("optimize"); m.OptimizePage.PresetCmd.Execute("recommended"); }, () => CanRecommend);

    public void Refresh()
    {
        m.OptimizePage.Refresh();
        m.HealthPage.Refresh();
        var all = m.OptimizePage.All.Where(r => r.Available).ToList();
        Tiers = Enum.GetValues<TweakTier>().Select(t =>
        {
            var mine = all.Where(r => r.T.Tier == t).ToList();
            return new TierStat(t.ToString(), (int)t + 1, mine.Count(r => r.IsOn), mine.Count);
        }).ToList();
        Cats = TweakCatalog.Categories.Select(c =>
        {
            var mine = all.Where(r => r.T.Category == c.Id).ToList();
            return new CatStat(c.Name, mine.Count(r => r.IsOn), mine.Count);
        }).Where(c => c.Total > 0).ToList();
        Fixes = m.HealthPage.Fixes.Count;
        Attention = m.HealthPage.Checks.Where(c => c.State == "Attention").Take(3).ToList();
        ActiveChanges = m.S.Tx.History().Count(c => c.Status == ChangeStatus.Applied);
        var next = new List<PlanRowLite>();
        foreach (var f in m.HealthPage.Fixes.Take(2)) next.Add(new PlanRowLite(f.Title, $"{f.Now} → {f.After}", "Fix"));
        var order = new Dictionary<string, int> { ["high"] = 0, ["medium"] = 1, ["situational"] = 2, ["low"] = 3 };
        foreach (var r in m.OptimizePage.All.Where(r => r.Available && !r.IsOn && r.T.Presets.Contains("recommended")).OrderBy(r => order.GetValueOrDefault(r.T.Impact, 4)).Take(4 - next.Count))
            next.Add(new PlanRowLite(r.Name, r.What.Split(". ")[0].TrimEnd('.') + ".", r.Tier));
        UpNext = next;
        Recent = m.S.Tx.History().Where(c => c.Status == ChangeStatus.Applied)
            .GroupBy(c => (c.BatchId, c.Label)).OrderByDescending(g => g.First().AppliedAt).Take(5)
            .Select(g =>
            {
                var c = g.First();
                var change = g.Count() > 1 ? $"{g.Count()} values changed" : $"{c.OldLabel ?? c.Old ?? "Not set"} → {c.NewLabel ?? c.New ?? "Not set"}";
                return new RecentRow(c.Label, c.ScopeName, change, c.AppliedAt.ToString("d MMM"));
            }).ToList();
        RaiseAll();
    }
}

public sealed record TierStat(string Name, int Level, int On, int Total)
{
    public string Text => $"{On}/{Total}";
    public double Share => Total == 0 ? 0 : (double)On / Total;
}

public sealed record CatStat(string Name, int On, int Total)
{
    public double Share => Total == 0 ? 0 : (double)On / Total;
    public string Text => $"{On}/{Total}";
}

public sealed record RecentRow(string Label, string Where, string Change, string When);

public sealed record PlanRowLite(string Name, string Reason, string Tier);
