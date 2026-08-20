using NuShaders.Formats.BPR;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.RoundTrip;

public class BPRShaderRoundTripTests
{
    [Fact]
    public void All_reference_shaders_round_trip()
    {
        if (!ReferencePaths.Available) return;
        var dir = ReferencePaths.BPRShaderDir;
        if (!Directory.Exists(dir)) return;

        var files = Directory.GetFiles(dir, "*.dat"); // excludes *_imports.yaml
        Assert.NotEmpty(files);

        var failures = new List<string>();
        foreach (var file in files)
        {
            var original = File.ReadAllBytes(file);
            byte[] rebuilt;
            try
            {
                rebuilt = BPRShaderResource.Read(original).ToBytes();
            }
            catch (Exception ex)
            {
                failures.Add($"{Path.GetFileName(file)}: parse threw {ex.GetType().Name}: {ex.Message}");
                continue;
            }

            // VS/PS import slots are already 0 on disk and ToBytes also zeros them -> direct compare.
            if (!original.AsSpan().SequenceEqual(rebuilt))
                failures.Add($"{Path.GetFileName(file)}: {ByteDiff.Describe(original, rebuilt)}");
        }

        Assert.True(failures.Count == 0,
            $"{failures.Count}/{files.Length} shaders failed round-trip:\n" + string.Join("\n", failures.Take(15)));
    }
}
