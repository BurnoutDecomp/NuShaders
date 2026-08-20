#ifndef ORENNAYAR_FXH
#define ORENNAYAR_FXH

// Energy-conserving Oren-Nayar diffuse BRDF.
// Fujii/Gotanda qualitative approximation.
// At roughness=0 this collapses to Lambertian (N·L).
float ComputeOrenNayarDiffuse(
    float3 N,
    float3 L,
    float3 V,
    float  roughness )
{
    float NdotL = dot( N, L );
    float NdotV = dot( N, V );

    float sigma2 = roughness * roughness;
    float A = 1.0 - 0.5 * sigma2 / ( sigma2 + 0.33 );
    float B = 0.45 * sigma2 / ( sigma2 + 0.09 );

    float3 Vt = normalize( V - N * NdotV );
    float3 Lt = normalize( L - N * NdotL );
    float cosPhiDiff = max( dot( Vt, Lt ), 0.0 );

    float sinNdotL = sqrt( max( 1.0 - NdotL * NdotL, 0.0 ) );
    float sinNdotV = sqrt( max( 1.0 - NdotV * NdotV, 0.0 ) );

    float s = NdotL < NdotV ? sinNdotL : sinNdotV;
    float t = NdotL > NdotV ? sinNdotL / max( NdotL, 0.001 )
                             : sinNdotV / max( NdotV, 0.001 );

    return max( NdotL, 0.0 ) * ( A + B * cosPhiDiff * s * t );
}

float ComputeOrenNayarDiffuseFromSpecMap(
    float3 N,
    float3 L,
    float3 V,
    float  specValue )
{
    return ComputeOrenNayarDiffuse( N, L, V, 1.0 - specValue );
}

float ComputeOrenNayarDiffuseFromRoughnessMap(
    float3 N,
    float3 L,
    float3 V,
    float  roughnessMapValue )
{
    return ComputeOrenNayarDiffuse( N, L, V, roughnessMapValue );
}

#endif
