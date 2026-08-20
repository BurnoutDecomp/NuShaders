using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.GUI.Common;

namespace NuShaders.GUI.ViewModels;

/// <summary>
/// The Textures tab: a filterable (virtualized) list of the workspace's textures + a large preview of the selected one,
/// with the ability to assign a name (persisted to that texture's own bundle's .debug.xml).
/// </summary>
public sealed class TexturePreviewViewModel : ViewModelBase
{
    private readonly BundleWorkspaceViewModel _ws;

    public ICollectionView ItemsView { get; }

    private string _filterText = "";
    public string FilterText { get => _filterText; set { if (SetProperty(ref _filterText, value)) ItemsView.Refresh(); } }

    private CatalogResource? _selectedTexture;
    public CatalogResource? SelectedTexture { get => _selectedTexture; set { if (SetProperty(ref _selectedTexture, value)) OnSelected(); } }

    private BitmapSource? _preview;
    public BitmapSource? Preview { get => _preview; private set => SetProperty(ref _preview, value); }

    private string _name = "";
    public string Name { get => _name; private set => SetProperty(ref _name, value); }

    private string _info = "";
    public string Info { get => _info; private set => SetProperty(ref _info, value); }

    private string _renameText = "";
    public string RenameText { get => _renameText; set { if (SetProperty(ref _renameText, value)) CommandManager.InvalidateRequerySuggested(); } }

    public RelayCommand RenameCommand { get; }

    public TexturePreviewViewModel(BundleWorkspaceViewModel ws)
    {
        _ws = ws;
        ItemsView = CollectionViewSource.GetDefaultView(_ws.Textures);
        ItemsView.Filter = o => o is CatalogResource r &&
            (_filterText.Length == 0 || r.DisplayName.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
        RenameCommand = new RelayCommand(Rename, () => SelectedTexture is not null && !string.IsNullOrWhiteSpace(RenameText));
    }

    private async void OnSelected()
    {
        RenameText = SelectedTexture?.Name is not null ? SelectedTexture.ShortName : "";
        Name = SelectedTexture?.ShortName ?? "";
        Preview = null; Info = "";
        if (SelectedTexture is not { DatPath: { } path } tex) return;

        var (bmp, info) = await Task.Run(() =>
        {
            BitmapSource? b = ThumbnailLoader.Load(tex, 256);
            string inf = "";
            if (File.Exists(path))
            {
                var hdr = BPRTextureResource.ReadPrimary(File.ReadAllBytes(path));
                inf = $"{hdr.Width} × {hdr.Height}    {hdr.Format}    {hdr.MipLevels} mip(s)\nid: {tex.Id}";
            }
            return (b, inf);
        });
        if (SelectedTexture?.Id.Value == tex.Id.Value) { Preview = bmp; Info = info; }   // ignore a stale load
    }

    private void Rename()
    {
        if (SelectedTexture is not { } tex || string.IsNullOrWhiteSpace(RenameText)) return;
        var renamed = _ws.RenameResource(tex, RenameText.Trim());
        if (renamed is not null) SelectedTexture = renamed;
    }
}
