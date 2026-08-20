struct DOFDownsampleInterpolators
{
    float4 mPosition    : POSITION;
    float4 mUV_00_01    : TEXCOORD0;
    float4 mUV_02_03    : TEXCOORD1;
};
sampler   SamplerSource : register(s0);
float4
main(
    DOFDownsampleInterpolators lInput ) : COLOR
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
    lColour = ( lColourTap00 + lColourTap01 + lColourTap02 + lColourTap03 ) * float(0.25);
    return float4( lColour, float(1.0) );
}
