using NuShaders.Formats.IO;

namespace NuShaders.Formats.DXBC;

/// <summary>
/// Managed parser for the DXBC RDEF (resource definitions) chunk — dependency-free, no fxc needed.
/// Targets SM5.0 / "RD11" layout: 32-byte resource bindings, 24-byte cbuffer descriptors,
/// 40-byte cbuffer variables. (SM5.1 uses wider records; out of scope — our shaders are vs_5_0/ps_5_0.)
/// All offsets inside RDEF are relative to the chunk-data start.
/// </summary>
public sealed class RdefReflection : IShaderReflection
{
    public static readonly RdefReflection Instance = new();

    private const int BindingStride = 32;
    private const int CbufferStride = 24;
    private const int VariableStride = 40;

    public DXBCReflection Reflect(ReadOnlySpan<byte> dxbc)
    {
        var container = new DXBCContainer(dxbc);
        if (!container.TryGetChunk("RDEF", out var rdef))
            throw new InvalidDataException("DXBC has no RDEF chunk (reflection stripped).");

        var r = new SpanReader(rdef);

        int cbCount = (int)r.U32LE(0x00);
        int cbOffset = (int)r.U32LE(0x04);
        int bindCount = (int)r.U32LE(0x08);
        int bindOffset = (int)r.U32LE(0x0C);
        ushort programType = r.U16LE(0x12);

        ShaderKind kind = programType switch
        {
            0xFFFF => ShaderKind.Pixel,
            0xFFFE => ShaderKind.Vertex,
            _ => ShaderKind.Unknown,
        };

        var bindings = new List<ResourceBinding>(bindCount);
        for (int i = 0; i < bindCount; i++)
        {
            int e = bindOffset + i * BindingStride;
            string name = r.CString((int)r.U32LE(e));
            uint d3dType = r.U32LE(e + 0x04);
            int register = (int)r.U32LE(e + 0x14);
            uint flags = r.U32LE(e + 0x1C);
            bindings.Add(new ResourceBinding(name, register, MapKind(d3dType), d3dType, flags));
        }

        var cbuffers = new List<CbufferReflection>(cbCount);
        for (int c = 0; c < cbCount; c++)
        {
            int e = cbOffset + c * CbufferStride;
            string cbName = r.CString((int)r.U32LE(e));
            int varCount = (int)r.U32LE(e + 0x04);
            int varOffset = (int)r.U32LE(e + 0x08);
            int cbSize = (int)r.U32LE(e + 0x0C);

            var vars = new List<CbufferVariable>(varCount);
            for (int v = 0; v < varCount; v++)
            {
                int ve = varOffset + v * VariableStride;
                string vName = r.CString((int)r.U32LE(ve));
                int start = (int)r.U32LE(ve + 0x04);
                int size = (int)r.U32LE(ve + 0x08);
                uint vFlags = r.U32LE(ve + 0x0C);
                int typeOffset = (int)r.U32LE(ve + 0x10);

                int rows = r.U16LE(typeOffset + 0x04);
                int cols = r.U16LE(typeOffset + 0x06);
                int elems = r.U16LE(typeOffset + 0x08);

                vars.Add(new CbufferVariable(vName, start, size, rows, cols, elems, v, vFlags));
            }

            cbuffers.Add(new CbufferReflection(cbName, cbSize, vars));
        }

        return new DXBCReflection(kind, bindings, cbuffers);
    }

    // D3D_SHADER_INPUT_TYPE: 0=CBUFFER, 1=TBUFFER, 2=TEXTURE, 3=SAMPLER, ...
    private static ResourceKind MapKind(uint d3dInputType) => d3dInputType switch
    {
        0 => ResourceKind.CBuffer,
        2 => ResourceKind.Texture,
        3 => ResourceKind.Sampler,
        _ => ResourceKind.Other,
    };
}
