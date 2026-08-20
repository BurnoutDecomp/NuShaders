using NuShaders.Formats.BPR;

namespace NuShaders.Tests.Unit;

/// <summary>Locks the resource-id hash (crc32 of the lowercased gamedb name) against known .debug.xml pairs.</summary>
public class BurnoutResourceNameTests
{
    private const string AODefault = "gamedb://burnout5/Playground/BenTest/AODefault.TextureConfig2d?ID=203785";
    private const string TSlice = "gamedb://burnout5/Burnout/Content_World/Images_Final/Cube_Maps/T-slice.TextureConfigCube?ID=321188";

    [Fact]
    public void Texture_id_matches_debug_xml()
        => Assert.Equal(0x11b669edu, BurnoutResourceName.TextureID(AODefault).Value);

    [Fact]
    public void TextureState_id_matches_debug_xml()
        => Assert.Equal(0x90ae7652u, BurnoutResourceName.TextureStateID(AODefault).Value);

    [Fact]
    public void Cube_texturestate_id_matches_debug_xml()
        => Assert.Equal(0xd4d1fc61u, BurnoutResourceName.TextureStateID(TSlice).Value);

    [Fact]
    public void Hash_lowercases_the_name()
        => Assert.Equal(BurnoutResourceName.TextureID(AODefault).Value,
                        BurnoutResourceName.TextureID(AODefault.ToUpperInvariant()).Value);

    [Fact]
    public void Synthesized_names_are_deterministic_and_distinct_per_map()
    {
        string a = BurnoutResourceName.SynthesizeGamedbName("CorrugatedSteel005", "Diffuse");
        string b = BurnoutResourceName.SynthesizeGamedbName("CorrugatedSteel005", "Spec");
        Assert.Equal(a, BurnoutResourceName.SynthesizeGamedbName("CorrugatedSteel005", "Diffuse"));
        Assert.NotEqual(BurnoutResourceName.TextureID(a).Value, BurnoutResourceName.TextureID(b).Value);
    }
}
