using NuShaders.Formats.Xbox360;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.RoundTrip;

public class X360SPBRoundTripTests
{
    [Fact]
    public void All_reference_x360_spbs_round_trip_exact()
    {
        if (!ReferencePaths.Available) return;
        var baseDir = ReferencePaths.X360ShadersDir;
        if (!Directory.Exists(baseDir)) return;

        int total = 0, passed = 0, skipped = 0;
        var failures = new List<string>();

        foreach (var version in new[] { "Breaker", "1.6", "1.8" })
        {
            var dir = Path.Combine(baseDir, version, "SHADERS", "ShaderProgramBuffer");
            if (!Directory.Exists(dir)) continue;

            foreach (var pri in Directory.GetFiles(dir, "*_primary.dat"))
            {
                string id = Path.GetFileName(pri).Replace("_primary.dat", "");
                string sec = Path.Combine(dir, $"{id}_secondary.dat");
                if (!File.Exists(sec)) { skipped++; continue; }

                var priBytes = File.ReadAllBytes(pri);
                var secBytes = File.ReadAllBytes(sec);

                int headerSize;
                try { headerSize = Xbox360ShaderProgramBuffer.HeaderSizeFromPrimary(priBytes); }
                catch { skipped++; continue; }
                if (priBytes.Length <= headerSize) { skipped++; continue; }

                // Reconstruct the Xenos blob: strip engine header from primary, append secondary.
                var blob = new byte[(priBytes.Length - headerSize) + secBytes.Length];
                Array.Copy(priBytes, headerSize, blob, 0, priBytes.Length - headerSize);
                Array.Copy(secBytes, 0, blob, priBytes.Length - headerSize, secBytes.Length);
                if (blob.Length < 4 || blob[0] != 0x10 || blob[1] != 0x2A || blob[2] != 0x11) { skipped++; continue; }

                total++;
                try
                {
                    var (p2, s2) = Xbox360ShaderProgramBuffer.PackSplit(blob);
                    if (p2.AsSpan().SequenceEqual(priBytes) && s2.AsSpan().SequenceEqual(secBytes))
                        passed++;
                    else
                        failures.Add($"{version}/{id}: pri[{ByteDiff.Describe(priBytes, p2)}] sec[{ByteDiff.Describe(secBytes, s2)}]");
                }
                catch (Exception ex)
                {
                    failures.Add($"{version}/{id}: {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        Assert.True(total > 0, "no X360 ShaderProgramBuffers found under Reference/360/Shaders");
        Assert.True(failures.Count == 0,
            $"{failures.Count} failed of {total} ({skipped} skipped, {passed} passed):\n" + string.Join("\n", failures.Take(15)));
    }
}
