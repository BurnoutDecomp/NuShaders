struct DOFBlurInterpolators
{
    float4 mPosition    : POSITION;
    float4 mUV_00_01    : TEXCOORD0;
    float4 mUV_02_03    : TEXCOORD1;
    float4 mUV_04_05    : TEXCOORD2;
};
float4 kTapWeights0_3;
float4 kTapWeights4;
sampler   SamplerSource : register(s0);
float4
main(
    DOFBlurInterpolators lInput ) : COLOR
{
 float3 lColour;
    lColour  = ( float3( tex2D( SamplerSource, lInput.mUV_00_01.xy ).rgb ) * float( kTapWeights0_3.x ) );
    lColour += ( float3( tex2D( SamplerSource, lInput.mUV_00_01.zw ).rgb ) * float( kTapWeights0_3.y ) );
    lColour += ( float3( tex2D( SamplerSource, lInput.mUV_02_03.xy ).rgb ) * float( kTapWeights0_3.z ) );
    lColour += ( float3( tex2D( SamplerSource, lInput.mUV_02_03.zw ).rgb ) * float( kTapWeights0_3.w ) );
    lColour += ( float3( tex2D( SamplerSource, lInput.mUV_04_05.xy ).rgb ) * float( kTapWeights4.x ) );
    return float4( lColour, float(1.0) );
}
