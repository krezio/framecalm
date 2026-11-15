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
    public sealed record Result(bool Newer, string Latest, string Url);

    public static Version Current => typeof(UpdateCheck).Assembly.GetName().Version ?? new Version(0, 0, 0);

    public static async Task<Result?> RunAsync()
    {
        if (Packaged.IsPackaged) return null; // the Store installs updates itself
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Framecalm-update-check");
            var json = await http.GetStringAsync("https://api.github.com/repos/krezio/framecalm-releases/releases/latest");
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.GetProperty("tag_name").GetString() ?? "";
            var url = doc.RootElement.GetProperty("html_url").GetString() ?? "https://framecalm.krezio.xyz";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var latest)) return null;
            var cur = new Version(Current.Major, Current.Minor, Math.Max(0, Current.Build));
            return new Result(latest > cur, tag, url);
        }
        catch { return null; }
    }
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
        menu.Items.Add("Open Framecalm", null, (_, _) => open());
        menu.Items.Add("Optimize now", null, (_, _) => optimize());
        menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
        menu.Items.Add("Quit", null, (_, _) => quit());
        _icon.ContextMenuStrip = menu;
        _icon.DoubleClick += (_, _) => open();
    }

    public void TellOnce()
    {
        if (_toldOnce) return;
        _toldOnce = true;
        _icon.ShowBalloonTip(4000, "Framecalm is still running",
            "It's in the tray, keeping Game Session ready. Right-click the icon to quit, or change this in Settings.", System.Windows.Forms.ToolTipIcon.None);
    }

    public void Dispose() { _icon.Visible = false; _icon.Dispose(); }
}
