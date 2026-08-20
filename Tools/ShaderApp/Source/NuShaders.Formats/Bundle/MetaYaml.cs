using System.Globalization;
using System.Text;

namespace NuShaders.Formats.Bundle;

/// <summary>
/// A bundle <c>.meta.yaml</c>: a <c>bundle:</c> header followed by a <c>resources:</c> map of
/// <c>0xID → { type, [secondaryMemoryType], alignment[] }</c> sorted by id ascending. Hand-rolled so it
/// re-emits byte-identically to the source: the header and every existing resource block are preserved
/// verbatim, and only newly <see cref="Upsert"/>ed entries are serialized (in the established format and
/// inserted at the correct sorted position). Style matches the corpus: 2-space indent for ids, 4 for
/// fields, 6 for alignment items; <c>0x</c> lowercase hex (ids 8-padded, type/alignment unpadded).
/// </summary>
public sealed class MetaYAML
{
    public sealed record Entry(uint Id, int Type, int? SecondaryMemoryType, int[] Alignment);

    private string _header = "";   // verbatim text from start through (and including) the "resources:" line + any pre-first-block lines
    private string _newline = "\n";
    private readonly List<(uint Id, string Block)> _blocks = [];   // each block = verbatim text incl. trailing newline(s)

    public IReadOnlyList<uint> ResourceIDs => _blocks.Select(b => b.Id).ToArray();
    public bool Contains(uint id) => _blocks.Any(b => b.Id == id);

    /// <summary>The resource's <c>type:</c> value (e.g. 0x1 Material, 0xe TextureState, 0x32 Shader), or null if absent.</summary>
    public int? TypeOf(uint id)
    {
        int i = _blocks.FindIndex(b => b.Id == id);
        if (i < 0) return null;
        foreach (var raw in _blocks[i].Block.Split('\n'))
        {
            string t = raw.Trim();
            if (t.StartsWith("type:", StringComparison.Ordinal))
            {
                string v = t["type:".Length..].Trim();
                if (v.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) v = v[2..];
                if (uint.TryParse(v, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint type)) return (int)type;
            }
        }
        return null;
    }

    /// <summary>A fresh, empty meta with the standard BPR bundle header (platform 1, compressed, mem-optimised).</summary>
    public static MetaYAML Create(string newline = "\n")
    {
        string header = string.Join(newline,
            "bundle:", "  platform: 1", "  compressed: true",
            "  mainMemOptimised: true", "  graphicsMemOptimised: true", "resources:") + newline;
        return new MetaYAML { _newline = newline, _header = header };
    }

    public static MetaYAML Read(string text)
    {
        var m = new MetaYAML { _newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n" };
        string nl = m._newline;

        int afterHeader = AfterResourcesLine(text, nl);
        if (afterHeader < 0) { m._header = text; return m; }

        string body = text[afterHeader..];
        var starts = new List<(int Pos, uint Id)>();
        int pos = 0;
        while (pos < body.Length)
        {
            int eol = body.IndexOf(nl, pos, StringComparison.Ordinal);
            int lineEnd = eol < 0 ? body.Length : eol;
            if (IsResourceStart(body[pos..lineEnd], out uint id))
                starts.Add((pos, id));
            pos = eol < 0 ? body.Length : eol + nl.Length;
        }

        // Any text between "resources:" and the first resource (e.g. blank line) stays in the header.
        int firstBlock = starts.Count > 0 ? starts[0].Pos : body.Length;
        m._header = text[..(afterHeader + firstBlock)];
        for (int k = 0; k < starts.Count; k++)
        {
            int s = starts[k].Pos;
            int e = k + 1 < starts.Count ? starts[k + 1].Pos : body.Length;
            m._blocks.Add((starts[k].Id, body[s..e]));
        }
        return m;
    }

    /// <summary>Add a resource (or replace one with the same id), keeping the list sorted by id ascending.</summary>
    public void Upsert(Entry entry)
    {
        string block = SerializeBlock(entry);
        int existing = _blocks.FindIndex(b => b.Id == entry.Id);
        if (existing >= 0) { _blocks[existing] = (entry.Id, block); return; }
        int insert = _blocks.FindIndex(b => b.Id > entry.Id);
        if (insert < 0) _blocks.Add((entry.Id, block));
        else _blocks.Insert(insert, (entry.Id, block));
    }

    public string ToYAML() => _header + string.Concat(_blocks.Select(b => b.Block));

    private string SerializeBlock(Entry e)
    {
        var sb = new StringBuilder();
        sb.Append("  0x").Append(e.Id.ToString("x8", CultureInfo.InvariantCulture)).Append(':').Append(_newline);
        sb.Append("    type: 0x").Append(e.Type.ToString("x", CultureInfo.InvariantCulture)).Append(_newline);
        if (e.SecondaryMemoryType.HasValue)
            sb.Append("    secondaryMemoryType: ").Append(e.SecondaryMemoryType.Value.ToString(CultureInfo.InvariantCulture)).Append(_newline);
        sb.Append("    alignment:").Append(_newline);
        foreach (int a in e.Alignment)
            sb.Append("      - 0x").Append(a.ToString("x", CultureInfo.InvariantCulture)).Append(_newline);
        return sb.ToString();
    }

    private static int AfterResourcesLine(string text, string nl)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            int eol = text.IndexOf(nl, pos, StringComparison.Ordinal);
            int lineEnd = eol < 0 ? text.Length : eol;
            if (text[pos..lineEnd].TrimEnd() == "resources:")
                return eol < 0 ? text.Length : eol + nl.Length;
            pos = eol < 0 ? text.Length : eol + nl.Length;
        }
        return -1;
    }

    private static bool IsResourceStart(string line, out uint id)
    {
        id = 0;
        string t = line.TrimEnd();
        if (!t.StartsWith("  0x", StringComparison.Ordinal) || !t.EndsWith(":", StringComparison.Ordinal)) return false;
        return uint.TryParse(t[4..^1], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out id);
    }
}
