using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using NuShaders.Formats.BPR;
using NuShaders.GUI.Common;
using NuShaders.Imaging;

namespace NuShaders.GUI.ViewModels;

/// <summary>Drives the "Pack PBR Texture" panel: gather the maps + options, run the packer off the UI thread, log the result.</summary>
public sealed class TexturePackerViewModel : ViewModelBase
{
    public MapSlot Albedo { get; } = new("Albedo (RGB)", required: true);
    public MapSlot AO { get; } = new("Ambient Occlusion");
    public MapSlot Roughness { get; } = new("Roughness");
    public MapSlot Metallic { get; } = new("Metallic");
    public MapSlot Displacement { get; } = new("Displacement / Height");
    public MapSlot Opacity { get; } = new("Opacity (optional)");
    public ObservableCollection<MapSlot> Maps { get; }

    public TexturePackerViewModel()
    {
        Maps = [Albedo, AO, Roughness, Metallic, Displacement, Opacity];
        PackCommand = new RelayCommand(() => _ = PackAsync(), CanPack);
    }

    public IReadOnlyList<DXGIFormat> DiffuseFormats { get; } = [DXGIFormat.BC1_UNORM, DXGIFormat.BC3_UNORM];
    public IReadOnlyList<DXGIFormat> SpecFormats { get; } = [DXGIFormat.BC3_UNORM, DXGIFormat.BC7_UNORM];

    private DXGIFormat _diffuseFormat = DXGIFormat.BC1_UNORM;
    public DXGIFormat DiffuseFormat { get => _diffuseFormat; set => SetProperty(ref _diffuseFormat, value); }

    private DXGIFormat _specFormat = DXGIFormat.BC3_UNORM;
    public DXGIFormat SpecFormat { get => _specFormat; set => SetProperty(ref _specFormat, value); }

    private bool _generateMips = true;
    public bool GenerateMips { get => _generateMips; set => SetProperty(ref _generateMips, value); }

    private string _materialName = "";
    public string MaterialName { get => _materialName; set => SetProperty(ref _materialName, value); }

    private string _outputFolder = "";
    public string OutputFolder { get => _outputFolder; set => SetProperty(ref _outputFolder, value); }

    private bool _mergeIntoBundle;
    public bool MergeIntoBundle { get => _mergeIntoBundle; set => SetProperty(ref _mergeIntoBundle, value); }

    private string _bundleFolder = "";
    public string BundleFolder { get => _bundleFolder; set => SetProperty(ref _bundleFolder, value); }

    // ---- advanced sampler (written to the TextureState binary; does not affect the resource id) ----
    public IReadOnlyList<TextureAddressMode> AddressModes { get; } =
        [TextureAddressMode.Wrap, TextureAddressMode.Clamp, TextureAddressMode.Mirror];

    private TextureAddressMode _addressMode = TextureAddressMode.Wrap;
    public TextureAddressMode AddressMode { get => _addressMode; set => SetProperty(ref _addressMode, value); }

    private bool _anisotropic;
    public bool Anisotropic { get => _anisotropic; set => SetProperty(ref _anisotropic, value); }

    public IReadOnlyList<int> AnisotropyLevels { get; } = [2, 4, 8, 16];

    private int _maxAnisotropy = 8;
    public int MaxAnisotropy { get => _maxAnisotropy; set => SetProperty(ref _maxAnisotropy, value); }

    private double _lodBias;
    public double LodBias { get => _lodBias; set => SetProperty(ref _lodBias, value); }

    // ---- state ----
    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set { if (SetProperty(ref _isBusy, value)) CommandManager.InvalidateRequerySuggested(); }
    }

    private string _log = "";
    public string Log { get => _log; private set => SetProperty(ref _log, value); }

    public RelayCommand PackCommand { get; }

    private bool CanPack() =>
        !IsBusy && Albedo.Path is not null && !string.IsNullOrWhiteSpace(MaterialName) && !string.IsNullOrWhiteSpace(OutputFolder);

    private void AppendLog(string line) => Log += line + Environment.NewLine;

    private SamplerParams BuildSampler()
    {
        var filter = Anisotropic ? SamplerFilter.Anisotropic : SamplerFilter.Linear;
        uint aniso = Anisotropic ? (uint)MaxAnisotropy : 1u;
        return new SamplerParams(
            AddressU: AddressMode, AddressV: AddressMode, AddressW: TextureAddressMode.Wrap,
            MagFilter: filter, MinFilter: filter, MipFilter: SamplerFilter.Linear,
            MaxAnisotropy: aniso, MipLodBias: (float)LodBias);
    }

    private async Task PackAsync()
    {
        IsBusy = true;
        Log = "";
        try
        {
            var maps = new SourceMaps(Albedo.Path, AO.Path, Roughness.Path, Metallic.Path, Displacement.Path, Opacity.Path);
            var options = new PackOptions(DiffuseFormat, SpecFormat, GenerateMips);
            var sampler = BuildSampler();
            string name = MaterialName.Trim();
            string outDir = OutputFolder;
            bool merge = MergeIntoBundle && !string.IsNullOrWhiteSpace(BundleFolder);
            string bundle = BundleFolder;

            AppendLog($"Packing '{name}' ({DiffuseFormat} / {SpecFormat}, mips={GenerateMips})…");
            var resources = await Task.Run(() =>
            {
                var (diffuse, spec) = PBRTexturePacker.Pack(maps, options);
                var res = BPRTextureSet.Build(new TextureSetInputs(name, diffuse, spec, sampler));
                Directory.CreateDirectory(outDir);
                TextureSetWriter.WriteStandalone(outDir, res, force: true);
                if (merge) TextureSetWriter.MergeIntoBundle(bundle, res);
                return res;
            });

            AppendLog($"Wrote {resources.Count} resources -> {outDir}");
            foreach (var r in resources)
            {
                string kind = r.MetaType == BPRTextureResource.MetaType ? "Texture     " : "TextureState";
                AppendLog($"  {r.Id} {kind} {r.Name}");
            }
            if (merge) AppendLog($"Merged into bundle: {bundle}");
            AppendLog("Done.");
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
