float4 gOffsetXYZ;
float4 gRightUp;
float4 gColourShift;
float4 gColourScale;
void
main(
    in  float2 iPos         : POSITION,
    in  float4 iColour      : COLOR0,
    in  float2 iUV          : TEXCOORD0,
    out float4 oPos         : POSITION,
    out float4 oColourShift : TEXCOORD0,
    out float4 oColourScale : TEXCOORD1,
    out float2 oUV          : TEXCOORD2 )
{
    oPos     = float4( gOffsetXYZ.xyz, 1.0f );
    oPos.xy += ( iPos.x * gRightUp.xy ) + ( iPos.y * gRightUp.zw );
    oColourShift = gColourShift;
    oColourScale = iColour * gColourScale;
    oUV          = iUV;
}
