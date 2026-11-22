using Framecalm.Core;
using System.IO;
using System.Diagnostics;
using System.Net.Http;
using System.Text.Json;
using System.Windows;
using Microsoft.Win32;

namespace Framecalm.App.Services;

/// <summary>Opening Framecalm when Windows starts: one per-user Run entry, started quietly in the tray.</summary>
public static class LaunchAtLogin
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string Name = "Framecalm";

    public static bool IsOn()
    {
        using var k = Registry.CurrentUser.OpenSubKey(RunKey);
        return k?.GetValue(Name) is string;
    }

    public static void Set(bool on)
    {
        using var k = Registry.CurrentUser.CreateSubKey(RunKey, true);
        if (on && Environment.ProcessPath is { } exe) k.SetValue(Name, $"\"{exe}\" --tray", RegistryValueKind.String);
        else k.DeleteValue(Name, false);
    }
}

/// <summary>True when running as the Microsoft Store (MSIX) package.</summary>
public static class Packaged
{
    [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    static extern int GetCurrentPackageFullName(ref int length, System.Text.StringBuilder? name);

    public static readonly bool IsPackaged = Check();

    static bool Check()
    {
        try { int len = 0; return GetCurrentPackageFullName(ref len, null) != 15700; } // 15700 = APPMODEL_ERROR_NO_PACKAGE
        catch { return false; }
    }
}

/// <summary>Asks GitHub for the newest public release. Sends nothing about the PC or the person:
/// just a request for the release list, with a generic user agent.</summary>
public static class UpdateCheck
{
    public sealed record Result(bool Newer, string Latest, string Url, string? Sha256 = null, long Size = 0);

    public static Version Current => typeof(UpdateCheck).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static async Task<Result?> RunAsync()
    {
        if (Packaged.IsPackaged) return null; // the Store installs updates itself
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Framecalm-update-check");
            var json = await http.GetStringAsync("https://api.github.com/repos/krezio/framecalm/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            // the installer itself, so "Get it" starts the download instead of opening the release page
            var url = "https://github.com/krezio/framecalm/releases/latest/download/Framecalm-Setup.exe";
            string? sha = null;
            long size = 0;
            if (doc.RootElement.TryGetProperty("assets", out var assets))
                foreach (var a in assets.EnumerateArray())
                    if (a.GetProperty("name").GetString() == "Framecalm-Setup.exe" && a.GetProperty("browser_download_url").GetString() is { } dl)
                    {
                        url = dl;
                        if (a.TryGetProperty("size", out var sz)) size = sz.GetInt64();
                        if (a.TryGetProperty("digest", out var dg) && dg.GetString() is { } d && d.StartsWith("sha256:")) sha = d[7..];
                    }
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
            var cur = new Version(Current.Major, Current.Minor, Math.Max(0, Current.Build));
            return new Result(latest > cur, tag, url, sha, size);
        }
        catch { return null; }
    }
}

/// <summary>Downloads the new installer, checks it, and runs it silently. The installer closes this copy,
/// replaces the files and opens the new version. Only for the installed app; the portable zip and the
/// Store version update another way.</summary>
public static class Updater
{
    public static bool CanInstall =>
        !Packaged.IsPackaged && Environment.ProcessPath is { } p &&
        p.StartsWith(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Framecalm"), StringComparison.OrdinalIgnoreCase);

    public static async Task<string> DownloadAsync(UpdateCheck.Result r, IProgress<double> progress)
    {
        var file = Path.Combine(Path.GetTempPath(), $"Framecalm-Setup-{r.Latest}.exe");
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Framecalm-updater");
        using var resp = await http.GetAsync(r.Url, HttpCompletionOption.ResponseHeadersRead);
        resp.EnsureSuccessStatusCode();
        var total = resp.Content.Headers.ContentLength ?? r.Size;
        using (var src = await resp.Content.ReadAsStreamAsync())
        using (var dst = File.Create(file))
        {
            var buf = new byte[81920];
            long done = 0;
            int n;
            while ((n = await src.ReadAsync(buf)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n));
                done += n;
                if (total > 0) progress.Report((double)done / total);
            }
        }

        // checked before anything runs: a Windows program, the expected size, and GitHub's SHA-256 when it publishes one
        var bytes = await File.ReadAllBytesAsync(file);
        if (bytes.Length < 1_000_000 || bytes[0] != (byte)'M' || bytes[1] != (byte)'Z') { File.Delete(file); throw new InvalidDataException("The download isn't a valid installer."); }
        if (r.Size > 0 && bytes.Length != r.Size) { File.Delete(file); throw new InvalidDataException("The download is incomplete."); }
        if (r.Sha256 is { } want)
        {
            var got = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));
            if (!got.Equals(want, StringComparison.OrdinalIgnoreCase)) { File.Delete(file); throw new InvalidDataException("The download doesn't match the published checksum."); }
        }
        return file;
    }

    public static void RunInstaller(string file) =>
        Process.Start(new ProcessStartInfo(file, "/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS /relaunch=1") { UseShellExecute = true });
}

/// <summary>The tray icon: double-click to open, right-click for a short menu.</summary>
public sealed class Tray : IDisposable
{
    readonly System.Windows.Forms.NotifyIcon _icon;
    bool _toldOnce;

    public Tray(Action open, Action optimize, Action quit)
    {
        _icon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Framecalm",
            Icon = Environment.ProcessPath is { } exe ? System.Drawing.Icon.ExtractAssociatedIcon(exe) : System.Drawing.SystemIcons.Application,
            Visible = true,
        };
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(L.T("Open Framecalm"), null, (_, _) => open());
        menu.Items.Add(L.T("Optimize now"), null, (_, _) => optimize());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add(L.T("Quit"), null, (_, _) => quit());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => open();
    }

    public void TellOnce()
    {
        if (_toldOnce) return;
        _toldOnce = true;
        _icon.ShowBalloonTip(4000, L.T("Framecalm is still running"),
            L.T("It's in the tray, keeping Game Session ready. Right-click the icon to quit, or change this in Settings."), System.Windows.Forms.ToolTipIcon.None);
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); }
}
