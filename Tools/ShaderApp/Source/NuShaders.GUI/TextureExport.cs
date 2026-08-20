using System.IO;
using System.Windows;
using Microsoft.Win32;
using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;

namespace NuShaders.GUI;

/// <summary>Right-click "Export as DDS…": wraps a bundle texture's primary+secondary .dat into a standard .dds via <see cref="DDSWriter"/>.</summary>
public static class TextureExport
{
    public static void SaveAsDDS(CatalogResource? texture, Window? owner)
    {
        if (texture?.DatPath is not { } primaryPath || !File.Exists(primaryPath))
        {
            MessageBox.Show(owner, "This texture isn't available on disk to export.", "Export as DDS", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        string secondaryPath = primaryPath.EndsWith("_primary.dat", StringComparison.OrdinalIgnoreCase)
            ? primaryPath[..^"_primary.dat".Length] + "_secondary.dat"
            : primaryPath;

        var dlg = new SaveFileDialog { Filter = "DirectDraw Surface (*.dds)|*.dds", FileName = SafeFileName(texture) + ".dds" };
        if (dlg.ShowDialog(owner) != true) return;

        try
        {
            var hdr = BPRTextureResource.ReadPrimary(File.ReadAllBytes(primaryPath));
            byte[] secondary = File.Exists(secondaryPath) ? File.ReadAllBytes(secondaryPath) : [];
            File.WriteAllBytes(dlg.FileName, DDSWriter.Build(hdr, secondary));
        }
        catch (Exception ex)
        {
            MessageBox.Show(owner, ex.Message, "Export as DDS failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static string SafeFileName(CatalogResource tex)
    {
        string n = tex.ShortName;
        foreach (char c in Path.GetInvalidFileNameChars()) n = n.Replace(c, '_');
        return string.IsNullOrWhiteSpace(n) ? tex.Id.ToHexUpper() : n;
    }
}
