using System.Windows.Input;

namespace NuShaders.GUI.Common;

/// <summary>A simple ICommand. CanExecute is re-queried automatically via CommandManager.RequerySuggested.</summary>
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => execute();
}
