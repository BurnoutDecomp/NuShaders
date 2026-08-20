using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;

namespace NuShaders.Imaging;

/// <summary>
/// Writes an <see cref="EmittedResource"/> set to disk in the bundle's <c>Texture/</c> + <c>TextureState/</c>
/// layout. Textures emit <c>{ID}_primary.dat</c> + <c>{ID}_secondary.dat</c>; TextureStates emit
/// <c>{ID}.dat</c> + <c>{ID}_imports.yaml</c>. Supports a standalone per-material folder (with its own
/// <c>.meta.yaml</c>) and/or merging into an existing bundle's <c>.meta.yaml</c>.
/// </summary>
public static class TextureSetWriter
{
    public sealed record WriteResult(string Folder, IReadOnlyList<string> Files);

    /// <summary>Write the resources into <paramref name="folder"/> with a fresh standalone <c>.meta.yaml</c>.</summary>
    public static WriteResult WriteStandalone(string folder, IReadOnlyList<EmittedResource> resources, bool force)
    {
        var files = new List<string>();
        var meta = MetaYAML.Create();
        foreach (var r in resources)
        {
            WriteResourceFiles(folder, r, force, files);
            meta.Upsert(ToEntry(r));
        }
        string metaPath = Path.Combine(folder, ".meta.yaml");
        File.WriteAllText(metaPath, meta.ToYAML());
        files.Add(metaPath);
        WriteDebugNames(folder, resources);
        return new WriteResult(folder, files);
    }

    /// <summary>Copy the resources into an existing bundle folder and upsert them into its <c>.meta.yaml</c>.</summary>
    public static void MergeIntoBundle(string bundleFolder, IReadOnlyList<EmittedResource> resources)
    {
        var discard = new List<string>();
        foreach (var r in resources)
            WriteResourceFiles(bundleFolder, r, force: true, discard);

        string metaPath = Path.Combine(bundleFolder, ".meta.yaml");
        var meta = File.Exists(metaPath) ? MetaYAML.Read(File.ReadAllText(metaPath)) : MetaYAML.Create();
        foreach (var r in resources) meta.Upsert(ToEntry(r));
        File.WriteAllText(metaPath, meta.ToYAML());
        WriteDebugNames(bundleFolder, resources);
    }

    /// <summary>Persist each resource's name into the folder's `.debug.xml` (created if absent) so names survive reopen.</summary>
    private static void WriteDebugNames(string folder, IReadOnlyList<EmittedResource> resources)
    {
        foreach (var r in resources)
            if (!string.IsNullOrEmpty(r.Name))
                DebugXML.UpsertName(folder, r.Id, DebugXML.TypeString(r.MetaType), r.Name);
    }

    private static MetaYAML.Entry ToEntry(EmittedResource r) => new(r.Id.Value, r.MetaType, r.SecondaryMemoryType, r.Alignment);

    private static void WriteResourceFiles(string root, EmittedResource r, bool force, List<string> files)
    {
        string id = r.Id.ToHexUpper();
        if (r.MetaType == BPRTextureResource.MetaType)
        {
            string dir = Path.Combine(root, "Texture");
            Directory.CreateDirectory(dir);
            WriteBytes(Path.Combine(dir, $"{id}_primary.dat"), r.Primary, force, files);
            if (r.Secondary is not null)
                WriteBytes(Path.Combine(dir, $"{id}_secondary.dat"), r.Secondary, force, files);
        }
        else
        {
            string dir = Path.Combine(root, "TextureState");
            Directory.CreateDirectory(dir);
            WriteBytes(Path.Combine(dir, $"{id}.dat"), r.Primary, force, files);
            if (r.Imports is not null)
                WriteText(Path.Combine(dir, $"{id}_imports.yaml"), r.Imports.ToYAML(), force, files);
        }
    }

    private static void WriteBytes(string path, byte[] data, bool force, List<string> files)
    {
        if (!force && File.Exists(path)) throw new IOException($"output exists (use --force): {path}");
        File.WriteAllBytes(path, data);
        files.Add(path);
    }

    private static void WriteText(string path, string text, bool force, List<string> files)
    {
        if (!force && File.Exists(path)) throw new IOException($"output exists (use --force): {path}");
        File.WriteAllText(path, text);
        files.Add(path);
    }
}
