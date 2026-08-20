namespace NuShaders.Formats.BPR;

/// <summary>A friendly read of a MaterialState's render mode, for labelling the pick-list.</summary>
public sealed record MaterialStateInfo(bool DepthWrite, bool TwoSided)
{
    public string Label => (DepthWrite ? "Opaque" : "Transparent") + (TwoSided ? " · two-sided" : "");
}

/// <summary>
/// Decodes the render-relevant bytes of a MaterialState (type 0xf, inline blend/depth/raster). The RasterizerState
/// begins at 0x88 (D3D11_RASTERIZER_DESC): FillMode @0x88, <b>CullMode @0x8C</b>, FrontCounterClockwise @0x90. CullMode
/// is D3D11_CULL_MODE: <b>1=NONE → two-sided</b>; 2=FRONT / 3=BACK → single-sided. Verified against the RenderDoc DX11
/// pipeline captures (single-sided shader = Cull Back, doublesided = Cull None) and the stock states (e.g. 0xD4659F9B /
/// "MaterialState671619170" = Cull Back = single-sided; 0x0BA364FE = Cull None = two-sided). Depth-write at 0x79
/// (1=write → opaque label) is still empirical. The MaterialState is never written by the studio (pick-only) — labels only.
/// </summary>
public static class MaterialStateDecoder
{
    public static MaterialStateInfo? Decode(ReadOnlySpan<byte> dat)
    {
        if (dat.Length < 0x90) return null;
        // CullMode == NONE (1) means no faces are culled → two-sided. BACK (3) / FRONT (2) cull → single-sided.
        return new MaterialStateInfo(DepthWrite: dat[0x79] != 0, TwoSided: dat[0x8C] == 1);
    }
}
