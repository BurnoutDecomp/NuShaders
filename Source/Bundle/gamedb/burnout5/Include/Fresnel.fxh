#ifndef FRESNEL_FXH
#define FRESNEL_FXH

void
CalculateFresnel(
    float3       normal,
    float3       view,
    float4       fresnelRanges,
    float        fresnelCurve,
    out float3   reflectedView,
    out float2   fresnelValues )
{
 float normalDotView = dot( normal, view );
#ifdef FRESNEL_USE_CURVE
    float    baseFresnel = pow( 1.0 - saturate( normalDotView ), fresnelCurve );
#else
    float    baseFresnel = 1.0 - saturate( normalDotView );
#endif
    reflectedView = 2.0 * normalDotView * normal - view;
    fresnelValues = lerp( fresnelRanges.xz, fresnelRanges.yw, baseFresnel );
}

#endif
