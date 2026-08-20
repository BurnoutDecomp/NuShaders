using System.Globalization;

namespace NuShaders.Formats.Bundle;

/// <summary>
/// A per-resource <c>&lt;ID&gt;_imports.yaml</c>: a list of <c>- 0xOFFSET: 0xID</c> entries mapping a
/// byte offset inside the resource to the resource id it imports. Hand-rolled to re-emit byte-identical
/// to YAP (lower-case 8-hex, "- "/": " spacing, LF newlines).
/// </summary>
public sealed class ImportsYAML
{
    public List<(uint Offset, uint Id)> Entries { get; } = [];

    public static ImportsYAML Read(string text)
    {
        var y = new ImportsYAML();
        foreach (var rawLine in text.Split('\n'))
        {
            string line = rawLine.Trim();
            int comment = line.IndexOf('#');            // tolerate hand-authored inline comments (game files have none)
            if (comment >= 0) line = line[..comment].Trim();
            if (line.Length == 0) continue;
            if (line.StartsWith("- ", StringComparison.Ordinal)) line = line[2..];
            int colon = line.IndexOf(':');
            if (colon < 0) continue;
            y.Entries.Add((ParseHex(line[..colon]), ParseHex(line[(colon + 1)..])));
        }
        return y;
    }

    public string ToYAML()
    {
        // LF as a separator, no trailing newline (matches YAP's output exactly).
        var lines = new List<string>(Entries.Count);
        foreach (var (offset, id) in Entries)
            lines.Add($"- 0x{offset.ToString("x8", CultureInfo.InvariantCulture)}: 0x{id.ToString("x8", CultureInfo.InvariantCulture)}");
        return string.Join('\n', lines);
    }

    private static uint ParseHex(string s)
    {
        s = s.Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    }
}
