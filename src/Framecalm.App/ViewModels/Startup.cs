using System.Collections.ObjectModel;
using Framecalm.App.Services;

namespace Framecalm.App.ViewModels;

/// <summary>Startup apps on their own page: what starts with Windows, with the same switch Task Manager uses.</summary>
public sealed class StartupVM(MainVM m) : Observable, IRefresh
{
    public ObservableCollection<StartupRowVM> Apps => m.HealthPage.Startup;
    public int OnCount => Apps.Count(a => a.Enabled);
    public string Summary => Apps.Count == 0
        ? "No startup apps for your account. Apps installed for all users can be managed in Task Manager."
        : $"{OnCount} of {Apps.Count} start with Windows. Fewer means a faster sign-in and less running in the background while you play.";
    public bool HasApps => Apps.Count > 0;

    bool _demoLaunch;
    public bool LaunchFramecalm
    {
        get => m.IsDemo ? _demoLaunch : SafeIsOn();
        set
        {
            if (m.IsDemo) _demoLaunch = value;
            else try { LaunchAtLogin.Set(value); } catch { m.ShowToast("Couldn't change that. Try again from Settings.", bad: true); }
            Raise();
        }
    }

    static bool SafeIsOn() { try { return LaunchAtLogin.IsOn(); } catch { return false; } }

    public void Refresh()
    {
        m.HealthPage.Refresh();
        foreach (var a in Apps) a.PropertyChanged += (_, _) => { Raise(nameof(OnCount)); Raise(nameof(Summary)); };
        RaiseAll();
    }
}
