sampler DiffuseSampler : register(s0);
void
main(
    in  float2 iUV         : TEXCOORD0,
    out float4 oColour      : COLOR,
    out float oDepth       : DEPTH )
{
    oColour = float4( 0,0,0,0 );
#ifdef D_RAWZ
    float3 lDepthBuffer = tex2D( DiffuseSampler, iUV ).arg;
    float3 lDepthFactor = float3( 65536.0 / 65793.0, 256.0 / 65793.0, 1.0 / 65793.0 );
    oDepth = dot( lDepthBuffer, lDepthFactor );
#elif defined(D_INTZ)
    oDepth  = tex2D( DiffuseSampler, iUV ).r;
#elif defined(D_MRT) || defined(D_MSAA_ENABLED)
    oDepth = frac( tex2D( DiffuseSampler, iUV ).r );
#else
    oDepth  = tex2D( DiffuseSampler, iUV ).r;
#endif
}
