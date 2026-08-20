namespace NuShaders.Formats.BPR;

/// <summary>
/// The subset of DXGI_FORMAT used by BPR textures (the value stored at primary+0x1C). Values match
/// the Windows DXGI_FORMAT enum so they can be passed straight to D3D11 at load.
/// </summary>
public enum DXGIFormat : uint
{
    Unknown = 0,
    R8G8B8A8_UNORM = 28,      // 0x1C — uncompressed RGBA
    BC1_UNORM = 71,           // 0x47 — DXT1 (RGB / 1-bit alpha)
    BC2_UNORM = 74,           // 0x4A — DXT3
    BC3_UNORM = 77,           // 0x4D — DXT5 (RGBA)
    BC4_UNORM = 80,           // 0x50 — single channel
    BC5_UNORM = 83,           // 0x53 — two channel (normal maps)
    BC7_UNORM = 98,           // 0x62 — high quality RGBA
}

public static class DXGIFormatExtensions
{
    /// <summary>Bytes per 4x4 block for a block-compressed format (BC1/BC4 = 8, BC2/BC3/BC5/BC7 = 16). 0 if not BCn.</summary>
    public static int BlockBytes(this DXGIFormat format) => format switch
    {
        DXGIFormat.BC1_UNORM or DXGIFormat.BC4_UNORM => 8,
        DXGIFormat.BC2_UNORM or DXGIFormat.BC3_UNORM or DXGIFormat.BC5_UNORM or DXGIFormat.BC7_UNORM => 16,
        _ => 0,
    };

    public static bool IsBlockCompressed(this DXGIFormat format) => format.BlockBytes() != 0;

    /// <summary>Byte size of a single mip surface (BCn = ceil(w/4)*ceil(h/4)*blockBytes; RGBA8 = w*h*4).</summary>
    public static long SurfaceBytes(this DXGIFormat format, int width, int height)
    {
        int w = Math.Max(1, width), h = Math.Max(1, height);
        int block = format.BlockBytes();
        if (block != 0)
            return (long)((w + 3) / 4) * ((h + 3) / 4) * block;
        return format == DXGIFormat.R8G8B8A8_UNORM ? (long)w * h * 4 : 0;
    }

    /// <summary>Total byte size of a full mip chain (mip 0 = width x height, halving each level, min 1).</summary>
    public static long MipChainBytes(this DXGIFormat format, int width, int height, int mipLevels)
    {
        long total = 0;
        for (int i = 0; i < mipLevels; i++)
            total += format.SurfaceBytes(width >> i, height >> i);
        return total;
    }
}
