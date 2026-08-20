using System.Buffers.Binary;
using System.Text;

namespace NuShaders.Formats.IO;

/// <summary>
/// Random-access reader over a byte buffer. The bundle formats are offset-based
/// (fixed-position header fields, tables addressed by absolute file offsets, and
/// pointers stored as file offsets), so every accessor takes an absolute offset.
/// Both little- and big-endian readers are provided (X360 is big-endian).
/// </summary>
public readonly ref struct SpanReader
{
    private readonly ReadOnlySpan<byte> _d;

    public SpanReader(ReadOnlySpan<byte> data) => _d = data;

    public int Length => _d.Length;

    public byte U8(int offset) => _d[offset];
    public sbyte I8(int offset) => (sbyte)_d[offset];

    public ushort U16LE(int offset) => BinaryPrimitives.ReadUInt16LittleEndian(_d[offset..]);
    public ushort U16BE(int offset) => BinaryPrimitives.ReadUInt16BigEndian(_d[offset..]);

    public uint U32LE(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_d[offset..]);
    public uint U32BE(int offset) => BinaryPrimitives.ReadUInt32BigEndian(_d[offset..]);

    public int I32LE(int offset) => BinaryPrimitives.ReadInt32LittleEndian(_d[offset..]);
    public int I32BE(int offset) => BinaryPrimitives.ReadInt32BigEndian(_d[offset..]);

    public float F32LE(int offset) => BinaryPrimitives.ReadSingleLittleEndian(_d[offset..]);

    public ReadOnlySpan<byte> Slice(int offset, int length) => _d.Slice(offset, length);

    /// <summary>Read a NUL-terminated ASCII string starting at <paramref name="offset"/>.</summary>
    public string CString(int offset)
    {
        int end = offset;
        while (end < _d.Length && _d[end] != 0) end++;
        return Encoding.ASCII.GetString(_d[offset..end]);
    }
}
