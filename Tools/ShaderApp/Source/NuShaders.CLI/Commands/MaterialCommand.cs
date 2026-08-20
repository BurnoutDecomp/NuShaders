using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.Formats.Hashing;
using NuShaders.Formats.Model;

namespace NuShaders.CLI.Commands;

/// <summary>
/// material: inspect/edit a BPR material in an extracted bundle. `show` prints the shader-bound parameter/sampler/state
/// surface; the `set-*` verbs apply one surgical edit (param float4, or re-point shader/texture/state) and write the
/// patched material to a non-destructive output folder (and optionally merge into a bundle).
/// </summary>
internal static class MaterialCommand
{
    public static int Run(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("material <show|set-param|set-shader|set-texture|set-state> [options]"); return 1; }
        var a = new ArgMap(args, 2);
        return args[1] switch
        {
            "show" => Show(a),
            "set-param" => SetParam(a),
            "set-shader" => SetShader(a),
            "set-texture" => SetTexture(a),
            "set-state" => SetState(a),
            _ => Unknown(args[1]),
        };
    }

    private static int Unknown(string op) { Console.Error.WriteLine($"unknown material op '{op}'"); return 1; }

    private static int Show(ArgMap a)
    {
        var cat = OpenCatalog(a);
        var res = Resolve(cat, a);
        var b = ShaderParameterBinding.Bind(cat, res);

        Console.WriteLine($"Material {res.Id} {res.DisplayName}");
        Console.WriteLine($"  Shader: {(b.ShaderID?.ToString() ?? "(none)")} {Label(cat, b.ShaderID)}{(b.ShaderFound ? "" : "   [NOT IN BUNDLE - param names unresolved]")}");
        Console.WriteLine("  Render states (per technique):");
        foreach (var (ti, off, msid) in b.MaterialStates)
            Console.WriteLine($"    technique {ti} @0x{off:x2} -> {(msid?.ToString() ?? "(none)")} {Label(cat, msid)}");
        Console.WriteLine("  Samplers (texture-first; the TextureState is resolved behind the scenes):");
        foreach (var s in b.Samplers)
        {
            string tex = s.TextureID is { } t ? $"{t} {Label(cat, t)}" : "(no texture)";
            Console.WriteLine($"    ch{s.Channel} {s.ShaderSamplerName,-24} -> {tex}");
            if (s.Sampler is { } sp)
                Console.WriteLine($"         wrap {sp.AddressU}/{sp.AddressV}/{sp.AddressW}  filter {sp.MagFilter}  aniso {sp.MaxAnisotropy}   (state {s.AssignedTextureState?.ToString() ?? "none"})");
        }
        Console.WriteLine("  Parameters:");
        foreach (var p in b.Parameters)
            Console.WriteLine($"    [{(p.IsPixel ? "PS" : "VS")}] {p.Name,-22} = ({Fmt(p.CurrentValue)})   default=({Fmt(p.ShaderDefault)}){(p.ResolvedInShader ? "" : "   [unresolved]")}");
        return 0;
    }

    private static int SetParam(ArgMap a)
    {
        var (cat, res, mat, imports) = LoadForEdit(a);
        bool pixel = !"vs".Equals(a.Get("stage"), StringComparison.OrdinalIgnoreCase);
        var consts = pixel ? mat.Decode().PSConstants : mat.Decode().VSConstants;
        string name = a.Require("name");
        uint hash = name.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? ResourceID.Parse(name).Value : CRC32.JamCRC(name);
        int idx = consts.ToList().FindIndex(c => c.NameHash == hash);
        if (idx < 0) { Console.Error.WriteLine($"param '{name}' (hash 0x{hash:x8}) not in {(pixel ? "PS" : "VS")} constants"); return 1; }
        float[] value = a.Require("value").Split(',', StringSplitOptions.TrimEntries).Select(float.Parse).ToArray();
        mat.SetConstant(pixel, idx, value);
        Console.WriteLine($"set {(pixel ? "PS" : "VS")} param {name} = ({Fmt(value)})");
        return WriteOut(a, res.Id, mat.ToBytes(), imports);
    }

    private static int SetShader(ArgMap a)
    {
        var (_, res, mat, imports) = LoadForEdit(a);
        var shader = ResourceID.Parse(a.Require("shader"));
        BPRMaterialResource.RepointImport(imports, BPRMaterialResource.ShaderSlotOffset, shader);
        Console.WriteLine($"re-pointed shader -> {shader}  (id repoint only; rebuild for incompatible layouts is the studio's Phase-3 path)");
        return WriteOut(a, res.Id, mat.ToBytes(), imports);
    }

    private static int SetTexture(ArgMap a)
    {
        var (_, res, mat, imports) = LoadForEdit(a);
        int channel = a.GetInt("channel", -1);
        var s = mat.Decode().Samplers.FirstOrDefault(x => x.Channel == channel)
            ?? throw new ArgumentException($"no sampler on channel {channel}");
        BPRMaterialResource.RepointImport(imports, s.TextureStateSlotOffset, ResourceID.Parse(a.Require("texturestate")));
        Console.WriteLine($"re-pointed channel {channel} texture-state");
        return WriteOut(a, res.Id, mat.ToBytes(), imports);
    }

    private static int SetState(ArgMap a)
    {
        var (_, res, mat, imports) = LoadForEdit(a);
        int tech = a.GetInt("technique", 0);
        var d = mat.Decode();
        if ((uint)tech >= (uint)d.Techniques.Count) throw new ArgumentException($"technique {tech} of {d.Techniques.Count}");
        BPRMaterialResource.RepointImport(imports, d.Techniques[tech].MaterialStateSlotOffset, ResourceID.Parse(a.Require("materialstate")));
        Console.WriteLine($"re-pointed technique {tech} material-state");
        return WriteOut(a, res.Id, mat.ToBytes(), imports);
    }

    // ---- helpers ----
    /// <summary>Open the primary bundle, optionally unioned with reference bundles from --shaders (";"-separated) for resolution.</summary>
    private static BundleCatalog OpenCatalog(ArgMap a)
    {
        var primary = BundleCatalog.Open(a.Require("bundle"));
        var refs = (a.Get("shaders") ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (refs.Length == 0) return primary;
        var cats = new List<BundleCatalog> { primary };
        cats.AddRange(refs.Select(BundleCatalog.Open));
        return BundleCatalog.Combine(primary.BundleFolder, cats);
    }

    private static (BundleCatalog Cat, CatalogResource Res, BPRMaterialResource Mat, ImportsYAML Imports) LoadForEdit(ArgMap a)
    {
        var cat = OpenCatalog(a);
        var res = Resolve(cat, a);
        var mat = BPRMaterialResource.Read(File.ReadAllBytes(res.DatPath!));
        var imp = res.ImportsPath is not null && File.Exists(res.ImportsPath)
            ? ImportsYAML.Read(File.ReadAllText(res.ImportsPath)) : new ImportsYAML();
        return (cat, res, mat, imp);
    }

    private static CatalogResource Resolve(BundleCatalog cat, ArgMap a)
        => cat.ByID(ResourceID.Parse(a.Require("id"))) ?? throw new ArgumentException($"material {a.Require("id")} not in bundle");

    private static int WriteOut(ArgMap a, ResourceID id, byte[] dat, ImportsYAML imports)
    {
        string outDir = a.Get("out") ?? Path.Combine(Directory.GetCurrentDirectory(), $"{id.ToHexUpper()}_edit");
        Directory.CreateDirectory(outDir);
        MaterialWriter.WriteStandalone(outDir, id, dat, imports);
        Console.WriteLine($"wrote material {id} -> {outDir}");
        if (a.Get("merge-bundle") is { } mb) { MaterialWriter.MergeIntoBundle(mb, id, dat, imports); Console.WriteLine($"merged into bundle: {mb}"); }
        return 0;
    }

    private static string Label(BundleCatalog cat, ResourceID? id) => id is { } i ? cat.ByID(i)?.DisplayName ?? "" : "";
    private static string Fmt(float[] v) => string.Join(", ", v.Select(x => x.ToString("0.###")));
}
