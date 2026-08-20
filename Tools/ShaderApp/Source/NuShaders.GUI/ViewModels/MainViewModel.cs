namespace NuShaders.GUI.ViewModels;

/// <summary>Root view-model: the shared bundle workspace + the feature tabs that consume it.</summary>
public sealed class MainViewModel
{
    public BundleWorkspaceViewModel Workspace { get; } = new();
    public MaterialEditorViewModel MaterialEditor { get; }

    public MainViewModel() => MaterialEditor = new MaterialEditorViewModel(Workspace);
}
