#ifndef NORMALMAPPING_FXH
#define NORMALMAPPING_FXH

float3
DecodeNormalMap(
    float2   normalTex,
    float3   normal,
    float3   tangent,
    float3   binormal,
    float    scale )
{
    float3   result;
    result.xy = ( normalTex.xy - float2( 0.5, 0.5 ) ) * scale;
    result.z = sqrt( 1.0 - saturate( dot( result.xy, result.xy ) ) );
#ifdef NORMAL_MAP_SWAP_TB
    result = normalize( ( result.z * normal ) + ( result.y * tangent ) - ( result.x * binormal ) );
#else
    result = normalize( ( result.z * normal ) + ( result.y * binormal ) + ( result.x * tangent ) );
#endif
    return result;
}
float3
ConvertGANormalsToXYZ(
    float2   normalTex )
{
    float3   result;
    result.xy = ( normalTex.xy - float2( 0.5, 0.5 ) ) * 2.0;
    result.z = sqrt( 1.0 - saturate( dot( result.xy, result.xy ) ) );
    return result;
}
float3
TransformTangetSpaceNormalToWorldSpaceNormal(
    float3   normalTex,
    float3   normal,
    float3   tangent,
    float3   binormal )
{
    float3   result;
    result = normalize( ( normalTex.z * normal ) + ( normalTex.y * binormal ) + ( normalTex.x * tangent ) );
    return result;
}

#endif
