using NuShaders.GUI.Common;

namespace NuShaders.GUI.ViewModels;

/// <summary>One entry in the Save ▸ "Merge into…" dropdown: a bundle's display name + the command that merges the
/// edited material into that bundle's folder. Each item carries its own command so the menu popup needs no RelativeSource.</summary>
public sealed class MergeTargetViewModel(string name, Action execute, Func<bool> canExecute)
{
    public string Name { get; } = name;
    public RelayCommand Command { get; } = new RelayCommand(execute, canExecute);
}
