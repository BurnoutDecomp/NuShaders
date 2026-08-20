sampler DiffuseSampler : register(s0);
sampler DepthSampler   : register(s1);
float4   gDepthConversion;
float4   gDepthFadeConstants;
void
main(
    in float4 iColour  : COLOR0,
    in float2 iUV0     : TEXCOORD0,
    in float3 iPos     : TEXCOORD1,
    out float4 oColour  : COLOR0
    )
{
    float4  lDiffuseTexture = tex2D( DiffuseSampler, iUV0 );
#ifdef D_RAWZ
    float4 lDepthTexture   = float4( tex2D( DepthSampler, iPos.xy ).arg, 1.0f );
    float  lfDepthBufferZ  = dot( lDepthTexture, gDepthConversion );
#elif defined(D_MRT) || defined(D_MSAA_ENABLED)
    float  lfDepthBufferZ   = frac( tex2D( DepthSampler, iPos.xy ).r );
    lfDepthBufferZ = ( lfDepthBufferZ * gDepthConversion.x ) + gDepthConversion.y;
#elif defined(D_INTZ)
    float  lfDepthBufferZ   = tex2D( DepthSampler, iPos.xy );
    lfDepthBufferZ = ( lfDepthBufferZ * gDepthConversion.x ) + gDepthConversion.y;
#else
    float4 lDepthTexture   = float4( tex2D( DepthSampler, iPos.xy ).arg, 1.0f );
    float  lfDepthBufferZ  = dot( lDepthTexture, gDepthConversion );
#endif
    float  lfPixelZ        = iPos.z;
    float  lfA             = gDepthFadeConstants.x * ( lfPixelZ - lfDepthBufferZ );
    float  lfB             = lfPixelZ * lfDepthBufferZ;
    float  lfDepthFade     = saturate( lfA / lfB );
    oColour    = lDiffuseTexture * float4( iColour );
    oColour.a *= lfDepthFade;
}
