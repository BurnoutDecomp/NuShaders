float4 gOffsetXYZ;
float4 gRightUp;
float4 gUVOffsets;
float4 gUVScales;
void
main(
    in  float2 iPos         : POSITION,
    in  float4 iColour      : COLOR0,
    in  float2 iUV          : TEXCOORD0,
    out float4 oPos         : POSITION,
    out float4 oUV_Base0_Overlay      : TEXCOORD0
    )
{
    oPos     = float4( gOffsetXYZ.xyz, 1.0f );
    oPos.xy += ( iPos.x * gRightUp.xy ) + ( iPos.y * gRightUp.zw );
    oUV_Base0_Overlay.xy = (iUV * gUVScales.xy) + gUVOffsets.xy;
    oUV_Base0_Overlay.zw = (iUV * gUVScales.zw) + gUVOffsets.zw;
}
