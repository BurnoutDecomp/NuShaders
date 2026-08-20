using NuShaders.Formats.BPR;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.RoundTrip;

public class BPRSPBRoundTripTests
{
    [Fact]
    public void All_reference_primaries_round_trip()
    {
        if (!ReferencePaths.Available) return;
        var dir = ReferencePaths.BPRShaderProgramBufferDir;
        if (!Directory.Exists(dir)) return;

        var files = Directory.GetFiles(dir, "*_primary.dat");
        Assert.NotEmpty(files);

        var failures = new List<string>();
        foreach (var file in files)
        {
            var original = File.ReadAllBytes(file);
            byte[] rebuilt;
            try
            {
                rebuilt = BPRShaderProgramBuffer.ReadPrimary(original).ToBytes();
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(file)}: parse threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            int nb = original[0x1C];
            int descOff = BPRShaderProgramBuffer.DescriptorOffset(nb);
            (int, int)[] masks =
            [
                (BPRShaderProgramBuffer.HeaderRuntimePtrOffset, 4),
                (descOff + BPRShaderProgramBuffer.DescriptorRuntimePtrOffset, 4),
            ];
            var a = ByteDiff.WithRangesZeroed(original, masks);
            var b = ByteDiff.WithRangesZeroed(rebuilt, masks);
            if (!a.AsSpan().SequenceEqual(b))
                failures.Add($"{Path.GetFileName(file)}: {ByteDiff.Describe(a, b)}");
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count}/{files.Length} primaries failed round-trip:\n" + string.Join("\n", failures.Take(15)));
    }
}
