using System.Globalization;

namespace NuShaders.Formats.Model;

/// <summary>A 32-bit Burnout bundle resource id. Bundles resolve the resource graph by id.</summary>
public readonly record struct ResourceID(uint Value)
{
    /// <summary>Parse an 8-hex-digit id, with or without a leading "0x".</summary>
    public static ResourceID Parse(string text)
    {
        ReadOnlySpan<char> s = text.AsSpan().Trim();
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        return new ResourceID(uint.Parse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
    }

    /// <summary>Lower-case 8-hex-digit form (e.g. "e3bf57ad") — matches .meta.yaml / imports.yaml.</summary>
    public string ToHexLower() => Value.ToString("x8", CultureInfo.InvariantCulture);

    /// <summary>Upper-case 8-hex-digit form (e.g. "E3BF57AD") — matches the extracted .dat file names.</summary>
    public string ToHexUpper() => Value.ToString("X8", CultureInfo.InvariantCulture);

    public override string ToString() => "0x" + ToHexLower();
}
