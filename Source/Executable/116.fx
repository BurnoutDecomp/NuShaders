void
main(
    in float4 iPos      : POSITION,
    in float4 iColour   : COLOR0,        
    in float2 iUV0      : TEXCOORD0,
    out float4 oPos      : POSITION,
    out float4 oColour   : COLOR0,
    out float2 oUV0      : TEXCOORD0)
{
    oPos    = float4(iPos.xyz, 1.0f);
    oColour = iColour;
    oUV0    = iUV0;
}
