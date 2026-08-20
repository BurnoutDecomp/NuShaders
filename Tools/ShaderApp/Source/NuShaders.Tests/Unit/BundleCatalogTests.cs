using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.Unit;

public class BundleCatalogTests
{
    [Theory]
    [InlineData("gamedb://burnout5/Burnout/Content_Vehicles/DefaultMaterial.Material?ID=167850", "DefaultMaterial")]
    [InlineData("gamedb://burnout5/Burnout/Content_Vehicles/defaulttexture_tif.texconfig.TextureConfig2d?ID=167852", "defaulttexture_tif.texconfig")]
    [InlineData("gamedb://burnout5/Shaders/Vehicle_Opaque_PaintGloss_Textured.fx.Shader?ID=239231", "Vehicle_Opaque_PaintGloss_Textured.fx")]
    [InlineData("MaterialState671619170", "MaterialState671619170")]
    [InlineData("MyCustomName", "MyCustomName")]
    public void ShortName_extracts_the_meaningful_tail(string name, string expected)
        => Assert.Equal(expected, new CatalogResource(new ResourceID(0x1234), 0x1, name, null, null).ShortName);

    [Fact]
    public void ShortName_falls_back_to_hex_when_unnamed()
        => Assert.Equal("0x4036706f", new CatalogResource(new ResourceID(0x4036706f), 0x1, null, null, null).ShortName);

    private static string? TrkBundle()
    {
        if (!ReferencePaths.Available) return null;
        string b = Path.Combine(ReferencePaths.PBRMatsDir, "Test Shader TRK", "TRK_UNIT148_GR");
        return Directory.Exists(b) && File.Exists(Path.Combine(b, ".meta.yaml")) ? b : null;
    }

    [Fact]
    public void Catalog_groups_resources_by_type()
    {
        var b = TrkBundle(); if (b is null) return;
        var cat = BundleCatalog.Open(b);
        Assert.NotEmpty(cat.Materials);
        Assert.NotEmpty(cat.MaterialStates);
        Assert.NotEmpty(cat.TextureStates);
        Assert.Contains(cat.Materials, m => m.DatPath is not null && File.Exists(m.DatPath));
    }

    [Fact]
    public void Bind_resolves_samplers_states_and_params_without_throwing()
    {
        var b = TrkBundle(); if (b is null) return;
        var cat = BundleCatalog.Open(b);
        var mat = cat.Materials.First(m => m.DatPath is not null);
        var bound = ShaderParameterBinding.Bind(cat, mat);
        Assert.NotEmpty(bound.MaterialStates);
        Assert.All(bound.Parameters, p => Assert.Equal(p.Size * 4, p.CurrentValue.Length));
    }
}
