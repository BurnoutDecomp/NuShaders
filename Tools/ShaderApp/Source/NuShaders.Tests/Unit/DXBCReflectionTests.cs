using NuShaders.Formats.DXBC;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.Unit;

public class DXBCReflectionTests
{
    [Fact]
    public void Reflects_specular_1bit_pixel_shader()
    {
        if (!ReferencePaths.Available) return;
        var path = Path.Combine(ReferencePaths.BPRShaderProgramBufferDir, "A3A50BAB_secondary.dat");
        if (!File.Exists(path)) return;

        var refl = RdefReflection.Instance.Reflect(File.ReadAllBytes(path));

        Assert.Equal(ShaderKind.Pixel, refl.Kind);

        Assert.Equal(7, refl.Bindings.Count);
        AssertBinding(refl, "DiffuseTextureSampler", ResourceKind.Sampler, 0);
        AssertBinding(refl, "DiffuseTextureSamplerTexture", ResourceKind.Texture, 0);
        AssertBinding(refl, "shadowMapSamplerHighDetail", ResourceKind.Sampler, 15);
        AssertBinding(refl, "shadowMapSamplerHighDetailTexture", ResourceKind.Texture, 15);
        AssertBinding(refl, "$Globals", ResourceKind.CBuffer, 0);

        var cb = Assert.Single(refl.Cbuffers);
        Assert.Equal("$Globals", cb.Name);
        Assert.Equal(800, cb.SizeBytes);
        Assert.Equal(22, cb.Variables.Count);

        var mat = cb.Variables.Single(v => v.Name == "materialDiffuse");
        Assert.Equal(768, mat.ByteOffset);
        Assert.Equal(16, mat.SizeBytes);
        Assert.Equal(1, mat.PackedRows);
        Assert.Equal(4, mat.Columns);

        var w2l = cb.Variables.Single(v => v.Name == "ShadowMap_WorldToLight");
        Assert.Equal(160, w2l.ByteOffset);
        Assert.Equal(192, w2l.SizeBytes);
        Assert.Equal(3, w2l.ElementCount);
        Assert.Equal(12, w2l.PackedRows); // 4 rows × 3 array elements
        Assert.Equal(4, w2l.Columns);
    }

    private static void AssertBinding(DXBCReflection refl, string name, ResourceKind kind, int register)
    {
        var b = refl.Bindings.Single(x => x.Name == name);
        Assert.Equal(kind, b.Kind);
        Assert.Equal(register, b.Register);
    }
}
