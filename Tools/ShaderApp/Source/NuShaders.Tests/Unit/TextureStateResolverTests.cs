using NuShaders.Formats.BPR;
using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;
using NuShaders.Tests.TestSupport;

namespace NuShaders.Tests.Unit;

public class TextureStateResolverTests
{
    private static BundleCatalog? Shaders()
    {
        if (!ReferencePaths.Available) return null;
        string b = Path.Combine(ReferencePaths.PBRMatsDir, "SHADERS");
        return Directory.Exists(b) && File.Exists(Path.Combine(b, ".meta.yaml")) ? BundleCatalog.Open(b) : null;
    }

    [Fact]
    public void Decode_and_FindMatch_on_a_real_bundle()
    {
        var cat = Shaders(); if (cat is null) return;
        var ts = cat.TextureStates.FirstOrDefault(t => t.DatPath is not null && t.ImportsPath is not null);
        if (ts is null) return;

        var d = TextureStateResolver.Decode(ts);
        Assert.NotNull(d.TextureID);   // a state points at a texture

        var match = TextureStateResolver.FindMatch(cat, d.TextureID!.Value, d.Sampler);
        Assert.NotNull(match);
        var dm = TextureStateResolver.Decode(match!);
        Assert.Equal(d.TextureID!.Value.Value, dm.TextureID!.Value.Value);
        Assert.Equal(d.Sampler, dm.Sampler);
    }

    [Fact]
    public void Create_is_deterministic_and_varies_by_settings()
    {
        var texID = new ResourceID(0x11b669ed);

        var a = TextureStateResolver.Create(texID, BPRTextureState.Default);
        var b = TextureStateResolver.Create(texID, BPRTextureState.Default);
        Assert.Equal(a.Id.Value, b.Id.Value);                       // same (texture, settings) -> same id
        Assert.Equal(BPRTextureState.MetaType, a.MetaType);
        Assert.Equal(BPRTextureState.FileSize, a.Primary.Length);
        Assert.Contains(a.Imports!.Entries, e => e.Offset == BPRTextureState.TextureRefOffset && e.Id == texID.Value);

        var clamped = TextureStateResolver.Create(texID, BPRTextureState.Default with { AddressU = TextureAddressMode.Clamp });
        Assert.NotEqual(a.Id.Value, clamped.Id.Value);              // different settings -> different id

        var otherTex = TextureStateResolver.Create(new ResourceID(0xdeadbeef), BPRTextureState.Default);
        Assert.NotEqual(a.Id.Value, otherTex.Id.Value);            // different texture -> different id
    }
}
