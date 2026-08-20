using NuShaders.Formats.Bundle;
using NuShaders.Formats.IO;
using NuShaders.Formats.Model;

namespace NuShaders.Formats.BPR;

public enum TextureAddressMode : uint { Wrap = 1, Mirror = 2, Clamp = 3, Border = 4, MirrorOnce = 5 }

/// <summary>D3D11_FILTER_TYPE per stage; 2 makes the game use anisotropic filtering (per TextureState_Remastered.mediawiki).</summary>
public enum SamplerFilter : uint { Point = 0, Linear = 1, Anisotropic = 2 }

/// <summary>The renderengine::SamplerState fields. Defaults reproduce the stock default sampler (e.g. 90AE7652.dat).</summary>
public sealed record SamplerParams(
    TextureAddressMode AddressU = TextureAddressMode.Wrap,
    TextureAddressMode AddressV = TextureAddressMode.Wrap,
    TextureAddressMode AddressW = TextureAddressMode.Wrap,
    SamplerFilter MagFilter = SamplerFilter.Linear,
    SamplerFilter MinFilter = SamplerFilter.Linear,
    SamplerFilter MipFilter = SamplerFilter.Linear,
    float MinLod = float.MinValue,
    float MaxLod = float.MaxValue,
    uint MaxAnisotropy = 1,
    float MipLodBias = 0f,
    uint Comparison = 0xFFFFFFFF,   // -1 sentinel → D3D11_COMPARISON_ALWAYS
    bool WhiteBorder = false);

/// <summary>
/// BPR (Remastered) TextureState resource, type 0x0e, 32-bit layout, 64-byte file. A renderengine::SamplerState
/// (0x00..0x38) followed by an imported Texture reference at 0x38. The sampler bytes are independent of the
/// resource id (the id comes from the name's fixed default suffix — see <see cref="BurnoutResourceName"/>), so
/// any sampler may be written here freely. Runtime pointer fields (0x34 sampler-iface, 0x38 texture ref) are 0
/// on disk; the texture ref is supplied via <c>{ID}_imports.yaml</c>. Verified against
/// Reference/BPR/SHADERS/TextureState/90AE7652.dat (default sampler) and the 13 distinct track sampler configs.
/// </summary>
public sealed class BPRTextureState
{
    public const int FileSize = 0x40;
    public const int TextureRefOffset = 0x38;

    // .meta.yaml descriptor for a TextureState resource.
    public const int MetaType = 0xe;
    public static int[] MetaAlignment => [0x10];

    public static readonly SamplerParams Default = new();

    public static byte[] Build(SamplerParams p)
    {
        var w = new SpanWriter(FileSize);
        w.U32LE((uint)p.AddressU);                  // 0x00 Address U
        w.U32LE((uint)p.AddressV);                  // 0x04 Address V
        w.U32LE((uint)p.AddressW);                  // 0x08 Address W
        w.U32LE((uint)p.MagFilter);                 // 0x0C Magnification filter
        w.U32LE((uint)p.MinFilter);                 // 0x10 Minification filter
        w.U32LE((uint)p.MipFilter);                 // 0x14 Mip filter
        w.F32LE(p.MinLod);                          // 0x18 Minimum LOD
        w.F32LE(p.MaxLod);                          // 0x1C Maximum LOD
        w.U32LE(p.MaxAnisotropy);                   // 0x20 Maximum anisotropy
        w.F32LE(p.MipLodBias);                      // 0x24 Mip LOD bias
        w.U32LE(p.Comparison);                      // 0x28 Comparison function
        w.U8(p.WhiteBorder ? (byte)1 : (byte)0);    // 0x2C White border color
        w.Zeros(3);                                 // 0x2D padding
        w.U32LE(0);                                 // 0x30 Reference count (runtime)
        w.U32LE(0);                                 // 0x34 Sampler state interface* (runtime; stock garbage → zero)
        w.U32LE(0);                                 // 0x38 Texture ref (engine-patched via imports.yaml)
        w.U32LE(0);                                 // 0x3C padding
        return w.ToArray();
    }

    public static SamplerParams ReadSampler(ReadOnlySpan<byte> data)
    {
        var r = new SpanReader(data);
        return new SamplerParams(
            (TextureAddressMode)r.U32LE(0x00), (TextureAddressMode)r.U32LE(0x04), (TextureAddressMode)r.U32LE(0x08),
            (SamplerFilter)r.U32LE(0x0C), (SamplerFilter)r.U32LE(0x10), (SamplerFilter)r.U32LE(0x14),
            r.F32LE(0x18), r.F32LE(0x1C), r.U32LE(0x20), r.F32LE(0x24), r.U32LE(0x28), r.U8(0x2C) != 0);
    }

    /// <summary>The single import that points the texture ref at 0x38 to the Texture resource.</summary>
    public static ImportsYAML BuildImports(ResourceID textureID)
    {
        var y = new ImportsYAML();
        y.Entries.Add(((uint)TextureRefOffset, textureID.Value));
        return y;
    }
}
