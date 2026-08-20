using NuShaders.Formats.BPR;
using NuShaders.Formats.Hashing;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.Unit;

public class BPRShaderDecodeTests
{
    [Fact]
    public void Decodes_specular_1bit_doublesided_shader()
    {
        if (!ReferencePaths.Available) return;
        var path = Path.Combine(ReferencePaths.BPRShaderDir, "E3BF57AD.dat");
        if (!File.Exists(path)) return;

        var shader = BPRShaderResource.Read(File.ReadAllBytes(path));
        Assert.Equal(2, shader.NumTechniques);

        var d = shader.Decode();
        Assert.Equal("Specular_1Bit_Doublesided", d.Name);
        Assert.Equal(18, d.Constants.Count);

        Assert.Equal(2, d.Techniques.Count);
        Assert.Equal("Default", d.Techniques[0].Name);
        Assert.Equal("ZOnly1BitDoubleSided", d.Techniques[1].Name);

        Assert.Equal(3, d.Techniques[0].Samplers.Count);
        var shadow = d.Techniques[0].Samplers.Single(s => s.Name == "shadowMapSamplerHighDetail");
        Assert.Equal(15, shadow.Channel);

        // Constant name hashes are ~CRC32(name); the 3 material constants carry instance-data defaults.
        var mat = d.Constants.Single(c => c.Name == "materialDiffuse");
        Assert.Equal(CRC32.JamCRC("materialDiffuse"), mat.NameHash);
        Assert.Equal(new[] { 1f, 1f, 1f, 1f }, mat.InstanceData);

        var specPower = d.Constants.Single(c => c.Name == "SpecularPower");
        Assert.Equal(12f, specPower.InstanceData![0]);

        var specularity = d.Constants.Single(c => c.Name == "Specularity");
        Assert.Equal(1.5f, specularity.InstanceData![0]);
    }

    [Fact]
    public void AddSampler_appends_to_technique_leaving_rest_intact()
    {
        if (!ReferencePaths.Available) return;
        var path = Path.Combine(ReferencePaths.BPRShaderDir, "E3BF57AD.dat");
        if (!File.Exists(path)) return;

        var stock = BPRShaderResource.Read(File.ReadAllBytes(path));
        var before = stock.Decode();

        byte[] edited = BPRShaderResource.AddSampler(stock.ToBytes(), 0, "ReflectionTextureSampler", 13);
        var after = BPRShaderResource.Read(edited).Decode();

        // Default technique gained exactly the new sampler; originals preserved (incl. slots).
        Assert.Equal(before.Techniques[0].Samplers.Count + 1, after.Techniques[0].Samplers.Count);
        var added = after.Techniques[0].Samplers.Single(s => s.Name == "ReflectionTextureSampler");
        Assert.Equal(13, added.Channel);
        foreach (var s in before.Techniques[0].Samplers)
            Assert.Equal(s.Channel, after.Techniques[0].Samplers.Single(x => x.Name == s.Name).Channel);

        // Everything else is untouched: other technique, constants, name.
        Assert.Equal(before.Name, after.Name);
        Assert.Equal(before.Constants.Count, after.Constants.Count);
        Assert.Equal(before.Techniques[1].Samplers.Count, after.Techniques[1].Samplers.Count);
        var matBefore = before.Constants.Single(c => c.Name == "materialDiffuse");
        var matAfter = after.Constants.Single(c => c.Name == "materialDiffuse");
        Assert.Equal(matBefore.NameHash, matAfter.NameHash);
        Assert.Equal(matBefore.InstanceData, matAfter.InstanceData);
    }
}
