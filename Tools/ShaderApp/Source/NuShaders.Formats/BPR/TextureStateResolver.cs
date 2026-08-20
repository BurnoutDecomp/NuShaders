using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;

namespace NuShaders.Formats.BPR;

/// <summary>A TextureState decoded into the pair the UI cares about: which Texture it points at + its sampler settings.</summary>
public sealed record DecodedTextureState(ResourceID Id, ResourceID? TextureID, SamplerParams Sampler);

/// <summary>
/// Lets the UI work in terms of (Texture + sampler settings) and keeps the TextureState an implementation detail:
/// decode an existing state's texture ref + sampler; find an existing state matching a (texture, settings) pair —
/// reusing the game's own states or ones we made; or build a new state with a deterministic, content-derived name
/// when nothing matches. A TextureState's id is name-derived and its sampler bytes are independent (see
/// <see cref="BPRTextureState"/>), so a tool-named state carrying any sampler is a valid resource.
/// </summary>
public static class TextureStateResolver
{
    /// <summary>Read a state's sampler (from its .dat) + the Texture it references (from its imports @0x38).</summary>
    public static DecodedTextureState Decode(CatalogResource textureState)
    {
        var sampler = BPRTextureState.ReadSampler(File.ReadAllBytes(textureState.DatPath!));
        ResourceID? tex = null;
        if (textureState.ImportsPath is not null && File.Exists(textureState.ImportsPath))
            foreach (var e in ImportsYAML.Read(File.ReadAllText(textureState.ImportsPath)).Entries)
                if (e.Offset == (uint)BPRTextureState.TextureRefOffset) { tex = new ResourceID(e.Id); break; }
        return new DecodedTextureState(textureState.Id, tex, sampler);
    }

    /// <summary>An existing TextureState referencing <paramref name="textureID"/> with exactly <paramref name="sampler"/>, or null.</summary>
    public static CatalogResource? FindMatch(BundleCatalog catalog, ResourceID textureID, SamplerParams sampler)
    {
        foreach (var ts in catalog.TextureStates)
        {
            if (ts.DatPath is null) continue;
            DecodedTextureState d;
            try { d = Decode(ts); } catch { continue; }
            if (d.TextureID?.Value == textureID.Value && d.Sampler == sampler) return ts;
        }
        return null;
    }

    /// <summary>Deterministic name for a (texture, settings) state — same inputs always yield the same id (dedups tool-made states).</summary>
    public static string Name(ResourceID textureID, SamplerParams p)
        => $"TEXTURESTATE_mod_{textureID.Value:x8}_{Token(p)}";

    /// <summary>Build the resource for a NEW TextureState (caller merges into the bundle + registers it).</summary>
    public static EmittedResource Create(ResourceID textureID, SamplerParams sampler)
    {
        string name = Name(textureID, sampler);
        return new EmittedResource(BurnoutResourceName.ResourceIDFor(name), name,
            BPRTextureState.MetaType, null, BPRTextureState.MetaAlignment,
            BPRTextureState.Build(sampler), null, BPRTextureState.BuildImports(textureID));
    }

    private static string Token(SamplerParams p) =>
        $"{(uint)p.AddressU}{(uint)p.AddressV}{(uint)p.AddressW}_{(uint)p.MagFilter}{(uint)p.MinFilter}{(uint)p.MipFilter}_" +
        $"{p.MaxAnisotropy}_{BitConverter.SingleToUInt32Bits(p.MinLod):x}_{BitConverter.SingleToUInt32Bits(p.MaxLod):x}_" +
        $"{BitConverter.SingleToUInt32Bits(p.MipLodBias):x}_{p.Comparison}_{(p.WhiteBorder ? 1 : 0)}";
}
