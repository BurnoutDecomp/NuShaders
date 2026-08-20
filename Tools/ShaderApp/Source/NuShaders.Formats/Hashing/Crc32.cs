using System.Text;

namespace NuShaders.Formats.Hashing;

/// <summary>
/// CRC-32 (reflected, polynomial 0xEDB88320) used by the Burnout bundle formats.
/// </summary>
public static class CRC32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < 256; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            table[i] = c;
        }
        return table;
    }

    /// <summary>Standard CRC-32 (init 0xFFFFFFFF, reflected, final complement).</summary>
    public static uint Compute(ReadOnlySpan<byte> data)
    {
        uint crc = 0xFFFFFFFFu;
        foreach (byte b in data)
            crc = (crc >> 8) ^ Table[(crc ^ b) & 0xFF];
        return crc ^ 0xFFFFFFFFu;
    }

    /// <summary>
    /// JAMCRC: CRC-32 without the final complement (= bitwise-NOT of <see cref="Compute(ReadOnlySpan{byte})"/>).
    /// This is the Burnout name hash used for shader-constant name hashes on both BPR and X360.
    /// Verified: materialDiffuse=0xF6E27CAA, world=0xC588EEBC, SpecularPower=0x81E0E773, Specularity=0x4A73909F.
    /// </summary>
    public static uint JamCRC(ReadOnlySpan<byte> data) => ~Compute(data);

    /// <summary>ASCII convenience overload of <see cref="Compute(ReadOnlySpan{byte})"/>.</summary>
    public static uint Compute(string asciiText) => Compute(Encoding.ASCII.GetBytes(asciiText));

    /// <summary>ASCII convenience overload of <see cref="JamCRC(ReadOnlySpan{byte})"/>.</summary>
    public static uint JamCRC(string asciiText) => JamCRC(Encoding.ASCII.GetBytes(asciiText));
}
