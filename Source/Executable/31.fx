float4  g_depthConversion;
float4  g_headlightConstants;
float4x4    g_clipToHeadlight;
sampler g_headlightConeSampler : register(s0);
sampler g_depthSampler   : register(s1);
float DepthSample2d( sampler tex, float2 texCoord )
{
#ifdef D_RAWZ
    float3 lDepthTexture   = tex2D( tex, texCoord ).arg;
    return dot( lDepthTexture, g_depthConversion.xyz );
#elif defined(D_INTZ)
    return tex2D( tex, texCoord ).r;
#elif defined(D_MRT) || defined(D_MSAA_ENABLED)
    return frac( tex2D( tex, texCoord ).r );
#else
    return tex2D( tex, texCoord ).r;
#endif
}
void
main(
    in  float4 iPos    : TEXCOORD0,
    out float4 oColour : COLOR0 )
{
    float4 screenPos  = iPos / iPos.w;
    float2 depthUV    = float2( ( screenPos.x + 1 )* 0.5, ( 1 - screenPos.y )* 0.5 ) ;
    depthUV += float2(0.5/640.0f, 0.5f/360.0f);
    screenPos.z       = DepthSample2d( g_depthSampler, depthUV );
    float4 fragPos    = mul( screenPos, g_clipToHeadlight );
    float4 absFragPos = abs( fragPos );
    float clipValue   = absFragPos.x < absFragPos.w && absFragPos.y < absFragPos.w && absFragPos.z < absFragPos.w;
    fragPos.xyz /= fragPos.w;
    fragPos.xyz += float3( 1.0, 1.0, 1.0 );
    fragPos.xyz *= 0.5;
    float atten = min( 1.0 - fragPos.z, g_headlightConstants.x );
    fragPos.xy += float2(0.5/128.0f, 0.5f/128.0f);
    oColour = tex2D( g_headlightConeSampler, fragPos.xy );
    oColour.a *= atten * clipValue * g_depthConversion.w;
}
