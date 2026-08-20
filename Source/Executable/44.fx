float4x4 gWorldViewProjection;
float4   gTwinkleVector;
float4   gIntensityParams;
void
main(
    in  float3 iPosPlusIntensity : POSITION,
    out float4 oPos              : POSITION,
    out float  oAlpha            : TEXCOORD0
    )
{
    oPos  = ( gWorldViewProjection[1] * iPosPlusIntensity.y ) + gWorldViewProjection[3];
    oPos += ( gWorldViewProjection[0] * iPosPlusIntensity.x );
    float lfTwinkle = dot( float4( iPosPlusIntensity.xyz, 1.0 ), gTwinkleVector.xyzw );
    lfTwinkle = frac( lfTwinkle );
    oAlpha = ( iPosPlusIntensity.z + gIntensityParams.x ) * ( ( lfTwinkle * gIntensityParams.y ) + gIntensityParams.z );
}
