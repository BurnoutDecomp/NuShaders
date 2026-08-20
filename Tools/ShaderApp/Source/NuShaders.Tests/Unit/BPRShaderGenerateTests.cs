using NuShaders.Formats.BPR;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.Unit;

public class BPRShaderGenerateTests
{
    [Fact]
    public void Generated_specular_1bit_matches_stock_semantics()
    {
        if (!ReferencePaths.Available) return;
        string spbDir = ReferencePaths.BPRShaderProgramBufferDir;
        string stock = Path.Combine(ReferencePaths.BPRShaderDir, "E3BF57AD.dat");
        if (!File.Exists(stock)) return;

        // Stock SPB DXBCs: Default VS=2EC61041 PS=A3A50BAB; ZOnly VS=D718BAC3 PS=C4D0E637.
        string[] ids = ["2EC61041", "A3A50BAB", "D718BAC3", "C4D0E637"];
        foreach (var id in ids)
            if (!File.Exists(Path.Combine(spbDir, $"{id}_secondary.dat"))) return;
        byte[] DXBC(string id) => File.ReadAllBytes(Path.Combine(spbDir, $"{id}_secondary.dat"));

        (string, float[])[] materials =
        [
            ("materialDiffuse", [1f, 1f, 1f, 1f]),
            ("SpecularPower", [12f, 0f, 0f, 0f]),
            ("Specularity", [1.5f, 0f, 0f, 0f]),
        ];
        BPRShaderResource.GenerateTechnique[] techs =
        [
            new("Default", DXBC("2EC61041"), DXBC("A3A50BAB"), 0x2EC61041, 0xA3A50BAB),
            new("ZOnly1BitDoubleSided", DXBC("D718BAC3"), DXBC("C4D0E637"), 0xD718BAC3, 0xC4D0E637),
        ];

        var (dat, imports) = BPRShaderResource.Generate("Specular_1Bit_Doublesided", materials, techs);

        var gen = BPRShaderResource.Read(dat).Decode();
        var stk = BPRShaderResource.Read(File.ReadAllBytes(stock)).Decode();

        // Constant table: identical name order + per-constant size/hash/index/instance-data.
        Assert.Equal(stk.Constants.Select(c => c.Name), gen.Constants.Select(c => c.Name));
        for (int i = 0; i < stk.Constants.Count; i++)
        {
            Assert.Equal(stk.Constants[i].Size, gen.Constants[i].Size);
            Assert.Equal(stk.Constants[i].NameHash, gen.Constants[i].NameHash);
            Assert.Equal(stk.Constants[i].Index, gen.Constants[i].Index);
            Assert.Equal(stk.Constants[i].InstanceData, gen.Constants[i].InstanceData);
        }

        // Techniques: idx1/idx2 (as constant-name sequences) + samplers identical.
        Assert.Equal(stk.Techniques.Count, gen.Techniques.Count);
        for (int t = 0; t < stk.Techniques.Count; t++)
        {
            Assert.Equal(stk.Techniques[t].Name, gen.Techniques[t].Name);
            Assert.Equal(
                stk.Techniques[t].Indices1.Select(p => stk.Constants[p].Name),
                gen.Techniques[t].Indices1.Select(p => gen.Constants[p].Name));
            Assert.Equal(
                stk.Techniques[t].Indices2.Select(p => stk.Constants[p].Name),
                gen.Techniques[t].Indices2.Select(p => gen.Constants[p].Name));
            Assert.Equal(
                stk.Techniques[t].Samplers.Select(s => (s.Name, s.Channel)),
                gen.Techniques[t].Samplers.Select(s => (s.Name, s.Channel)));
        }

        // imports.yaml has VS+PS per technique.
        Assert.Equal(4, imports.Entries.Count);
    }
}
