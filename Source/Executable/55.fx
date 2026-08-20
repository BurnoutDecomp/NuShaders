struct PixelData
{
    float4 mPosition           : POSITION;
    float4 mSourceUvVignetteUv : TEXCOORD0;
};
float4 GlobalParams;
float4 DofParamsA;
float4 DofParamsB;
float4 BloomColour;
float4 VignetteInnerRgbPlusMul;
float4 VignetteOuterRgbPlusAdd;
float4 Tint2dColour;
float4 BlurMatrixZZZ;
float4 MotionBlurStencilValues;
sampler   SamplerSource : register(s0);
sampler   SamplerBloom  : register(s1);
sampler   SamplerDof    : register(s2);
sampler3D Sampler3dTint : register(s3);
sampler   SamplerDepth  : register(s4);
sampler   SamplerSSAO  : register(s6);
void
main(
    in PixelData iInput,
    out float4 oColour : COLOR
    )
{
    float2 lSourceUv = float2( iInput.mSourceUvVignetteUv.xy );
    float3 lSSAOColour = float3( 1.0, 1.0, 1.0 );
    float3 lBloomColour = float3( tex2D( SamplerBloom,  lSourceUv ).rgb );
    float3 lPixelColour = float3( tex2D( SamplerSource, lSourceUv ).rgb ) * lSSAOColour;
    {
        float lfRecipWhiteLevel = float( GlobalParams.x );
        lPixelColour *= lfRecipWhiteLevel;
    }
    {
        float3 lBloomRGBScale = float3( BloomColour.rgb );
        lBloomColour *= lBloomRGBScale;
        lPixelColour += lBloomColour - saturate( lPixelColour * lBloomColour );
    }
    {
        lPixelColour = float3( tex3D( Sampler3dTint, lPixelColour.rgb ).rgb );
    }
    {
        float3 lVignetteInnerRgb        = float3( VignetteInnerRgbPlusMul.xyz );
        float4 lVignetteOuterRgbPlusAdd = float4( VignetteOuterRgbPlusAdd );
        float2 lVignetteUv              = float2( iInput.mSourceUvVignetteUv.zw );
        float lfVignette = length( lVignetteUv );
        lfVignette = saturate( lfVignette + lVignetteOuterRgbPlusAdd.w );
        lfVignette = smoothstep( 0.0h, 1.0h, lfVignette );
        float3 lVignetteColour = lerp( lVignetteInnerRgb.rgb, lVignetteOuterRgbPlusAdd.rgb, lfVignette );
        lPixelColour.rgb *= lVignetteColour.rgb;
    }
    {
        float3 lTintColour = float3( Tint2dColour.rgb );
        lPixelColour += lTintColour;
    }
    oColour = float4( lPixelColour, 1.0h );
}
