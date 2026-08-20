struct BloomDownsampleInterpolators
{
    float4 mPosition    : POSITION;
    float4 mUV_00_01    : TEXCOORD0;
    float4 mUV_02_03    : TEXCOORD1;
};
float3 kDotWithWhiteLevel;
float3 kThresholdAndScale;
sampler   SamplerSource : register(s0);
float4
main(
    BloomDownsampleInterpolators lInput ) : COLOR
{
 float3 lColour;
    float3 lColourTap00;
    float3 lColourTap01;
    float3 lColourTap02;
    float3 lColourTap03;
    lColourTap00 = ( tex2D( SamplerSource, lInput.mUV_00_01.xy )).rgb;
 lColourTap01 = ( tex2D( SamplerSource, lInput.mUV_00_01.zw )).rgb;
 lColourTap02 = ( tex2D( SamplerSource, lInput.mUV_02_03.xy )).rgb;
 lColourTap03 = ( tex2D( SamplerSource, lInput.mUV_02_03.zw )).rgb;
    float lhThreshold = float(kThresholdAndScale.x);
    float3 lDot = kDotWithWhiteLevel;
    lColourTap00 *= dot( lColourTap00, lDot ) - lhThreshold;
    lColourTap01 *= dot( lColourTap01, lDot ) - lhThreshold;
    lColourTap02 *= dot( lColourTap02, lDot ) - lhThreshold;
    lColourTap03 *= dot( lColourTap03, lDot ) - lhThreshold;
    lColourTap00 = saturate( lColourTap00 );
    lColourTap01 = saturate( lColourTap01 );
    lColourTap02 = saturate( lColourTap02 );
    lColourTap03 = saturate( lColourTap03 );
    lColour = ( lColourTap00 + lColourTap01 + lColourTap02 + lColourTap03 ) * float(kThresholdAndScale.y);
    return float4( lColour, float(1.0) );
}
