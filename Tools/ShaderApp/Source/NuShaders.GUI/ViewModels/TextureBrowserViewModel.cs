using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.GUI.Common;

namespace NuShaders.GUI.ViewModels;

/// <summary>One texture row: name + dimensions/format + a thumbnail that decodes lazily on first bind (so a virtualized
/// list only decodes the visible rows — scales to thousands of textures from worldtex/dictionary bundles).</summary>
public sealed class TextureBrowserItem : ViewModelBase
{
    public CatalogResource Resource { get; }
    public string ShortName => Resource.ShortName;
    public string DisplayName => Resource.DisplayName;

    private bool _started;
    private BitmapSource? _thumbnail;
    public BitmapSource? Thumbnail
    {
        get { Start(); return _thumbnail; }   // first read (row realized) kicks off the decode
        private set => SetProperty(ref _thumbnail, value);
    }

    private string _info = "";
    public string Info { get => _info; private set => SetProperty(ref _info, value); }

    public TextureBrowserItem(CatalogResource resource) => Resource = resource;

    private void Start()
    {
        if (_started) return;
        _started = true;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var (bmp, info) = await Task.Run(() =>
            {
                var b = ThumbnailLoader.Load(Resource, 96);
                string inf = "";
                if (Resource.DatPath is not null && File.Exists(Resource.DatPath))
                {
                    var hdr = BPRTextureResource.ReadPrimary(File.ReadAllBytes(Resource.DatPath));
                    inf = $"{hdr.Width}×{hdr.Height}  {hdr.Format}";
                }
                return (b, inf);
            });
            Thumbnail = bmp;
            Info = info;
        }
        catch { /* preview is best-effort */ }
    }
}

/// <summary>A searchable, virtualized list of the bundle's textures; returns the picked one.</summary>
public sealed class TextureBrowserViewModel : ViewModelBase
{
    public ObservableCollection<TextureBrowserItem> Items { get; } = [];
    public ICollectionView ItemsView { get; }

    private string _filterText = "";
    public string FilterText { get => _filterText; set { if (SetProperty(ref _filterText, value)) ItemsView.Refresh(); } }

    private TextureBrowserItem? _selectedItem;
    public TextureBrowserItem? SelectedItem
    {
        get => _selectedItem;
        set { if (SetProperty(ref _selectedItem, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    public CatalogResource? Result => _selectedItem?.Resource;
    public string CountLabel => $"{Items.Count} textures";

    public RelayCommand OkCommand { get; }
    public RelayCommand CancelCommand { get; }
    public event Action<bool>? CloseRequested;

    public TextureBrowserViewModel(IEnumerable<CatalogResource> textures)
    {
        foreach (var t in textures) Items.Add(new TextureBrowserItem(t));
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = o => o is TextureBrowserItem it &&
            (_filterText.Length == 0 || it.DisplayName.Contains(_filterText, StringComparison.OrdinalIgnoreCase));
        OkCommand = new RelayCommand(() => CloseRequested?.Invoke(true), () => _selectedItem is not null);
        CancelCommand = new RelayCommand(() => CloseRequested?.Invoke(false));
    }
}
