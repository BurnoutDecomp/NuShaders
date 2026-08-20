using NuShaders.Formats.IO;

namespace NuShaders.Formats.Xbox360;

/// <summary>
/// Header fields of an Xbox 360 (Xenos) shader blob from XDK fxc. Big-endian.
/// Magic 10 2A 11 xx; byte 3 is the Xenos shader type (0x00 = pixel, 0x01 = vertex).
/// </summary>
public readonly record struct XenosBinary(bool IsPixel, int MicroSize, int ConstSize, int ParamCount)
{
    public static XenosBinary Parse(ReadOnlySpan<byte> blob)
    {
        if (blob.Length < 56 || blob[0] != 0x10 || blob[1] != 0x2A || blob[2] != 0x11)
            throw new InvalidDataException("not an Xbox 360 Xenos shader blob (bad magic).");

        bool isPixel = blob[3] switch
        {
            0x00 => true,
            0x01 => false,
            _ => throw new InvalidDataException($"unknown Xenos shader type byte 0x{blob[3]:X2}."),
        };

        var r = new SpanReader(blob);
        return new XenosBinary(isPixel, (int)r.U32BE(0x04), (int)r.U32BE(0x08), (int)r.U32BE(0x34));
    }
}
