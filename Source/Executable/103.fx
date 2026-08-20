float4  gPsConstants;
sampler DiffuseSampler : register(s0);
void
main(
    in  float4 iUvBlendAlpha : TEXCOORD0,
    out float4  oColour       : COLOR0
    )
{
    oColour       = float4( tex2D( DiffuseSampler, iUvBlendAlpha.xy ).xyzx );
    oColour.xyz  *= float( gPsConstants.w );
    oColour.xyzw *= float( iUvBlendAlpha.w );
}
