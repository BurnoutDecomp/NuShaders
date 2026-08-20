float4 gOffsetXYZ;
float4 gRightUp;
float4 gColourShift;
float4 gColourScale;
void
main(
    in  float2 iPos    : POSITION,
    in  float4 iColour : COLOR0,
    out float4 oPos    : POSITION,
    out float4 oColour : COLOR0 )
{
    oPos     = float4( gOffsetXYZ.xyz, 1.0f );
    oPos.xy += ( iPos.x * gRightUp.xy ) + ( iPos.y * gRightUp.zw );
    oColour = iColour * gColourScale;
    oColour += gColourShift;
}
