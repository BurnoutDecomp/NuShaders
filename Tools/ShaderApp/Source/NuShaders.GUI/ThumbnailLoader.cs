using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using NuShaders.Formats.Bundle;
using NuShaders.Imaging;

namespace NuShaders.GUI;

/// <summary>Decodes a bundle Texture's preview to a frozen WPF bitmap (safe to build on a background thread).</summary>
public static class ThumbnailLoader
{
    public static BitmapSource? Load(CatalogResource texture, int maxSize = 128)
    {
        if (texture.DatPath is null || !File.Exists(texture.DatPath)) return null;
        string secondary = texture.DatPath.EndsWith("_primary.dat", StringComparison.OrdinalIgnoreCase)
            ? texture.DatPath[..^"_primary.dat".Length] + "_secondary.dat"
            : texture.DatPath;

        DecodedThumbnail? thumb;
        try
        {
            thumb = TextureThumbnail.Decode(File.ReadAllBytes(texture.DatPath),
                File.Exists(secondary) ? File.ReadAllBytes(secondary) : [], maxSize);
        }
        catch { return null; }
        if (thumb is null) return null;

        // RGBA (decoder) -> BGRA (WPF Bgra32)
        var bgra = new byte[thumb.Rgba.Length];
        for (int i = 0; i < bgra.Length; i += 4)
        {
            bgra[i + 0] = thumb.Rgba[i + 2];
            bgra[i + 1] = thumb.Rgba[i + 1];
            bgra[i + 2] = thumb.Rgba[i + 0];
            bgra[i + 3] = thumb.Rgba[i + 3];
        }
        var bmp = BitmapSource.Create(thumb.Width, thumb.Height, 96, 96, PixelFormats.Bgra32, null, bgra, thumb.Width * 4);
        bmp.Freeze();
        return bmp;
    }
}
