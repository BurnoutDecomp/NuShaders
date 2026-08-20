using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.RoundTrip;

public class MetaYAMLRoundTripTests
{
    [Fact]
    public void Reference_meta_round_trips_byte_identical()
    {
        if (!ReferencePaths.Available) return;
        if (!File.Exists(ReferencePaths.BPRMetaPath)) return;

        string original = File.ReadAllText(ReferencePaths.BPRMetaPath);
        string rebuilt = MetaYAML.Read(original).ToYAML();
        Assert.Equal(original, rebuilt);
    }

    [Fact]
    public void Upsert_inserts_texture_and_texturestate_in_sorted_position_with_correct_format()
    {
        const string seed =
            "bundle:\n  platform: 1\n  compressed: true\nresources:\n" +
            "  0x10000000:\n    type: 0x32\n    alignment:\n      - 0x10\n" +
            "  0xf0000000:\n    type: 0x32\n    alignment:\n      - 0x10\n";

        var meta = MetaYAML.Read(seed);
        // A Texture (type 0x0) and a TextureState (type 0xe) with mid-range ids.
        meta.Upsert(new MetaYAML.Entry(0x80000000, BPRTextureResource.MetaType,
            BPRTextureResource.MetaSecondaryMemoryType, BPRTextureResource.MetaAlignment));
        meta.Upsert(new MetaYAML.Entry(0x90000000, BPRTextureState.MetaType, null, BPRTextureState.MetaAlignment));

        string expected =
            "bundle:\n  platform: 1\n  compressed: true\nresources:\n" +
            "  0x10000000:\n    type: 0x32\n    alignment:\n      - 0x10\n" +
            "  0x80000000:\n    type: 0x0\n    secondaryMemoryType: 1\n    alignment:\n      - 0x4\n      - 0x10\n" +
            "  0x90000000:\n    type: 0xe\n    alignment:\n      - 0x10\n" +
            "  0xf0000000:\n    type: 0x32\n    alignment:\n      - 0x10\n";
        Assert.Equal(expected, meta.ToYAML());
    }

    [Fact]
    public void Upsert_replaces_existing_id()
    {
        const string seed = "bundle:\n  platform: 1\nresources:\n  0x00000005:\n    type: 0x32\n    alignment:\n      - 0x10\n";
        var meta = MetaYAML.Read(seed);
        meta.Upsert(new MetaYAML.Entry(0x00000005, 0x0, 1, [0x4, 0x10]));
        Assert.Single(meta.ResourceIDs);
        Assert.Contains("type: 0x0", meta.ToYAML());
        Assert.DoesNotContain("type: 0x32", meta.ToYAML());
    }
}
