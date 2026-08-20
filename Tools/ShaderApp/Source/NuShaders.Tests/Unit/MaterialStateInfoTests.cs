using NuShaders.Formats.BPR;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.Unit;

public class MaterialStateInfoTests
{
    [Theory]
    [InlineData(1, true)]    // D3D11_CULL_NONE  → nothing culled → two-sided
    [InlineData(2, false)]   // D3D11_CULL_FRONT → single-sided
    [InlineData(3, false)]   // D3D11_CULL_BACK  → single-sided (the common case)
    public void CullMode_at_0x8C_drives_two_sided(byte cull, bool expectTwoSided)
    {
        var dat = new byte[192];
        dat[0x8C] = cull;
        Assert.Equal(expectTwoSided, MaterialStateDecoder.Decode(dat)!.TwoSided);
    }

    [Fact]
    public void Real_states_match_the_renderdoc_cull()
    {
        if (!ReferencePaths.Available) return;
        // Ground truth (RenderDoc DX11 captures + bytes): single-sided shader = Cull Back, doublesided = Cull None.
        Check(Path.Combine(ReferencePaths.PBRMatsDir, "SHADERS", "MaterialState", "D4659F9B.dat"), expectTwoSided: false);    // MaterialState671619170
        Check(Path.Combine(ReferencePaths.PBRMatsDir, "GLOBALPROPS", "MaterialState", "0BA364FE.dat"), expectTwoSided: true);

        static void Check(string path, bool expectTwoSided)
        {
            if (!File.Exists(path)) return;
            var info = MaterialStateDecoder.Decode(File.ReadAllBytes(path));
            Assert.NotNull(info);
            Assert.Equal(expectTwoSided, info!.TwoSided);
        }
    }
}
