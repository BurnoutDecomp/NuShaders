using System.Buffers.Binary;

namespace NuShaders.Formats.IO;

/// <summary>
/// Growable binary writer with both append and random-access patch operations.
/// The packers lay out a structure, remember offsets, then back-fill pointers, so
/// <see cref="PatchU32LE"/> / <see cref="PatchU32BE"/> write into already-appended bytes.
/// </summary>
public sealed class SpanWriter
{
    private byte[] _buf;
    private int _len;

    public SpanWriter(int capacity = 256) => _buf = new byte[Math.Max(capacity, 16)];

    public int Length => _len;

    private void Ensure(int extra)
    {
        if (_len + extra <= _buf.Length) return;
        Array.Resize(ref _buf, Math.Max(_buf.Length * 2, _len + extra));
    }

    public void U8(byte value) { Ensure(1); _buf[_len++] = value; }

    public void U16LE(ushort value) { Ensure(2); BinaryPrimitives.WriteUInt16LittleEndian(_buf.AsSpan(_len), value); _len += 2; }
    public void U16BE(ushort value) { Ensure(2); BinaryPrimitives.WriteUInt16BigEndian(_buf.AsSpan(_len), value); _len += 2; }

    public void U32LE(uint value) { Ensure(4); BinaryPrimitives.WriteUInt32LittleEndian(_buf.AsSpan(_len), value); _len += 4; }
    public void U32BE(uint value) { Ensure(4); BinaryPrimitives.WriteUInt32BigEndian(_buf.AsSpan(_len), value); _len += 4; }

    public void F32LE(float value) { Ensure(4); BinaryPrimitives.WriteSingleLittleEndian(_buf.AsSpan(_len), value); _len += 4; }

    public void Bytes(ReadOnlySpan<byte> bytes) { Ensure(bytes.Length); bytes.CopyTo(_buf.AsSpan(_len)); _len += bytes.Length; }

    public void Zeros(int count)
    {
        if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
        Ensure(count);
        Array.Clear(_buf, _len, count);
        _len += count;
    }

    /// <summary>Pad with <paramref name="pad"/> bytes until the length is a multiple of <paramref name="alignment"/>.</summary>
    public void AlignTo(int alignment, byte pad = 0)
    {
        int target = Alignment.Align(_len, alignment);
        while (_len < target) U8(pad);
    }

    public void PatchU32LE(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(_buf.AsSpan(offset), value);
    public void PatchU32BE(int offset, uint value) => BinaryPrimitives.WriteUInt32BigEndian(_buf.AsSpan(offset), value);

    public byte[] ToArray() => _buf.AsSpan(0, _len).ToArray();
}
