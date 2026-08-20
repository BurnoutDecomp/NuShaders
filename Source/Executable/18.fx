float4x4 worldViewProj   : worldViewProj;
void
main(
    in  float3 iPos    : POSITION,
    in  float4 iColour : COLOR0,
    in  float2 iUV     : TEXCOORD0,
    out float4 oPos    : POSITION,
    out float4 oColour : COLOR0,
    out float2 oUV     : TEXCOORD0 )
{
    oPos    = mul( float4( iPos, 1.0f ), worldViewProj );
    oColour = iColour;
    oUV     = iUV;
}
