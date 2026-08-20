using NuShaders.Formats.BPR;
using NuShaders.Formats.Model;

namespace NuShaders.Formats.Bundle;

/// <summary>
/// Writes a Material resource (`Material/{ID}.dat` + `{ID}_imports.yaml`) and its `.meta.yaml` entry, either to a fresh
/// standalone output folder (non-destructive) or merged into an existing bundle. Mirrors the texture writer's pattern.
/// </summary>
public static class MaterialWriter
{
    /// <summary>Write to a fresh per-edit output folder with its own `.meta.yaml` (non-destructive save). Writes the
    /// resource's name to `.debug.xml` when <paramref name="name"/> is provided.</summary>
    public static void WriteStandalone(string folder, ResourceID id, byte[] dat, ImportsYAML imports, string? name = null)
    {
        WriteFiles(folder, id, dat, imports);
        var meta = MetaYAML.Create();
        meta.Upsert(Entry(id));
        File.WriteAllText(Path.Combine(folder, ".meta.yaml"), meta.ToYAML());
        if (!string.IsNullOrEmpty(name)) DebugXML.UpsertName(folder, id, DebugXML.TypeString(BPRMaterialResource.MetaType), name);
    }

    /// <summary>Copy the material into an existing extracted bundle and upsert its `.meta.yaml` (and `.debug.xml`) entry.</summary>
    public static void MergeIntoBundle(string bundleFolder, ResourceID id, byte[] dat, ImportsYAML imports, string? name = null)
    {
        WriteFiles(bundleFolder, id, dat, imports);
        string metaPath = Path.Combine(bundleFolder, ".meta.yaml");
        var meta = File.Exists(metaPath) ? MetaYAML.Read(File.ReadAllText(metaPath)) : MetaYAML.Create();
        meta.Upsert(Entry(id));
        File.WriteAllText(metaPath, meta.ToYAML());
        if (!string.IsNullOrEmpty(name)) DebugXML.UpsertName(bundleFolder, id, DebugXML.TypeString(BPRMaterialResource.MetaType), name);
    }

    private static MetaYAML.Entry Entry(ResourceID id) =>
        new(id.Value, BPRMaterialResource.MetaType, null, BPRMaterialResource.MetaAlignment);

    private static void WriteFiles(string root, ResourceID id, byte[] dat, ImportsYAML imports)
    {
        string dir = Path.Combine(root, "Material");
        Directory.CreateDirectory(dir);
        string upper = id.ToHexUpper();
        File.WriteAllBytes(Path.Combine(dir, $"{upper}.dat"), dat);
        File.WriteAllText(Path.Combine(dir, $"{upper}_imports.yaml"), imports.ToYAML());
    }
}
