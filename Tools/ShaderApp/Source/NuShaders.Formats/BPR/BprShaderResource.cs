using System.Buffers.Binary;
using System.Text;
using NuShaders.Formats.DXBC;
using NuShaders.Formats.Hashing;
using NuShaders.Formats.IO;

namespace NuShaders.Formats.BPR;

/// <summary>
/// BPR (Remastered) Shader resource, type 0x32, little-endian. A wrapper that pairs N techniques
/// (each importing a VS + PS ShaderProgramBuffer) with the material-constant + sampler metadata.
///
/// Round-trip strategy: the header (0x30) and technique array (numTech × 0x3C) contain uninitialised
/// pointer garbage, and everything past them is pointer-addressed (file offsets) with no padding, so
/// <see cref="Read"/> captures those three regions raw and <see cref="ToBytes"/> replays them
/// (zeroing the VS/PS import slots, which are already 0 on disk and supplied via imports.yaml).
/// <see cref="Decode"/> parses the semantic model for inspection / cloning.
/// </summary>
public sealed class BPRShaderResource
{
    public const int TechniqueStride = 0x3C;
    public const int TechVSSlot = 0x00;
    public const int TechPSSlot = 0x04;
    private const int ImportSlotsBytes = 8; // VS* + PS*

    private byte[] _data = [];

    public byte[] Header = [];          // 0x00 .. techniqueArrayOffset
    public byte[] TechniqueArray = [];  // numTech * 0x3C
    public byte[] DataBlob = [];        // remainder (pointer-addressed tables + strings)

    public int TechniqueArrayOffset { get; private set; }
    public int NumTechniques { get; private set; }

    public static BPRShaderResource Read(ReadOnlySpan<byte> data)
    {
        var s = new BPRShaderResource
        {
            _data = data.ToArray(),
            TechniqueArrayOffset = (int)new SpanReader(data).U32LE(0x00),
            NumTechniques = data[0x04],
        };
        int techLen = s.NumTechniques * TechniqueStride;
        s.Header = data[..s.TechniqueArrayOffset].ToArray();
        s.TechniqueArray = data.Slice(s.TechniqueArrayOffset, techLen).ToArray();
        s.DataBlob = data[(s.TechniqueArrayOffset + techLen)..].ToArray();
        return s;
    }

    public byte[] ToBytes()
    {
        var w = new SpanWriter(Header.Length + TechniqueArray.Length + DataBlob.Length);
        w.Bytes(Header);
        var techs = (byte[])TechniqueArray.Clone();
        for (int t = 0; t < NumTechniques; t++)
            Array.Clear(techs, t * TechniqueStride + TechVSSlot, ImportSlotsBytes); // engine-written imports
        w.Bytes(techs);
        w.Bytes(DataBlob);
        return w.ToArray();
    }

    /// <summary>File offsets of every cross-resource import slot (each technique's VS* then PS*).</summary>
    public IEnumerable<(int Offset, string Stage, int Technique)> ImportSlots()
    {
        for (int t = 0; t < NumTechniques; t++)
        {
            yield return (TechniqueArrayOffset + t * TechniqueStride + TechVSSlot, "VS", t);
            yield return (TechniqueArrayOffset + t * TechniqueStride + TechPSSlot, "PS", t);
        }
    }

    /// <summary>
    /// Surgically add a sampler to one technique's sampler table without disturbing the proven layout:
    /// append a fresh (N+1)-entry sampler array + the new name string at the end of the .dat, then repoint
    /// the technique's samplers* (tech+0x2C) and bump its count (tech+0x30). The old array is left dead.
    /// Internal pointers resolve as base+fileOffset (the byte-identical clone works, and YAP can't
    /// regenerate a per-type reloc table), so appended entries with correct file offsets relocate fine.
    /// Lets a cloned 0x32 expose an engine-global slot (e.g. env cube s13) the shader samples by name.
    /// </summary>
    public static byte[] AddSampler(ReadOnlySpan<byte> data, int techIndex, string samplerName, int slot)
    {
        var dat = data.ToArray();
        int techArrayOff = (int)BinaryPrimitives.ReadUInt32LittleEndian(dat.AsSpan(0x00));
        int numTech = dat[0x04];
        if ((uint)techIndex >= (uint)numTech)
            throw new ArgumentOutOfRangeException(nameof(techIndex), $"technique {techIndex} of {numTech}");
        int b = techArrayOff + techIndex * TechniqueStride;
        int n = dat[b + 0x30];
        int oldSampOff = (int)BinaryPrimitives.ReadUInt32LittleEndian(dat.AsSpan(b + 0x2C));

        var w = new SpanWriter(dat.Length + samplerName.Length + 16);
        w.Bytes(dat);
        w.AlignTo(4);
        int nameOff = w.Length;
        w.Bytes(Encoding.ASCII.GetBytes(samplerName));
        w.U8(0);
        w.AlignTo(4);
        int newArrOff = w.Length;
        for (int k = 0; k < n; k++) w.Bytes(dat.AsSpan(oldSampOff + k * 8, 8).ToArray()); // existing entries verbatim
        w.U32LE((uint)nameOff);
        w.U16LE((ushort)slot);
        w.U16LE(0);

        var outDat = w.ToArray();
        BinaryPrimitives.WriteUInt32LittleEndian(outDat.AsSpan(b + 0x2C), (uint)newArrOff);
        outDat[b + 0x30] = (byte)(n + 1);
        return outDat;
    }

    // ---- semantic decode (for analyze / cloning) ----
    public sealed record DecodedConstant(string Name, int Index, int Size, uint NameHash, float[]? InstanceData);
    public sealed record DecodedSampler(string Name, int Channel);
    public sealed record DecodedTechnique(string Name, IReadOnlyList<DecodedSampler> Samplers, int[] Indices1, int[] Indices2);
    public sealed record DecodedShader(string Name, IReadOnlyList<DecodedConstant> Constants, IReadOnlyList<DecodedTechnique> Techniques);

    public DecodedShader Decode()
    {
        var r = new SpanReader(_data);
        string name = r.CString((int)r.U32LE(0x08));

        int numConst = _data[0x1C];
        int numInst = _data[0x1D];
        int idxOff = (int)r.U32LE(0x0C);
        int sizeOff = (int)r.U32LE(0x10);
        int instOff = (int)r.U32LE(0x14);
        int hashOff = (int)r.U32LE(0x18);
        int namesOff = (int)r.U32LE(0x20);

        var constants = new List<DecodedConstant>(numConst);
        for (int i = 0; i < numConst; i++)
        {
            int index = r.I8(idxOff + i);
            int size = r.U8(sizeOff + i);
            uint hash = r.U32LE(hashOff + i * 4);
            string cname = r.CString((int)r.U32LE(namesOff + i * 4));
            float[]? inst = null;
            if (index >= 0 && index < numInst)
            {
                int o = instOff + index * 16;
                inst = [r.F32LE(o), r.F32LE(o + 4), r.F32LE(o + 8), r.F32LE(o + 12)];
            }
            constants.Add(new DecodedConstant(cname, index, size, hash, inst));
        }

        var techniques = new List<DecodedTechnique>(NumTechniques);
        for (int t = 0; t < NumTechniques; t++)
        {
            int b = TechniqueArrayOffset + t * TechniqueStride;
            string tname = r.CString((int)r.U32LE(b + 0x38));
            int nSamp = Math.Max(0, (int)r.I8(b + 0x30));
            int sampOff = (int)r.U32LE(b + 0x2C);
            var samplers = new List<DecodedSampler>(nSamp);
            for (int k = 0; k < nSamp; k++)
            {
                int se = sampOff + k * 8;
                samplers.Add(new DecodedSampler(r.CString((int)r.U32LE(se)), (short)r.U16LE(se + 4)));
            }

            // idx1/idx2 lengths = sum of the count-byte triples at +0x20..+0x25.
            int len1 = _data[b + 0x20] + _data[b + 0x21] + _data[b + 0x22];
            int len2 = _data[b + 0x23] + _data[b + 0x24] + _data[b + 0x25];
            int i1 = (int)r.U32LE(b + 0x08);
            int i2 = (int)r.U32LE(b + 0x0C);
            var idx1 = new int[len1];
            for (int k = 0; k < len1; k++) idx1[k] = _data[i1 + k];
            var idx2 = new int[len2];
            for (int k = 0; k < len2; k++) idx2[k] = _data[i2 + k];

            techniques.Add(new DecodedTechnique(tname, samplers, idx1, idx2));
        }

        return new DecodedShader(name, constants, techniques);
    }

    // ============================================================ generate from scratch =====
    public sealed record GenerateTechnique(string Name, byte[] VSDXBC, byte[] PSDXBC, uint VSID, uint PSID);

    /// <summary>
    /// Build a Shader (0x32) from compiled VS/PS DXBCs. The constant table = union of VS- and PS-used
    /// $Globals vars, ordered [material, per-object(world), VS per-frame, PS-only per-frame] (cbuffer
    /// order within each group). idx1/idx2 per technique = the stage's used positions grouped
    /// [per-instance, per-object, per-frame]; the count-byte triples are those group sizes. Import and
    /// per-technique scratch pointers are written 0 (the engine fills them at load — they are 0xCD
    /// uninitialised heap on disk in stock files). Returns the .dat + imports.yaml.
    /// NOTE: semantic content is validated against stock; the on-disk LAYOUT differs (in-game is the final gate).
    /// </summary>
    public static (byte[] Dat, NuShaders.Formats.Bundle.ImportsYAML Imports) Generate(
        string shaderName,
        IReadOnlyList<(string Name, float[] Value)> materials,
        IReadOnlyList<GenerateTechnique> techniques,
        IShaderReflection? reflection = null)
    {
        reflection ??= RdefReflection.Instance;

        var meta = new Dictionary<string, (int Reg, int ByteOffset)>(StringComparer.Ordinal);
        var vsUsed = new List<HashSet<string>>();
        var psUsed = new List<HashSet<string>>();
        var psSamplers = new List<List<(string Name, int Reg)>>();

        foreach (var t in techniques)
        {
            var vr = reflection.Reflect(t.VSDXBC);
            var pr = reflection.Reflect(t.PSDXBC);
            foreach (var cb in vr.Cbuffers) foreach (var v in cb.Variables) meta.TryAdd(v.Name, (v.PackedRows, v.ByteOffset));
            foreach (var cb in pr.Cbuffers) foreach (var v in cb.Variables) meta.TryAdd(v.Name, (v.PackedRows, v.ByteOffset));
            vsUsed.Add(UsedNames(vr));
            psUsed.Add(UsedNames(pr));
            psSamplers.Add(pr.Bindings.Where(x => x.Kind == ResourceKind.Sampler)
                .Select(x => (x.Name, x.Register)).OrderBy(s => s.Register).ToList());
        }

        var vsUnion = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in vsUsed) vsUnion.UnionWith(s);
        var psUnion = new HashSet<string>(StringComparer.Ordinal);
        foreach (var s in psUsed) psUnion.UnionWith(s);
        var used = new HashSet<string>(vsUnion, StringComparer.Ordinal);
        used.UnionWith(psUnion);

        // Only material constants the shader actually uses go in the table + instance data.
        var usedMaterials = materials.Where(m => used.Contains(m.Name)).ToList();
        var materialNames = usedMaterials.Select(m => m.Name).ToList();
        var materialSet = new HashSet<string>(materialNames, StringComparer.Ordinal);
        int ByteOff(string n) => meta[n].ByteOffset;

        var table = new List<string>();
        table.AddRange(materialNames.Where(used.Contains));
        table.AddRange(used.Where(IsPerObject).OrderBy(ByteOff));
        table.AddRange(vsUnion.Where(n => !materialSet.Contains(n) && !IsPerObject(n)).OrderBy(ByteOff));
        table.AddRange(psUnion.Where(n => !vsUnion.Contains(n) && !materialSet.Contains(n) && !IsPerObject(n)).OrderBy(ByteOff));

        var posOf = new Dictionary<string, int>(StringComparer.Ordinal);
        for (int i = 0; i < table.Count; i++) posOf[table[i]] = i;
        int N = table.Count;

        var idx1 = new List<int>[techniques.Count];
        var idx2 = new List<int>[techniques.Count];
        var tri1 = new int[techniques.Count][];
        var tri2 = new int[techniques.Count][];
        for (int t = 0; t < techniques.Count; t++)
        {
            idx1[t] = StageIndices(vsUsed[t], materialNames, materialSet, posOf, ByteOff, out tri1[t]);
            idx2[t] = StageIndices(psUsed[t], materialNames, materialSet, posOf, ByteOff, out tri2[t]);
        }

        var w = new SpanWriter(1024);
        w.Zeros(0x30);                                  // header (patched below)
        const int techArrayOff = 0x30;
        w.Zeros(techniques.Count * TechniqueStride);    // technique structs (patched below)

        int Str(string s) { int off = w.Length; w.Bytes(Encoding.ASCII.GetBytes(s)); w.U8(0); return off; }

        var idx1Off = new int[techniques.Count];
        var idx2Off = new int[techniques.Count];
        var sampOff = new int[techniques.Count];
        var techNameOff = new int[techniques.Count];
        for (int t = 0; t < techniques.Count; t++)
        {
            idx1Off[t] = w.Length; foreach (int p in idx1[t]) w.U8((byte)p);
            idx2Off[t] = w.Length; foreach (int p in idx2[t]) w.U8((byte)p);
            w.AlignTo(4);
            var sn = psSamplers[t].Select(s => Str(s.Name)).ToList();
            w.AlignTo(4);
            sampOff[t] = w.Length;
            for (int k = 0; k < psSamplers[t].Count; k++) { w.U32LE((uint)sn[k]); w.U16LE((ushort)psSamplers[t][k].Reg); w.U16LE(0); }
            techNameOff[t] = Str(techniques[t].Name);
            w.AlignTo(4);
        }

        int constIndicesOff = w.Length;
        foreach (string n in table) w.U8((byte)(materialSet.Contains(n) ? materialNames.IndexOf(n) : 0xFF));
        int constSizesOff = w.Length;
        foreach (string n in table) w.U8((byte)meta[n].Reg);
        w.AlignTo(16);
        int instDataOff = w.Length;
        foreach (var m in usedMaterials) for (int j = 0; j < 4; j++) w.F32LE(j < m.Value.Length ? m.Value[j] : 0f);
        w.AlignTo(4);
        int nameHashesOff = w.Length;
        foreach (string n in table) w.U32LE(CRC32.JamCRC(n));
        var constNameOff = table.Select(Str).ToList();
        w.AlignTo(4);
        int constNamesOff = w.Length;
        foreach (int o in constNameOff) w.U32LE((uint)o);
        int shaderNameOff = Str(shaderName);

        // Per-technique scratch/output buffers: the engine resolves each used constant by name at load
        // and WRITES its descriptor into these 4 buffers (their on-disk pointers are 0xCD heap in stock
        // files). Allocate them generously and isolated at the end so the engine's exact size never matters.
        int scratchSize = 512 + N * 16;
        var scratchOff = new int[techniques.Count][];
        for (int t = 0; t < techniques.Count; t++)
        {
            scratchOff[t] = new int[4];
            for (int s = 0; s < 4; s++)
            {
                w.AlignTo(16);
                scratchOff[t][s] = w.Length;
                w.Zeros(scratchSize);
            }
        }

        byte[] dat = w.ToArray();
        void P32(int o, uint v) => BinaryPrimitives.WriteUInt32LittleEndian(dat.AsSpan(o), v);
        P32(0x00, techArrayOff);
        dat[0x04] = (byte)techniques.Count;
        dat[0x05] = 3;
        P32(0x08, (uint)shaderNameOff);
        P32(0x0C, (uint)constIndicesOff);
        P32(0x10, (uint)constSizesOff);
        P32(0x14, (uint)instDataOff);
        P32(0x18, (uint)nameHashesOff);
        dat[0x1C] = (byte)N;
        dat[0x1D] = (byte)usedMaterials.Count;
        P32(0x20, (uint)constNamesOff);
        for (int t = 0; t < techniques.Count; t++)
        {
            int b = techArrayOff + t * TechniqueStride;
            P32(b + 0x08, (uint)idx1Off[t]);
            P32(b + 0x0C, (uint)idx2Off[t]);
            dat[b + 0x20] = (byte)tri1[t][0]; dat[b + 0x21] = (byte)tri1[t][1]; dat[b + 0x22] = (byte)tri1[t][2];
            dat[b + 0x23] = (byte)tri2[t][0]; dat[b + 0x24] = (byte)tri2[t][1]; dat[b + 0x25] = (byte)tri2[t][2];
            P32(b + 0x10, (uint)scratchOff[t][0]);
            P32(b + 0x14, (uint)scratchOff[t][1]);
            P32(b + 0x18, (uint)scratchOff[t][2]);
            P32(b + 0x1C, (uint)scratchOff[t][3]);
            P32(b + 0x2C, (uint)sampOff[t]);
            dat[b + 0x30] = (byte)psSamplers[t].Count;
            P32(b + 0x38, (uint)techNameOff[t]);
        }

        var imports = new NuShaders.Formats.Bundle.ImportsYAML();
        for (int t = 0; t < techniques.Count; t++)
        {
            int b = techArrayOff + t * TechniqueStride;
            imports.Entries.Add(((uint)(b + TechVSSlot), techniques[t].VSID));
            imports.Entries.Add(((uint)(b + TechPSSlot), techniques[t].PSID));
        }
        return (dat, imports);
    }

    private static bool IsPerObject(string name) => name is "world" or "worldViewProj";

    private static HashSet<string> UsedNames(DXBCReflection r)
    {
        var s = new HashSet<string>(StringComparer.Ordinal);
        foreach (var cb in r.Cbuffers) foreach (var v in cb.Variables) if (v.IsUsed) s.Add(v.Name);
        return s;
    }

    private static List<int> StageIndices(HashSet<string> stageUsed, List<string> materialNames,
        HashSet<string> materialSet, Dictionary<string, int> posOf, Func<string, int> byteOff, out int[] triple)
    {
        var mat = materialNames.Where(stageUsed.Contains).ToList();
        var obj = stageUsed.Where(IsPerObject).OrderBy(byteOff).ToList();
        var frame = stageUsed.Where(n => !materialSet.Contains(n) && !IsPerObject(n)).OrderBy(byteOff).ToList();
        triple = [mat.Count, obj.Count, frame.Count];
        var idx = new List<int>(mat.Count + obj.Count + frame.Count);
        foreach (var n in mat) idx.Add(posOf[n]);
        foreach (var n in obj) idx.Add(posOf[n]);
        foreach (var n in frame) idx.Add(posOf[n]);
        return idx;
    }
}
