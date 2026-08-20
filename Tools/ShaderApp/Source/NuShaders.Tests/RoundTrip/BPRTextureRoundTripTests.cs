using NuShaders.Formats.BPR;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.RoundTrip;

public class BPRTextureRoundTripTests
{
    // The placed-texture tail (0x30..0x3F) is PC-unused garbage in stock files; we zero it and mask it here.
    private static readonly (int, int)[] Masks = [(0x30, 0x10)];

    [Fact]
    public void All_reference_texture_primaries_round_trip()
    {
        if (!ReferencePaths.Available) return;
        var dir = ReferencePaths.BPRTextureDir;
        if (!Directory.Exists(dir)) return;

        var files = Directory.GetFiles(dir, "*_primary.dat");
        Assert.NotEmpty(files);

        var failures = new List<string>();
        foreach (var file in files)
        {
            var original = File.ReadAllBytes(file);
            var rebuilt = BPRTextureResource.ReadPrimary(original).ToBytes();

            var a = ByteDiff.WithRangesZeroed(original, Masks);
            var b = ByteDiff.WithRangesZeroed(rebuilt, Masks);
            if (!a.AsSpan().SequenceEqual(b))
                failures.Add($"{Path.GetFileName(file)}: {ByteDiff.Describe(a, b)}");
        }

        Assert.True(failures.Count == 0, $"{failures.Count}/{files.Length} failed:\n" + string.Join("\n", failures));
    }

    [Fact]
    public void Secondary_length_matches_header_format_and_mips()
    {
        if (!ReferencePaths.Available) return;
        var dir = ReferencePaths.BPRTextureDir;
        if (!Directory.Exists(dir)) return;

        foreach (var primaryPath in Directory.GetFiles(dir, "*_primary.dat"))
        {
            var secondaryPath = primaryPath.Replace("_primary.dat", "_secondary.dat");
            if (!File.Exists(secondaryPath)) continue;

            var tex = BPRTextureResource.ReadPrimary(File.ReadAllBytes(primaryPath));
            long expected = tex.ExpectedSecondaryBytes();
            long actual = new FileInfo(secondaryPath).Length;
            string name = Path.GetFileName(primaryPath);

            Assert.True(expected > 0, $"{name}: unrecognised format {tex.Format} ({(uint)tex.Format})");
            // Secondary = contiguous mip chain zero-padded to 0x100 (verified by byte inspection of 828AA32F).
            long paddedExpected = (expected + 0xFF) & ~0xFFL;
            Assert.True(paddedExpected == actual,
                $"{name}: {tex.Format} {tex.Width}x{tex.Height} mips={tex.MipLevels} expected {expected}→pad {paddedExpected} bytes, file {actual}");
        }
    }
}
