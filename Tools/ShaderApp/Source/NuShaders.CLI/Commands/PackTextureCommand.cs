using NuShaders.Formats.BPR;
using NuShaders.Formats.Model;
using NuShaders.Imaging;

namespace NuShaders.CLI.Commands;

/// <summary>
/// pack-texture: pack a material's PBR source maps (albedo/AO/roughness/metallic/displacement) into the two
/// BPR textures the PBR shader expects (Diffuse + Spec/ORMH), BC-encode them, and emit the Texture +
/// TextureState resources + a .meta.yaml. Writes a standalone per-material folder; with --merge-bundle it
/// also injects them into an existing bundle.
/// </summary>
internal static class PackTextureCommand
{
    public static int Run(ArgMap args)
    {
        var maps = new SourceMaps(
            args.Get("albedo"), args.Get("ao"), args.Get("roughness"),
            args.Get("metallic"), args.Get("displacement"), args.Get("opacity"));
        if (maps.AlbedoPath is null) { Console.Error.WriteLine("missing required --albedo"); return 1; }

        var options = new PackOptions(
            ParseFormat(args.Get("diffuse-format"), DXGIFormat.BC1_UNORM),
            ParseFormat(args.Get("spec-format"), DXGIFormat.BC3_UNORM),
            GenerateMips: !args.Has("no-mips"),
            ForceSize: args.Has("size") ? args.GetInt("size", 0) : null);

        string name = args.Require("name");
        var (diffuse, spec) = PBRTexturePacker.Pack(maps, options);
        var inputs = new TextureSetInputs(name, diffuse, spec,
            DiffuseIDOverride: args.Get("diffuse-id") is { } d ? ResourceID.Parse(d) : null,
            SpecIDOverride: args.Get("spec-id") is { } s ? ResourceID.Parse(s) : null);
        var resources = BPRTextureSet.Build(inputs);

        string outDir = args.Get("out-dir") ?? Path.Combine(Directory.GetCurrentDirectory(), name);
        Directory.CreateDirectory(outDir);
        var result = TextureSetWriter.WriteStandalone(outDir, resources, args.Has("force"));

        Console.WriteLine($"packed '{name}': {resources.Count} resources, {result.Files.Count} files -> {outDir}");
        foreach (var r in resources)
        {
            string kind = r.MetaType == BPRTextureResource.MetaType ? "Texture     " : "TextureState";
            Console.WriteLine($"  {r.Id} {kind} {r.Name}");
        }

        if (args.Get("merge-bundle") is { } bundle)
        {
            TextureSetWriter.MergeIntoBundle(bundle, resources);
            Console.WriteLine($"merged into bundle: {bundle}");
        }
        return 0;
    }

    private static DXGIFormat ParseFormat(string? s, DXGIFormat fallback) => s?.ToUpperInvariant() switch
    {
        null => fallback,
        "BC1" => DXGIFormat.BC1_UNORM,
        "BC3" => DXGIFormat.BC3_UNORM,
        "BC5" => DXGIFormat.BC5_UNORM,
        "BC7" => DXGIFormat.BC7_UNORM,
        _ => throw new ArgumentException($"unknown format '{s}' (want BC1|BC3|BC5|BC7)"),
    };
}
