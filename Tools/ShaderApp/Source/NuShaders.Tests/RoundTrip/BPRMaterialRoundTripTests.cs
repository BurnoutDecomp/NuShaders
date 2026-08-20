using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.RoundTrip;

public class BPRMaterialRoundTripTests
{
    private static List<string> MaterialFiles()
    {
        var list = new List<string>();
        if (!ReferencePaths.Available || !Directory.Exists(ReferencePaths.PBRMatsDir)) return list;
        foreach (var f in Directory.EnumerateFiles(ReferencePaths.PBRMatsDir, "*.dat", SearchOption.AllDirectories))
            if (Path.GetFileName(Path.GetDirectoryName(f)) == "Material")
                list.Add(f);
        return list;
    }

    [Fact]
    public void All_materials_round_trip_byte_identical()
    {
        var files = MaterialFiles();
        if (files.Count == 0) return;
        var failures = new List<string>();
        foreach (var f in files)
        {
            var orig = File.ReadAllBytes(f);
            var rebuilt = BPRMaterialResource.Read(orig).ToBytes();
            if (!orig.AsSpan().SequenceEqual(rebuilt)) failures.Add(Path.GetFileName(f));
        }
        Assert.True(failures.Count == 0, $"{failures.Count}/{files.Count} failed: " + string.Join(", ", failures.Take(10)));
    }

    [Fact]
    public void All_materials_decode_without_error()
    {
        var files = MaterialFiles();
        if (files.Count == 0) return;
        var failures = new List<string>();
        foreach (var f in files)
        {
            try
            {
                var d = BPRMaterialResource.Read(File.ReadAllBytes(f)).Decode();
                if (d.NumTechniques <= 0 || d.NumSamplers < 0)
                    failures.Add($"{Path.GetFileName(f)}: tech={d.NumTechniques} samp={d.NumSamplers}");
            }
            catch (Exception ex) { failures.Add($"{Path.GetFileName(f)}: {ex.GetType().Name}"); }
        }
        Assert.True(failures.Count == 0, $"{failures.Count}/{files.Count} failed: " + string.Join("; ", failures.Take(10)));
    }

    [Fact]
    public void E46FBF81_decodes_expected_params_and_set_changes_only_one_float4()
    {
        string? path = MaterialFiles().FirstOrDefault(f =>
            Path.GetFileNameWithoutExtension(f).Equals("E46FBF81", StringComparison.OrdinalIgnoreCase));
        if (path is null) return;

        var orig = File.ReadAllBytes(path);
        var mat = BPRMaterialResource.Read(orig);
        var d = mat.Decode();

        Assert.Equal(0xe46fbf81u, d.NameHash);
        Assert.Equal(3, d.NumSamplers);
        Assert.Contains(d.PSConstants, c => c.NameHash == 0xF6E27CAA && c.Value.SequenceEqual([1f, 1f, 1f, 1f])); // materialDiffuse
        Assert.Contains(d.PSConstants, c => c.NameHash == 0x81E0E773 && c.Value[0] == 20f);                       // SpecularPower
        Assert.Contains(d.PSConstants, c => c.NameHash == 0x4A73909F);                                            // Specularity

        int idx = d.PSConstants.ToList().FindIndex(c => c.NameHash == 0xF6E27CAA);
        mat.SetConstant(pixel: true, idx, [1f, 0f, 0f, 1f]);
        var edited = mat.ToBytes();

        int diffStart = -1, diffEnd = -1;
        for (int i = 0; i < orig.Length; i++)
            if (orig[i] != edited[i]) { if (diffStart < 0) diffStart = i; diffEnd = i; }
        Assert.True(diffStart >= 0 && diffEnd - diffStart + 1 <= 16, $"diff spans 0x{diffStart:x}..0x{diffEnd:x} (>16 bytes)");
    }

    [Fact]
    public void SetNameHash_changes_only_the_id_field()
    {
        string? path = MaterialFiles().FirstOrDefault();
        if (path is null) return;
        var orig = File.ReadAllBytes(path);
        var mat = BPRMaterialResource.Read(orig);
        mat.SetNameHash(0x12345678);
        var edited = mat.ToBytes();
        for (int i = 0; i < orig.Length; i++)
            if (i is < 0x04 or >= 0x08)
                Assert.Equal(orig[i], edited[i]);
        Assert.Equal(0x12345678u, BPRMaterialResource.Read(edited).NameHash);
    }

    [Fact]
    public void RepointImport_changes_only_the_targeted_entry()
    {
        var imp = new ImportsYAML();
        imp.Entries.Add((0x10, 0x1111));
        imp.Entries.Add((0x24, 0x2222));
        BPRMaterialResource.RepointImport(imp, 0x10, new ResourceID(0x9999));
        Assert.Equal(2, imp.Entries.Count);
        Assert.Equal(0x9999u, imp.Entries.First(e => e.Offset == 0x10).Id);
        Assert.Equal(0x2222u, imp.Entries.First(e => e.Offset == 0x24).Id);
    }
}
