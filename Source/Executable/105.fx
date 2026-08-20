float4  gPsConstants;
float4  gDepthConversion;
sampler DiffuseSampler : register(s0);
sampler DepthSampler   : register(s1);
void
main(
    in  float4 iUvBlendAlpha : TEXCOORD0,
    in  float3 iDepthFadeUvZ : TEXCOORD1,
    out float4 oColour        : COLOR0
    )
{
    float4 lDepthTexture = float4( tex2D( DepthSampler, iDepthFadeUvZ.xy ).arg, 1.0f );
    float2 lUv0 = iUvBlendAlpha.xy;
    float2 lUv1 = lUv0 + gPsConstants.xy;
    float3 lFrameA = float3( tex2D( DiffuseSampler, lUv0 ).xyz );
    float3 lFrameB = float3( tex2D( DiffuseSampler, lUv1 ).xyz );
    oColour       = lerp( lFrameA, lFrameB, (float)iUvBlendAlpha.z ).xyzx;
    oColour.xyz  *= (float)gPsConstants.w;
    float  lfPixelZ        = iDepthFadeUvZ.z;
    float  lfDepthBufferZ  = dot( lDepthTexture, gDepthConversion );
    float  lfA             = gPsConstants.z * ( lfPixelZ - lfDepthBufferZ );
    float  lfB             = lfPixelZ * lfDepthBufferZ;
    float   lfDepthFade     = float( saturate( lfA / lfB ) );
    float   lfAlpha         = lfDepthFade * (float)iUvBlendAlpha.w;
    oColour *= lfAlpha;
}
