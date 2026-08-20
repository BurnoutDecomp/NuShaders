using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.Unit;

public class BPRMaterialBuilderTests
{
    [Fact]
    public void BuildFromTemplate_fed_a_materials_own_inputs_reproduces_it_byte_identically()
    {
        if (!ReferencePaths.Available) return;
        string bundle = Path.Combine(ReferencePaths.PBRMatsDir, "SHADERS");
        if (!File.Exists(Path.Combine(bundle, ".meta.yaml"))) return;

        var cat = BundleCatalog.Open(bundle);
        var mat = cat.Materials.FirstOrDefault(m => m.DatPath is not null && m.ImportsPath is not null);
        if (mat is null) return;

        var dat = File.ReadAllBytes(mat.DatPath!);
        var imports = ImportsYAML.Read(File.ReadAllText(mat.ImportsPath!));
        var d = BPRMaterialResource.Read(dat).Decode();
        uint ImportAt(int off) { foreach (var e in imports.Entries) if (e.Offset == (uint)off) return e.Id; return 0; }

        var values = d.VSConstants.Concat(d.PSConstants).Select(c => new MaterialParamValue(c.NameHash, c.Value)).ToList();
        var textures = d.Samplers
            .Select(s => new MaterialTextureAssign(s.Channel, new ResourceID(ImportAt(s.TextureStateSlotOffset))))
            .Where(t => t.TextureStateID.Value != 0).ToList();
        var states = d.Techniques
            .Select(t => { uint id = ImportAt(t.MaterialStateSlotOffset); return id != 0 ? new ResourceID(id) : (ResourceID?)null; })
            .ToList();
        var shaderID = new ResourceID(ImportAt(BPRMaterialResource.ShaderSlotOffset));

        var (rebuiltDat, _) = BPRMaterialBuilder.BuildFromTemplate(dat, imports, mat.Id, shaderID, values, textures, states);

        Assert.Equal(dat, rebuiltDat);   // same values + same id → byte-identical (the builder writes, it doesn't corrupt)
    }
}
