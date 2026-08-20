#ifndef GGX_FXH
#define GGX_FXH

// Cook-Torrance specular with GGX/Trowbridge-Reitz distribution,
// Schlick Fresnel, and height-correlated Smith visibility.
//
//   f_spec = D * F * G / (4 * NdotL * NdotV)
//   L_spec = f_spec * NdotL          (cosine term from rendering equation)
//          = D * F * Vis * NdotL     (Vis = G / (4 * NdotL * NdotV))
//
// Returns the complete lit specular intensity (scalar) for one directional
// light, so the caller just multiplies by light colour / specular strength.
float ComputeGGXSpecular(
    float3 N,
    float3 L,
    float3 V,
    float  roughness,
    float  F0 )
{
    float NdotL = saturate( dot( N, L ) );
    float NdotV = saturate( dot( N, V ) );
    if ( NdotL <= 0.0 )
        return 0.0;

    float3 H    = normalize( L + V );
    float NdotH = saturate( dot( N, H ) );
    float VdotH = saturate( dot( V, H ) );

    float a  = roughness * roughness;   // alpha = roughness^2
    float a2 = a * a;

    // D: GGX normal distribution
    float d     = NdotH * NdotH * ( a2 - 1.0 ) + 1.0;
    float D     = a2 / ( 3.14159265 * d * d );

    // F: Schlick Fresnel
    float F     = F0 + ( 1.0 - F0 ) * pow( 1.0 - VdotH, 5.0 );

    // Vis: height-correlated Smith visibility (G folded with 1/(4 NdotL NdotV))
    float lambdaV = NdotL * sqrt( NdotV * NdotV * ( 1.0 - a2 ) + a2 );
    float lambdaL = NdotV * sqrt( NdotL * NdotL * ( 1.0 - a2 ) + a2 );
    float Vis     = 0.5 / ( lambdaV + lambdaL + 1e-5 );

    return D * F * Vis * NdotL;
}

// One GGX specular lobe (D*F*Vis*NdotL), RGB F0 (metals tint the highlight with their albedo).
float3 GGXLobeRGB( float3 N, float3 L, float3 V, float roughness, float3 F0 )
{
    float NdotL = saturate( dot( N, L ) );
    float NdotV = saturate( dot( N, V ) );
    if ( NdotL <= 0.0 )
        return float3( 0.0, 0.0, 0.0 );

    float3 H    = normalize( L + V );
    float NdotH = saturate( dot( N, H ) );
    float VdotH = saturate( dot( V, H ) );

    float a  = roughness * roughness;
    float a2 = a * a;

    float d = NdotH * NdotH * ( a2 - 1.0 ) + 1.0;
    float D = a2 / ( 3.14159265 * d * d );

    float3 F = F0 + ( 1.0 - F0 ) * pow( 1.0 - VdotH, 5.0 );

    float lambdaV = NdotL * sqrt( NdotV * NdotV * ( 1.0 - a2 ) + a2 );
    float lambdaL = NdotV * sqrt( NdotL * NdotL * ( 1.0 - a2 ) + a2 );
    float Vis     = 0.5 / ( lambdaV + lambdaL + 1e-5 );

    return D * F * Vis * NdotL;
}

// Karis' analytic split-sum environment BRDF (no LUT). Returns (scale, bias): reflectance ~= F0*x + y.
// Used here only for the single-scatter directional albedo (x+y) the multi-scatter compensation needs.
float2 GGXEnvBRDFApprox( float NdotV, float roughness )
{
    const float4 c0 = float4( -1.0, -0.0275, -0.572,  0.022 );
    const float4 c1 = float4(  1.0,  0.0425,  1.040, -0.040 );
    float4 r    = roughness * c0 + c1;
    float  a004 = min( r.x * r.x, exp2( -9.28 * NdotV ) ) * r.x + r.y;
    return float2( -1.04, 1.04 ) * a004 + r.zw;
}

#ifndef D_GGX_LOBE2_WEIGHT
#define D_GGX_LOBE2_WEIGHT 0.25   // weight of the broad second lobe (cinematic soft halo)
#endif
#ifndef D_GGX_LOBE2_SPREAD
#define D_GGX_LOBE2_SPREAD 3.0    // how much broader the second lobe is (roughness multiplier)
#endif

// RGB-F0 metallic-roughness specular. Sharp GGX core, optionally:
//   D_GGX_MULTILOBE    - add a broad second lobe (energy-preserving lerp) for a soft cinematic halo.
//   D_GGX_MULTISCATTER - Kulla-Conty multi-scatter energy compensation (no LUT) so rough metals keep
//                        their energy instead of darkening (single-scatter GGX loses energy at high a).
float3 ComputeGGXSpecularRGB(
    float3 N,
    float3 L,
    float3 V,
    float  roughness,
    float3 F0 )
{
    float3 spec = GGXLobeRGB( N, L, V, roughness, F0 );                  // sharp core lobe
#ifdef D_GGX_MULTILOBE
    float  r2    = min( roughness * D_GGX_LOBE2_SPREAD, 1.0 );           // broader lobe
    float3 broad = GGXLobeRGB( N, L, V, r2, F0 );
    spec = lerp( spec, broad, D_GGX_LOBE2_WEIGHT );                      // core + soft halo (energy-conserving)
#endif
#ifdef D_GGX_MULTISCATTER
    float  NdotV = saturate( dot( N, V ) );
    float2 ab    = GGXEnvBRDFApprox( NdotV, roughness );
    float  Ess   = ab.x + ab.y;                                         // single-scatter directional albedo (F0=1)
    spec *= 1.0 + F0 * ( 1.0 / max( Ess, 1e-3 ) - 1.0 );                // Kulla-Conty energy add-back (tinted by F0)
#endif
    return spec;
}

float ComputeGGXSpecularFromSpecMap(
    float3 N,
    float3 L,
    float3 V,
    float  specValue,
    float  F0 )
{
    return ComputeGGXSpecular( N, L, V, 1.0 - specValue, F0 );
}

float ComputeGGXSpecularFromRoughnessMap(
    float3 N,
    float3 L,
    float3 V,
    float  roughnessMapValue,
    float  F0 )
{
    return ComputeGGXSpecular( N, L, V, roughnessMapValue, F0 );
}

#endif
