using System.Text;
using NuShaders.Formats.IO;

namespace NuShaders.Formats.DXBC;

/// <summary>
/// Parses the DXBC chunk table: magic "DXBC", a 16-byte hash, version, total size @0x18,
/// chunk count @0x1C, then a uint32 offset table @0x20. Each chunk is FourCC(4) + size(4) + data.
/// </summary>
public sealed class DXBCContainer
{
    private readonly byte[] _data;
    private readonly (string FourCc, int Offset, int Size)[] _chunks;

    /// <summary>Total container size as recorded in the header (@0x18).</summary>
    public int TotalSize { get; }

    public DXBCContainer(ReadOnlySpan<byte> data)
    {
        if (data.Length < 0x20 ||
            data[0] != (byte)'D' || data[1] != (byte)'X' || data[2] != (byte)'B' || data[3] != (byte)'C')
            throw new InvalidDataException("Not a DXBC container (bad magic).");

        var r = new SpanReader(data);
        TotalSize = (int)r.U32LE(0x18);
        int count = (int)r.U32LE(0x1C);

        _chunks = new (string, int, int)[count];
        for (int i = 0; i < count; i++)
        {
            int chunkOff = (int)r.U32LE(0x20 + i * 4);
            string fourCc = Encoding.ASCII.GetString(data.Slice(chunkOff, 4));
            int size = (int)r.U32LE(chunkOff + 4);
            _chunks[i] = (fourCc, chunkOff + 8, size);
        }

        _data = data.ToArray();
    }

    public bool TryGetChunk(string fourCc, out ReadOnlySpan<byte> chunk)
    {
        foreach (var c in _chunks)
        {
            if (c.FourCc == fourCc)
            {
                chunk = _data.AsSpan(c.Offset, c.Size);
                return true;
            }
        }
        chunk = default;
        return false;
    }

    public ReadOnlySpan<byte> GetChunk(string fourCc) =>
        TryGetChunk(fourCc, out var chunk) ? chunk : throw new InvalidDataException($"DXBC has no '{fourCc}' chunk.");
}
