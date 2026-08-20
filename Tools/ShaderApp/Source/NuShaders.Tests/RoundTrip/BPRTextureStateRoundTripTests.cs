using NuShaders.Formats.BPR;
using NuShaders.Formats.Model;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.RoundTrip;

public class BPRTextureStateRoundTripTests
{
    private const int SamplerFieldsBytes = 0x2D;   // 0x00..0x2C are the real sampler fields; 0x2D.. is padding/runtime/import

    [Fact]
    public void Default_sampler_reproduces_the_stock_texturestate_bytes()
    {
        if (!ReferencePaths.Available) return;
        var dir = ReferencePaths.BPRTextureStateDir;
        if (!Directory.Exists(dir)) return;

        byte[] built = BPRTextureState.Build(BPRTextureState.Default);
        Assert.Equal(BPRTextureState.FileSize, built.Length);

        var failures = new List<string>();
        foreach (var file in Directory.GetFiles(dir, "*.dat"))
        {
            var original = File.ReadAllBytes(file);
            // Compare only the sampler fields (0x00..0x2C); 0x2D.. is padding/refcount/iface/texture-ref.
            if (!built.AsSpan(0, SamplerFieldsBytes).SequenceEqual(original.AsSpan(0, SamplerFieldsBytes)))
                failures.Add($"{Path.GetFileName(file)}: {ByteDiff.Describe(built.AsSpan(0, SamplerFieldsBytes), original.AsSpan(0, SamplerFieldsBytes))}");

            // Round-trip the sampler through Read → Build.
            var reread = BPRTextureState.Build(BPRTextureState.ReadSampler(original));
            if (!reread.AsSpan(0, SamplerFieldsBytes).SequenceEqual(original.AsSpan(0, SamplerFieldsBytes)))
                failures.Add($"{Path.GetFileName(file)}: re-read sampler mismatch");
        }
        // All stock TextureStates here use the default sampler; both should match.
        Assert.True(failures.Count == 0, string.Join("\n", failures));
    }

    [Fact]
    public void Imports_yaml_matches_reference()
    {
        if (!ReferencePaths.Available) return;
        var dir = ReferencePaths.BPRTextureStateDir;
        var impPath = Path.Combine(dir, "90AE7652_imports.yaml");
        if (!File.Exists(impPath)) return;

        // 90AE7652 imports the AODefault texture 0x11b669ed at offset 0x38.
        string expected = File.ReadAllText(impPath).Replace("\r\n", "\n").TrimEnd('\n');
        string actual = BPRTextureState.BuildImports(new ResourceID(0x11b669ed)).ToYAML();
        Assert.Equal(expected, actual);
    }
}
