struct BloomBlurOldInterpolators
{
    float4 mPosition    : POSITION;
    float4 mUV_00_01    : TEXCOORD0;
    float4 mUV_02_03    : TEXCOORD1;
};
float4 kTapWeights0_3;
sampler   SamplerSource : register(s0);
float4
main(
    BloomBlurOldInterpolators lInput ) : COLOR
{
 float4 lColour;
    lColour  = ( float4( tex2D( SamplerSource, lInput.mUV_00_01.xy ).rgba ) * float( kTapWeights0_3.x ) );
    lColour += ( float4( tex2D( SamplerSource, lInput.mUV_00_01.zw ).rgba ) * float( kTapWeights0_3.y ) );
    lColour += ( float4( tex2D( SamplerSource, lInput.mUV_02_03.xy ).rgba ) * float( kTapWeights0_3.z ) );
    return lColour;
}
