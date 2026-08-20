namespace NuShaders.Formats.DXBC;

/// <summary>
/// Reads reflection (stage, resource bindings, cbuffer layout) from a compiled DXBC blob.
/// Default impl is the managed RDEF parser; a Vortice/D3DReflect impl could swap in behind
/// this interface (CLI <c>--reflect native</c>) if an exotic shader trips the parser.
/// </summary>
public interface IShaderReflection
{
    DXBCReflection Reflect(ReadOnlySpan<byte> dxbc);
}
