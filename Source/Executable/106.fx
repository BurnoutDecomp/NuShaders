float4x4 worldViewProj;
float4   colourScale;
void
main(
    in float4 iPosPlusBlend : POSITION,
    in float4 iColour       : COLOR0,
    in float4 iUvUv         : TEXCOORD0,
    out float4 oPos      : POSITION,
    out float4 oColour   : COLOR0,
    out float4 oUvUv     : TEXCOORD0,
    out float  oMiscData : TEXCOORD1)
{
    oPos      = mul( float4( iPosPlusBlend.xyz, 1.0f ), worldViewProj );
    oColour   = iColour * colourScale;
    oUvUv     = iUvUv;
    oMiscData = iPosPlusBlend.w;
}
