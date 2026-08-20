#ifndef UTILITY_FXH
#define UTILITY_FXH

float
LinearStep1Fast(
    float start,
    float invRange,
    float value )
{
    return saturate( ( value - start ) * invRange );
}

float3
AdjustContrast(
    float3   colour,
    float    contrast )
{
#ifdef CONTRAST_USE_FIXED_MID
    return ( ( ( colour - 0.5 ) * contrast ) + 0.5 );
#else
    float lfHalfWhiteLevel = FogColourPlusWhiteLevel.w * 0.5;
    return ( ( ( colour - lfHalfWhiteLevel ) * contrast ) + lfHalfWhiteLevel );
#endif
}

float3
AdjustSaturation(
    float3   colour,
    float    saturation )
{
    float    luminance = dot( colour, k_luminanceMapping );
    return lerp( luminance.xxx, colour, saturation );
}

#endif
