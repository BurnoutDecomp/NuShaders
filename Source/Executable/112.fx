float4x4 gWorldViewProj;
float4 gStartColour;
float4 gEndColour;
void
main(
    in float3 iPos                   : POSITION,
    in float4 iUvTimeAlpha           : TEXCOORD0,
    out float4 oPos    : POSITION,
    out float4 oColour : COLOR0,
    out float2 oUV     : TEXCOORD0 )
{
    oPos       = mul( float4( iPos, 1.0f ), gWorldViewProj );
    oColour    = lerp( gStartColour, gEndColour, iUvTimeAlpha.z );
    oColour.w *= iUvTimeAlpha.w;
    oUV        = iUvTimeAlpha.xy;
}
