using System.Text.Json;
using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;

namespace NuShaders.CLI.Commands;

/// <summary>
/// pack-bpr-shader: with --generate, build a Shader (0x32) from compiled VS/PS DXBCs (a JSON spec);
/// otherwise clone a reference Shader under a new id, swapping the Default technique's VS/PS imports.
/// Both emit &lt;name&gt;.dat + &lt;name&gt;_imports.yaml.
/// </summary>
internal static class PackBPRShaderCommand
{
    public static int Run(ArgMap args)
    {
        if (args.Has("generate")) return RunGenerate(args);

        string refPath = args.Require("reference");
        var shader = BPRShaderResource.Read(File.ReadAllBytes(refPath));
        byte[] datBytes = shader.ToBytes();

        // Optionally append extra samplers (name:slot[:technique], comma-separated). Lets the clone
        // expose engine-global slots the new shader samples by name (e.g. ReflectionTextureSampler:13).
        var added = new List<string>();
        foreach (var spec in (args.Get("add-sampler") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = spec.Split(':');
            if (p.Length < 2) { Console.Error.WriteLine($"bad --add-sampler '{spec}' (want name:slot[:tech])"); return 1; }
            int tech = p.Length > 2 ? int.Parse(p[2]) : 0;
            datBytes = BPRShaderResource.AddSampler(datBytes, tech, p[0], int.Parse(p[1]));
            added.Add($"{p[0]}@s{p[1]}(tech{tech})");
        }

        // Carry over all import slots from the reference, then override the Default technique's VS/PS.
        var map = new SortedDictionary<uint, uint>();
        string refImports = args.Get("reference-imports") ?? DeriveImportsPath(refPath);
        if (File.Exists(refImports))
            foreach (var (offset, id) in ImportsYAML.Read(File.ReadAllText(refImports)).Entries)
                map[offset] = id;

        var slots = shader.ImportSlots().ToList();
        if (args.Get("vs-id") is { } vs)
            map[(uint)slots.First(s => s.Technique == 0 && s.Stage == "VS").Offset] = ResourceID.Parse(vs).Value;
        if (args.Get("ps-id") is { } ps)
            map[(uint)slots.First(s => s.Technique == 0 && s.Stage == "PS").Offset] = ResourceID.Parse(ps).Value;

        var imports = new ImportsYAML();
        foreach (var kv in map) imports.Entries.Add((kv.Key, kv.Value));

        string outDir = args.Get("out-dir") ?? Path.GetDirectoryName(Path.GetFullPath(refPath))!;
        string name = args.Require("name").ToUpperInvariant();
        Directory.CreateDirectory(outDir);
        string datOut = Path.Combine(outDir, $"{name}.dat");
        string impOut = Path.Combine(outDir, $"{name}_imports.yaml");
        if (!args.Has("force") && (File.Exists(datOut) || File.Exists(impOut)))
        {
            Console.Error.WriteLine("output exists (use --force)");
            return 1;
        }

        File.WriteAllBytes(datOut, datBytes);
        File.WriteAllText(impOut, imports.ToYAML());

        var d = shader.Decode();
        string extra = added.Count > 0 ? $", +sampler[{string.Join(", ", added)}]" : "";
        Console.WriteLine($"cloned Shader '{d.Name}' -> {name}: {shader.NumTechniques} technique(s), {imports.Entries.Count} imports{extra} -> {outDir}");
        return 0;
    }

    private static string DeriveImportsPath(string datPath)
    {
        string dir = Path.GetDirectoryName(Path.GetFullPath(datPath))!;
        string baseName = Path.GetFileNameWithoutExtension(datPath);
        return Path.Combine(dir, $"{baseName}_imports.yaml");
    }

    // ---- generate from a JSON spec: { name, materials:[{name,value[]}], techniques:[{name,vs,ps,vsID,psID}] } ----
    private static int RunGenerate(ArgMap args)
    {
        string specPath = args.Require("spec");
        string specDir = Path.GetDirectoryName(Path.GetFullPath(specPath))!;
        var spec = JsonSerializer.Deserialize<GenSpec>(File.ReadAllText(specPath),
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidDataException($"could not parse spec: {specPath}");

        string Resolve(string p) => Path.IsPathRooted(p) ? p : Path.Combine(specDir, p);
        var materials = spec.Materials.Select(m => (m.Name, m.Value)).ToArray();
        var techniques = spec.Techniques.Select(t => new BPRShaderResource.GenerateTechnique(
            t.Name, File.ReadAllBytes(Resolve(t.VS)), File.ReadAllBytes(Resolve(t.PS)),
            ResourceID.Parse(t.VSID).Value, ResourceID.Parse(t.PSID).Value)).ToArray();

        var (dat, imports) = BPRShaderResource.Generate(spec.Name, materials, techniques);

        string outDir = args.Get("out-dir") ?? specDir;
        string name = args.Require("name").ToUpperInvariant();
        Directory.CreateDirectory(outDir);
        string datOut = Path.Combine(outDir, $"{name}.dat");
        string impOut = Path.Combine(outDir, $"{name}_imports.yaml");
        if (!args.Has("force") && (File.Exists(datOut) || File.Exists(impOut)))
        {
            Console.Error.WriteLine("output exists (use --force)");
            return 1;
        }

        File.WriteAllBytes(datOut, dat);
        File.WriteAllText(impOut, imports.ToYAML());
        Console.WriteLine($"generated Shader '{spec.Name}' -> {name}: {techniques.Length} technique(s), {imports.Entries.Count} imports -> {outDir}");
        return 0;
    }

    private sealed record GenSpec(string Name, GenMaterial[] Materials, GenTech[] Techniques);
    private sealed record GenMaterial(string Name, float[] Value);
    private sealed record GenTech(string Name, string VS, string PS, string VSID, string PSID);
}
