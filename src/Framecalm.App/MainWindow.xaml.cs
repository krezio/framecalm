using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Framecalm.App.Controls;
using Framecalm.App.ViewModels;

namespace Framecalm.App;

public partial class MainWindow : Window
{
    readonly MainVM _vm;

    public MainWindow(MainVM vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.PropertyChanged += OnVmChanged;
        Loaded += async (_, _) =>
        {
            if (vm.Page is not FirstRunVM) await vm.LoadAsync();
        };
        SourceInitialized += (_, _) =>
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int dark = 1;
            DwmSetWindowAttribute(hwnd, 20, ref dark, sizeof(int)); // dark title/border
            int caption = 0x000B0909;
            DwmSetWindowAttribute(hwnd, 35, ref caption, sizeof(int));
        };
        StateChanged += (_, _) =>
        {
            MaxBtn.Content = WindowState == WindowState.Maximized ? "" : "";
            Root.Margin = WindowState == WindowState.Maximized ? new Thickness(7) : new Thickness(0);
        };
    }

    object? _lastPage;

    void OnVmChanged(object? s, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not ("Page" or "") || ReferenceEquals(_lastPage, _vm.Page)) return;
        _lastPage = _vm.Page;
        // pages slide up a touch and fade in; skipped when motion is reduced
        if (Motion.Reduced) return;
        var t = (TranslateTransform)PageHost.RenderTransform;
        PageHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)) { EasingFunction = Motion.Out });
        t.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(320)) { EasingFunction = Motion.Out });
    }

    // ---------- tray ----------
    Framecalm.App.Services.Tray? _tray;
    bool _quitting;

    /// <summary>Puts Framecalm next to the clock. Closing the window then hides it there instead of quitting,
    /// when the setting is on.</summary>
    public void EnableTray()
    {
        if (_tray != null) return;
        _tray = new Framecalm.App.Services.Tray(ShowFromTray, OptimizeFromTray, Quit);
        Closing += (_, e) =>
        {
            if (_quitting || !_vm.S.Settings.CloseToTray) return;
            e.Cancel = true;
            Hide();
            _tray.TellOnce();
        };
        Closed += (_, _) => { _tray?.Dispose(); Application.Current.Shutdown(); };
    }

    void ShowFromTray()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    void OptimizeFromTray()
    {
        ShowFromTray();
        _vm.Go("dashboard");
        Dispatcher.InvokeAsync(async () =>
        {
            await WaitUntil(() => _vm.Loaded, 20000);
            if (_vm.DashboardPage.OptimizeCmd.CanExecute(null)) _vm.DashboardPage.OptimizeCmd.Execute(null);
        });
    }

    void Quit()
    {
        _quitting = true;
        Close();
        Application.Current.Shutdown();
    }

    void Min_Click(object s, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Max_Click(object s, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    void Close_Click(object s, RoutedEventArgs e) => Close();

    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
}
