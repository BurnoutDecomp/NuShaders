using NuShaders.Formats.Hashing;

namespace NuShaders.Tests.Unit;

public class CRC32Tests
{
    // Burnout constant-name hashes verified against the real Shader (0x32) resource.
    [Theory]
    [InlineData("materialDiffuse", 0xF6E27CAAu)]
    [InlineData("world", 0xC588EEBCu)]
    [InlineData("SpecularPower", 0x81E0E773u)]
    [InlineData("Specularity", 0x4A73909Fu)]
    public void JamCRC_matches_known_constant_hashes(string name, uint expected)
        => Assert.Equal(expected, CRC32.JamCRC(name));

    [Fact]
    public void Standard_crc32_check_value()
        => Assert.Equal(0xCBF43926u, CRC32.Compute("123456789")); // canonical CRC-32 check value

    [Fact]
    public void JamCRC_is_complement_of_standard()
        => Assert.Equal(~CRC32.Compute("Roughness"), CRC32.JamCRC("Roughness"));
}
