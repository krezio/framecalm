using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using Framecalm.App.ViewModels;
using Framecalm.Demo;

namespace Framecalm.App;

/// <summary>
/// `--shots dir`: demo-mode screenshots of every screen.
/// `--record out.mp4 --scenario hero`: films the app (demo data only) with a scripted cursor, piping frames to ffmpeg at 30 fps.
/// Other scenarios (trailer, reels) come from local extras when they're compiled in.
/// </summary>
public partial class MainWindow
{
    public const double RecordScale = 1.5;
    const int Fps = 30;

    Stopwatch? _recClock;
    readonly Dictionary<string, double> _events = new();
    public void Mark(string name) { if (_recClock != null) _events[name] = _recClock.Elapsed.TotalSeconds; }
    public MainVM VM => _vm;

    // ---------------- screenshots ----------------
    public async Task RunScreenshotsAsync(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            ((DemoPc.Bench)_vm.S.Bench).Speed = 40;
            async Task Shot(string name, int settle = 900) { await Task.Delay(settle); Save(Path.Combine(dir, name + ".png")); }

            _vm.ShowIntro();
            await Shot("01-first-launch", 1400);
            await _vm.LoadAsync();
            _vm.Go("dashboard");
            await Shot("02a-dashboard", 1400);
            _vm.Go("auto");
            await Shot("02a2-auto-plan", 1200);
            _vm.Go("optimize");
            await Shot("02b-optimize", 900);
            var opt = _vm.OptimizePage;
            opt.PresetCmd.Execute("competitive");
            opt.All.First(r => r.T.Id == "mmcss-games").Open = true;
            opt.Category = opt.Categories.First(c => c.Id == "graphics");
            await Task.Delay(300);
            _vm.ClearToast();
            await Shot("02c-optimize-staged", 900);
            opt.All.First(r => r.T.Id == "vbs-off").Desired = true;
            opt.ApplyCmd.Execute(null);
            await Shot("02d-extreme-confirm", 900);
            ((ConfirmVM)_vm.Dialog!).OkCmd.Execute(null);
            await WaitUntil(() => !opt.HasStaged, 10000);
            await Shot("02e-optimize-applied", 1200);
            _vm.ClearToast();
            _vm.Go("dashboard");
            await Shot("02f-dashboard-after", 1200);
            _vm.Go("library");
            await Shot("02-library", 1600);

            var mc = _vm.Find("minecraft-java")!;
            _vm.Open(mc);
            await Shot("03-game-before", 1600);

            await mc.FixGpu(quiet: true);
            var tune = new TuneVM(_vm, mc, autotune: false);
            _vm.Open(tune);
            tune.Profiles[2].IsSelected = true;
            await Shot("04-profile-picker");
            tune.ReviewCmd.Execute(null);
            tune.Rows[1].Open = true;
            await Shot("05-review-changes");

            tune.ApplyCmd.Execute(null);
            var run = (RunVM)_vm.Page;
            await WaitUntil(() => run.Steps.Count(s => s.State == "done") >= 4);
            await Shot("06-applying", 200);
            await WaitUntil(() => run.Stage == "results", 60000);
            await Shot("07-results-proof", 1800);

            run.DoneCmd.Execute(null);
            await Shot("08-game-after", 1800);

            var fn = _vm.Find("fortnite")!;
            await fn.FixGpu(quiet: true);
            var at = new AutoTuneVM(_vm, fn, 144);
            _vm.Open(at);
            await WaitUntil(() => at.Stage == "done", 90000);
            await Shot("09-auto-tune", 1600);

            var tt = new TuneVM(_vm, fn, autotune: true);
            _vm.Open(tt);
            await Shot("10-target-picker");

            _vm.Open(_vm.Find("valorant")!);
            await Shot("11-guided-game", 1200);

            _vm.Go("session");
            _vm.SessionPage.Game = _vm.Find("fortnite");
            _vm.SessionPage.StartCmd.Execute(null);
            await Shot("12-game-session", 1400);
            _vm.SessionPage.StopCmd.Execute(null);

            _vm.Go("health");
            await Shot("13-health", 1000);
            _vm.Go("startup");
            await Shot("13b-startup", 1000);

            _vm.Go("proof");
            _vm.ProofPage.Game = _vm.Find("rocket-league");
            _vm.ProofPage.CaptureCmd.Execute(null);
            await WaitUntil(() => _vm.ProofPage.Result != null, 30000);
            await Shot("14-benchmarks", 1400);

            _vm.Go("history");
            await Shot("15-history", 1000);

            _vm.Go("library");
            await Shot("16-library-after", 1600);

            _vm.Go("settings");
            await Shot("17-settings", 800);

            // conflict: change a value "by hand", then try to undo
            _vm.Go("history");
            _vm.S.Demo!.Configs["minecraft-java"].Write([("renderDistance", "14")]);
            var ids = _vm.S.Tx.History().Where(c => c.Scope == "minecraft-java" && c.Kind == Core.TargetKind.ConfigFile && c.Status == Core.ChangeStatus.Applied).Select(c => c.Id).ToList();
            await _vm.HistoryPage.UndoAsync(ids, "Minecraft");
            await Shot("18-conflict", 900);
            _vm.Dialog = null;
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(dir, "shots-error.txt"), ex.ToString()); }
        finally { Application.Current.Shutdown(); }
    }

    public async Task RunPeekAsync(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            if (_vm.Page is FirstRunVM) { await _vm.LoadAsync(); _vm.Go("library"); }
            await WaitUntil(() => _vm.Loaded, 20000);
            await Task.Delay(7000); // covers arrive in the background on a real PC
            Save(Path.Combine(dir, "real-library.png"));
            _vm.Go("health");
            await Task.Delay(1200);
            Save(Path.Combine(dir, "real-this-pc.png"));
            _vm.Go("dashboard");
            await Task.Delay(1500);
            Save(Path.Combine(dir, "real-dashboard.png"));
            _vm.Go("optimize");
            await Task.Delay(1200);
            Save(Path.Combine(dir, "real-optimize.png"));
            if (_vm.Games.FirstOrDefault() is { } g) { _vm.Open(g); await Task.Delay(1500); Save(Path.Combine(dir, "real-game.png")); }
        }
        catch (Exception ex) { File.WriteAllText(Path.Combine(dir, "peek-error.txt"), ex.ToString()); }
        finally { Application.Current.Shutdown(); }
    }

    public void Save(string path)
    {
        Root.UpdateLayout();
        var w = (int)Math.Round(Root.ActualWidth * RecordScale);
        var h = (int)Math.Round(Root.ActualHeight * RecordScale);
        var rtb = new RenderTargetBitmap(w, h, 96 * RecordScale, 96 * RecordScale, PixelFormats.Pbgra32);
        var bg = new DrawingVisual();
        using (var dc = bg.RenderOpen()) dc.DrawRectangle((Brush)FindResource("Bg"), null, new Rect(0, 0, Root.ActualWidth, Root.ActualHeight));
        rtb.Render(bg);
        rtb.Render(Root);
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var fs = File.Create(path);
        enc.Save(fs);
    }

    // ---------------- recording ----------------
    public async Task RecordAsync(string outFile, string scenario)
    {
        try { await RecordScriptAsync(scenario, outFile); }
        catch (Exception ex) { File.WriteAllText(outFile + ".error.txt", ex.ToString()); }
        finally { Application.Current.Shutdown(); }
    }

    /// <summary>Records one scripted run and returns when (seconds into the video) each marked moment happened.</summary>
    public async Task<Dictionary<string, double>> RecordScriptAsync(string script, string outFile)
    {
        ((DemoPc.Bench)_vm.S.Bench).Speed = 30;
        FakeCursorLayer.Visibility = Visibility.Visible;
        Canvas.SetLeft(FakeCursor, Root.ActualWidth * 0.78);
        Canvas.SetTop(FakeCursor, Root.ActualHeight * 0.9);
        if (!_vm.Loaded) await _vm.LoadAsync();
        await Task.Delay(800);

        Func<Task>? run = null, preroll = null;
        ExtraScript(script, ref run, ref preroll);
        run ??= script switch { "hero" => HeroScript, "tune" => TuneScript, _ => throw new ArgumentException($"Unknown scenario '{script}'. Built in: hero, tune.") };
        if (preroll != null) await preroll();

        int w = (int)Math.Round(Root.ActualWidth * RecordScale) / 2 * 2;
        int h = (int)Math.Round(Root.ActualHeight * RecordScale) / 2 * 2;
        var ffmpeg = Process.Start(new ProcessStartInfo("ffmpeg",
            $"-y -loglevel error -f rawvideo -pix_fmt bgra -s {w}x{h} -r {Fps} -i - " +
            $"-c:v libx264 -preset slow -crf 18 -pix_fmt yuv420p -movflags +faststart \"{outFile}\"")
        { RedirectStandardInput = true, UseShellExecute = false, CreateNoWindow = true })!;

        byte[]? latest = null;
        var gate = new object();
        var recording = true;
        var clock = new Stopwatch();
        _events.Clear();
        _recClock = clock;
        var grabbing = true;
        var grabber = GrabLoop(w, h, b => { lock (gate) latest = b; }, () => grabbing);
        while (true) { lock (gate) if (latest != null) break; await Task.Delay(5); }

        clock.Start();
        var writer = Task.Run(() =>
        {
            var stdin = ffmpeg.StandardInput.BaseStream;
            long frame = 0;
            while (recording)
            {
                var wait = frame * 1000.0 / Fps - clock.Elapsed.TotalMilliseconds;
                if (wait > 1) Thread.Sleep((int)wait);
                byte[] f;
                lock (gate) f = latest!;
                stdin.Write(f, 0, f.Length);
                frame++;
            }
            stdin.Flush();
            stdin.Close();
        });

        try { await run(); Mark("end"); }
        finally
        {
            grabbing = false;
            await grabber;
            recording = false;
            await writer;
            await ffmpeg.WaitForExitAsync();
            _recClock = null;
        }
        return new Dictionary<string, double>(_events);
    }

    async Task GrabLoop(int w, int h, Action<byte[]> publish, Func<bool> keepGoing)
    {
        var rtb = new RenderTargetBitmap(w, h, 96 * RecordScale, 96 * RecordScale, PixelFormats.Pbgra32);
        var stride = w * 4;
        var bg = new DrawingVisual();
        while (keepGoing())
        {
            rtb.Clear();
            using (var dc = bg.RenderOpen()) dc.DrawRectangle((Brush)FindResource("Bg"), null, new Rect(0, 0, Root.ActualWidth, Root.ActualHeight));
            rtb.Render(bg);
            rtb.Render(Root);
            var buf = new byte[stride * h];
            rtb.CopyPixels(buf, stride, 0);
            publish(buf);
            await Task.Delay(8);
        }
    }

    // ---------------- reusable steps (also used by extras) ----------------
    public RadioButton? SideItem(string automationName) =>
        Find<RadioButton>(r => System.Windows.Automation.AutomationProperties.GetName(r) == automationName);

    /// <summary>Dashboard, then Optimize: stage the Competitive preset, open one tweak's details, apply.</summary>
    public async Task OptimizeSteps(string preset = "competitive", string detailsId = "mmcss-games", string category = "graphics")
    {
        var opt = _vm.OptimizePage;
        _vm.Go("dashboard");
        Mark("start");
        await Task.Delay(1800);
        await ClickAsync(SideItem("Optimize: tweaks, PC fixes and startup apps"));
        Mark("optimize");
        await Task.Delay(1300);
        await ClickAsync(Find<Button>(b => b.CommandParameter as string == preset));
        Mark("preset");
        await Task.Delay(500);
        _vm.ClearToast();
        await Task.Delay(700);
        await ClickAsync(Find<RadioButton>(r => r.DataContext is CategoryVM c && c.Id == category));
        await Task.Delay(800);
        var row = opt.All.First(r => r.T.Id == detailsId);
        await ClickAsync(Find<Button>(b => b.DataContext == row && (System.Windows.Automation.AutomationProperties.GetName(b) ?? "").StartsWith("Details")));
        Mark("details");
        await Task.Delay(2400);
        await ClickAsync(Find<Button>(b => b.DataContext is OptimizeVM && b.Content is TextBlock));
        Mark("apply");
        if (_vm.Dialog is ConfirmVM)
        {
            Mark("confirm");
            await Task.Delay(2600);
            await ClickAsync(Find<Button>(b => b.Content as string == "Apply anyway"));
        }
        await WaitUntil(() => !opt.HasStaged, 10000);
        Mark("applied");
        await Task.Delay(2200);
        _vm.ClearToast();
    }

    /// <summary>Website hero (~30 s): one-click plan from the Dashboard, applied, then a look inside Optimize.</summary>
    public async Task HeroScript()
    {
        _vm.Go("dashboard");
        Mark("start");
        await Task.Delay(2200);
        await ClickAsync(Find<Button>(b => System.Windows.Automation.AutomationProperties.GetName(b) == "Optimize this PC"));
        Mark("auto");
        Mark("plan-apply");
        await WaitUntil(() => !_vm.DashboardPage.Busy, 15000);
        Mark("applied");
        await Task.Delay(2600);
        _vm.ClearToast();
        await ClickAsync(SideItem("Optimize: tweaks, PC fixes and startup apps"));
        Mark("optimize");
        await Task.Delay(900);
        await ClickAsync(Find<RadioButton>(r => r.DataContext is CategoryVM c && c.Id == "graphics"));
        await Task.Delay(700);
        var row = _vm.OptimizePage.All.First(r => r.T.Id == "mmcss-games");
        await ClickAsync(Find<Button>(b => b.DataContext == row && (System.Windows.Automation.AutomationProperties.GetName(b) ?? "").StartsWith("Details")));
        Mark("details");
        await Task.Delay(3000);
    }

    /// <summary>Tune Minecraft with a profile and prove it (~25 s).</summary>
    public async Task TuneScript()
    {
        _vm.Go("library");
        Mark("games");
        await Task.Delay(1800);
        Mark("gpu");
        await MoveCursorAsync(Find<Button>(b => b.DataContext is LibraryVM && b.Content is TextBlock), 0.5);
        Ring();
        _vm.Library.FixAllGpuCmd.Execute(null);
        await Task.Delay(1600);
        Mark("tune-open");
        await ClickAsync(Find<Button>(b => b.DataContext is GameVM { Id: "minecraft-java" } && b.Command is not null && b.ToolTip != null));
        await Task.Delay(1700);
        await ClickAsync(Find<Button>(b => b.Content as string == "Choose a profile"));
        await Task.Delay(900);
        var tune = (TuneVM)_vm.Page;
        await ClickAsync(Find<RadioButton>(r => r.DataContext is ProfileOption { Profile: Core.Profile.Performance }));
        await Task.Delay(700);
        Mark("tune-review");
        await ClickAsync(Find<Button>(b => b.Content as string == "Review changes"));
        await Task.Delay(1200);
        var row = tune.Rows.FirstOrDefault(r => r.Index == "02");
        await MoveCursorAsync(Find<Button>(b => b.DataContext == row && b.Command is not null), 0.3);
        Ring();
        if (row != null) row.Open = true;
        await Task.Delay(2000);
        Mark("tune-apply");
        await MoveCursorAsync(Find<Button>(b => b.DataContext == tune && b.Content is TextBlock), 0.5);
        Ring();
        await Task.Delay(150);
        tune.ApplyCmd.Execute(null);
        await WaitUntil(() => _vm.Page is RunVM, 3000);
        var run = (RunVM)_vm.Page;
        await WaitUntil(() => run.Stage == "results", 60000);
        Mark("results");
        await MoveCursorAsync(Find<Button>(b => b.Content as string == "Done"), 0.5);
        await Task.Delay(3600);
    }

    partial void ExtraScript(string name, ref Func<Task>? run, ref Func<Task>? preroll);

    // ---------------- helpers (also used by extras) ----------------
    public async Task ClickAsync(FrameworkElement? target, double xFraction = 0.5)
    {
        if (target is null) return;
        await MoveCursorAsync(target, xFraction);
        await Task.Delay(120);
        Ring();
        await Task.Delay(140);
        switch (target)
        {
            case RadioButton rb: rb.IsChecked = true; break;
            case CheckBox cb: cb.IsChecked = cb.IsChecked != true; break;
            case ListBoxItem li: li.IsSelected = true; break;
            case Button b when b.Command?.CanExecute(b.CommandParameter) == true: b.Command.Execute(b.CommandParameter); break;
            case ButtonBase bb: bb.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); break;
        }
    }

    public async Task MoveCursorAsync(FrameworkElement? target, double xFraction)
    {
        if (target is null) return;
        target.UpdateLayout();
        var p = target.TranslatePoint(new Point(target.ActualWidth * xFraction, target.ActualHeight * 0.55), Root);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var dur = TimeSpan.FromMilliseconds(800);
        FakeCursor.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(p.X, dur) { EasingFunction = ease });
        FakeCursor.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(p.Y, dur) { EasingFunction = ease });
        await Task.Delay(dur);
    }

    public void Ring()
    {
        var x = Canvas.GetLeft(FakeCursor);
        var y = Canvas.GetTop(FakeCursor);
        Canvas.SetLeft(ClickRing, x - ClickRing.Width / 2);
        Canvas.SetTop(ClickRing, y - ClickRing.Height / 2);
        var st = (ScaleTransform)ClickRing.RenderTransform;
        var d = TimeSpan.FromMilliseconds(450);
        st.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.3, 1.2, d));
        st.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.3, 1.2, d));
        ClickRing.BeginAnimation(OpacityProperty, new DoubleAnimation(0.9, 0, d));
    }

    public static async Task WaitUntil(Func<bool> cond, int timeoutMs = 30000)
    {
        var sw = Stopwatch.StartNew();
        while (!cond() && sw.ElapsedMilliseconds < timeoutMs) await Task.Delay(30);
    }

    public T? Find<T>(Func<T, bool> match) where T : FrameworkElement
    {
        Root.UpdateLayout();
        var stack = new Stack<DependencyObject>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            var d = stack.Pop();
            if (d is T t && t.IsVisible && match(t)) return t;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(d); i++) stack.Push(VisualTreeHelper.GetChild(d, i));
        }
        return null;
    }
}
