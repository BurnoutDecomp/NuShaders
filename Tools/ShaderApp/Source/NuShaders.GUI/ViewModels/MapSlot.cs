using NuShaders.GUI.Common;

namespace NuShaders.GUI.ViewModels;

/// <summary>One source-map file picker row (label + selected path).</summary>
public sealed class MapSlot(string label, bool required = false) : ViewModelBase
{
    private string? _path;

    public string Label { get; } = label;
    public bool Required { get; } = required;

    public string? Path
    {
        get => _path;
        set => SetProperty(ref _path, string.IsNullOrWhiteSpace(value) ? null : value);
    }
}
