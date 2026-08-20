using System.Windows.Media.Imaging;
using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;
using NuShaders.GUI.Common;

namespace NuShaders.GUI.ViewModels;

/// <summary>
/// One sampler row, presented texture-first: the bound Texture (thumbnail + name) plus its sampler settings
/// (wrap/filter/anisotropy). The TextureState is hidden — the editor resolves (texture + settings) to a
/// find-or-created TextureState on save (<see cref="TextureStateResolver"/>).
/// </summary>
public sealed class SamplerSlotViewModel : ViewModelBase
{
    private readonly BundleWorkspaceViewModel _ws;

    public short Channel { get; }
    public string ShaderSamplerName { get; }
    public int SlotOffset { get; }
    public uint? OriginalTextureStateID { get; }
    public TextureKind Kind { get; }
    public bool CanCreate => Kind != TextureKind.None;

    public IReadOnlyList<TextureAddressMode> AddressModes { get; } = Enum.GetValues<TextureAddressMode>();
    public IReadOnlyList<SamplerFilter> Filters { get; } = Enum.GetValues<SamplerFilter>();

    private ResourceID? _textureID;
    public ResourceID? TextureID
    {
        get => _textureID;
        private set
        {
            if (!SetProperty(ref _textureID, value)) return;
            OnPropertyChanged(nameof(TextureName));
            OnPropertyChanged(nameof(TextureFullName));
            OnPropertyChanged(nameof(HasTexture));
            LoadThumbnail();
        }
    }

    public bool HasTexture => _textureID is not null;
    public string TextureName => _textureID is { } id ? _ws.Combined?.ByID(id)?.ShortName ?? "0x" + id.ToHexLower() : "(none)";
    public string TextureFullName => _textureID is { } id ? _ws.Combined?.ByID(id)?.DisplayName ?? "0x" + id.ToHexLower() : "(none)";

    private BitmapSource? _thumbnail;
    public BitmapSource? Thumbnail { get => _thumbnail; private set => SetProperty(ref _thumbnail, value); }

    private SamplerParams _sampler;
    public TextureAddressMode AddressU { get => _sampler.AddressU; set { if (value != _sampler.AddressU) { _sampler = _sampler with { AddressU = value }; OnPropertyChanged(); } } }
    public TextureAddressMode AddressV { get => _sampler.AddressV; set { if (value != _sampler.AddressV) { _sampler = _sampler with { AddressV = value }; OnPropertyChanged(); } } }
    public TextureAddressMode AddressW { get => _sampler.AddressW; set { if (value != _sampler.AddressW) { _sampler = _sampler with { AddressW = value }; OnPropertyChanged(); } } }
    public SamplerFilter Filter
    {
        get => _sampler.MagFilter;
        set { if (value != _sampler.MagFilter) { _sampler = _sampler with { MagFilter = value, MinFilter = value, MipFilter = value }; OnPropertyChanged(); } }
    }
    public uint MaxAnisotropy { get => _sampler.MaxAnisotropy; set { if (value != _sampler.MaxAnisotropy) { _sampler = _sampler with { MaxAnisotropy = value }; OnPropertyChanged(); } } }

    /// <summary>Full sampler settings (edited fields + preserved LOD/comparison/border) for find-or-create on save.</summary>
    public SamplerParams CurrentSampler => _sampler;

    public SamplerSlotViewModel(BoundSampler s, BundleWorkspaceViewModel ws)
    {
        _ws = ws;
        Channel = s.Channel;
        ShaderSamplerName = s.ShaderSamplerName ?? "";
        SlotOffset = s.TextureStateSlotOffset;
        OriginalTextureStateID = s.AssignedTextureState?.Value;
        Kind = InferKind(s.Purpose, ShaderSamplerName);
        _sampler = s.Sampler ?? BPRTextureState.Default;
        _textureID = s.TextureID;
        LoadThumbnail();
    }

    public void SetTexture(CatalogResource texture) => TextureID = texture.Id;

    private async void LoadThumbnail()
    {
        Thumbnail = null;
        if (_textureID is not { } id || _ws.Combined?.ByID(id) is not { } tex) return;
        BitmapSource? bmp = null;
        try { bmp = await Task.Run(() => ThumbnailLoader.Load(tex, 64)); } catch { /* preview is best-effort */ }
        if (_textureID?.Value == id.Value) Thumbnail = bmp;   // ignore a stale load if the texture changed meanwhile
    }

    private static TextureKind InferKind(TexturePurpose purpose, string name)
    {
        if (purpose == TexturePurpose.EnvironmentMap) return TextureKind.None;   // cube — prebuilt, not packable here
        string n = name.ToLowerInvariant();
        if (n.Contains("spec") || n.Contains("orm") || n.Contains("rough") || n.Contains("metal") || n.Contains("ao"))
            return TextureKind.Spec;
        return TextureKind.Diffuse;
    }
}
