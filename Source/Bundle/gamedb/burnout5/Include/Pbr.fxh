#ifndef PBR_FXH
#define PBR_FXH

// ===========================================================================
//  Reusable metallic-roughness PBR for Burnout shaders. Include after Platform.fxh.
//  Self-contained: every input is passed in (no global/interpolator/texture assumptions
//  beyond the cube argument), so any shader can drive it:
//
//    float3 F0    = PbrF0( albedo, metallic );
//    float3 col   = PbrDirect( N, L, V, albedo, roughness, metallic, F0, keyCol, keySpecCol, indirect, shadow );
//    float3 env   = PbrSampleEnv( TEXCUBE_ARG(cube), reflVec, roughness );   // (optionally blend SSR over env)
//    col         += PbrReflection( env, NdotV, roughness, F0, metallic, indirect, shadow, hdrLevel );
//
//  Convention: albedo = base colour (metals tint their reflection via F0); roughness 0=mirror..1=matte;
//  metallic 0=dielectric..1=metal; indirect = scene ambient irradiance; shadow = 0(shadowed)..1(lit).
// ===========================================================================

#include "GGX.fxh"
#ifdef D_OREN_NAYAR
#include "OrenNayar.fxh"
#endif

#ifndef D_PBR_SPECULAR_SCALE
#define D_PBR_SPECULAR_SCALE 1.0      // direct-specular (GGX) multiplier
#endif
#ifndef D_PBR_REFLECTION_SCALE
#define D_PBR_REFLECTION_SCALE 1.0    // overall environment-reflection intensity
#endif
#ifndef D_PBR_REFLECTION_AMBIENT
#define D_PBR_REFLECTION_AMBIENT 0.85 // how much the cube's intensity tracks sky brightness (dims it at night). 0=constant cube, 1=fully fades to night
#endif
#ifndef D_PBR_REFLECTION_FILL
#define D_PBR_REFLECTION_FILL 0.5     // additive sky-coloured floor so dark cube directions stay visible (lower = more cube, higher = brighter/flatter)
#endif
#ifndef D_PBR_REFLECTION_CUBE
#define D_PBR_REFLECTION_CUBE 2.25     // cube-reflection intensity boost (the way to get MORE cube without removing the floor; the cube source is dark)
#endif
#ifndef D_PBR_REFLECTION_SHADOW
#define D_PBR_REFLECTION_SHADOW 0.3   // how much a cast shadow darkens the whole reflection (0=none, 1=fully)
#endif
#ifndef D_PBR_REFLECTION_MAXMIP
#define D_PBR_REFLECTION_MAXMIP 7.0   // top cube mip index (roughness->mip blur, when the cube is mipped)
#endif

// Normal-incidence reflectance: dielectric 0.04, metal = albedo.
float3 PbrF0( float3 albedo, float metallic )
{
    return lerp( float3(0.04, 0.04, 0.04), albedo, metallic );
}

// Roughness-controlled environment sample from a cube.
//  - D_PBR_REFLECTION_BLUR: no-mip fake prefilter (jitter the reflection vector over a Fibonacci disc,
//    cone = roughness * BLUR). Smooth -> one sharp tap; rough -> 12-tap blur. Works on un-mipped cubes.
//  - D_PBR_REFLECTION_MIP: forced cube LOD (needs a mipped cube).
//  - default: LOD = roughness * MAXMIP (needs a mipped cube).
float3 PbrSampleEnv( TEXCUBE_PARAM(envCube), float3 reflVec, float roughness )
{
#ifdef D_PBR_REFLECTION_BLUR
    float lSpread = roughness * D_PBR_REFLECTION_BLUR;
    [branch] if ( lSpread < 0.003 )
        return SAMPLECUBE_LOD( envCube, reflVec, 0.0 ).rgb;
    float3 lUp  = abs( reflVec.y ) < 0.99 ? float3(0.0,1.0,0.0) : float3(1.0,0.0,0.0);
    float3 lTan = normalize( cross( lUp, reflVec ) );
    float3 lBit = cross( reflVec, lTan );
    float3 lSum = float3(0.0,0.0,0.0);
    [unroll] for ( int i = 0; i < 12; ++i )
    {
        float  t = ( (float)i + 0.5 ) / 12.0;
        float  a = (float)i * 2.3998277;                         // golden angle
        float2 o = float2( cos(a), sin(a) ) * ( lSpread * sqrt(t) );
        float3 d = normalize( reflVec + lTan * o.x + lBit * o.y );
        lSum += SAMPLECUBE_LOD( envCube, d, 0.0 ).rgb;
    }
    return lSum / 12.0;
#elif defined(D_PBR_REFLECTION_MIP)
    return SAMPLECUBE_LOD( envCube, reflVec, D_PBR_REFLECTION_MIP ).rgb;
#else
    return SAMPLECUBE_LOD( envCube, reflVec, roughness * D_PBR_REFLECTION_MAXMIP ).rgb;
#endif
}

// Environment-reflection contribution. The cube is the directional reflection; it is modulated toward
// the ambient/sky colour (ambientColour -- a live engine source the caller passes, e.g. the sky/fog
// colour; the SH irradiance reads ~0 on the cloned shader) so it tracks day/night, ADDITIVELY lifted by
// a sky-coloured floor so dark cube directions stay visible (never black), and darkened under shadow.
// Weighted by a roughness-aware Fresnel (+ optional multi-scatter energy compensation).
float3 PbrReflection( float3 env, float NdotV, float roughness, float3 F0, float metallic, float3 ambientColour, float shadow, float hdrLevel )
{
    float  shadowK   = lerp( 1.0 - D_PBR_REFLECTION_SHADOW, 1.0, shadow );
    float  skyLum    = saturate( dot( ambientColour, float3(0.2126, 0.7152, 0.0722) ) );     // sky brightness 0..1 (day~1, night~0)
    float  cubeAtten = lerp( 1.0, skyLum, D_PBR_REFLECTION_AMBIENT );                         // reduce the cube's intensity at night (no tint)
    env  = env * ( cubeAtten * D_PBR_REFLECTION_CUBE );                                       // boost cube prominence (more cube) + night fade
    env += ambientColour * ( D_PBR_REFLECTION_FILL * metallic );                             // sky-coloured ambient floor (dark directions stay visible)
    env *= shadowK;
    float3 fresnel = F0 + ( max( 1.0 - roughness, F0 ) - F0 ) * pow( 1.0 - NdotV, 5.0 );
#ifdef D_GGX_MULTISCATTER
    float2 lAb = GGXEnvBRDFApprox( NdotV, roughness );                   // multi-scatter energy comp for the IBL
    fresnel   *= 1.0 + F0 * ( 1.0 / max( lAb.x + lAb.y, 1e-3 ) - 1.0 );  // rough metals reflect fuller from the cube
#endif
    return env * fresnel * hdrLevel * D_PBR_REFLECTION_SCALE;
}

// Direct key-light lighting: diffuse (Oren-Nayar if D_OREN_NAYAR, else Lambert) + GGX specular, shadowed.
float3 PbrDirect( float3 N, float3 L, float3 V, float3 albedo, float roughness, float metallic, float3 F0,
                  float3 keyLightColour, float3 keyLightSpecColour, float3 indirect, float shadow )
{
    float lNdotL = dot( N, L );
#ifdef D_OREN_NAYAR
    float lDiffuseFactor = ComputeOrenNayarDiffuseFromRoughnessMap( N, L, V, roughness ) * shadow;
#else
    float lDiffuseFactor = saturate( lNdotL ) * shadow;
#endif
    float3 lDiffuseAlbedo = albedo * ( 1.0 - metallic );
    float3 lDiffuseLight  = indirect + keyLightColour * lDiffuseFactor;
    float3 lSpec = keyLightSpecColour * ( D_PBR_SPECULAR_SCALE * ComputeGGXSpecularRGB( N, L, V, roughness, F0 ) );
    return lDiffuseAlbedo * lDiffuseLight + lSpec * shadow;
}

#endif // PBR_FXH
