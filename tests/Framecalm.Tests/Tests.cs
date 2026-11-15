using Framecalm.Backups;
using Framecalm.Benchmarks;
using Framecalm.Core;
using Framecalm.GameAdapters;

namespace Framecalm.Tests;

static class Fx
{
    public static string Path(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    public static string Text(string name) => File.ReadAllText(Path(name));
    public static GameDefinition Def(string id) => Catalog.All.First(d => d.Id == id);

    public static readonly HardwareInfo MidLaptop = new()
    {
        Cpu = "Test CPU", Threads = 12, RamBytes = 16L << 30, RefreshHz = 144, IsLaptop = true,
        Gpus = [new GpuInfo("Test GPU", "NVIDIA", 6L << 30, GpuKind.Dedicated, null, null)],
    };

    /// <summary>A copy of a fixture in a fresh temp folder, so tests can write to it.</summary>
    public static string Copy(string name)
    {
        var dir = Directory.CreateTempSubdirectory("framecalm-test-").FullName;
        var dest = System.IO.Path.Combine(dir, name);
        File.Copy(Path(name), dest);
        return dest;
    }
}

public class CatalogTests
{
    [Fact]
    public void All_game_definitions_load_and_validate() => Assert.True(Catalog.All.Count >= 8);

    [Fact]
    public void Automatic_games_have_adapters_and_format_rules()
    {
        foreach (var d in Catalog.All.Where(d => d.Support == SupportLevel.Automatic))
        {
            Assert.Empty(Catalog.Validate(d));
            Assert.NotNull(d.Version);
        }
    }

    [Fact]
    public void Strict_anti_cheat_games_are_guided_only()
    {
        foreach (var d in Catalog.All.Where(d => d.StrictAntiCheat))
        {
            Assert.Equal(SupportLevel.Guided, d.Support);
            Assert.Null(d.Adapter);
        }
    }
}

public class AdapterTests
{
    readonly MinecraftOptionsAdapter _mc = new();
    readonly IniAdapter _ini = new("unreal-ini");

    [Fact]
    public void Minecraft_known_version_passes_check()
    {
        var c = _mc.Check(Fx.Text("options-1.21.4.txt"), Fx.Def("minecraft-java"));
        Assert.True(c.Ok, c.Reason);
        Assert.Equal("4189", c.Version);
    }

    [Fact]
    public void Minecraft_unknown_version_fails_closed() =>
        Assert.False(_mc.Check(Fx.Text("options-future.txt"), Fx.Def("minecraft-java")).Ok);

    [Fact]
    public void Minecraft_set_changes_only_that_line_and_keeps_quotes_and_line_endings()
    {
        var def = Fx.Def("minecraft-java");
        var text = Fx.Text("options-crlf.txt");
        var clouds = def.Settings.First(s => s.Key == "renderClouds");
        var rd = def.Settings.First(s => s.Key == "renderDistance");
        var after = _mc.Set(_mc.Set(text, clouds, "false"), rd, "8");
        Assert.Contains("renderClouds:\"false\"\r\n", after);
        Assert.Contains("renderDistance:8\r\n", after);
        Assert.Equal(text.Split('\n').Length, after.Split('\n').Length);
        Assert.Equal(text.Replace("renderClouds:\"fast\"", "renderClouds:\"false\"").Replace("renderDistance:12", "renderDistance:8"), after);
    }

    [Fact]
    public void Ini_reads_and_writes_inside_the_right_section_and_keeps_spelling()
    {
        var def = Fx.Def("fortnite");
        var text = Fx.Text("GameUserSettings.ini");
        Assert.True(_ini.Check(text, def).Ok);
        var vsync = def.Settings.First(s => s.Key == "bUseVSync");
        Assert.Equal("True", _ini.Get(text, vsync));
        var after = _ini.Set(text, vsync, "false");
        Assert.Contains("bUseVSync=False", after);
        Assert.Equal("3", _ini.Get(after, def.Settings.First(s => s.Key == "sg.ShadowQuality")));
    }

    [Fact]
    public void Ini_with_unknown_layout_fails_closed()
    {
        Assert.False(_ini.Check(Fx.Text("unknown.ini"), Fx.Def("fortnite")).Ok);
        Assert.False(_ini.Check(Fx.Text("unknown.ini"), Fx.Def("rocket-league")).Ok);
    }

    [Fact]
    public void Ini_never_inserts_missing_keys() =>
        Assert.Throws<InvalidOperationException>(() => _ini.Set(Fx.Text("TASystemSettings.ini"), new SettingDef { Key = "LensFlares", Section = "SystemSettings", Label = "x", Why = "", Tradeoff = "" }, "False"));
}

public class RecommenderTests
{
    [Fact]
    public void Never_makes_a_setting_heavier()
    {
        var def = Fx.Def("minecraft-java");
        var text = Fx.Text("options-crlf.txt"); // already light: render 12, sim 10, fast graphics
        var mc = new MinecraftOptionsAdapter();
        foreach (var p in new[] { Profile.Light, Profile.Balanced, Profile.Performance, Profile.MaxFps })
            foreach (var c in Recommender.Propose(def, s => mc.Get(text, s), Fx.MidLaptop, p))
                Assert.True(c.Setting.Special == "refresh" || Recommender.IsFaster(c.Setting, c.New, c.Old), $"{p}: {c.Setting.Key} {c.Old} -> {c.New}");
    }

    [Fact]
    public void Frame_cap_below_refresh_is_raised_to_match_the_screen()
    {
        var def = Fx.Def("minecraft-java");
        var mc = new MinecraftOptionsAdapter();
        var text = Fx.Text("options-1.21.4.txt");
        var cap = Recommender.Propose(def, s => mc.Get(text, s), Fx.MidLaptop, Profile.Light).Single(c => c.Setting.Key == "maxFps");
        Assert.Equal("144", cap.New);
    }

    [Fact]
    public void Opt_in_settings_are_skipped_unless_asked()
    {
        var def = new GameDefinition
        {
            Id = "t", Name = "t",
            Settings = [new SettingDef { Key = "res", Label = "Resolution", Kind = SettingKind.Int, Faster = "lower", OptIn = true, Targets = new() { ["maxfps"] = "720" }, Why = "", Tradeoff = "" }],
        };
        Assert.Empty(Recommender.Propose(def, _ => "1080", Fx.MidLaptop, Profile.MaxFps));
        Assert.Single(Recommender.Propose(def, _ => "1080", Fx.MidLaptop, Profile.MaxFps, new HashSet<string> { "res" }));
    }

    [Fact]
    public void Auto_tune_rounds_put_cheap_big_wins_first_and_stop_at_target()
    {
        var def = Fx.Def("fortnite");
        var ini = new IniAdapter("unreal-ini");
        var text = Fx.Text("GameUserSettings.ini");
        var cands = Recommender.Propose(def, s => ini.Get(text, s), Fx.MidLaptop, Profile.MaxFps);
        var ordered = AutoTunePlanner.Order(cands, Bottleneck.Gpu);
        Assert.Equal("FrameRateLimit", ordered[0].Setting.Key);
        Assert.Equal("sg.ShadowQuality", ordered[1].Setting.Key);
        var rounds = AutoTunePlanner.Rounds(ordered, 80, 100, Bottleneck.Gpu);
        Assert.True(rounds.Sum(r => r.Count) < ordered.Count, "should stop before using every change");
    }
}

public class TransactionTests
{
    static (TransactionManager tx, ConfigFileTarget target, MemoryStateStore store) Setup(string fixture = "options-1.21.4.txt")
    {
        var file = Fx.Copy(fixture);
        var target = new ConfigFileTarget(file, new MinecraftOptionsAdapter(), Fx.Def("minecraft-java"));
        var store = new MemoryStateStore();
        return (new TransactionManager(store, _ => target), target, store);
    }

    static ChangeRecord Rec(string key, string old, string @new) => new() { Scope = "minecraft-java", ScopeName = "Minecraft", Key = key, Label = key, Old = old, New = @new };

    [Fact]
    public void Apply_backs_up_writes_verifies_and_records_history()
    {
        var (tx, target, store) = Setup();
        var original = File.ReadAllText(target.Path);
        var r = tx.Apply(target, [Rec("renderDistance", "16", "10"), Rec("enableVsync", "true", "false")]);
        Assert.True(r.Ok, r.Message);
        Assert.Equal("10", target.Read("renderDistance"));
        Assert.Equal(2, tx.History().Count);
        var backup = store.List("backups/").Single();
        Assert.Equal(AtomicFile.Hash(System.Text.Encoding.UTF8.GetBytes(original)), store.Hash(backup));
    }

    [Fact]
    public void Apply_refuses_if_the_value_moved_since_review()
    {
        var (tx, target, _) = Setup();
        var before = File.ReadAllText(target.Path);
        var r = tx.Apply(target, [Rec("renderDistance", "20", "10")]);
        Assert.False(r.Ok);
        Assert.Equal(before, File.ReadAllText(target.Path));
    }

    [Fact]
    public void Undo_restores_and_verifies()
    {
        var (tx, target, _) = Setup();
        var original = File.ReadAllText(target.Path);
        tx.Apply(target, [Rec("renderDistance", "16", "10")]);
        var u = tx.Undo(tx.History().Select(h => h.Id));
        Assert.Equal(1, u.Undone);
        Assert.Empty(u.Conflicts);
        Assert.Equal(original, File.ReadAllText(target.Path));
        Assert.All(tx.History(), h => Assert.Equal(ChangeStatus.Undone, h.Status));
    }

    [Fact]
    public void Undo_after_a_manual_edit_is_a_conflict_and_does_not_overwrite()
    {
        var (tx, target, _) = Setup();
        tx.Apply(target, [Rec("renderDistance", "16", "10")]);
        target.Write([("renderDistance", "14")]); // the player changed it in-game
        var u = tx.Undo(tx.History().Select(h => h.Id));
        Assert.Single(u.Conflicts);
        Assert.Equal("14", u.Conflicts[0].CurrentValue);
        Assert.Equal("14", target.Read("renderDistance"));

        // and the user can choose either way
        var keep = tx.Undo([u.Conflicts[0].Change.Id], keep: new HashSet<string> { u.Conflicts[0].Change.Id });
        Assert.Equal("14", target.Read("renderDistance"));
        Assert.Equal(ChangeStatus.Kept, tx.History().Single().Status);
    }

    [Fact]
    public void Forced_undo_puts_the_original_back()
    {
        var (tx, target, _) = Setup();
        tx.Apply(target, [Rec("renderDistance", "16", "10")]);
        target.Write([("renderDistance", "14")]);
        var id = tx.History().Single().Id;
        tx.Undo([id], force: new HashSet<string> { id });
        Assert.Equal("16", target.Read("renderDistance"));
    }

    [Fact]
    public void Crash_mid_write_is_rolled_back_on_next_start()
    {
        var (_, target, store) = Setup();
        var original = File.ReadAllText(target.Path);
        // simulate: journal + backup written, file changed, then the process died before "committed"
        store.CopyIn(target.Path, "backups/crash/options.txt");
        var journal = new Journal
        {
            BatchId = "crash", Kind = TargetKind.ConfigFile, Target = target.Path, BackupName = "backups/crash/options.txt",
            OriginalHash = AtomicFile.HashFile(target.Path), Changes = [Rec("renderDistance", "16", "10")],
        };
        store.WriteText("journal/crash.json", System.Text.Json.JsonSerializer.Serialize(journal, Json.Options));
        target.Write([("renderDistance", "10")]);

        var tx = new TransactionManager(store, _ => target);
        var notes = tx.Recover();
        Assert.Single(notes);
        Assert.Equal(original, File.ReadAllText(target.Path));
        Assert.Empty(tx.Recover()); // idempotent
    }

    [Fact]
    public void Write_to_a_file_that_became_unrecognised_fails_and_restores()
    {
        var (tx, target, _) = Setup();
        // replace the file with something the adapter doesn't recognise between review and apply
        File.WriteAllText(target.Path, "garbage");
        var r = tx.Apply(target, [Rec("renderDistance", "16", "10")]);
        Assert.False(r.Ok);
        Assert.Equal("garbage", File.ReadAllText(target.Path));
    }
}

public class StatsTests
{
    [Fact]
    public void Average_fps_is_frames_over_time_not_mean_of_fps()
    {
        var s = RunStats.From([10, 10, 30]); // 3 frames in 50 ms
        Assert.Equal(60, s.AvgFps, 3);
    }

    [Fact]
    public void Small_differences_are_reported_as_noise()
    {
        var before = new[] { 100.0, 104, 97 }.Select(f => new RunStats(f, 10, 12, 15, 1000)).ToList();
        var after = new[] { 102.0, 99, 104 }.Select(f => new RunStats(f, 10, 12, 15, 1000)).ToList();
        var c = new Comparison { Before = before, After = after };
        Assert.False(c.IsReal);
        Assert.StartsWith("Within noise", c.Verdict());
    }

    [Fact]
    public void Real_gains_are_reported_with_numbers()
    {
        var c = new Comparison
        {
            Before = [new(60, 16, 20, 30, 1), new(61, 16, 20, 30, 1), new(59, 16, 20, 30, 1)],
            After = [new(90, 11, 13, 16, 1), new(91, 11, 13, 16, 1), new(89, 11, 13, 16, 1)],
        };
        Assert.True(c.IsReal);
        Assert.Contains("30 FPS", c.Verdict());
    }

    [Fact]
    public void Bottleneck_reads_gpu_busy_share()
    {
        FrameSample[] F(double ft, double gpu) => Enumerable.Repeat(new FrameSample(ft, gpu, 0), 100).ToArray();
        Assert.Equal(Bottleneck.Gpu, BottleneckAnalyzer.Analyze(F(10, 9.5), 240).Kind);
        Assert.Equal(Bottleneck.Cpu, BottleneckAnalyzer.Analyze(F(10, 4), 240).Kind);
        Assert.Equal(Bottleneck.Unknown, BottleneckAnalyzer.Analyze(F(10, 0), 240).Kind);
    }

    [Fact]
    public void PresentMon_csv_both_versions_parse()
    {
        var v2 = PresentMonRunner.Parse(File.ReadAllLines(Fx.Path("presentmon-v2.csv")));
        Assert.Equal(3, v2.Count);
        Assert.Equal(12.0, v2[0].GpuBusy);
        var v1 = PresentMonRunner.Parse(File.ReadAllLines(Fx.Path("presentmon-v1.csv")));
        Assert.Equal(2, v1.Count);
        Assert.Equal(8.3, v1[0].FrameTime);
    }
}

public class SystemFixTests
{
    static Framecalm.Windows.SystemState State(string hz, int max, string? recording, bool laptop)
    {
        var reg = new Framecalm.Demo.MemoryRegistryTarget(TargetKind.Registry, "reg");
        if (recording != null) reg.Values[Framecalm.Windows.SystemFixes.RecordingKey] = recording;
        reg.Values[Framecalm.Windows.SystemFixes.GameModeKey] = "dword:1";
        var display = new Framecalm.Demo.MemoryRegistryTarget(TargetKind.Display, "display");
        display.Values["refresh"] = hz;
        var power = new Framecalm.Demo.MemoryRegistryTarget(TargetKind.PowerPlan, "power");
        power.Values["plan"] = "balanced";
        return new(reg, display, power, max, ("fast", "High performance"), "Balanced", laptop, OnBattery: false, IsWindows11: true);
    }

    [Fact]
    public void Screen_below_its_refresh_rate_is_offered_as_a_fix()
    {
        var fixes = Framecalm.Windows.SystemFixes.Find(State("60", 144, "dword:1", laptop: true));
        Assert.Contains(fixes, f => f.Id == "refresh" && f.New == "144");
        Assert.Contains(fixes, f => f.Id == "recording" && f.New == "dword:0");
        Assert.Contains(fixes, f => f.Id == "windowed");
        Assert.DoesNotContain(fixes, f => f.Id == "gamemode"); // already on
        Assert.False(fixes.Single(f => f.Id == "power").DefaultOn); // laptops: opt-in
    }

    [Fact]
    public void A_well_set_up_pc_gets_no_fixes_after_applying_them()
    {
        var s = State("144", 144, "dword:0", laptop: false);
        var tx = new TransactionManager(new MemoryStateStore(), j => new[] { s.Registry, s.Display, s.Power }.First(t => t.Path == j.Target));
        foreach (var f in Framecalm.Windows.SystemFixes.Find(s))
            Assert.True(tx.Apply(f.Target, [new ChangeRecord { Key = f.Key, Label = f.Title, Old = f.Old, New = f.New }]).Ok);
        Assert.Empty(Framecalm.Windows.SystemFixes.Find(s));
        tx.Undo(tx.History().Select(h => h.Id));
        Assert.NotEmpty(Framecalm.Windows.SystemFixes.Find(s)); // undo really puts them back
    }
}

public class TweakTests
{
    [Fact]
    public void Catalog_loads_and_every_tweak_is_complete()
    {
        var all = Framecalm.Windows.TweakCatalog.All;
        Assert.True(all.Count >= 45);
        foreach (var t in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(t.What), t.Id);
            Assert.False(string.IsNullOrWhiteSpace(t.Tradeoff), t.Id);
            Assert.NotEmpty(t.Templates);
            Assert.All(t.Presets, p => Assert.Contains(Framecalm.Windows.TweakCatalog.Presets, x => x.Id == p));
        }
    }

    [Fact]
    public void Helper_allowlist_only_accepts_catalog_keys()
    {
        var key = Framecalm.Windows.TweakCatalog.Find("system-responsiveness")!.Templates[0].Key;
        Assert.Null(Framecalm.Windows.TweakIo.Validate(new() { Key = key, Value = "10" }));
        Assert.NotNull(Framecalm.Windows.TweakIo.Validate(new() { Key = key, Value = "ten" }));
        Assert.NotNull(Framecalm.Windows.TweakIo.Validate(new() { Key = @"reg|HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run|evil|sz", Value = "x" }));
        var nic = Framecalm.Windows.TweakCatalog.Find("nagle-off")!.Templates[0].Key.Replace("{nic}", "{6C1D2F0A-4B7E-4A11-9C3D-2B5E8F7A9D10}");
        Assert.True(Framecalm.Windows.TweakCatalog.IsAllowedKey(nic));
        Assert.False(Framecalm.Windows.TweakCatalog.IsAllowedKey(nic.Replace("{6C1D", @"..{6C1D")));
    }

    [Fact]
    public void Tweak_applies_verifies_and_undoes_through_transactions()
    {
        var target = new Framecalm.Windows.TweakTarget(new Framecalm.Windows.MemoryTweakBackend());
        var tx = new TransactionManager(new MemoryStateStore(), _ => target);
        var t = Framecalm.Windows.TweakCatalog.Find("mmcss-games")!;
        Assert.Equal(Framecalm.Windows.TweakState.Off, target.State(t));
        var recs = t.Atoms(target.Backend.Nics).Select(a => new ChangeRecord { Scope = "tweak:" + t.Id, Key = a.Key, Label = t.Name, Old = target.Read(a.Key), New = a.Value }).ToList();
        Assert.True(tx.Apply(target, recs).Ok);
        Assert.Equal(Framecalm.Windows.TweakState.On, target.State(t));
        var u = tx.Undo(recs.Select(r => r.Id));
        Assert.Equal(3, u.Undone);
        Assert.Equal(Framecalm.Windows.TweakState.Off, target.State(t));
    }
}
