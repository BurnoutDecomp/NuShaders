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
    float3 lSSAOColour = float3( tex2D( SamplerSSAO,  lSourceUv ).rgb );
    float3 lBloomColour = float3( tex2D( SamplerBloom,  lSourceUv ).rgb );
    float3 lDofColour = float3( tex2D( SamplerDof, lSourceUv ).rgb );
#ifdef D_MRT
        float  lDepthBuffer = tex2D( SamplerDepth, lSourceUv ).r;
#elif defined(D_RAWZ)
        float3 lDepthBuffer = tex2D( SamplerDepth, lSourceUv ).arg;
#elif defined(D_INTZ)
        float  lDepthBuffer = tex2D( SamplerDepth, lSourceUv ).r;
#elif defined(D_MSAA_ENABLED)
        float  lDepthBuffer = tex2D( SamplerDepth, lSourceUv ).r;
#else
        float3 lDepthBuffer = float3( tex2D( SamplerDepth, lSourceUv ).arg );
#endif
#ifdef D_MRT
    float  lfDepth      = frac( lDepthBuffer );
#elif defined(D_RAWZ)
    float3 lDepthFactor = float3( 65536.0 / 65793.0, 256.0 / 65793.0, 1.0 / 65793.0 );
    float  lfDepth      = dot( lDepthBuffer, lDepthFactor );
#elif defined(D_INTZ)
    float  lfDepth      = lDepthBuffer;
#elif defined(D_MSAA_ENABLED)
    float  lfDepth      = frac( lDepthBuffer );
#else
    float3 lDepthFactor = float3( 65536.0 / 65793.0, 256.0 / 65793.0, 1.0 / 65793.0 );
    float  lfDepth      = dot( lDepthBuffer, lDepthFactor );
#endif
    float3 lPixelColour = float3( tex2D( SamplerSource, lSourceUv ).rgb ) * lSSAOColour;
    {
        float2 lDofNearFar = float2( DofParamsA.yz  );
        float3 lDofParamsB = float3( DofParamsB.xyz );
        float lfNearGradient = ( lDofNearFar.x - lfDepth ) * lDofParamsB.y;
        float lfFarGradient  = ( lfDepth - lDofNearFar.y ) * lDofParamsB.z;
        float lfCentreDepth  = saturate( max( lfNearGradient, lfFarGradient ) ) * lDofParamsB.x;
        lPixelColour = lerp( lPixelColour, lDofColour, lfCentreDepth );
    }
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
