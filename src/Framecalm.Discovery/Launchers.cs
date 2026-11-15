using System.Text.Json;
using System.Text.RegularExpressions;
using Framecalm.Core;
using Microsoft.Win32;

namespace Framecalm.Discovery;

/// <summary>Finds installed games from launcher manifests only (offline, no disk-wide scans).</summary>
public sealed class GameDiscovery : IGameDiscovery
{
    public IReadOnlyList<GameInstall> Discover(IReadOnlyList<GameDefinition> catalog)
    {
        var found = new List<GameInstall>();
        void Try(Func<IEnumerable<GameInstall>> f) { try { found.AddRange(f()); } catch { /* one broken launcher never stops the rest */ } }
        Try(Steam);
        Try(Epic);
        Try(Riot);
        Try(BattleNet);
        Try(Xbox);
        Try(Minecraft);
        Try(Roblox);

        var result = new List<GameInstall>();
        foreach (var g in found)
        {
            var def = Match(g, catalog);
            if (def is null && g.Launcher == Launcher.Riot) continue; // Riot lists game modes and tools as separate products
            var install = def is null ? g : g with
            {
                Id = def.Id,
                Name = def.Name,
                Support = def.Support,
                Definition = def,
                ProcessName = def.ProcessNames.FirstOrDefault() ?? g.ProcessName,
                ConfigPath = def.ConfigPath is null ? null : Paths.Expand(def.ConfigPath),
                ExePath = g.ExePath ?? FindExe(g.InstallDir, def.ProcessNames),
                Cover = def.Cover ?? g.Cover,
            };
            if (result.Any(r => r.Id == install.Id)) continue;
            result.Add(install);
        }
        return result.OrderBy(r => r.Support).ThenBy(r => r.Name).ToList();
    }

    static GameDefinition? Match(GameInstall g, IReadOnlyList<GameDefinition> catalog)
    {
        var raw = g.Id.Split(':', 2) is [_, var rest] ? rest : g.Id;
        return catalog.FirstOrDefault(d => g.Launcher switch
        {
            Launcher.Steam => d.SteamIds.Contains(raw),
            Launcher.Epic => d.EpicIds.Contains(raw, StringComparer.OrdinalIgnoreCase),
            Launcher.Riot => d.RiotIds.Contains(raw, StringComparer.OrdinalIgnoreCase),
            Launcher.Minecraft => d.Id == "minecraft-java",
            Launcher.Roblox => d.Id == "roblox",
            _ => false,
        }) ?? catalog.FirstOrDefault(d => d.NameMatches.Any(m => g.Name.Contains(m, StringComparison.OrdinalIgnoreCase)) && d.Id != "minecraft-java");
    }

    static string? FindExe(string? dir, string[] names)
    {
        if (dir is null || !Directory.Exists(dir) || names.Length == 0) return null;
        // shallow, bounded search inside the game's own folder only
        foreach (var n in names)
        {
            try
            {
                var hit = Directory.EnumerateFiles(dir, n + ".exe", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 5, IgnoreInaccessible = true }).FirstOrDefault();
                if (hit != null) return hit;
            }
            catch { }
        }
        return null;
    }

    // ---------------- Steam ----------------
    public static string? SteamRoot() =>
        (Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string)?.Replace('/', '\\');

    static readonly Regex VdfPair = new("\"([^\"]+)\"\\s+\"([^\"]*)\"");

    static IEnumerable<GameInstall> Steam()
    {
        var root = SteamRoot();
        if (root is null || !Directory.Exists(root)) yield break;
        var libs = new List<string> { root };
        var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf))
            foreach (Match m in VdfPair.Matches(File.ReadAllText(vdf)))
                if (m.Groups[1].Value == "path") libs.Add(m.Groups[2].Value.Replace(@"\\", @"\"));
        foreach (var lib in libs.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var acf in Directory.GetFiles(apps, "appmanifest_*.acf"))
            {
                var kv = VdfPair.Matches(File.ReadAllText(acf)).GroupBy(m => m.Groups[1].Value).ToDictionary(g => g.Key, g => g.First().Groups[2].Value);
                if (!kv.TryGetValue("appid", out var id) || !kv.TryGetValue("name", out var name)) continue;
                if (name.Contains("Steamworks Common") || name.Contains("Proton") || name.Contains("Redistributable")) continue;
                var dir = kv.TryGetValue("installdir", out var d) ? Path.Combine(apps, "common", d) : null;
                yield return new GameInstall { Id = "steam:" + id, Name = name, Launcher = Launcher.Steam, InstallDir = dir, ArtPath = SteamArt(root, id), Cover = "steam:" + id };
            }
        }
    }

    /// <summary>The cover Steam already downloaded for its own library view. Shown locally only, never copied anywhere.</summary>
    public static string? SteamArt(string root, string appId)
    {
        var cache = Path.Combine(root, "appcache", "librarycache");
        var flat = Path.Combine(cache, $"{appId}_library_600x900.jpg");
        if (File.Exists(flat)) return flat;
        var dir = Path.Combine(cache, appId);
        if (Directory.Exists(dir))
            return Directory.EnumerateFiles(dir, "library_600x900*.jpg", SearchOption.AllDirectories).FirstOrDefault()
                ?? Directory.EnumerateFiles(dir, "library_capsule*.jpg", SearchOption.AllDirectories).FirstOrDefault();
        return null;
    }

    // ---------------- Epic ----------------
    static IEnumerable<GameInstall> Epic()
    {
        var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
        if (!Directory.Exists(dir)) yield break;
        foreach (var f in Directory.GetFiles(dir, "*.item"))
        {
            JsonElement j;
            try { j = JsonDocument.Parse(File.ReadAllText(f)).RootElement; } catch { continue; }
            string S(string n) => j.TryGetProperty(n, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
            var loc = S("InstallLocation");
            var exe = S("LaunchExecutable");
            yield return new GameInstall
            {
                Id = "epic:" + S("AppName"), Name = S("DisplayName"), Launcher = Launcher.Epic, InstallDir = loc,
                ExePath = exe.Length > 0 && loc.Length > 0 ? Path.Combine(loc, exe) : null,
            };
        }
    }

    // ---------------- Riot ----------------
    static IEnumerable<GameInstall> Riot()
    {
        var meta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Riot Games", "Metadata");
        if (!Directory.Exists(meta)) yield break;
        foreach (var yaml in Directory.EnumerateFiles(meta, "*.product_settings.yaml", SearchOption.AllDirectories))
        {
            var product = Path.GetFileName(yaml).Split('.')[0];
            var line = File.ReadLines(yaml).FirstOrDefault(l => l.TrimStart().StartsWith("product_install_full_path:"));
            var path = line?.Split(':', 2)[1].Trim().Trim('"');
            if (product is "riot_client" or "bacon") continue;
            yield return new GameInstall
            {
                Id = "riot:" + product, Name = product == "valorant" ? "VALORANT" : product == "league_of_legends" ? "League of Legends" : product,
                Launcher = Launcher.Riot, InstallDir = path,
            };
        }
    }

    // ---------------- Battle.net (from Windows' own uninstall list) ----------------
    static IEnumerable<GameInstall> BattleNet()
    {
        foreach (var (name, loc) in Uninstall(p => p == "Blizzard Entertainment"))
            if (!name.Contains("Battle.net", StringComparison.OrdinalIgnoreCase))
                yield return new GameInstall { Id = "bnet:" + name, Name = name, Launcher = Launcher.BattleNet, InstallDir = loc };
    }

    static IEnumerable<(string Name, string? Location)> Uninstall(Func<string, bool> publisher)
    {
        foreach (var root in new[] { Registry.LocalMachine, Registry.CurrentUser })
            foreach (var path in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
            {
                using var k = root.OpenSubKey(path);
                if (k is null) continue;
                foreach (var s in k.GetSubKeyNames())
                {
                    using var e = k.OpenSubKey(s);
                    if (e?.GetValue("Publisher") is string p && publisher(p) && e.GetValue("DisplayName") is string n)
                        yield return (n, e.GetValue("InstallLocation") as string);
                }
            }
    }

    // ---------------- Xbox app (PC Game Pass) ----------------
    static readonly Regex XboxExe = new(@"<Executable[^>]*\sName=""([^""]+\.exe)""", RegexOptions.IgnoreCase);

    static IEnumerable<GameInstall> Xbox()
    {
        foreach (var drive in DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.Fixed && d.IsReady))
        {
            var root = Path.Combine(drive.RootDirectory.FullName, "XboxGames");
            if (!Directory.Exists(root)) continue;
            foreach (var game in Directory.GetDirectories(root))
            {
                // real games have a Content folder; add-on packs and launch stubs next to them don't
                var content = Path.Combine(game, "Content");
                if (!Directory.Exists(content)) continue;
                string? exe = null;
                var config = Path.Combine(content, "MicrosoftGame.config");
                try
                {
                    if (File.Exists(config) && XboxExe.Match(File.ReadAllText(config)) is { Success: true } m)
                    {
                        var candidate = Path.Combine(content, m.Groups[1].Value);
                        if (File.Exists(candidate)) exe = candidate;
                    }
                }
                catch { }
                var name = Path.GetFileName(game);
                // add-on packs install as their own folders with only a launch helper inside
                if (exe is null || Path.GetFileName(exe).Equals("gamelaunchhelper.exe", StringComparison.OrdinalIgnoreCase) || Regex.IsMatch(name, @"DLC\d+")) continue;
                yield return new GameInstall
                {
                    Id = "xbox:" + name, Name = name, Launcher = Launcher.Xbox, InstallDir = game, ExePath = exe, Cover = "search:" + name,
                    ProcessName = exe is null ? null : Path.GetFileNameWithoutExtension(exe),
                };
            }
        }
    }

    // ---------------- Roblox (desktop player) ----------------
    static IEnumerable<GameInstall> Roblox()
    {
        var versions = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Roblox", "Versions");
        if (!Directory.Exists(versions)) yield break;
        var exe = Directory.GetDirectories(versions).Select(d => Path.Combine(d, "RobloxPlayerBeta.exe")).Where(File.Exists)
            .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        if (exe != null) yield return new GameInstall { Id = "roblox:player", Name = "Roblox", Launcher = Launcher.Roblox, InstallDir = Path.GetDirectoryName(exe), ExePath = exe };
    }

    // ---------------- Minecraft Java ----------------
    static IEnumerable<GameInstall> Minecraft()
    {
        var mc = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        if (File.Exists(Path.Combine(mc, "options.txt")))
            yield return new GameInstall { Id = "minecraft:java", Name = "Minecraft: Java Edition", Launcher = Launcher.Minecraft, InstallDir = mc, ExePath = JavaRuntime(mc) };
    }

    /// <summary>The launcher's bundled Java (what actually renders the game), for the GPU preference fix.</summary>
    static string? JavaRuntime(string mc)
    {
        var candidates = new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Packages", "Microsoft.4297127D64EC6_8wekyb3d8bbwe", "LocalCache", "Local", "runtime"),
            Path.Combine(mc, "runtime"),
        };
        foreach (var c in candidates.Where(Directory.Exists))
        {
            var hit = Directory.EnumerateFiles(c, "javaw.exe", new EnumerationOptions { RecurseSubdirectories = true, MaxRecursionDepth = 6, IgnoreInaccessible = true })
                .OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
            if (hit != null) return hit;
        }
        return null;
    }
}
