using NuShaders.Formats.Hashing;
using NuShaders.Formats.Model;

namespace NuShaders.Formats.BPR;

/// <summary>
/// Burnout bundle resource ids are <c>CRC-32(name.ToLowerInvariant())</c> of the resource's gamedb name.
/// Verified this session: the Texture name <c>gamedb://burnout5/Playground/BenTest/AODefault.TextureConfig2d?ID=203785</c>
/// hashes to <c>0x11b669ed</c>, and its TextureState (the name + the fixed sampler suffix) to <c>0x90ae7652</c>.
/// The sampler suffix on a TextureState name is a FIXED CONSTANT regardless of the actual sampler bytes — the id
/// depends only on the texture name + this suffix; the sampler is written into the binary independently.
/// </summary>
public static class BurnoutResourceName
{
    /// <summary>The constant sampler descriptor appended to every TextureState name (independent of the real sampler).</summary>
    public const string DefaultSamplerSuffix = "_1_1_1_0_0_0_3.40282e+38_1_-1_-3.40282e+38_0";

    /// <summary>Resource id = CRC-32 of the lowercased name.</summary>
    public static ResourceID ResourceIDFor(string name) => new(CRC32.Compute(name.ToLowerInvariant()));

    /// <summary>Texture resource id from its gamedb name.</summary>
    public static ResourceID TextureID(string gamedbName) => ResourceIDFor(gamedbName);

    /// <summary>TextureState name = <c>TEXTURESTATE_</c> + the texture gamedb name + the fixed sampler suffix.</summary>
    public static string TextureStateName(string gamedbName, string samplerSuffix = DefaultSamplerSuffix)
        => "TEXTURESTATE_" + gamedbName + samplerSuffix;

    /// <summary>TextureState resource id (hash of <see cref="TextureStateName"/>).</summary>
    public static ResourceID TextureStateID(string gamedbName, string samplerSuffix = DefaultSamplerSuffix)
        => ResourceIDFor(TextureStateName(gamedbName, samplerSuffix));

    /// <summary>
    /// Synthesize a gamedb name for a NEW user texture from a material name + a map suffix (e.g. "Diffuse").
    /// The path is cosmetic (only the hash matters), but a stable descriptive name keeps the id reproducible
    /// across packs and the .debug.xml meaningful. The numeric <c>?ID=</c> is derived from the path so the same
    /// inputs always yield the same id.
    /// </summary>
    public static string SynthesizeGamedbName(string material, string mapSuffix)
    {
        string path = $"gamedb://burnout5/Mods/{material}/{material}_{mapSuffix}.TextureConfig2d";
        uint configID = CRC32.Compute(path.ToLowerInvariant()) % 1_000_000u;
        return $"{path}?ID={configID}";
    }

    /// <summary>Synthesize a gamedb Material name for a new material (its id = <see cref="ResourceIDFor"/> of this).</summary>
    public static string SynthesizeMaterialName(string material)
    {
        string path = $"gamedb://burnout5/Mods/{material}.Material";
        uint configID = CRC32.Compute(path.ToLowerInvariant()) % 1_000_000u;
        return $"{path}?ID={configID}";
    }
}
