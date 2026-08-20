using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;

namespace NuShaders.Formats.BPR;

/// <summary>A param value to write into a rebuilt material, matched to the template's constant by JAMCRC name-hash.</summary>
public sealed record MaterialParamValue(uint NameHash, float[] Value);

/// <summary>A texture-state assignment for a rebuilt material, matched to the template's sampler by channel.</summary>
public sealed record MaterialTextureAssign(int Channel, ResourceID TextureStateID);

/// <summary>
/// Rebuilds a material for a (different) shader by CLONING a template material that already uses that shader — so the
/// VS/PS constant blocks and sampler array are the engine-proven layout for it — then writing the user's values into
/// the clone (params by name-hash, textures + render-mode by channel/technique). This is the safe shader-swap path;
/// a fully de-novo build (no template) is intentionally NOT done here (it risks the engine load transform). Output is
/// flagged "verify in-game" by the caller.
/// </summary>
public static class BPRMaterialBuilder
{
    public static (byte[] Dat, ImportsYAML Imports) BuildFromTemplate(
        byte[] templateDat, ImportsYAML templateImports,
        ResourceID targetID, ResourceID shaderID,
        IReadOnlyList<MaterialParamValue> values,
        IReadOnlyList<MaterialTextureAssign> textures,
        IReadOnlyList<ResourceID?> materialStatesByTechnique)
    {
        var mat = BPRMaterialResource.Read(templateDat);
        var d = mat.Decode();
        var imports = new ImportsYAML();
        foreach (var e in templateImports.Entries) imports.Entries.Add(e);   // start from the template's import map

        // Params: write each value onto the template's matching constant (by name-hash), in its VS/PS block + index.
        var valueByHash = new Dictionary<uint, float[]>();
        foreach (var v in values) valueByHash[v.NameHash] = v.Value;
        WriteConstants(d.VSConstants, false);
        WriteConstants(d.PSConstants, true);
        void WriteConstants(IReadOnlyList<MaterialConstant> consts, bool pixel)
        {
            for (int i = 0; i < consts.Count; i++)
                if (valueByHash.TryGetValue(consts[i].NameHash, out var val))
                    mat.SetConstant(pixel, i, val);
        }

        // Textures: repoint each template sampler's TextureState slot (matched by channel).
        var tsByChannel = new Dictionary<int, ResourceID>();
        foreach (var t in textures) tsByChannel[t.Channel] = t.TextureStateID;
        foreach (var s in d.Samplers)
            if (tsByChannel.TryGetValue(s.Channel, out var tsID))
                BPRMaterialResource.RepointImport(imports, s.TextureStateSlotOffset, tsID);

        // Render mode: repoint each template technique's MaterialState slot (by technique index).
        for (int t = 0; t < d.Techniques.Count && t < materialStatesByTechnique.Count; t++)
            if (materialStatesByTechnique[t] is { } msID)
                BPRMaterialResource.RepointImport(imports, d.Techniques[t].MaterialStateSlotOffset, msID);

        mat.SetNameHash(targetID.Value);
        BPRMaterialResource.RepointImport(imports, BPRMaterialResource.ShaderSlotOffset, shaderID);
        return (mat.ToBytes(), imports);
    }
}
