using NuShaders.Imaging;

namespace NuShaders.Tests.Unit;

public class ColorConvertTests
{
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(1f, 255)]
    [InlineData(0.5f, 128)]      // round-half-up: 0.5*255 = 127.5 -> 128
    [InlineData(2f, 255)]        // HDR clamps for display
    [InlineData(-1f, 0)]
    public void ToByte_clamps_and_rounds(float v, int expected)
        => Assert.Equal((byte)expected, ColorConvert.ToByte(v));

    [Fact]
    public void Hex_round_trips_for_LDR_colors()
    {
        Assert.Equal("FF0000", ColorConvert.ToHex(1f, 0f, 0f));
        Assert.Equal("00FF00", ColorConvert.ToHex(0f, 1f, 0f));
        Assert.Equal("FFFFFF", ColorConvert.ToHex(1f, 1f, 1f));

        Assert.True(ColorConvert.TryParseHex("#80C0FF", out float r, out float g, out float b));
        Assert.Equal(0x80 / 255f, r, 5);
        Assert.Equal(0xC0 / 255f, g, 5);
        Assert.Equal(0xFF / 255f, b, 5);

        // byte -> hex -> byte is stable
        Assert.True(ColorConvert.TryParseHex(ColorConvert.ToHex(0.25f, 0.5f, 0.75f), out float r2, out float g2, out float b2));
        Assert.Equal(ColorConvert.ToByte(0.25f), ColorConvert.ToByte(r2));
        Assert.Equal(ColorConvert.ToByte(0.5f), ColorConvert.ToByte(g2));
        Assert.Equal(ColorConvert.ToByte(0.75f), ColorConvert.ToByte(b2));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("12345")]      // too short
    [InlineData("GGGGGG")]     // not hex
    [InlineData("1234567")]    // too long
    public void TryParseHex_rejects_malformed(string? input)
        => Assert.False(ColorConvert.TryParseHex(input, out _, out _, out _));
}
