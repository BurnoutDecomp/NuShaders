struct PixelData
{
    float4 mPosition           : POSITION;
    float4 mSourceUvVignetteUv : TEXCOORD0;
    float3 mBlurCoeffs         : TEXCOORD1;
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
    in float2 iScreenPos : VPOS,
    out float4 oColour : COLOR
    )
{
    float2 lSourceUv = float2( iInput.mSourceUvVignetteUv.xy );
    float3 lSSAOColour = float3( 1.0, 1.0, 1.0 );
    float3 lBloomColour = float3( tex2D( SamplerBloom,  lSourceUv ).rgb );
#ifdef D_MRT
        float lDepthStencil       = tex2D( SamplerDepth, lSourceUv ).r;
        float lDepthBuffer        = lDepthStencil;
        float lfMotionBlurAmount  = ( lDepthStencil >= 2.0f )?MotionBlurStencilValues.y:MotionBlurStencilValues.x;
#elif defined(D_RAWZ)
        float4 lDepthStencil      = tex2D( SamplerDepth, lSourceUv ).argb;
        float3 lDepthBuffer       = lDepthStencil.xyz;
        float  lfMotionBlurAmount = lDepthStencil.w;
#elif defined(D_INTZ)
        float4 lDepthStencil      = tex2D( SamplerDepth, lSourceUv ).argb;
        float3 lDepthBuffer       = lDepthStencil.y;
        float  lfMotionBlurAmount = lDepthStencil.w;
#elif defined(D_MSAA_ENABLED)
        float lDepthStencil       = tex2D( SamplerDepth, lSourceUv ).r;
        float lDepthBuffer        = lDepthStencil;
        float lfMotionBlurAmount  = ( lDepthStencil >= 2.0f )?MotionBlurStencilValues.y:MotionBlurStencilValues.x;
#else
        float4 lDepthStencil       = float4( tex2D( SamplerDepth, lSourceUv ).argb );
        float3 lDepthBuffer        = lDepthStencil.xyz;
        float  lfMotionBlurAmount  = lDepthStencil.w;
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
    float3 lCoeffs = ( BlurMatrixZZZ.xyz * lfDepth   ) + iInput.mBlurCoeffs.xyz;
    float2 lUvDiff = ( lSourceUv.xy      * lCoeffs.z ) + lCoeffs.xy;
    lUvDiff *= lfMotionBlurAmount;
    float lfNoise = frac( ( iScreenPos.x * 0.6180339887 ) + ( iScreenPos.y * iScreenPos.y * 0.3819660112 ) );
        float2 lUv = lSourceUv - ( lUvDiff * 0.5f );
            float2 lUvStep = lUvDiff * ( 1.0f / float((16)) );
            lUv += lUvStep * lfNoise;
        float3  lPixelColour = float3( tex2D( SamplerSource, lUv ).rgb ) * lSSAOColour;
        for( int i = 1; i < (16); i++ )
        {
            lUv += lUvStep;
            lPixelColour += float3( tex2D( SamplerSource, lUv ).rgb ) * lSSAOColour;
        }
        lPixelColour *= ( 1.0h / float((16)) );
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
