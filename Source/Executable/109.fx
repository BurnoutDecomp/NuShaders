sampler DiffuseSampler : register(s0);
sampler DepthSampler   : register(s1);
float4   gDepthConversion;
float4   gDepthFadeConstants;
void
main(
    in float4 iColour               : COLOR0,
    in float4 iUvUv                 : TEXCOORD0,
    in float4 iZFadeXyzPlusMiscData : TEXCOORD1,
    out float4 oColour   : COLOR0 )
{
    float4  colourMap0    = float4( tex2D( DiffuseSampler, iUvUv.xy ) );
    float4  colourMap1    = float4( tex2D( DiffuseSampler, iUvUv.zw ) );
    float  lfDepthBufferZ  = tex2D( DepthSampler, iZFadeXyzPlusMiscData.xy ).r;
    float  lBlend = float( iZFadeXyzPlusMiscData.w );
    oColour = lerp( colourMap0, colourMap1, lBlend );
    oColour *= float4( iColour );
    float  lfPixelZ        = iZFadeXyzPlusMiscData.z;
    float  lfA             = gDepthFadeConstants.x * ( lfPixelZ - lfDepthBufferZ );
    float  lfB             = lfPixelZ * lfDepthBufferZ;
    float   lfDepthFade     = float( saturate( lfA / lfB ) );
    oColour.a *= lfDepthFade;
}
