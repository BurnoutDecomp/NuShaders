float4x4 worldViewProj   : worldViewProj;
void
main(
    in  float3 iPos    : POSITION,
    in  float4 iColour : COLOR0,
    in  float2 iUV     : TEXCOORD0,
    out float4 oPos             : POSITION,
    out float4 oColour          : TEXCOORD0,
    out float2 oUV              : TEXCOORD1,
    out float3 oScreenPosition  : TEXCOORD2 )
{
    oPos    = mul( float4( iPos, 1.0f ), worldViewProj );
    oColour    = iColour;
    oScreenPosition.xyz = oPos.xyw;
    oUV     = iUV;
}
