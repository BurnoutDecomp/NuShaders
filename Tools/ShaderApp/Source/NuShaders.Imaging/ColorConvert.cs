using System.Globalization;

namespace NuShaders.Imaging;

/// <summary>
/// Pure float &lt;-&gt; 8-bit / hex colour conversion for the material colour editor. BPR material colour constants are
/// raw linear tint multipliers with no gamma anywhere in the shaders (verified), so the mapping is the direct
/// <c>[0,1] -&gt; [0,255]</c> with clamping — what you store is what the swatch shows. Lives here (not the WPF GUI) so it
/// is unit-testable.
/// </summary>
public static class ColorConvert
{
    /// <summary>Clamp a [0,1] float to a 0..255 byte (values &gt;1 clamp to 255 — display only; the stored float is untouched).</summary>
    public static byte ToByte(float v) => (byte)Math.Clamp((int)MathF.Round(v * 255f), 0, 255);

    /// <summary>Format RGB as "RRGGBB" (clamped); alpha is handled separately.</summary>
    public static string ToHex(float r, float g, float b) => $"{ToByte(r):X2}{ToByte(g):X2}{ToByte(b):X2}";

    /// <summary>Parse "RRGGBB" (optional leading '#') to [0,1] floats. Returns false on malformed input (caller keeps current values).</summary>
    public static bool TryParseHex(string? s, out float r, out float g, out float b)
    {
        r = g = b = 0f;
        if (s is null) return false;
        string h = s.Trim().TrimStart('#');
        if (h.Length != 6 || !uint.TryParse(h, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out uint rgb))
            return false;
        r = ((rgb >> 16) & 0xFF) / 255f;
        g = ((rgb >> 8) & 0xFF) / 255f;
        b = (rgb & 0xFF) / 255f;
        return true;
    }
}
