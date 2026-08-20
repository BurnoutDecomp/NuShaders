using BCnEncoder.Decoder;
using BCnEncoder.Shared;
using NuShaders.Formats.BPR;

namespace NuShaders.Imaging;

/// <summary>A decoded preview surface: <paramref name="Rgba"/> is Width*Height*4 bytes, row-major, R,G,B,A.</summary>
public sealed record DecodedThumbnail(int Width, int Height, byte[] Rgba);

/// <summary>
/// Decodes a BPR Texture (primary header + raw BC mip chain) to RGBA for preview thumbnails. Walks to a small mip
/// near <c>maxSize</c> so even 1k/2k textures decode cheaply. Returns null for empty/unsupported formats (caller shows
/// a placeholder). Mirrors <see cref="PBRTexturePacker"/>'s BC format mapping.
/// </summary>
public static class TextureThumbnail
{
    public static DecodedThumbnail? Decode(ReadOnlySpan<byte> primary, ReadOnlySpan<byte> secondary, int maxSize = 128)
    {
        if (primary.Length < BPRTextureResource.PrimarySize) return null;
        var hdr = BPRTextureResource.ReadPrimary(primary);
        if (hdr.Width == 0 || hdr.Height == 0) return null;
        var fmt = ToDecodeFormat(hdr.Format);
        if (fmt is null) return null;

        // Walk the mip chain to a level near maxSize (keep dims >= 4 so BC blocks decode cleanly).
        int mipCount = Math.Max(1, (int)hdr.MipLevels);
        int w = hdr.Width, h = hdr.Height;
        long offset = 0;
        for (int mip = 0; mip < mipCount - 1 && Math.Max(w, h) > maxSize && Math.Min(w, h) > 4; mip++)
        {
            offset += hdr.Format.MipChainBytes(w, h, 1);
            w = Math.Max(1, w / 2);
            h = Math.Max(1, h / 2);
        }

        long surf = hdr.Format.MipChainBytes(w, h, 1);
        if (offset < 0 || offset + surf > secondary.Length) return null;

        ColorRgba32[] pixels;
        try { pixels = new BcDecoder().DecodeRaw(secondary.Slice((int)offset, (int)surf).ToArray(), w, h, fmt.Value); }
        catch { return null; }

        var rgba = new byte[w * h * 4];
        int n = Math.Min(pixels.Length, w * h);
        for (int i = 0; i < n; i++)
        {
            rgba[i * 4 + 0] = pixels[i].r;
            rgba[i * 4 + 1] = pixels[i].g;
            rgba[i * 4 + 2] = pixels[i].b;
            rgba[i * 4 + 3] = pixels[i].a;
        }
        return new DecodedThumbnail(w, h, rgba);
    }

    private static CompressionFormat? ToDecodeFormat(DXGIFormat f) => f switch
    {
        DXGIFormat.BC1_UNORM => CompressionFormat.Bc1,
        DXGIFormat.BC3_UNORM => CompressionFormat.Bc3,
        DXGIFormat.BC4_UNORM => CompressionFormat.Bc4,
        DXGIFormat.BC5_UNORM => CompressionFormat.Bc5,
        DXGIFormat.BC7_UNORM => CompressionFormat.Bc7,
        _ => null,
    };
}
