using System.Collections.ObjectModel;
using System.Windows.Input;
using Framecalm.Windows;

namespace Framecalm.App.ViewModels;

// =====================================================================================
// Optimize this PC: one plan built from the hardware (laptop or desktop, GPU, what's already set),
// shown as a list with a reason on every line, applied in one go.
// =====================================================================================
public sealed class AutoOptimizeVM : Observable, IRefresh
{
    readonly MainVM m;
    public AutoOptimizeVM(MainVM main) { m = main; }

    // ids that cost battery and heat: left out on laptops
    static readonly HashSet<string> LaptopSkip = ["cpu-min-100", "core-parking-off", "boost-aggressive", "pcie-aspm-off", "epp-performance", "idle-off", "disk-idle-off", "hibernate-off"];

    string _mode = "recommended";
    public bool ModeEveryday { get => _mode == "recommended"; set { if (value) SetMode("recommended"); } }
    public bool ModeCompetitive { get => _mode == "competitive"; set { if (value) SetMode("competitive"); } }
    public bool ModeAllOut { get => _mode == "extreme"; set { if (value) SetMode("extreme"); } }
    void SetMode(string mode) { _mode = mode; Build(); }

    /// <summary>The home screen's one click: build the everyday plan for this PC and apply all of it.</summary>
    public async Task RunEverydayAsync()
    {
        m.OptimizePage.Refresh();
        m.HealthPage.Refresh();
        _mode = "recommended";
        Build();
        if (Count == 0) return;
        await Apply();
    }

    public ObservableCollection<PlanRowVM> Fixes { get; } = [];
    public ObservableCollection<PlanRowVM> Tweaks { get; } = [];
    public ObservableCollection<PlanRowVM> Skipped { get; } = [];
    public bool HasFixes => Fixes.Count > 0;
    public bool HasTweaks => Tweaks.Count > 0;
    public bool HasSkipped => Skipped.Count > 0;

    public string Machine => $"{(m.Hw.IsLaptop ? "laptop" : "desktop")} with {m.Hw.Dedicated?.ShortName ?? m.Hw.Primary?.ShortName ?? "its graphics"}";
    public string Lede => $"Picked for this {Machine}. Every line says why. Untick anything you don't want; nothing changes until you apply.";
    public int Count => Fixes.Count(r => r.Include) + Tweaks.Count(r => r.Include);
    public string CountText => Count == 0 ? "Nothing left to do at this level" : Count == 1 ? "1 change" : $"{Count} changes";
    public string Note
    {
        get
        {
            var notes = new List<string> { "Restore point first, everything undoable." };
            if (Tweaks.Any(r => r.Include && r.Tweak?.Restart == true)) notes.Add("Some need a restart.");
            return string.Join(" ", notes);
        }
    }
    public string ApplyText => Count == 1 ? "Apply 1 change" : $"Apply {Count} changes";

    bool _busy;
    public bool Busy { get => _busy; private set => Set(ref _busy, value); }

    public ICommand ApplyCmd => new AsyncCmd(Confirm, () => Count > 0 && !Busy);
    public ICommand BackCmd => new Cmd(() => m.Go("dashboard"));

    public void Refresh()
    {
        m.OptimizePage.Refresh();
        m.HealthPage.Refresh();
        Build();
    }

    void Build()
    {
        Fixes.Clear(); Tweaks.Clear(); Skipped.Clear();
        foreach (var f in m.HealthPage.Fixes)
            Fixes.Add(new PlanRowVM(this, f.Title, $"{f.Now} → {f.After}. {f.Why}", null, f));

        var laptop = m.Hw.IsLaptop;
        var dgpu = m.Hw.Dedicated != null;
        foreach (var r in m.OptimizePage.All.Where(r => r.Available && !r.IsOn && r.T.Presets.Contains(_mode)))
        {
            var t = r.T;
            string? skip = null;
            if (laptop && LaptopSkip.Contains(t.Id)) skip = "Laptop: it would cost battery life and heat for a small gain.";
            else if (t.Id == "hags-on" && !dgpu) skip = "Only helps with a dedicated graphics card.";
            if (skip != null) { Skipped.Add(new PlanRowVM(this, t.Name, skip, t, null) { Include = false }); continue; }
            Tweaks.Add(new PlanRowVM(this, t.Name, Reason(t, laptop), t, null));
        }
        RaiseAll();
    }

    static string Reason(Tweak t, bool laptop) => t.Id switch
    {
        "usb-suspend-off" when laptop => "Laptop: stops USB mice and headsets dropping out after idle.",
        "cpu-min-100" or "core-parking-off" or "boost-aggressive" or "epp-performance" or "pcie-aspm-off" => "Desktop: power use isn't a concern, so the CPU and GPU link stay at full speed.",
        "mouse-accel-off" => "Same hand movement, same distance: the biggest single change for aim.",
        "delivery-opt-off" => "Stops Windows uploading updates to strangers, which shows up as ping spikes.",
        "hags-on" => "You have a dedicated GPU; needed for DLSS Frame Generation.",
        _ => t.Tier switch
        {
            TweakTier.Safe => $"Safe on any PC. {t.What.Split(". ")[0].TrimEnd('.')}.",
            TweakTier.Advanced => $"{t.What.Split(". ")[0].TrimEnd('.')}. Trade-off: {t.Tradeoff.Split(". ")[0].TrimEnd('.')}.",
            _ => $"Extreme. {t.Tradeoff.Split(". ")[0].TrimEnd('.')}.",
        },
    };

    internal void Changed() { Raise(nameof(Count)); Raise(nameof(CountText)); Raise(nameof(ApplyText)); Raise(nameof(Note)); }

    Task Confirm()
    {
        var extreme = Tweaks.Where(r => r.Include && r.Tweak?.Tier == TweakTier.Extreme).ToList();
        if (extreme.Count == 0) return Apply();
        m.Dialog = new ConfirmVM("This plan includes Extreme changes",
            "They switch off protections or features. Every one can be undone from History.",
            extreme.Select(r => $"{r.Name}: {r.Tweak!.Tradeoff}").ToList(),
            "Apply anyway", async () => { m.Dialog = null; await Apply(); });
        return Task.CompletedTask;
    }

    async Task Apply()
    {
        Busy = true;
        var opt = m.OptimizePage;
        var tweaks = Tweaks.Where(r => r.Include).Select(r => r.Tweak!.Id).ToHashSet();
        var fixes = Fixes.Where(r => r.Include).Select(r => r.Fix!).ToHashSet();
        var n = tweaks.Count + fixes.Count;
        opt.DiscardCmd.Execute(null);
        foreach (var r in opt.All.Where(r => tweaks.Contains(r.T.Id))) r.Desired = true;
        if (opt.HasStaged) await opt.ApplyStagedAsync();
        if (fixes.Count > 0)
        {
            foreach (var f in m.HealthPage.Fixes) f.Include = fixes.Contains(f);
            await m.HealthPage.ApplyFixesAsync();
        }
        Busy = false;
        var failed = opt.HasStaged;
        if (m.Page is not DashboardVM) m.Go("dashboard"); else m.DashboardPage.Refresh();
        m.RaiseNav();
        if (!failed)
            m.ShowToast($"Done: {n} change{(n == 1 ? "" : "s")} applied and verified.{(Tweaks.Any(r => r.Include && r.Tweak!.Restart) ? " Restart Windows to finish." : "")} Undo any of it in History.");
    }
}

public sealed class PlanRowVM(AutoOptimizeVM owner, string name, string reason, Tweak? tweak, FixRowVM? fix) : Observable
{
    public string Name => name;
    public string Reason => reason;
    public Tweak? Tweak => tweak;
    public FixRowVM? Fix => fix;
    public string Tier => tweak?.Tier.ToString() ?? "Fix";
    bool _include = true;
    public bool Include { get => _include; set { if (Set(ref _include, value)) owner.Changed(); } }
}
