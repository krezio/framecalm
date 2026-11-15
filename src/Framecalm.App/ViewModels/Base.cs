using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Framecalm.App.ViewModels;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Raise([CallerMemberName] string? n = null) { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n)); CommandManager.InvalidateRequerySuggested(); }
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(n);
        return true;
    }
    protected void RaiseAll() { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(string.Empty)); CommandManager.InvalidateRequerySuggested(); }
}

public sealed class Cmd(Action<object?> run, Func<object?, bool>? can = null) : ICommand
{
    public Cmd(Action run, Func<bool>? can = null) : this(_ => run(), can is null ? null : _ => can()) { }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? p) => can?.Invoke(p) ?? true;
    public void Execute(object? p) => run(p);
}

public sealed class AsyncCmd(Func<object?, Task> run, Func<object?, bool>? can = null) : ICommand
{
    bool _busy;
    public AsyncCmd(Func<Task> run, Func<bool>? can = null) : this(_ => run(), can is null ? null : _ => can()) { }
    public event EventHandler? CanExecuteChanged { add => CommandManager.RequerySuggested += value; remove => CommandManager.RequerySuggested -= value; }
    public bool CanExecute(object? p) => !_busy && (can?.Invoke(p) ?? true);
    public async void Execute(object? p)
    {
        _busy = true;
        CommandManager.InvalidateRequerySuggested();
        try { await run(p); }
        catch (Exception ex) { MainVM.Current?.ShowToast("Something went wrong: " + ex.Message, bad: true); }
        finally { _busy = false; CommandManager.InvalidateRequerySuggested(); }
    }
}
