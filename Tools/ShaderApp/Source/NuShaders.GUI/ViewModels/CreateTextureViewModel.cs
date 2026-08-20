using System.IO;
using System.Windows.Input;
using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.GUI.Common;
using NuShaders.Imaging;

namespace NuShaders.GUI.ViewModels;

/// <summary>The kind of texture a material slot takes (drives which map pickers the create-dialog shows).</summary>
public enum TextureKind { None, Diffuse, Spec }

/// <summary>
/// Authors a single texture for one material slot: pack the relevant maps into one BC surface, build the Texture +
/// TextureState, and (per the immediate write model) merge them straight into the opened bundle. On success exposes
/// the new TextureState as a <see cref="CatalogResource"/> so the editor can register it and assign it to the slot.
/// The Spec kind has two packing modes: ORM (R:AO G:rough B:metal A:height, for PBR shaders) and grayscale Specular
/// (R=G=B = a single spec mask, for the stock BPR Specular_*.fx shaders).
/// </summary>
public sealed class CreateTextureViewModel : ViewModelBase
{
    private readonly BundleWorkspaceViewModel _ws;

    public TextureKind Kind { get; }
    public bool IsDiffuse => Kind == TextureKind.Diffuse;
    public bool IsSpec => Kind == TextureKind.Spec;

    // Spec sub-mode toggle: false = ORM, true = grayscale Specular. Two mutually-exclusive bools for the radio buttons.
    private bool _specularMode;
    public bool ORMChecked { get => !_specularMode; set { if (value) SetSpecularMode(false); } }
    public bool SpecularChecked { get => _specularMode; set { if (value) SetSpecularMode(true); } }
    public bool IsSpecORM => IsSpec && !_specularMode;
    public bool IsSpecSpecular => IsSpec && _specularMode;

    public string Title => IsDiffuse ? "Create Diffuse texture"
        : _specularMode ? "Create Specular texture (grayscale)"
        : "Create ORM texture (AO, Roughness, Metallic, Displacement)";

    public MapSlot Albedo { get; } = new("Albedo (RGB)", required: true);
    public MapSlot Opacity { get; } = new("Opacity (A, optional)");
    public MapSlot AO { get; } = new("Ambient Occlusion (R)");
    public MapSlot Roughness { get; } = new("Roughness (G)");
    public MapSlot Metallic { get; } = new("Metallic (B)");
    public MapSlot Displacement { get; } = new("Displacement / Height (A)");
    public MapSlot Specular { get; } = new("Specular map (grayscale)", required: true);

    public IReadOnlyList<DXGIFormat> Formats
    {
        get
        {
            if (IsDiffuse) return [DXGIFormat.BC1_UNORM, DXGIFormat.BC3_UNORM];
            return _specularMode ? [DXGIFormat.BC1_UNORM, DXGIFormat.BC3_UNORM] : [DXGIFormat.BC3_UNORM];
        }
    }

    private DXGIFormat _format;
    public DXGIFormat Format { get => _format; set => SetProperty(ref _format, value); }

    private bool _generateMips = true;
    public bool GenerateMips { get => _generateMips; set => SetProperty(ref _generateMips, value); }

    /// <summary>Which loaded bundle to write the new texture into (the open bundle, or a reference bundle like WORLDTEX).</summary>
    public IReadOnlyList<BundleTarget> Bundles { get; }
    private BundleTarget? _targetBundle;
    public BundleTarget? TargetBundle { get => _targetBundle; set => SetProperty(ref _targetBundle, value); }

    private string _textureName;
    private bool _nameLocked;
    public string TextureName { get => _textureName; set { if (SetProperty(ref _textureName, value)) _nameLocked = true; } }

    /// <summary>Default the texture name to the first browsed map file's stem (until the user types their own name).</summary>
    public void SuggestNameFromFile(string path)
    {
        if (_nameLocked) return;
        string stem = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrWhiteSpace(stem)) return;
        SetProperty(ref _textureName, stem, nameof(TextureName));
        _nameLocked = true;   // only the first browse seeds it; later browses leave it alone
    }

    private string _log = "";
    public string Log { get => _log; private set => SetProperty(ref _log, value); }

    private bool _isBusy;
    public bool IsBusy { get => _isBusy; private set { if (SetProperty(ref _isBusy, value)) CommandManager.InvalidateRequerySuggested(); } }

    public CatalogResource? ResultTexture { get; private set; }
    public RelayCommand CreateCommand { get; }
    public event Action<bool>? CloseRequested;

    public CreateTextureViewModel(BundleWorkspaceViewModel ws, TextureKind kind, string textureName)
    {
        _ws = ws;
        Kind = kind;
        _textureName = textureName;
        _format = Formats[0];
        Bundles = ws.LoadedBundles;
        _targetBundle = Bundles.Count > 0 ? Bundles[0] : null;   // default to the open bundle
        CreateCommand = new RelayCommand(() => _ = CreateAsync(), () => !IsBusy && CanPack());
    }

    private void SetSpecularMode(bool on)
    {
        if (_specularMode == on) return;
        _specularMode = on;
        OnPropertyChanged(nameof(ORMChecked));
        OnPropertyChanged(nameof(SpecularChecked));
        OnPropertyChanged(nameof(IsSpecORM));
        OnPropertyChanged(nameof(IsSpecSpecular));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(Formats));
        Format = Formats[0];
        CommandManager.InvalidateRequerySuggested();
    }

    private bool CanPack() => IsDiffuse ? Albedo.Path is not null
        : _specularMode ? Specular.Path is not null
        : AO.Path is not null || Roughness.Path is not null || Metallic.Path is not null || Displacement.Path is not null;

    private async Task CreateAsync()
    {
        IsBusy = true; Log = "";
        try
        {
            bool isDiffuse = IsDiffuse, specular = _specularMode;
            string mapSuffix = isDiffuse ? "Diffuse" : specular ? "Specular" : "Spec";
            string name = string.IsNullOrWhiteSpace(TextureName) ? "Material" : TextureName.Trim();
            string bundle = TargetBundle?.Folder ?? _ws.BundleFolder;
            var fmt = Format; bool mips = GenerateMips;
            string? albedo = Albedo.Path, opacity = Opacity.Path, spec = Specular.Path;
            string? ao = AO.Path, rough = Roughness.Path, metal = Metallic.Path, disp = Displacement.Path;

            var (texID, texName, primaryPath) = await Task.Run(() =>
            {
                EncodedSurface surf = isDiffuse ? PBRTexturePacker.PackDiffuse(albedo!, opacity, fmt, mips)
                    : specular ? PBRTexturePacker.PackSpecular(spec!, fmt, mips)
                    : PBRTexturePacker.PackSpec(ao, rough, metal, disp, fmt, mips);
                var resources = BPRTextureSet.BuildSingle(name, mapSuffix, surf);
                TextureSetWriter.MergeIntoBundle(bundle, resources);   // merges both the Texture and its default-sampler TextureState
                var tex = resources.First(r => r.MetaType == BPRTextureResource.MetaType);
                return (tex.Id, tex.Name, Path.Combine(bundle, "Texture", tex.Id.ToHexUpper() + "_primary.dat"));
            });

            // The slot binds the TEXTURE; the editor find-or-creates a TextureState for it (+ the slot's sampler settings) on save.
            ResultTexture = new CatalogResource(texID, BPRTextureResource.MetaType, texName,
                File.Exists(primaryPath) ? primaryPath : null, null);
            Log += $"Created texture {texID} → {TargetBundle?.Name ?? "bundle"}\n  {texName}";
            CloseRequested?.Invoke(true);
        }
        catch (Exception ex) { Log += "ERROR: " + ex.Message; }
        finally { IsBusy = false; }
    }
}
