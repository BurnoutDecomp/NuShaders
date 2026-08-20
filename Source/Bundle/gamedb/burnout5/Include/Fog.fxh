#ifndef FOG_FXH
#define FOG_FXH

#ifndef USE_SHARED_GLOBALS
#ifdef D_PLATFORM_X360
float4      ScattCoeffs : register(c16)
#else
float4      ScattCoeffs
#endif
<
 string scope = "global";
>;

float4 FogColourPlusWhiteLevel
<
 string scope = "global";
>;
#endif // USE_SHARED_GLOBALS

float CalculateScattering(
    in float lfDistance )
{
 float lfFogFactor = saturate( ( lfDistance * ScattCoeffs.x ) - ScattCoeffs.y );
 return pow( lfFogFactor, ScattCoeffs.z ) * ScattCoeffs.w;
}

#endif
