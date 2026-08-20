using System.Text;
using System.Text.RegularExpressions;
using NuShaders.Formats.IO;

namespace NuShaders.Formats.Xbox360;

/// <summary>
/// Xbox 360 ShaderProgramBuffer (type 0x12, BIG-endian). The engine prefixes the Xenos blob with a
/// small big-endian header and splits it into a primary (header + microcode) and a 128-aligned
/// secondary (the constant/physical tail). PackSplit mirrors Build/pack_xbox360_shader.ps1 (the
/// byte-validated round-trip path); PackGenerate mirrors Build/pack_xbox360_shader_v2.ps1.
/// </summary>
public sealed class Xbox360ShaderProgramBuffer
{
    public const int PSHeaderSize = 0x3C;   // pixel
    public const int VSHeaderSize = 0x37C;  // vertex
    public const int SecondaryAlignment = 0x80;
    public const int PrimaryAlignment = 0x10;

    /// <summary>Engine header size from the primary's big-endian engineType (byte 3: 0x01 = PS, 0x00 = VS).</summary>
    public static int HeaderSizeFromPrimary(ReadOnlySpan<byte> primary) => primary[3] switch
    {
        0x01 => PSHeaderSize,
        0x00 => VSHeaderSize,
        _ => throw new InvalidDataException($"unknown engine type byte 0x{primary[3]:X2}."),
    };

    /// <summary>Split a Xenos blob into (primary, secondary). The blob already contains its descriptor table.</summary>
    public static (byte[] Primary, byte[] Secondary) PackSplit(ReadOnlySpan<byte> xenos)
    {
        var x = XenosBinary.Parse(xenos);
        if (x.ParamCount > 0xFFFF) throw new InvalidDataException($"parameter count {x.ParamCount} exceeds 16-bit range.");

        int engineType = x.IsPixel ? 1 : 0;
        int headerSize = x.IsPixel ? PSHeaderSize : VSHeaderSize;
        int sf1 = x.IsPixel ? x.MicroSize + 40 : x.MicroSize + 40 + 832;
        int sf2 = x.ConstSize;

        int secondaryFileSize = Alignment.Align(x.ConstSize, SecondaryAlignment);
        int xenosInPrimary = xenos.Length - secondaryFileSize;
        if (xenosInPrimary <= 0)
            throw new InvalidDataException($"Xenos blob ({xenos.Length}) smaller than secondary allocation ({secondaryFileSize}).");

        var w = new SpanWriter(headerSize + xenosInPrimary + PrimaryAlignment);
        w.U32BE((uint)engineType);
        w.U16BE((ushort)x.ParamCount);
        w.U8(0x01);
        w.U8(0x00);
        w.U32BE((uint)sf1);
        w.U32BE((uint)sf2);
        w.Zeros(headerSize - 16);
        w.Bytes(xenos[..xenosInPrimary]);
        w.AlignTo(PrimaryAlignment);
        byte[] primary = w.ToArray();

        var secondary = new byte[Alignment.Align(secondaryFileSize, PrimaryAlignment)];
        xenos[xenosInPrimary..].CopyTo(secondary);

        return (primary, secondary);
    }

    public const int StructHeaderSize = 20;
    public const int PSD3DHeaderSize = 40;
    public const int VSD3DHeaderSize = 872;

    private sealed record Constant(string Name, int Reg, byte Type, int RegCount);

    /// <summary>
    /// Generate a ShaderProgramBuffer from a RAW XDK-fxc Xenos blob by building the descriptor table
    /// from the shader's constants (parsed from <paramref name="xsdText"/> = the XDK xsd reflection
    /// dump, plus a NUL-delimited name scan of the blob for optimised-away constants). Mirrors
    /// Build/pack_xbox360_shader_v2.ps1 and splits at microSize.
    /// </summary>
    public static (byte[] Primary, byte[] Secondary) PackGenerate(ReadOnlySpan<byte> xenos, string xsdText)
    {
        var x = XenosBinary.Parse(xenos);
        int engineType = x.IsPixel ? 1 : 0;
        int d3dHeaderSize = x.IsPixel ? PSD3DHeaderSize : VSD3DHeaderSize;
        int headerSize = StructHeaderSize + d3dHeaderSize;

        var constants = new List<Constant>();
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);

        // 1) xsd "defconst Name, dataType, class, [rows, cols], <reg>" — array elements accumulate RegCount.
        foreach (Match m in Regex.Matches(xsdText, @"defconst\s+([\w\[\]]+),\s*(\w+),\s*\w+,\s*\[(\d+),\s*(\d+)\],\s*([bcs])(\d+)(?:-(\d+))?"))
        {
            string baseName = Regex.Replace(m.Groups[1].Value, @"\[\d+\]$", "");
            string dataType = m.Groups[2].Value;
            int regStart = int.Parse(m.Groups[6].Value);
            int regEnd = m.Groups[7].Success ? int.Parse(m.Groups[7].Value) : regStart;
            byte type = dataType is "sampler2d" or "samplerCUBE" ? (byte)3 : dataType == "bool" ? (byte)0 : (byte)2;
            int regCount = regEnd - regStart + 1;
            if (seen.TryGetValue(baseName, out int idx))
                constants[idx] = constants[idx] with { RegCount = constants[idx].RegCount + regCount };
            else
            {
                seen[baseName] = constants.Count;
                constants.Add(new Constant(baseName, regStart, type, regCount));
            }
        }

        // 2) Scan for NUL-delimited identifiers the compiler kept but optimised out of active use.
        foreach (string token in Encoding.ASCII.GetString(xenos).Split('\0'))
        {
            if (token.Length < 3 || seen.ContainsKey(token)) continue;
            if (!(char.IsLetter(token[0]) || token[0] == '_')) continue;
            bool isIdent = true;
            foreach (char ch in token)
                if (!(char.IsLetterOrDigit(ch) || ch == '_')) { isIdent = false; break; }
            if (!isIdent) continue;
            if (Regex.IsMatch(token, "^(vs_|ps_|def|dcl|END|Xbox|fxc|CTAB)")) continue;
            seen[token] = constants.Count;
            constants.Add(new Constant(token, 0, 2, 1));
        }

        int descSize = constants.Count * 8;

        // Name strings (NUL-terminated, padded to 16); offsets are primary-file-relative.
        var nameBytes = new List<byte>();
        var localNameOffsets = new int[constants.Count];
        for (int i = 0; i < constants.Count; i++)
        {
            localNameOffsets[i] = nameBytes.Count;
            nameBytes.AddRange(Encoding.ASCII.GetBytes(constants[i].Name));
            nameBytes.Add(0);
        }
        while (nameBytes.Count % 16 != 0) nameBytes.Add(0);

        var desc = new SpanWriter(descSize);
        for (int i = 0; i < constants.Count; i++)
        {
            int nameOff = headerSize + x.MicroSize + descSize + localNameOffsets[i];
            desc.U32BE((uint)nameOff);
            desc.U8((byte)constants[i].Reg);
            desc.U8(constants[i].Type);
            desc.U8((byte)constants[i].RegCount);
            desc.U8(0);
        }

        int bindingsOffset = d3dHeaderSize + x.MicroSize;

        var w = new SpanWriter(headerSize + x.MicroSize + descSize + nameBytes.Count + PrimaryAlignment);
        w.U32BE((uint)engineType);
        w.U16BE((ushort)constants.Count);
        w.U8(0x01);
        w.U8(0x00);
        w.U32BE((uint)bindingsOffset);
        w.U32BE((uint)x.ConstSize);
        w.U32BE(0); // physical ptr, relocated at load
        w.Zeros(d3dHeaderSize);
        w.Bytes(xenos[..x.MicroSize]);
        w.Bytes(desc.ToArray());
        w.Bytes([.. nameBytes]);
        w.AlignTo(PrimaryAlignment);
        byte[] primary = w.ToArray();

        var secondary = new byte[Alignment.Align(x.ConstSize, PrimaryAlignment)];
        xenos.Slice(x.MicroSize, x.ConstSize).CopyTo(secondary);

        return (primary, secondary);
    }
}
