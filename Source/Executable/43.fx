struct BrnSunCoronaFlareInterpolators
{
    float4 mPosition    : POSITION;
    float2 mUV          : TEXCOORD0;
};
float4    kColourAndPower;
sampler   OcclusionSource : register(s0);
float4
main(
    BrnSunCoronaFlareInterpolators lInput ) : COLOR
{
    float2  lhUvTex = (float2)lInput.mUV;
    float2  lhUvOcc = float2(0.0, 0.0);
    float   lhDotUV         = dot(lhUvTex,lhUvTex);
    float   lhOneMinusDotUV = saturate( float(1.0) - lhDotUV );
    float   lhPower         = lhOneMinusDotUV * lhOneMinusDotUV;
    float3  lhTexture       = float3(kColourAndPower.rgb) * lhPower;
    float   lhAlpha = (float)tex2D( OcclusionSource, lhUvOcc ).r;
    return float4( lhTexture, lhAlpha );
}
