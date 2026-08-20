using NuShaders.Formats.IO;

namespace NuShaders.Formats.BPR;

/// <summary>
/// Wraps a BPR texture (primary header + raw BC secondary blob) into a standard <c>.dds</c> file: the <c>'DDS '</c>
/// magic + a 124-byte DDS_HEADER + a 20-byte DDS_HEADER_DXT10 (so the exact DXGI format — including BC7 — round-trips)
/// + the mip-chain bytes (the secondary's 0x100 padding trimmed off).
/// </summary>
public static class DDSWriter
{
    private const uint Magic = 0x20534444;        // "DDS "
    private const uint FourCcDx10 = 0x30315844;   // "DX10"

    public static byte[] Build(BPRTextureResource header, ReadOnlySpan<byte> secondary)
    {
        int width = Math.Max(1, (int)header.Width);
        int height = Math.Max(1, (int)header.Height);
        int mips = Math.Max(1, (int)header.MipLevels);
        int dataLen = (int)header.Format.MipChainBytes(width, height, mips);
        int copy = Math.Min(dataLen, secondary.Length);

        const uint flags = 0x1 | 0x2 | 0x4 | 0x1000 | 0x20000 | 0x80000;   // CAPS|HEIGHT|WIDTH|PIXELFORMAT|MIPMAPCOUNT|LINEARSIZE
        uint caps = 0x1000;                                                // DDSCAPS_TEXTURE
        if (mips > 1) caps |= 0x400000u | 0x8u;                            // MIPMAP | COMPLEX

        var w = new SpanWriter(148 + copy);
        w.U32LE(Magic);

        // DDS_HEADER (124 bytes)
        w.U32LE(124);                                                 // dwSize
        w.U32LE(flags);                                               // dwFlags
        w.U32LE((uint)height);                                        // dwHeight
        w.U32LE((uint)width);                                         // dwWidth
        w.U32LE((uint)header.Format.SurfaceBytes(width, height));     // dwPitchOrLinearSize (mip 0 surface)
        w.U32LE(0);                                                   // dwDepth
        w.U32LE((uint)mips);                                          // dwMipMapCount
        w.Zeros(44);                                                  // dwReserved1[11]
        // DDS_PIXELFORMAT (32 bytes)
        w.U32LE(32);                                                  // dwSize
        w.U32LE(0x4);                                                 // dwFlags = DDPF_FOURCC
        w.U32LE(FourCcDx10);                                          // dwFourCC = "DX10"
        w.U32LE(0); w.U32LE(0); w.U32LE(0); w.U32LE(0); w.U32LE(0);   // dwRGBBitCount + RGBA masks
        w.U32LE(caps);                                                // dwCaps
        w.U32LE(0); w.U32LE(0); w.U32LE(0);                           // dwCaps2/3/4
        w.U32LE(0);                                                   // dwReserved2

        // DDS_HEADER_DXT10 (20 bytes)
        w.U32LE((uint)header.Format);                                 // dxgiFormat
        w.U32LE(3);                                                   // resourceDimension = TEXTURE2D
        w.U32LE(0);                                                   // miscFlag
        w.U32LE(1);                                                   // arraySize
        w.U32LE(0);                                                   // miscFlags2

        w.Bytes(secondary[..copy].ToArray());                         // the BC mip chain (padding trimmed)
        return w.ToArray();
    }
}
