using NuShaders.Formats.Bundle;
using NuShaders.Formats.Model;

namespace NuShaders.Formats.BPR;

/// <summary>An editable material parameter: the shader's human name + default joined to the material's current float[4*Size].</summary>
public sealed record BoundParameter(
    string Name, uint NameHash, int Size, float[] ShaderDefault, float[] CurrentValue,
    bool IsPixel, int MaterialConstIndex, bool ResolvedInShader);

/// <summary>A material sampler joined to the shader's sampler name (by channel) + the assigned TextureState,
/// resolved through to the actual Texture it references and that state's sampler settings.</summary>
public sealed record BoundSampler(
    int Index, short Channel, TexturePurpose Purpose, string? ShaderSamplerName,
    ResourceID? AssignedTextureState, ResourceID? TextureID, SamplerParams? Sampler, int TextureStateSlotOffset);

/// <summary>A material's full editable surface: shader, params, samplers, per-technique material-state refs.</summary>
public sealed record BoundMaterial(
    ResourceID MaterialID, ResourceID? ShaderID, string? ShaderName, bool ShaderFound,
    IReadOnlyList<BoundParameter> Parameters, IReadOnlyList<BoundSampler> Samplers,
    IReadOnlyList<(int TechniqueIndex, int SlotOffset, ResourceID? MaterialStateID)> MaterialStates);

/// <summary>
/// Joins a material's binary contents to the parameter/sampler surface its shader exposes: reads the material + its
/// imports, locates the shader in the bundle, <see cref="BPRShaderResource.Decode"/>s it, and matches each material
/// constant to a shader constant by JAMCRC name-hash (→ human name + default) and each sampler to a shader sampler by
/// channel. Degrades gracefully (hex names) when the shader isn't in the bundle.
/// </summary>
public static class ShaderParameterBinding
{
    public static BoundMaterial Bind(BundleCatalog catalog, CatalogResource material)
    {
        if (material.DatPath is null) throw new InvalidOperationException($"material {material.Id} has no .dat on disk");
        var mat = BPRMaterialResource.Read(File.ReadAllBytes(material.DatPath)).Decode();
        var imports = material.ImportsPath is not null && File.Exists(material.ImportsPath)
            ? ImportsYAML.Read(File.ReadAllText(material.ImportsPath))
            : new ImportsYAML();
        uint ImportAt(int offset)
        {
            foreach (var e in imports.Entries) if (e.Offset == (uint)offset) return e.Id;
            return 0;
        }

        uint shaderIDRaw = ImportAt(BPRMaterialResource.ShaderSlotOffset);
        ResourceID? shaderID = shaderIDRaw != 0 ? new ResourceID(shaderIDRaw) : null;
        var catShader = shaderID is { } sid ? catalog.ByID(sid) : null;

        BPRShaderResource.DecodedShader? shader = null;
        if (catShader?.DatPath is not null && File.Exists(catShader.DatPath))
            shader = BPRShaderResource.Read(File.ReadAllBytes(catShader.DatPath)).Decode();

        var constByHash = shader?.Constants.GroupBy(c => c.NameHash).ToDictionary(g => g.Key, g => g.First())
            ?? new Dictionary<uint, BPRShaderResource.DecodedConstant>();
        var samplerByChannel = shader?.Techniques.SelectMany(t => t.Samplers).GroupBy(s => s.Channel)
            .ToDictionary(g => g.Key, g => g.First().Name) ?? new Dictionary<int, string>();

        var parameters = new List<BoundParameter>();
        Add(mat.VSConstants, false);
        Add(mat.PSConstants, true);
        void Add(IReadOnlyList<MaterialConstant> consts, bool pixel)
        {
            for (int i = 0; i < consts.Count; i++)
            {
                var mc = consts[i];
                bool resolved = constByHash.TryGetValue(mc.NameHash, out var sc);
                string name = resolved ? sc!.Name : $"0x{mc.NameHash:x8}";
                float[] def = resolved && sc!.InstanceData is not null ? sc.InstanceData : mc.Value;
                parameters.Add(new BoundParameter(name, mc.NameHash, mc.Size, def, mc.Value, pixel, i, resolved));
            }
        }

        var samplers = new List<BoundSampler>();
        for (int i = 0; i < mat.Samplers.Count; i++)
        {
            var ms = mat.Samplers[i];
            uint ts = ImportAt(ms.TextureStateSlotOffset);
            ResourceID? tsID = ts != 0 ? new ResourceID(ts) : null;

            // resolve the TextureState through to the actual texture + its sampler settings (TextureState stays hidden in the UI)
            ResourceID? texID = null;
            SamplerParams? sampler = null;
            if (tsID is { } id && catalog.ByID(id) is { DatPath: not null } tsRes)
                try { var d = TextureStateResolver.Decode(tsRes); texID = d.TextureID; sampler = d.Sampler; }
                catch { /* unreadable state → leave unresolved */ }

            samplers.Add(new BoundSampler(i, ms.Channel, ms.Purpose,
                samplerByChannel.GetValueOrDefault(ms.Channel),
                tsID, texID, sampler, ms.TextureStateSlotOffset));
        }

        var states = new List<(int, int, ResourceID?)>();
        for (int t = 0; t < mat.Techniques.Count; t++)
        {
            int off = mat.Techniques[t].MaterialStateSlotOffset;
            uint id = ImportAt(off);
            states.Add((t, off, id != 0 ? new ResourceID(id) : null));
        }

        return new BoundMaterial(material.Id, shaderID, catShader?.Name, shader is not null, parameters, samplers, states);
    }

    /// <summary>
    /// Re-bind a material's editable surface against a DIFFERENT shader (a swap): present the NEW shader's material
    /// constants (those with InstanceData defaults) and samplers, carrying the material's current values over by
    /// name-hash and its textures by channel. The rows describe what a Save REBUILD will write — MaterialConstIndex is
    /// -1 because the material's blocks don't match the new shader's layout yet. Render-mode (material states) is
    /// shader-independent so it's carried from the current binding.
    /// </summary>
    public static BoundMaterial BindForShader(BundleCatalog catalog, CatalogResource material,
        BPRShaderResource.DecodedShader shader, ResourceID shaderID, string? shaderName)
    {
        var cur = Bind(catalog, material);   // current values, against the material's existing shader
        var valueByHash = new Dictionary<uint, float[]>();
        foreach (var p in cur.Parameters) valueByHash[p.NameHash] = p.CurrentValue;
        var texByChannel = new Dictionary<int, (ResourceID Tex, SamplerParams? Sampler, ResourceID? State)>();
        foreach (var s in cur.Samplers)
            if (s.TextureID is { } t) texByChannel[s.Channel] = (t, s.Sampler, s.AssignedTextureState);

        // A constant-table position used by the PS (idx2) is a PS param; otherwise it's a VS param.
        var psPos = new HashSet<int>();
        var vsPos = new HashSet<int>();
        foreach (var tq in shader.Techniques) { foreach (int p in tq.Indices2) psPos.Add(p); foreach (int p in tq.Indices1) vsPos.Add(p); }

        var parameters = new List<BoundParameter>();
        for (int i = 0; i < shader.Constants.Count; i++)
        {
            var c = shader.Constants[i];
            if (c.InstanceData is null) continue;   // only material constants (with instance defaults) are editable params
            bool isPixel = psPos.Contains(i) || !vsPos.Contains(i);
            float[] val = valueByHash.TryGetValue(c.NameHash, out var v) ? v : c.InstanceData;
            parameters.Add(new BoundParameter(c.Name, c.NameHash, c.Size, c.InstanceData, val, isPixel, -1, true));
        }

        var samplers = new List<BoundSampler>();
        var seen = new HashSet<int>();
        foreach (var tq in shader.Techniques)
            foreach (var ds in tq.Samplers)
            {
                if (!seen.Add(ds.Channel)) continue;
                ResourceID? texID = null, state = null;
                SamplerParams? smp = null;
                if (texByChannel.TryGetValue(ds.Channel, out var tex)) { texID = tex.Tex; smp = tex.Sampler; state = tex.State; }
                samplers.Add(new BoundSampler(samplers.Count, (short)ds.Channel, TexturePurpose.None, ds.Name, state, texID, smp, 0));
            }

        return new BoundMaterial(material.Id, shaderID, shaderName, true, parameters, samplers, cur.MaterialStates);
    }
}
