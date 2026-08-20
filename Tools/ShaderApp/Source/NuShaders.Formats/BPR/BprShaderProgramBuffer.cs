using System.Text;
using NuShaders.Formats.DXBC;
using NuShaders.Formats.IO;

namespace NuShaders.Formats.BPR;

/// <summary>
/// BPR (Remastered PC, DX11) ShaderProgramBuffer, type 0x12, little-endian.
/// The secondary is the DXBC blob padded to 0x80; the primary is a reflection/binding table.
/// <see cref="ReadPrimary"/>/<see cref="ToBytes"/> faithfully round-trip a stock primary (runtime
/// pointers excepted); <see cref="FromDXBC"/> generates one from a compiled shader (the packer).
/// </summary>
public sealed class BPRShaderProgramBuffer
{
    public const int HeaderSize = 0x20;
    public const int BitmaskSize = 0x34;       // 0x20..0x53
    public const int BindingTableOffset = 0x54;
    public const int DescriptorSize = 0x30;
    public const int SecondaryAlignment = 0x80;

    // Runtime-pointer fields the engine overwrites at load — emitted as 0, masked in round-trip.
    public const int HeaderRuntimePtrOffset = 0x04;
    public const int DescriptorRuntimePtrOffset = 0x14;

    // The 36-byte fixed template that follows the 16-byte sampler map (identical in 235/242 stock primaries).
    private static ReadOnlySpan<byte> FixedTemplate =>
    [
        0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0x00,0xFF,
        0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0xFF,0x00,0x00,0x00,
        0x00,0x00,0x00,0x00,
    ];

    // ---- header ----
    public uint MType;
    public uint RuntimePointer;   // @0x04
    public uint Reserved08;
    public uint Version0C;        // = 1
    public uint Reserved10;
    public uint DXBCSize;         // @0x14
    public uint StringTableSize;  // @0x18
    public uint BindingCountRaw;  // @0x1C (binding count is the low byte; upper bytes can carry stray bits on VS)

    // ---- sections (captured raw on read, generated on build; both serialized by ToBytes) ----
    public byte[] Bitmask = [];          // 0x20..0x53
    public byte[] BindingTable = [];     // BindingCount * 8
    public byte[] PreDescriptorPad = []; // alignment bytes between binding table and descriptor
    public byte[] Descriptor = [];       // 0x30
    public byte[] Gap = [];              // descriptor end -> var table: zeros, or an embedded source-path string
    public byte[] VarTable = [];         // varCount * 16
    public byte[] StringTable = [];

    public bool HasDescriptor = true;    // false for resourceless shaders (no cbuffer -> no descriptor/var table)
    public byte[] Tail = [];             // raw remainder after the binding table when there is no descriptor

    public int BindingCount => (int)(BindingCountRaw & 0xFF);

    /// <summary>Var table offset (the descriptor stores it absolutely at +0x2C).</summary>
    public static int DescriptorOffset(int bindingCount) => Alignment.Align(BindingTableOffset + bindingCount * 8, 16);

    // ===================================================================== read =====
    public static BPRShaderProgramBuffer ReadPrimary(ReadOnlySpan<byte> data)
    {
        var r = new SpanReader(data);
        var p = new BPRShaderProgramBuffer
        {
            MType = r.U32LE(0x00),
            RuntimePointer = r.U32LE(0x04),
            Reserved08 = r.U32LE(0x08),
            Version0C = r.U32LE(0x0C),
            Reserved10 = r.U32LE(0x10),
            DXBCSize = r.U32LE(0x14),
            StringTableSize = r.U32LE(0x18),
            BindingCountRaw = r.U32LE(0x1C),
        };

        int nb = p.BindingCount;
        p.Bitmask = data.Slice(0x20, BitmaskSize).ToArray();

        int bindEnd = BindingTableOffset + nb * 8;
        p.BindingTable = data[BindingTableOffset..bindEnd].ToArray();

        // A descriptor / var table exists only when the shader binds a cbuffer (type 0x1A).
        bool hasCbuffer = false;
        for (int i = 0; i < nb; i++)
            if (p.BindingTable[i * 8 + 5] == 0x1A) { hasCbuffer = true; break; }
        if (!hasCbuffer)
        {
            p.HasDescriptor = false;
            p.Tail = data[bindEnd..].ToArray();
            return p;
        }

        int descOff = DescriptorOffset(nb);
        p.PreDescriptorPad = data[bindEnd..descOff].ToArray();
        p.Descriptor = data.Slice(descOff, DescriptorSize).ToArray();

        var dr = new SpanReader(p.Descriptor);
        int varCount = (int)dr.U32LE(0x24);
        int varTableOff = (int)dr.U32LE(0x2C);

        p.Gap = data[(descOff + DescriptorSize)..varTableOff].ToArray();
        p.VarTable = data.Slice(varTableOff, varCount * 16).ToArray();

        int strBase = varTableOff + varCount * 16;
        p.StringTable = data[strBase..].ToArray();
        return p;
    }

    // ==================================================================== write =====
    public byte[] ToBytes()
    {
        var w = new SpanWriter(HeaderSize + Bitmask.Length + BindingTable.Length + PreDescriptorPad.Length +
                               DescriptorSize + Gap.Length + VarTable.Length + StringTable.Length);
        w.U32LE(MType);
        w.U32LE(0); // runtime pointer (engine-written)
        w.U32LE(Reserved08);
        w.U32LE(Version0C);
        w.U32LE(Reserved10);
        w.U32LE(DXBCSize);
        w.U32LE(StringTableSize);
        w.U32LE(BindingCountRaw);

        w.Bytes(Bitmask);
        w.Bytes(BindingTable);

        if (!HasDescriptor)
        {
            w.Bytes(Tail);
            return w.ToArray();
        }

        w.Bytes(PreDescriptorPad);

        var desc = (byte[])Descriptor.Clone();
        Array.Clear(desc, DescriptorRuntimePtrOffset, 4); // runtime pointer (engine-written)
        w.Bytes(desc);

        w.Bytes(Gap);
        w.Bytes(VarTable);
        w.Bytes(StringTable);
        return w.ToArray();
    }

    /// <summary>The secondary resource: the DXBC blob zero-padded to 0x80.</summary>
    public static byte[] PadSecondary(ReadOnlySpan<byte> dxbc)
    {
        int dxbcSize = (int)new SpanReader(dxbc).U32LE(0x18);
        var secondary = new byte[Alignment.Align(dxbcSize, SecondaryAlignment)];
        dxbc[..Math.Min(dxbc.Length, dxbcSize)].CopyTo(secondary);
        return secondary;
    }

    // ==================================================================== build =====
    /// <summary>Generate a primary from a compiled DXBC blob (the packer). Mirrors Build/pack_bpr_shader.ps1.</summary>
    public static BPRShaderProgramBuffer FromDXBC(ReadOnlySpan<byte> dxbc, int gap = 0x320, IShaderReflection? reflection = null)
    {
        reflection ??= RdefReflection.Instance;
        DXBCReflection refl = reflection.Reflect(dxbc);
        uint dxbcSize = new SpanReader(dxbc).U32LE(0x18);

        // Bindings: samplers (0x0A) -> textures (0x05) -> cbuffers (0x1A), each by register.
        var binds = new List<(string Name, int Reg, byte Type, int Order)>();
        foreach (var b in refl.Bindings)
        {
            (byte type, int order) = b.Kind switch
            {
                ResourceKind.Sampler => ((byte)0x0A, 0),
                ResourceKind.Texture => ((byte)0x05, 1),
                ResourceKind.CBuffer => ((byte)0x1A, 2),
                _ => ((byte)0, -1),
            };
            if (order < 0) continue;
            string name = b.Kind == ResourceKind.CBuffer && b.Name == "_Globals" ? "$Globals" : b.Name;
            binds.Add((name, b.Register, type, order));
        }
        binds.Sort((x, y) => x.Order != y.Order ? x.Order.CompareTo(y.Order) : x.Reg.CompareTo(y.Reg));

        // Cbuffer variables in declaration order; byte0 of the packed field is the index.
        var vars = new List<(string Name, int Offset, int Size, int Rows, int Cols, int Index)>();
        int vi = 0;
        foreach (var cb in refl.Cbuffers)
            foreach (var v in cb.Variables)
                vars.Add((v.Name, v.ByteOffset, v.SizeBytes, v.PackedRows, v.Columns, vi++));

        int nb = binds.Count;
        int descOff = DescriptorOffset(nb);
        int descEnd = descOff + DescriptorSize;
        int varTableOff = descEnd + gap;
        int strBase = varTableOff + vars.Count * 16;

        // String table: binding names (table order), then $Globals (shared), then var names. Deduped.
        var strBytes = new List<byte>();
        var strOff = new Dictionary<string, int>(StringComparer.Ordinal);
        int AddStr(string s)
        {
            if (strOff.TryGetValue(s, out int existing)) return existing;
            int off = strBase + strBytes.Count;
            strOff[s] = off;
            strBytes.AddRange(Encoding.ASCII.GetBytes(s));
            strBytes.Add(0);
            return off;
        }
        foreach (var b in binds) AddStr(b.Name);
        int cbNameOff = AddStr("$Globals");
        foreach (var v in vars) AddStr(v.Name);

        var bindingTable = new SpanWriter(nb * 8);
        foreach (var b in binds)
        {
            bindingTable.U32LE((uint)strOff[b.Name]);
            bindingTable.U32LE((uint)(b.Reg | (b.Type << 8) | (1 << 16)));
        }

        int offsetB = descEnd + BindingTableOffset + nb * 8;
        var descriptor = new SpanWriter(DescriptorSize);
        descriptor.U32LE(2); descriptor.U32LE(4); descriptor.U32LE(0); descriptor.U32LE((uint)gap);
        descriptor.U32LE(1); descriptor.U32LE(0); descriptor.U32LE((uint)offsetB); descriptor.U32LE(0);
        descriptor.U32LE((uint)cbNameOff); descriptor.U32LE((uint)vars.Count); descriptor.U32LE(1); descriptor.U32LE((uint)varTableOff);

        var varTable = new SpanWriter(vars.Count * 16);
        foreach (var v in vars)
        {
            varTable.U32LE((uint)strOff[v.Name]);
            varTable.U32LE((uint)v.Offset);
            varTable.U32LE((uint)v.Size);
            varTable.U32LE((uint)(v.Index | (0x03 << 8) | (v.Rows << 16) | (v.Cols << 24)));
        }

        var bitmask = new byte[BitmaskSize];
        bitmask.AsSpan(0, 16).Fill(0xFF);
        foreach (var b in binds)
            if (b.Type == 0x0A) bitmask[(b.Reg + 15) % 16] = (byte)b.Reg;
        FixedTemplate.CopyTo(bitmask.AsSpan(16));

        return new BPRShaderProgramBuffer
        {
            MType = refl.Kind == ShaderKind.Pixel ? 1u : 0u,
            Version0C = 1,
            DXBCSize = dxbcSize,
            StringTableSize = (uint)strBytes.Count,
            BindingCountRaw = (uint)nb,
            Bitmask = bitmask,
            BindingTable = bindingTable.ToArray(),
            PreDescriptorPad = new byte[descOff - (BindingTableOffset + nb * 8)],
            Descriptor = descriptor.ToArray(),
            Gap = new byte[gap],
            VarTable = varTable.ToArray(),
            StringTable = strBytes.ToArray(),
        };
    }
}
