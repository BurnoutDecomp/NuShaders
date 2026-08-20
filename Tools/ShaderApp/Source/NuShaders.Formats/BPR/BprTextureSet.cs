using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;

namespace NuShaders.Formats.BPR;

/// <summary>An already block-encoded texture surface (format + dimensions + raw BC mip blocks).</summary>
public sealed record EncodedSurface(DXGIFormat Format, int Width, int Height, int MipLevels, byte[] BCBlocks);

/// <summary>One resource ready to write to a bundle: the .dat blob(s), optional imports, and its .meta.yaml descriptor.</summary>
public sealed record EmittedResource(
    ResourceID Id, string Name, int MetaType, int? SecondaryMemoryType, int[] Alignment,
    byte[] Primary, byte[]? Secondary, ImportsYAML? Imports);

/// <summary>Inputs for a packed PBR texture set: a material name + the two encoded surfaces (diffuse, spec/ORMH).</summary>
public sealed record TextureSetInputs(
    string MaterialName, EncodedSurface Diffuse, EncodedSurface Spec,
    SamplerParams? Sampler = null,
    ResourceID? DiffuseIDOverride = null, ResourceID? SpecIDOverride = null);

/// <summary>
/// Turns the two encoded surfaces of a PBR material into the four bundle resources the game needs:
/// a Texture + a TextureState for the diffuse, and the same for the spec/ORMH map. Pure (no image deps):
/// it takes BC blocks in and produces resource bytes + ids + meta descriptors out.
/// </summary>
public static class BPRTextureSet
{
    public const string DiffuseMapSuffix = "Diffuse";
    public const string SpecMapSuffix = "Spec";

    public static IReadOnlyList<EmittedResource> Build(TextureSetInputs inputs)
    {
        var sampler = inputs.Sampler ?? BPRTextureState.Default;
        var result = new List<EmittedResource>();
        result.AddRange(BuildOne(inputs.MaterialName, DiffuseMapSuffix, inputs.Diffuse, sampler, inputs.DiffuseIDOverride));
        result.AddRange(BuildOne(inputs.MaterialName, SpecMapSuffix, inputs.Spec, sampler, inputs.SpecIDOverride));
        return result;
    }

    /// <summary>Build one Texture + its TextureState (for authoring a single material slot inline).</summary>
    public static IReadOnlyList<EmittedResource> BuildSingle(string material, string mapSuffix, EncodedSurface surf, SamplerParams? sampler = null)
        => BuildOne(material, mapSuffix, surf, sampler ?? BPRTextureState.Default, null).ToList();

    private static IEnumerable<EmittedResource> BuildOne(
        string material, string mapSuffix, EncodedSurface surf, SamplerParams sampler, ResourceID? texIDOverride)
    {
        string gamedbName = BurnoutResourceName.SynthesizeGamedbName(material, mapSuffix);
        ResourceID texID = texIDOverride ?? BurnoutResourceName.TextureID(gamedbName);
        ResourceID stateID = BurnoutResourceName.TextureStateID(gamedbName);

        var (primary, secondary) = BPRTextureResource.Build(surf.Format, surf.Width, surf.Height, surf.MipLevels, surf.BCBlocks);
        yield return new EmittedResource(texID, gamedbName, BPRTextureResource.MetaType,
            BPRTextureResource.MetaSecondaryMemoryType, BPRTextureResource.MetaAlignment, primary, secondary, null);

        byte[] stateDat = BPRTextureState.Build(sampler);
        yield return new EmittedResource(stateID, BurnoutResourceName.TextureStateName(gamedbName),
            BPRTextureState.MetaType, null, BPRTextureState.MetaAlignment, stateDat, null, BPRTextureState.BuildImports(texID));
    }
}
