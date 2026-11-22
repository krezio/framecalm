using Framecalm.Core;
using System.Windows;
using Framecalm.App.ViewModels;

namespace Framecalm.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var args = e.Args;
        string? Arg(string name)
        {
            var i = Array.IndexOf(args, name);
            return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
        }

        DispatcherUnhandledException += (_, ex) =>
        {
            try
            {
                var log = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Framecalm", "errors.log");
                Directory.CreateDirectory(Path.GetDirectoryName(log)!);
                File.AppendAllText(log, $"[{DateTime.Now:u}] {ex.Exception}{Environment.NewLine}{Environment.NewLine}");
            }
            catch { }
            if (Array.IndexOf(e.Args, "--peek") >= 0 || Array.IndexOf(e.Args, "--shots") >= 0) { ex.Handled = true; return; }
            MessageBox.Show(L.T(ex.Exception.Message), L.T("Framecalm hit a snag"), MessageBoxButton.OK, MessageBoxImage.Warning);
            ex.Handled = true;
        };

        Services.Translate.Choose(Arg("--lang"));

        if (Arg("--export-icon") is { } iconPath) { IconExporter.Export(iconPath); Shutdown(); return; }
        if (Arg("--export-store-art") is { } artDir) { IconExporter.ExportStoreArt(artDir); Shutdown(); return; }

        // read-only self-check of the real (non-demo) code paths: writes what Framecalm sees on this PC to a text file
        if (Arg("--diagnose") is { } report)
        {
            var lines = new List<string> { $"Framecalm self-check · {DateTime.Now:u}" };
            void Step(string name, Action run) { try { run(); } catch (Exception ex) { lines.Add($"[FAIL] {name}: {ex.GetType().Name}: {ex.Message}"); } }
            var s = new Services.AppServices(demo: false);
            Core.HardwareInfo? hw = null;
            Step("hardware", () =>
            {
                hw = s.HardwareProbe.Probe();
                lines.Add($"[ok] hardware: {hw.Cpu} · {hw.Threads} threads · {hw.RamBytes >> 30} GB · {(hw.IsLaptop ? "laptop" : "desktop")} · {hw.ScreenWidth}x{hw.ScreenHeight} @ {hw.RefreshHz} Hz (max {Framecalm.Windows.DisplayTarget.MaxRefresh()} Hz)");
                foreach (var g in hw.Gpus) lines.Add($"       gpu: {g.Name} · {g.Kind} · {g.VramBytes >> 20} MB · driver {g.DriverVersion} ({g.DriverDate:yyyy-MM-dd})");
            });
            Step("games", () =>
            {
                s.Games = s.Discovery.Discover(s.Catalog).ToList();
                lines.Add($"[ok] games: {s.Games.Count} found");
                foreach (var g in s.Games)
                {
                    var check = g.Support == Core.SupportLevel.Automatic ? s.ReadConfig(g).Check : null;
                    lines.Add($"       {g.Launcher,-9} {g.Name} · {g.Support}{(check is null ? "" : check.Ok ? $" · config ok ({check.Version})" : $" · guided: {check.Reason}")} · exe {(g.ExePath is null ? "not found" : "found")} · art {(g.ArtPath != null ? "local" : g.Cover ?? "generated")}");
                }
            });
            Step("pc fixes", () =>
            {
                var fixes = Framecalm.Windows.SystemFixes.Find(s.PcState(hw!));
                lines.Add($"[ok] pc fixes: {fixes.Count} available");
                foreach (var f in fixes) lines.Add($"       {f.Title}: {f.NowLabel} -> {f.NewLabel}{(f.DefaultOn ? "" : " (off by default)")}");
            });
            Step("health", () => { var h = s.Health(hw!); lines.Add($"[ok] health: {h.Count} checks"); foreach (var c in h) lines.Add($"       {c.State,-9} {c.Title}: {c.Finding}"); });
            Step("startup", () => { var st = s.StartupItems(); lines.Add($"[ok] startup apps: {st.Count} ({st.Count(x => x.Enabled)} enabled)"); });
            Step("power", () => lines.Add($"[ok] power plans: {string.Join(", ", Framecalm.Windows.PowerPlans.All().Select(p => p.Name))} · active {Framecalm.Windows.PowerPlans.Active()}"));
            Step("history", () => lines.Add($"[ok] history: {s.Tx.History().Count} records · recovery notes {s.Tx.Recover().Count}"));
            Step("benchmarks", () => lines.Add(s.Bench.Available ? "[ok] benchmarks: PresentMon and helper found" : $"[info] benchmarks: {s.Bench.UnavailableReason}"));
            File.WriteAllLines(report, lines);
            Shutdown();
            return;
        }

        // uninstaller: put back everything Framecalm changed. Values the user changed since are left alone.
        if (args.Contains("--undo-all-quiet"))
        {
            try
            {
                var s = new Services.AppServices(demo: false);
                s.Games = s.Discovery.Discover(s.Catalog).ToList();
                s.Tx.Recover();
                s.Session.Stop();
                s.Tx.Undo(s.Tx.History().Where(c => c.Status == Core.ChangeStatus.Applied).Select(c => c.Id).ToList());
            }
            catch { /* never block an uninstall */ }
            Shutdown();
            return;
        }

        // local-only tools (trailer cards, reels) hook in here when present
        var handled = false;
        RunExtras(args, ref handled);
        if (handled) return;

        // read-only look at the real PC: screenshots of the library and This PC pages, nothing is changed
        if (Arg("--peek") is { } peekDir)
        {
            var real = new MainVM(false);
            var w = new MainWindow(real) { WindowStartupLocation = WindowStartupLocation.Manual, Left = -20000, Top = 0, Width = 1280, Height = 800, ShowActivated = false, ShowInTaskbar = false };
            MainWindow = w;
            w.Show();
            _ = w.RunPeekAsync(peekDir);
            return;
        }

        var shots = Arg("--shots");
        var record = Arg("--record");
        if (record != null) shots = null;
        var demo = shots != null || record != null || args.Contains("--demo");
        var vm = new MainVM(demo);
        if (args.Contains("--first-run") && demo) vm.ShowIntro();
        var window = new MainWindow(vm);

        if (shots != null || record != null)
        {
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = -20000;
            window.Top = 0;
            window.Width = 1280;
            window.Height = 800;
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
        }

        MainWindow = window;
        if (shots == null && record == null)
        {
            try { window.EnableTray(); } catch { }
            // started by Windows at sign-in: stay in the tray until opened
            if (args.Contains("--tray")) { ShutdownMode = ShutdownMode.OnExplicitShutdown; _ = vm.LoadAsync(); return; }
        }
        window.Show();

        if (shots != null) _ = window.RunScreenshotsAsync(shots);
        else if (record != null) _ = window.RecordAsync(record, Arg("--scenario") ?? "hero");
    }

    partial void RunExtras(string[] args, ref bool handled);
}
