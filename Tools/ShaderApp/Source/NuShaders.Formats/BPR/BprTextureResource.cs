using NuShaders.Formats.IO;

namespace NuShaders.Formats.BPR;

/// <summary>
/// BPR (Remastered) Texture resource, type 0x00, 32-bit layout (the game is x86), little-endian.
/// The primary is a fixed 64-byte header; the secondary is the raw block-compressed pixel data with
/// NO DDS header (all mip surfaces concatenated, most-detailed first). Runtime pointer/interface
/// fields are 0 on disk; the placed-texture tail (0x30..0x3F) is PC-unused (stock files hold
/// per-file garbage there) so it is written as zeros — round-trip tests mask that range.
/// Verified against Reference/BPR/SHADERS/Texture/11B669ED (64x64 BC1, 1 mip → 2048-byte secondary).
/// </summary>
public sealed class BPRTextureResource
{
    public const int PrimarySize = 0x40;
    public const int Dimension2D = 7;   // see Texture_Remastered.mediawiki "Dimension" (6=1D,7=2D,8=3D,9=Cube)

    /// <summary>The secondary blob is the contiguous mip chain zero-padded to this size (verified against stock: 828AA32F = 10936 raw → 11008).</summary>
    public const int SecondaryAlignment = 0x100;

    // .meta.yaml descriptor for a Texture resource.
    public const int MetaType = 0x0;
    public const int MetaSecondaryMemoryType = 1;
    public static int[] MetaAlignment => [0x4, 0x10];

    public DXGIFormat Format;
    public ushort Width;
    public ushort Height;
    public ushort Depth = 1;
    public ushort ArraySize = 1;
    public byte MostDetailedMip;
    public byte MipLevels = 1;
    public int Dimension = Dimension2D;
    public uint Usage;

    /// <summary>Build the (primary header, secondary blob) pair for a 2D texture. The secondary is the
    /// contiguous BC mip chain zero-padded to <see cref="SecondaryAlignment"/> (stock convention).</summary>
    public static (byte[] Primary, byte[] Secondary) Build(DXGIFormat format, int width, int height, int mipLevels, byte[] bcBlocks)
    {
        var primary = WriteHeader(format, width, height, mipLevels);
        byte[] secondary = new byte[Alignment.Align(bcBlocks.Length, SecondaryAlignment)];
        bcBlocks.CopyTo(secondary, 0);
        return (primary, secondary);
    }

    private static byte[] WriteHeader(DXGIFormat format, int width, int height, int mipLevels)
    {
        var w = new SpanWriter(PrimarySize);
        w.U32LE(0);                         // 0x00 Texture interface* (runtime)
        w.U32LE(0);                         // 0x04 D3D11_USAGE (DEFAULT)
        w.U32LE(Dimension2D);               // 0x08 Dimension (2D)
        w.U32LE(0);                         // 0x0C Pixel data ptr (runtime; file offset filled by loader)
        w.U32LE(0);                         // 0x10 ID3D11ShaderResourceView* (runtime)
        w.U32LE(0);                         // 0x14 ID3D11ShaderResourceView* (runtime)
        w.U32LE(0);                         // 0x18 ?
        w.U32LE((uint)format);              // 0x1C DXGI_FORMAT
        w.U32LE(0);                         // 0x20 Flags
        w.U16LE((ushort)width);             // 0x24 Width
        w.U16LE((ushort)height);            // 0x26 Height
        w.U16LE(1);                         // 0x28 Depth
        w.U16LE(1);                         // 0x2A ArraySize
        w.U8(0);                            // 0x2C MostDetailedMip
        w.U8((byte)mipLevels);              // 0x2D MipLevels
        w.U16LE(0);                         // 0x2E pad
        w.Zeros(0x10);                      // 0x30..0x3F placed-texture fields (PC-unused → zero)
        return w.ToArray();
    }

    public byte[] ToBytes() => WriteHeader(Format, Width, Height, MipLevels);

    public static BPRTextureResource ReadPrimary(ReadOnlySpan<byte> primary)
    {
        var r = new SpanReader(primary);
        return new BPRTextureResource
        {
            Usage = r.U32LE(0x04),
            Dimension = (int)r.U32LE(0x08),
            Format = (DXGIFormat)r.U32LE(0x1C),
            Width = r.U16LE(0x24),
            Height = r.U16LE(0x26),
            Depth = r.U16LE(0x28),
            ArraySize = r.U16LE(0x2A),
            MostDetailedMip = r.U8(0x2C),
            MipLevels = r.U8(0x2D),
        };
    }

    /// <summary>Expected secondary byte length for this header's format/size/mips (for validation).</summary>
    public long ExpectedSecondaryBytes() => Format.MipChainBytes(Width, Height, Math.Max(1, (int)MipLevels));
}
