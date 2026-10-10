#ifndef SHADOW_FXH
#define SHADOW_FXH

#include "../Include/Transform.fxh"

// D_ROAD_X360 (build option) restores the console-authentic shadow filter, but
// ONLY in shaders that opt in with SHADOW_ROAD_X360_USER before including this
// header (the six road/tunnel shaders whose microcode it was recovered from).
// Everything else keeps the stock TUB-PC behaviour even when the flag is set.
#if defined(D_ROAD_X360) && defined(SHADOW_ROAD_X360_USER)
#define SHADOW_X360_ROADS_ACTIVE
#endif

#ifndef USE_SHARED_GLOBALS
#ifdef D_PLATFORM_X360
float4x4 ShadowMap_WorldToLight[3] : register(c4)
#else
float4x4 ShadowMap_WorldToLight[3]
#endif
<
    string scope = "global";
>;

float4 ShadowMap_Constants
<
 string scope = "global";
>;

float4 ShadowMap_Constants2
<
 string scope = "global";
>;

float4 ShadowMap_ObjectCsmSelect
<
    string scope = "object";
>;
#endif // USE_SHARED_GLOBALS

#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
// Optional native SM3 pixel input: reflection flag and half an atlas texel in
// U/V. Zero retains the original receiver path on older native executables.
float4 ShadowMap_ReflectionPC : register(c223);
#endif

#ifdef D_PLATFORM_BPR
// SM5/DX11: hardware comparison sampler (matches the Remastered binding: t15 + s15 comparison)
Texture2D              shadowMapSamplerHighDetailTexture : register(t15);
SamplerComparisonState shadowMapSamplerHighDetail        : register(s15);
#else
texture2D shadowMap0
<
 string scope   = "persistent";
 string purpose = "shadowMap";
>;

sampler2D shadowMapSamplerHighDetail : register(s15)
<
 string scope   = "persistent";
 string purpose = "shadowMap";
> = sampler_state
{
 Texture = <shadowMap0>;
 MinFilter = Linear;
 MagFilter = Linear;
 MipFilter = none;
};
#endif

#ifdef D_NO_SHADOWS
    #define SHADOWMAP_INTERPOLATORS1( semantic0 )
    #define SHADOWMAP_INTERPOLATORS2( semantic0, semantic1 )
    #define CALC_SHADOWMAP_INTERPOLATORS1( wsp, eyeZ )          1.0f
    #define CALC_SHADOWMAP_INTERPOLATORS2( wsp, eyeZ )          1.0f
    #define CALC_SHADOWMAP_INTERPOLATORS3( wsp, eyeZ )          1.0f
    #define CALC_SHADOWMAP_INTERPOLATORS3_4( wsp, eyeZ )        1.0f
    #define CALC_SHADOWMAP_INTERPOLATORS2_SELECT( wsp, eyeZ )   1.0f
    #define CALC_SHADOWMAP_INTERPOLATORS2_SELECT_VS( wsp, eyeZ, objDist )  1.0f
    #define CALC_SHADOWMAP_INTERPOLATORS_DUMMY
    #define CALC_SHADOW_FACTOR_1( NDotL )   1.0f
    #define CALC_SHADOW_FACTOR_2( NDotL )   1.0f
    #define CALC_SHADOW_FACTOR_2_SELECT( NDotL )    1.0f
    #define CALC_SHADOW_FACTOR_2_SELECT_VEHICLE( NDotL )    1.0f
    #define CALC_SHADOW_FACTOR_2_SELECT_VEHICLE_DAMAGED( NDotL, damage )    1.0f
    #define CALC_SHADOW_FACTOR_3( NDotL )   1.0f
#else
    #define SHADOWMAP_INTERPOLATORS1( semantic0 ) \
        float4 LightSpacePos0 : TEXCOORD## semantic0 ;
    #define SHADOWMAP_INTERPOLATORS2( semantic0, semantic1 ) \
        float4 LightSpacePos0 : TEXCOORD## semantic0 ; \
        float4 LightSpacePos1 : TEXCOORD## semantic1 ;
    #define CALC_SHADOWMAP_INTERPOLATORS1( wsp, eyeZ )          GetShadowMapPositions1CSM( wsp, eyeZ, OUT.LightSpacePos0 )
    #define CALC_SHADOWMAP_INTERPOLATORS2( wsp, eyeZ )          GetShadowMapPositions2CSM( wsp, eyeZ, OUT.LightSpacePos0, OUT.LightSpacePos1 )
    #define CALC_SHADOWMAP_INTERPOLATORS3( wsp, eyeZ )          GetShadowMapPositions3CSM( wsp, eyeZ, OUT.LightSpacePos0, OUT.LightSpacePos1 )
    #define CALC_SHADOWMAP_INTERPOLATORS3_4( wsp, eyeZ )        GetShadowMapPositions3CSM_4( wsp, eyeZ, OUT.LightSpacePos0, OUT.LightSpacePos1 )
    #define CALC_SHADOWMAP_INTERPOLATORS2_SELECT( wsp, eyeZ )   GetShadowMapPositions2CSMSelect( wsp, eyeZ, OUT.LightSpacePos0, OUT.LightSpacePos1 )
    #define CALC_SHADOWMAP_INTERPOLATORS2_SELECT_VS( wsp, eyeZ, objDist )   GetShadowMapPositions2CSMSelectVS( wsp, eyeZ, objDist, OUT.LightSpacePos0, OUT.LightSpacePos1 )
    #define CALC_SHADOWMAP_INTERPOLATORS_DUMMY                  OUT.LightSpacePos0 = float4( 0.0, 0.0, 0.0, 0.0 ); \
                                                                OUT.LightSpacePos1 = float4( 0.0, 0.0, 0.0, 0.0 );
    #define CALC_SHADOW_FACTOR_1( NDotL )                                 CalcShadowFactor1CSM( IN.LightSpacePos0, NDotL )
    #define CALC_SHADOW_FACTOR_2( NDotL )                                 CalcShadowFactor2CSM( IN.LightSpacePos0, IN.LightSpacePos1, NDotL )
    #define CALC_SHADOW_FACTOR_2_SELECT( NDotL )                          CalcShadowFactor2CSMSelect( IN.LightSpacePos0, IN.LightSpacePos1, NDotL )
    #define CALC_SHADOW_FACTOR_2_SELECT_VEHICLE( NDotL )                  CalcShadowFactorCSM_Vehicle_Damaged_2CSM_Select( IN.LightSpacePos0, IN.LightSpacePos1, NDotL, 0.0 )
    #define CALC_SHADOW_FACTOR_2_SELECT_VEHICLE_DAMAGED( NDotL, damage )  CalcShadowFactorCSM_Vehicle_Damaged_2CSM_Select( IN.LightSpacePos0, IN.LightSpacePos1, NDotL, damage )
#ifdef D_PLATFORM_X360
    #define CALC_SHADOW_FACTOR_3_FORCE_ANISO( NDotL )                     CalcShadowFactor3CSM_X360_Aniso( IN.LightSpacePos0, IN.LightSpacePos1, NDotL )
    #define CALC_SHADOW_FACTOR_3_FORCE_NO_ANISO( NDotL )                  CalcShadowFactor3CSM( IN.LightSpacePos0, IN.LightSpacePos1, NDotL )
#ifdef D_SHADOWMAP_ANISOTROPIC
    #define CALC_SHADOW_FACTOR_3( NDotL )                                 CalcShadowFactor3CSM_X360_Aniso( IN.LightSpacePos0, IN.LightSpacePos1, NDotL )
#else
    #define CALC_SHADOW_FACTOR_3( NDotL )                                 CalcShadowFactor3CSM( IN.LightSpacePos0, IN.LightSpacePos1, NDotL )
#endif
#else
#ifdef SHADOW_X360_ROADS_ACTIVE
    // D_ROAD_X360: use the reconstructed console shadow filter on non-X360 targets too
    #define CALC_SHADOW_FACTOR_3( NDotL )                                 CalcShadowFactor3CSM_X360_Aniso( IN.LightSpacePos0, IN.LightSpacePos1, NDotL )
#else
    #define CALC_SHADOW_FACTOR_3( NDotL )                                 CalcShadowFactor3CSM( IN.LightSpacePos0, IN.LightSpacePos1, NDotL )
#endif
#endif
#if defined(D_HACK_FORCE_3CSM)
    #define SHADOWMAP_INTERPOLATORS( semantic0, semantic1, semantic2 ) \
        float4 LightSpacePos0 : TEXCOORD## semantic0 ; \
        float4 LightSpacePos1 : TEXCOORD## semantic1 ;
    #define GetShadowMapPositions( wsp, l0, l1 )        GetShadowMapPositions3CSM( wsp, OUT.hPosition.w, OUT.LightSpacePos0, OUT.LightSpacePos1 )
    #define CalcShadowFactorCSM_New( l0, l1, NDotL )    CalcShadowFactor3CSM( IN.LightSpacePos0, IN.LightSpacePos1, NDotL )
#endif
#endif

#ifdef D_SOFT_SHADOWS
#define M_PI 3.14159265358979323846
#define fmodp(x,n) ((n)*frac((x)/(n)))
float2 rand(float2 ij)
{
  const float4 a=float4(M_PI * M_PI * M_PI * M_PI, exp(4.0), pow(13.0, M_PI / 2.0), sqrt(1997.0));
  float4 result =float4(ij,ij);
  for(int i = 0; i < 3; i++)
  {
      result.x = frac(dot(result, a));
      result.y = frac(dot(result, a));
      result.z = frac(dot(result, a));
      result.w = frac(dot(result, a));
  }
  return (result.xy) - 0.5;
}
#endif

#ifdef D_PLATFORM_BPR
// SM5: same call shape as the SM3 non-soft path (callers pass the sampler), but
// a hardware comparison sampler. (Do not define D_SOFT_SHADOWS for BPR.)
#ifndef D_BPR_SHADOW_BIAS
#define D_BPR_SHADOW_BIAS 0.01   // depth bias to kill self-shadow acne (diagnostic value; tune down once confirmed)
#endif
float CalcOrthoShadowFactorBySampler( SamplerComparisonState shadowMapSampler, in float3 lLightSpacePos )
{
    // Hardware PCF: .xy = shadow-map UV, .z = depth to compare (biased toward the light).
    return shadowMapSamplerHighDetailTexture.SampleCmpLevelZero( shadowMapSampler, lLightSpacePos.xy, lLightSpacePos.z - D_BPR_SHADOW_BIAS );
}
#else
float CalcOrthoShadowFactorBySampler(
        sampler2D   shadowMapSampler,
 in float3   lLightSpacePos
#ifdef D_SOFT_SHADOWS
       ,in float    eyeZ
#endif
                                   )
{
#ifdef D_SOFT_SHADOWS
 if (eyeZ > 140.0f)
 {
  return 0.0f;
 }
 else
 {
    float4 shadowedSum = 0;
    float4 shadowedSum2 = 0;
 float4 jitterOffset = 0.0;
 float4 jitterTex2dOffset = 0.0;
 float jitterKernelSize = 8.0f;
 int kernelSize = 2;
 int intpart;
 #define jitterScale 12
    #define GB_KERNEL_SIZE 2.0
    #define GB_KERNEL_SPARSE_SIZE 4.0
    float blurRadius = 5.0 * ( 1.0 - ( min( eyeZ, 140.0f ) / 140.0f ) );
    float falloff = blurRadius*blurRadius*2.0;
 float weightTotal = 0.0f;
    for ( float v = -GB_KERNEL_SIZE*GB_KERNEL_SIZE; v <= GB_KERNEL_SIZE*GB_KERNEL_SIZE; v+=GB_KERNEL_SPARSE_SIZE )
    {
        for ( float u = -GB_KERNEL_SIZE*GB_KERNEL_SIZE; u <= GB_KERNEL_SIZE*GB_KERNEL_SIZE; u+=GB_KERNEL_SPARSE_SIZE )
        {
            float weight = exp(-(u*u + v*v) / (falloff));
            float4 t = tex2Dproj( shadowMapSampler, float4( lLightSpacePos, 1.0 ) + float4(u/4096.0, v/3072.0, 0, 0) ).r;
            shadowedSum += weight * t;
   weightTotal += weight;
        }
    }
    shadowedSum *= 1.0 / (weightTotal);
    return saturate(shadowedSum.x);
    }
#else
    return tex2Dproj( shadowMapSampler, float4( lLightSpacePos, 1.0 ) ).r;
#endif
}
#endif // D_PLATFORM_BPR  (CalcOrthoShadowFactorBySampler)

#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
bool ReflectionShadowPositionValidPC(float3 position, float cascade)
{
    float2 lower = float2(ShadowMap_ReflectionPC.y, cascade / 3.0 + ShadowMap_ReflectionPC.z);
    float2 upper = float2(1.0 - ShadowMap_ReflectionPC.y, (cascade + 1.0) / 3.0 - ShadowMap_ReflectionPC.z);
    return all(position.xy >= lower) && all(position.xy <= upper)
        && position.z >= 0.0 && position.z <= 1.0;
}

void SelectReflectionShadow3PC(float3 position0, float3 position1, float3 position2,
                              inout float3 position, inout float cascade)
{
    if (ShadowMap_ReflectionPC.x > 0.0)
    {
        if (cascade < 1.0 && !ReflectionShadowPositionValidPC(position, cascade))
        {
            position = position1;
            cascade = 1.0;
        }
        if (cascade < 2.0 && !ReflectionShadowPositionValidPC(position, cascade))
        {
            position = position2;
            cascade = 2.0;
        }
    }
}

void SelectReflectionShadow2PC(float3 position1, float firstCascade,
                              inout float3 position, inout float cascade)
{
    if (ShadowMap_ReflectionPC.x > 0.0 && cascade == firstCascade
        && !ReflectionShadowPositionValidPC(position, cascade))
    {
        position = position1;
        cascade = firstCascade + 1.0;
    }
}

float CalcReflectionShadowFactorPC(float3 position, float cascade, float eyeZ)
{
    bool valid = ReflectionShadowPositionValidPC(position, cascade);
    if (ShadowMap_ReflectionPC.x > 0.0)
    {
        // Keep the hardware PCF footprint in this tile even when the sample's
        // result is discarded. Sampler CLAMP alone only bounds the whole atlas.
        float2 lower = float2(ShadowMap_ReflectionPC.y, cascade / 3.0 + ShadowMap_ReflectionPC.z);
        float2 upper = float2(1.0 - ShadowMap_ReflectionPC.y, (cascade + 1.0) / 3.0 - ShadowMap_ReflectionPC.z);
        position.xy = clamp(position.xy, lower, upper);
    }
#ifdef D_SOFT_SHADOWS
    float factor = CalcOrthoShadowFactorBySampler(shadowMapSamplerHighDetail, position, eyeZ);
#else
    float factor = CalcOrthoShadowFactorBySampler(shadowMapSamplerHighDetail, position);
#endif
    return (ShadowMap_ReflectionPC.x > 0.0 && !valid) ? 1.0 : factor;
}
#endif

void
GetShadowMapPositions2CSMSelect(
    in float3 positionWorld,
    in float eyeZ,
    out float4 texCoord0,
    out float4 texCoord1 )
{
    eyeZ = GetShadowReceiverDepthPC(positionWorld, eyeZ);
    float3 position0 = mul( float4( positionWorld,1 ), ShadowMap_WorldToLight[ShadowMap_ObjectCsmSelect.x] ).xyz;
    float3 position1 = mul( float4( positionWorld,1 ), ShadowMap_WorldToLight[ShadowMap_ObjectCsmSelect.y] ).xyz;
    texCoord0 = float4( position0, eyeZ );
    texCoord1 = float4( position1, ShadowMap_ObjectCsmSelect.z );
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    // Preserve the pair's first cascade in the sign of its positive split.
    // The matched pixel program uses the absolute split for the original test.
    texCoord1.w *= (ShadowMap_ObjectCsmSelect.x < 0.5) ? 1.0 : -1.0;
#endif
}

void
GetShadowMapPositions2CSMSelectVS(
    in float3 positionWorld,
    in float eyeZ,
    in float objectDistance,
    out float4 texCoord0,
    out float4 texCoord1 )
{
    eyeZ = GetShadowReceiverDepthPC(positionWorld, eyeZ);
    int     firstCsmIndex  = ( objectDistance < ShadowMap_Constants2.x ) ? 0 : 1;
    float   distanceThresh = ( objectDistance < ShadowMap_Constants2.x ) ? ShadowMap_Constants.x : ShadowMap_Constants.y;
    float3 position0 = mul( float4( positionWorld,1 ), ShadowMap_WorldToLight[firstCsmIndex] ).xyz;
    float3 position1 = mul( float4( positionWorld,1 ), ShadowMap_WorldToLight[firstCsmIndex+1] ).xyz;
    texCoord0 = float4( position0, eyeZ );
    texCoord1 = float4( position1, distanceThresh );
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    texCoord1.w *= (firstCsmIndex == 0) ? 1.0 : -1.0;
#endif
}

void
GetShadowMapPositions3CSM(
    in float3 positionWorld,
    in float eyeZ,
    out float4 texCoord0,
    out float4 texCoord1 )
{
    eyeZ = GetShadowReceiverDepthPC(positionWorld, eyeZ);
#if defined(D_PLATFORM_BPR) && !defined(SHADOW_X360_ROADS_ACTIVE)
    // BPR does the cascade transform + PCF in the pixel shader (matches the
    // Remastered Specular_1Bit), so just carry world position + eyeZ.
    texCoord0 = float4( positionWorld, eyeZ );
    texCoord1 = float4( 0, 0, 0, 0 );
#else
    float3 position0 = mul( float4( positionWorld,1 ), ShadowMap_WorldToLight[0] ).xyz;
    float3 position1 = mul( float4( positionWorld,1 ), ShadowMap_WorldToLight[1] ).xyz;
    float3 position2 = mul( float4( positionWorld,1 ), ShadowMap_WorldToLight[2] ).xyz;
    texCoord0 = float4( position0, eyeZ );
    texCoord1 = float4( position1.xy, position2.xy );
#ifndef SHADOW_X360_ROADS_ACTIVE
    // PC-only keep-alive hack; absent from the console VS (it perturbs the VS CTAB)
    texCoord1.x += ShadowMap_Constants2.x * 1e-30;
#endif
#endif
}

void
GetShadowMapPositions3CSM_4(
    in float4 positionWorld,
    in float  eyeZ,
    out float4 texCoord0,
    out float4 texCoord1 )
{
    GetShadowMapPositions3CSM( positionWorld.xyz, eyeZ, texCoord0, texCoord1 );
}

void
GetShadowMapPositions2CSM(
    in float3 positionWorld,
    in float eyeZ,
    out float4 texCoord0,
    out float4 texCoord1 )
{
    eyeZ = GetShadowReceiverDepthPC(positionWorld, eyeZ);
    float3 position0 = mul( float4( positionWorld,1), ShadowMap_WorldToLight[0] ).xyz;
    float3 position1 = mul( float4( positionWorld,1), ShadowMap_WorldToLight[1] ).xyz;
    texCoord0 = float4( position0, eyeZ );
    texCoord1 = float4( position1, 1.0 );
}

void
GetShadowMapPositions1CSM(
    in float3 positionWorld,
    in float eyeZ,
    out float4 texCoord0 )
{
    eyeZ = GetShadowReceiverDepthPC(positionWorld, eyeZ);
    float3 position0 = mul( float4( positionWorld,1 ), ShadowMap_WorldToLight[0] ).xyz;
    texCoord0 = float4( position0, eyeZ );
}

float ApplyFade( float factor, float fadeValue )
{
    return factor * fadeValue + (float)1 - fadeValue;
}

float
CalcShadowFactor3CSM(
 in float4   lLightSpacePosPacked0,
    in float4   lLightSpacePosPacked1,
    in float     NDotL )
{
#ifdef D_PLATFORM_BPR
    // Ported from Remastered Specular_1Bit PS: transform world position into the
    // selected cascade's light space in the pixel shader, then do a 3x3 Gaussian PCF.
    // lLightSpacePosPacked0 = (worldPos.xyz, eyeZ) (see GetShadowMapPositions3CSM).
    float3 worldPos = lLightSpacePosPacked0.xyz;
    float  eyeZ     = lLightSpacePosPacked0.w;
    float  shadowed;
    if ( eyeZ > 140.0 )
    {
        shadowed = 0.0;
    }
    else
    {
        float4x4 w2l = ( eyeZ < ShadowMap_Constants.x ) ? ShadowMap_WorldToLight[0]
                     : ( eyeZ < ShadowMap_Constants.y ) ? ShadowMap_WorldToLight[1]
                                                        : ShadowMap_WorldToLight[2];
        float  texelStep = ShadowMap_Constants3.x;                       // world-space PCF kernel step
        float3 lsCenter  = mul( float4( worldPos, 1.0 ), w2l ).xyz;
        float3 lsDeltaX  = mul( float4( worldPos + float3( texelStep, 0, 0 ), 1.0 ), w2l ).xyz - lsCenter;
        float3 lsDeltaY  = mul( float4( worldPos + float3( 0, texelStep, 0 ), 1.0 ), w2l ).xyz - lsCenter;
#ifdef SHADOW_APPLY_Z_BIAS
        lsCenter.z      -= ShadowMap_Constants2.z * SHADOW_Z_BIAS_VALUE; // engine-scaled depth bias (exact match to the Remastered boardwalk PS)
#endif
        float  blur      = 5.0 * ( 1.0 - min( 140.0, eyeZ ) / 140.0 );
        float  falloff   = max( 2.0 * blur * blur, 1e-4 );
        float  sum = 0.0, weightTotal = 0.0;
        [unroll] for ( int i = -1; i <= 1; i++ )
        {
            [unroll] for ( int j = -1; j <= 1; j++ )
            {
                float  weight = exp2( 1.44269502 * ( -(float)( i*i + j*j ) / falloff ) );
                float3 sp     = lsCenter + (float)i * lsDeltaY + (float)j * lsDeltaX;
                sum         += weight * shadowMapSamplerHighDetailTexture.SampleCmpLevelZero( shadowMapSamplerHighDetail, sp.xy, sp.z );
                weightTotal += weight;
            }
        }
        shadowed = saturate( sum / weightTotal );
    }
    float cutoffFactor = saturate( ( NDotL - 0.25 ) * 20.0 );
    float fadeValue    = saturate( (float)ShadowMap_Constants.w - eyeZ * (float)ShadowMap_Constants2.w );
    return ApplyFade( shadowed * cutoffFactor, fadeValue );
#else
    float3 lLightSpacePos0 = lLightSpacePosPacked0.xyz;
    float3 lLightSpacePos1 = float3( lLightSpacePosPacked1.xy, lLightSpacePosPacked0.z );
    float3 lLightSpacePos2 = float3( lLightSpacePosPacked1.zw, lLightSpacePosPacked0.z );
    float3 lLightSpacePos1or2   = ( lLightSpacePosPacked0.w < ShadowMap_Constants.y ? lLightSpacePos1 : lLightSpacePos2 );
    float3 tc3                  = ( lLightSpacePosPacked0.w < ShadowMap_Constants.x ? lLightSpacePos0 : lLightSpacePos1or2 );
    float3 metaMapTexCoord      = tc3;
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    float cascade = (lLightSpacePosPacked0.w < ShadowMap_Constants.x) ? 0.0
                  : (lLightSpacePosPacked0.w < ShadowMap_Constants.y) ? 1.0 : 2.0;
    SelectReflectionShadow3PC(lLightSpacePos0, lLightSpacePos1, lLightSpacePos2, metaMapTexCoord, cascade);
#endif
#ifdef SHADOW_APPLY_Z_BIAS
    // The console double-sided and Sign pixel shaders pull the compare depth toward the
    // light before the PCF taps (Diffuse_Opaque_Doublesided PS: z - ShadowMap_Constants2.z *
    // 0.0005). A double-sided caster keeps its front faces in the map, so without this the
    // surface shadows itself in texel-row stripes.
    metaMapTexCoord.z          -= ShadowMap_Constants2.z * SHADOW_Z_BIAS_VALUE;
#endif
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    float factor = CalcReflectionShadowFactorPC(metaMapTexCoord, cascade, lLightSpacePosPacked0.w);
#elif defined(D_SOFT_SHADOWS)
    float factor = CalcOrthoShadowFactorBySampler( shadowMapSamplerHighDetail, metaMapTexCoord, lLightSpacePosPacked0.w );
#else
    float factor = CalcOrthoShadowFactorBySampler( shadowMapSamplerHighDetail, metaMapTexCoord );
#endif
    float cutoffFactor = saturate( ( NDotL - 0.25 ) * 20.0 );
    float fadeValue    = saturate( (float)ShadowMap_Constants.w - (float)lLightSpacePosPacked0.w * (float)ShadowMap_Constants2.w );
    return ApplyFade( factor * cutoffFactor, fadeValue );
#endif
}

#if defined(D_PLATFORM_X360) || defined(SHADOW_X360_ROADS_ACTIVE)
// X360 anisotropic variant, which got culled when the source was preprocessed for TUB PC
#ifdef SHADOW_X360_ROADS_ACTIVE
// Reconstructed from the retail X360 (Breaker) road/tunnel pixel shader microcode
// (resources E357DCFD / 245284A8 / F59D9209 / 89C8D9A5 / 1A2FE055 / E9A93A7A):
//  - two sample points at tc +/- 0.25 * (ddx(tc) + ddy(tc)), each a bilinear
//    2x2 PCF with the depth compare done against cascade 0's z, no depth bias.
//    (The microcode reconstructs the bilinear PCF manually -- 4 point fetches
//    at +/-0.5 texel plus getWeights -- because Xenos has no compare sampler;
//    one hardware-compare bilinear tap per point computes the same thing.)
//  - the footprint falls back to a fixed 0.75-texel kernel when smaller than
//    ~1 texel (console literals: scale (640, 960), fallback
//    (0.001171875, 0.00078125) = 0.75 / (640, 960)).
//  - the anisotropic offset collapses to zero on quads that straddle a
//    cascade seam (screen-space gradients of the cascade selectors).
//  - tail: open-road shaders (SHADOW_APPLY_FADE_ROAD) settle at
//    ShadowMap_Constants2.y beyond the fade distance; tunnel shaders fade the
//    whole direct term to zero. The NDotL cutoff matches the stock formula
//    (call sites passing a literal 1.0 fold it away, exactly as the microcode
//    shows for the two plain DriveableSurface shaders).
#ifndef D_ROAD_X360_SHADOW_TEXELS
#define D_ROAD_X360_SHADOW_TEXELS float2( 640.0, 960.0 )
#endif

float CalcShadowCompareTap_X360Aniso( float2 uv, float z, float cascade )
{
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    return CalcReflectionShadowFactorPC(float3(uv, z), cascade, 0.0);
#elif defined(D_PLATFORM_BPR)
    return shadowMapSamplerHighDetailTexture.SampleCmpLevelZero( shadowMapSamplerHighDetail, uv, z );
#else
    return tex2Dproj( shadowMapSamplerHighDetail, float4( uv, z, 1.0 ) ).r;
#endif
}

float
CalcShadowFactor3CSM_X360_Aniso(
 in float4   lLightSpacePosPacked0,
    in float4   lLightSpacePosPacked1,
    in float     NDotL )
{
    float  eyeZ = lLightSpacePosPacked0.w;
    float2 sel  = float2( ( eyeZ < ShadowMap_Constants.y ) ? 1.0 : 0.0,
                          ( eyeZ < ShadowMap_Constants.x ) ? 1.0 : 0.0 );
    float2 tc12 = ( sel.x > 0.0 ) ? lLightSpacePosPacked1.xy : lLightSpacePosPacked1.zw;
    float2 tc   = ( sel.y > 0.0 ) ? lLightSpacePosPacked0.xy : tc12;
    float  tcz  = lLightSpacePosPacked0.z;   // always cascade 0's depth
    float cascade = (sel.y > 0.0) ? 0.0 : (sel.x > 0.0) ? 1.0 : 2.0;
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    float3 position = float3(tc, tcz);
    SelectReflectionShadow3PC(lLightSpacePosPacked0.xyz,
        float3(lLightSpacePosPacked1.xy, tcz), float3(lLightSpacePosPacked1.zw, tcz), position, cascade);
    tc = position.xy;
    sel = float2((cascade < 2.0) ? 1.0 : 0.0, (cascade < 1.0) ? 1.0 : 0.0);
#endif

    // collapse the kernel on quads that straddle a cascade seam
    float  seam   = abs( ddx( sel.x ) ) + abs( ddy( sel.x ) )
                  + abs( ddx( sel.y ) ) + abs( ddy( sel.y ) );
    float  spread = ( seam > 0.0 ) ? 0.0 : 0.25;

    // anisotropic footprint; fixed 0.75-texel kernel when under ~1 texel
    float2 dir = ddx( tc ) + ddy( tc );
    if ( dot( abs( dir ), D_ROAD_X360_SHADOW_TEXELS ) < 1.0 )
        dir = 0.75 / D_ROAD_X360_SHADOW_TEXELS;

    float2 off    = spread * dir;
    float  factor = 0.5 * ( CalcShadowCompareTap_X360Aniso( tc + off, tcz, cascade )
                          + CalcShadowCompareTap_X360Aniso( tc - off, tcz, cascade ) );

    float cutoffFactor = saturate( ( NDotL - 0.25 ) * 20.0 );
    float fadeValue    = saturate( (float)ShadowMap_Constants.w - eyeZ * (float)ShadowMap_Constants2.w );
#ifdef SHADOW_APPLY_FADE_ROAD
    // open-road: beyond the fade distance the shadow settles at ShadowMap_Constants2.y
    // (this constant is referenced nowhere else in the TUB-PC preprocessed tree --
    // selecting it is evidently what SHADOW_APPLY_FADE_ROAD was for)
    return saturate( factor * cutoffFactor ) * fadeValue
         + ( 1.0 - fadeValue ) * (float)ShadowMap_Constants2.y;
#else
    // tunnel: fades the whole direct term to zero
    return factor * cutoffFactor * fadeValue;
#endif
}
#else
// Currently we just call the standard 3CSM until we decompile the 360 version of the function
float
CalcShadowFactor3CSM_X360_Aniso(
 in float4   lLightSpacePosPacked0,
    in float4   lLightSpacePosPacked1,
    in float     NDotL )
{
    return CalcShadowFactor3CSM( lLightSpacePosPacked0, lLightSpacePosPacked1, NDotL );
}
#endif
#endif

float
CalcShadowFactor2CSM(
 in float4   lLightSpacePosPacked0,
    in float4   lLightSpacePosPacked1,
    in float     NDotL )
{
    float3 lLightSpacePos0 = lLightSpacePosPacked0.xyz;
    float3 lLightSpacePos1 = lLightSpacePosPacked1.xyz;
    float3 lLightSpacePos0or1   = ( lLightSpacePosPacked0.w < ShadowMap_Constants.x ? lLightSpacePos0 : lLightSpacePos1 );
    float3 metaMapTexCoord      = lLightSpacePos0or1;
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    float cascade = (lLightSpacePosPacked0.w < ShadowMap_Constants.x) ? 0.0 : 1.0;
    SelectReflectionShadow2PC(lLightSpacePos1, 0.0, metaMapTexCoord, cascade);
    float factor = CalcReflectionShadowFactorPC(metaMapTexCoord, cascade, lLightSpacePosPacked0.w);
#elif defined(D_SOFT_SHADOWS)
    float factor = CalcOrthoShadowFactorBySampler( shadowMapSamplerHighDetail, metaMapTexCoord, lLightSpacePosPacked0.w );
#else
    float factor = CalcOrthoShadowFactorBySampler( shadowMapSamplerHighDetail, metaMapTexCoord );
#endif
    float cutoffFactor = saturate( ( NDotL - 0.25 ) * 20.0 );
    factor *= cutoffFactor;
    return ( lLightSpacePosPacked0.w<(float)ShadowMap_Constants.y ) ? factor : 0.75;
}

float
CalcShadowFactor2CSMSelect(
 in float4   lLightSpacePosPacked0,
    in float4   lLightSpacePosPacked1,
    in float     NDotL )
{
    float3 lLightSpacePos0 = lLightSpacePosPacked0.xyz;
    float3 lLightSpacePos1 = lLightSpacePosPacked1.xyz;
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    float split = abs(lLightSpacePosPacked1.w);
#else
    float split = lLightSpacePosPacked1.w;
#endif
    float3 lLightSpacePos0or1   = ( lLightSpacePosPacked0.w < split ? lLightSpacePos0 : lLightSpacePos1 );
    float3 metaMapTexCoord      = lLightSpacePos0or1;
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    float firstCascade = (lLightSpacePosPacked1.w < 0.0) ? 1.0 : 0.0;
    float cascade = firstCascade + ((lLightSpacePosPacked0.w < split) ? 0.0 : 1.0);
    SelectReflectionShadow2PC(lLightSpacePos1, firstCascade, metaMapTexCoord, cascade);
    float factor = CalcReflectionShadowFactorPC(metaMapTexCoord, cascade, lLightSpacePosPacked0.w);
#elif defined(D_SOFT_SHADOWS)
    float factor = CalcOrthoShadowFactorBySampler( shadowMapSamplerHighDetail, metaMapTexCoord, lLightSpacePosPacked0.w );
#else
    float factor = CalcOrthoShadowFactorBySampler( shadowMapSamplerHighDetail, metaMapTexCoord );
#endif
    float cutoffFactor = saturate( ( NDotL - 0.25 ) * 20.0 );
    float fadeValue    = saturate( (float)ShadowMap_Constants.w - (float)lLightSpacePosPacked0.w * (float)ShadowMap_Constants2.w );
    return ApplyFade( factor * cutoffFactor, fadeValue );
}

float
CalcShadowFactor1CSM(
 in float4   lLightSpacePosPacked0,
    in float     NDotL )
{
    float3 lLightSpacePos0 = lLightSpacePosPacked0.xyz;
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    float factor = CalcReflectionShadowFactorPC(lLightSpacePos0, 0.0, 0.0);
#elif defined(D_SOFT_SHADOWS)
    float factor = CalcOrthoShadowFactorBySampler( shadowMapSamplerHighDetail, lLightSpacePos0, 0.0 );
#else
    float factor = CalcOrthoShadowFactorBySampler( shadowMapSamplerHighDetail, lLightSpacePos0 );
#endif
    float cutoffFactor = saturate( ( NDotL - 0.25 ) * 20.0 );
    float fadeValue    = saturate( (float)ShadowMap_Constants.w - ( (float)lLightSpacePosPacked0.w/(float)ShadowMap_Constants.x ) * (float)ShadowMap_Constants.w );
    return ApplyFade( factor * cutoffFactor, fadeValue );
}

float
CalcShadowFactorCSM_Vehicle_Damaged_2CSM_Select
(
    in float4   lLightSpacePosPacked0,
    in float4   lLightSpacePosPacked1,
    in float     NDotL,
    in float     damage )
{
    float3 lLightSpacePos0 = lLightSpacePosPacked0.xyz;
    float3 lLightSpacePos1 = lLightSpacePosPacked1.xyz;
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    float split = abs(lLightSpacePosPacked1.w);
#else
    float split = lLightSpacePosPacked1.w;
#endif
    float3 lLightSpacePos0or1 = ( lLightSpacePosPacked0.w < split ? lLightSpacePos0 : lLightSpacePos1 );
    float3 metaMapTexCoord    = lLightSpacePos0or1;
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    float firstCascade = (lLightSpacePosPacked1.w < 0.0) ? 1.0 : 0.0;
    float cascade = firstCascade + ((lLightSpacePosPacked0.w < split) ? 0.0 : 1.0);
    SelectReflectionShadow2PC(lLightSpacePos1, firstCascade, metaMapTexCoord, cascade);
#endif
    float damageBiasModifier = saturate( damage - 0.05 ) * 0.002;
    float biasFactor = 1.0 - NDotL * NDotL;
    float nearBias   = min( -0.00019 * biasFactor, -0.000051 ) - damageBiasModifier;
    metaMapTexCoord.z+=nearBias * ShadowMap_Constants2.z;
#ifdef D_PC_REFLECTION_SHADOW_BOUNDS
    float factor = CalcReflectionShadowFactorPC(metaMapTexCoord, cascade, lLightSpacePosPacked0.w);
#elif defined(D_SOFT_SHADOWS)
    float factor = CalcOrthoShadowFactorBySampler( shadowMapSamplerHighDetail, metaMapTexCoord, lLightSpacePosPacked0.w );
#else
    float factor = CalcOrthoShadowFactorBySampler( shadowMapSamplerHighDetail, metaMapTexCoord );
#endif
    float cutoffFactor = saturate( ( NDotL - 0.25 ) * 20.0 );
    return factor * cutoffFactor;
}

#endif
