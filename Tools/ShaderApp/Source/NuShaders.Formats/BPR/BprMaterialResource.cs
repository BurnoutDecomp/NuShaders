using System.Buffers.Binary;
using NuShaders.Formats.Bundle;
using NuShaders.Formats.IO;
using NuShaders.Formats.Model;

namespace NuShaders.Formats.BPR;

/// <summary>CgsGraphics::TexturePurpose (Material.mediawiki).</summary>
public enum TexturePurpose
{
    Invalid = -1, None = 0, AmbientOcclusionMap = 1, ShadowMap0 = 2,
    GlassFracturePersistent = 3, EnvironmentMap = 4, GlassFracture = 5, ShadowMapAniso = 6,
}

/// <summary>One per-material constant override: a JAMCRC name hash (maps to a shader constant) + its float[4*Size] value.</summary>
public sealed record MaterialConstant(uint NameHash, int Size, float[] Value);

/// <summary>One sampler binding: a texture-purpose string + id + channel + purpose enum + the file offset of its TextureState import slot.</summary>
public sealed record MaterialSampler(string PurposeName, uint Id, short Channel, short Scope, TexturePurpose Purpose, int TextureStateSlotOffset);

/// <summary>One technique: the file offset of its MaterialState import slot.</summary>
public sealed record MaterialTechnique(int MaterialStateSlotOffset);

public sealed record DecodedMaterial(
    uint NameHash, int NumTechniques, int NumSamplers, int NumInternalSamplers, int NumExternalSamplers,
    int ShaderSlotOffset,
    IReadOnlyList<MaterialTechnique> Techniques,
    IReadOnlyList<MaterialSampler> Samplers,
    IReadOnlyList<MaterialConstant> VSConstants,
    IReadOnlyList<MaterialConstant> PSConstants);

/// <summary>
/// BPR (Remastered) Material resource, type 0x1 (RwMaterial / CgsGraphics::MaterialAssembly), 32-bit layout, primary-only.
/// An offset-addressed blob: the header points at the technique structs (each importing a MaterialState), the sampler
/// array (each importing a TextureState), and the VS/PS ShaderConstantsInternal blocks (the editable per-material float
/// params). Import slots on disk are 0/0xDAD0BEEF placeholders; the real ids live in the paired <c>{ID}_imports.yaml</c>.
///
/// Round-trip strategy (mirrors <see cref="BPRShaderResource"/>): capture the bytes and replay them; every edit is an
/// in-place patch (a constant's float4, or an imports.yaml entry), so an unedited round-trip is byte-identical and an
/// edit changes only the touched bytes. Verified against Reference/Material.mediawiki + real materials.
/// </summary>
public sealed class BPRMaterialResource
{
    public const int MetaType = 0x1;
    public static int[] MetaAlignment => [0x10];

    public const int ShaderSlotOffset = 0x10;   // header: Shader* import
    private const int TechniqueStride = 0x20;
    private const int SamplerStride = 0x14;

    private byte[] _data = [];

    public static BPRMaterialResource Read(ReadOnlySpan<byte> data) => new() { _data = data.ToArray() };

    public byte[] ToBytes() => (byte[])_data.Clone();

    public uint NameHash => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(0x04));

    /// <summary>Patch the material's name-hash (0x04) — used to clone a working material under a new resource id.</summary>
    public void SetNameHash(uint hash) => BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan(0x04), hash);

    public DecodedMaterial Decode()
    {
        var r = new SpanReader(_data);
        int mappMaterials = (int)r.U32LE(0x00);
        uint nameHash = r.U32LE(0x04);
        int numTech = r.U8(0x08);
        int numSamp = r.I8(0x09);
        int numInternal = r.I8(0x0A);
        int numExternal = r.I8(0x0B);
        int mpaSamplers = (int)r.U32LE(0x0C);
        int mpVS = (int)r.U32LE(0x14);
        int mpPS = (int)r.U32LE(0x18);

        var techs = new List<MaterialTechnique>(numTech);
        for (int t = 0; t < numTech; t++)
            techs.Add(new MaterialTechnique(mappMaterials + t * TechniqueStride + 0x00));

        var samps = new List<MaterialSampler>(Math.Max(0, numSamp));
        for (int k = 0; k < numSamp; k++)
        {
            int b = mpaSamplers + k * SamplerStride;
            int purposeOff = (int)r.U32LE(b + 0x00);
            string purposeName = purposeOff > 0 && purposeOff < _data.Length ? r.CString(purposeOff) : "";
            samps.Add(new MaterialSampler(
                purposeName, r.U32LE(b + 0x04), (short)r.U16LE(b + 0x08), (short)r.U16LE(b + 0x0A),
                (TexturePurpose)(int)r.U32LE(b + 0x0C), b + 0x10));
        }

        return new DecodedMaterial(nameHash, numTech, numSamp, numInternal, numExternal, ShaderSlotOffset,
            techs, samps, DecodeConstants(r, mpVS), DecodeConstants(r, mpPS));
    }

    private List<MaterialConstant> DecodeConstants(SpanReader r, int blockOff)
    {
        var list = new List<MaterialConstant>();
        if (blockOff <= 0 || blockOff + 0x10 > _data.Length) return list;
        int count = (int)r.U32LE(blockOff + 0x00);
        int sizesOff = (int)r.U32LE(blockOff + 0x04);
        int dataPtrsOff = (int)r.U32LE(blockOff + 0x08);
        int namesOff = (int)r.U32LE(blockOff + 0x0C);
        for (int i = 0; i < count; i++)
        {
            int size = (int)r.U32LE(sizesOff + i * 4);           // float4 units (1 = one float4 = 0x10 bytes)
            int dataOff = (int)r.U32LE(dataPtrsOff + i * 4);
            uint hash = r.U32LE(namesOff + i * 4);
            var vals = new float[size * 4];
            for (int j = 0; j < vals.Length; j++) vals[j] = r.F32LE(dataOff + j * 4);
            list.Add(new MaterialConstant(hash, size, vals));
        }
        return list;
    }

    // ---- surgical in-place edits (only the touched bytes change) ----

    /// <summary>Overwrite the float[4*Size] of constant <paramref name="index"/> in the VS (false) or PS (true) block.</summary>
    public void SetConstant(bool pixel, int index, float[] value)
    {
        int blockOff = (int)U32(pixel ? 0x18 : 0x14);
        int count = (int)U32(blockOff);
        if ((uint)index >= (uint)count) throw new ArgumentOutOfRangeException(nameof(index));
        int size = (int)U32((int)U32(blockOff + 0x04) + index * 4);
        int dataOff = (int)U32((int)U32(blockOff + 0x08) + index * 4);
        for (int j = 0; j < size * 4; j++)
            BinaryPrimitives.WriteSingleLittleEndian(_data.AsSpan(dataOff + j * 4), j < value.Length ? value[j] : 0f);
    }

    public void SetSamplerPurpose(int samplerIndex, TexturePurpose purpose)
    {
        int b = (int)U32(0x0C) + samplerIndex * SamplerStride;
        BinaryPrimitives.WriteUInt32LittleEndian(_data.AsSpan(b + 0x0C), (uint)(int)purpose);
    }

    private uint U32(int offset) => BinaryPrimitives.ReadUInt32LittleEndian(_data.AsSpan(offset));

    /// <summary>Set (or add) the imports entry for <paramref name="offset"/> to <paramref name="newID"/>, keeping entries sorted by offset.</summary>
    public static void RepointImport(ImportsYAML imports, int offset, ResourceID newID)
    {
        for (int i = 0; i < imports.Entries.Count; i++)
            if (imports.Entries[i].Offset == (uint)offset) { imports.Entries[i] = ((uint)offset, newID.Value); return; }
        imports.Entries.Add(((uint)offset, newID.Value));
        imports.Entries.Sort((a, b) => a.Offset.CompareTo(b.Offset));
    }
}
