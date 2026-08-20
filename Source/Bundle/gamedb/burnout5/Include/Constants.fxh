#ifndef CONSTANTS_FXH
#define CONSTANTS_FXH

static const float3 k_luminanceMapping = float3( 0.299, 0.587, 0.114 );

#ifdef USE_HDR_CONSTANTS
float4 HDRConstants
<
 string scope = "global";
>;
#endif

#endif
