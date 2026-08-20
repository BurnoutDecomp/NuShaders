float3  gv3OuterColour;
float3  gv3InnerColour;
sampler DiffuseSampler : register(s0);
void
main(
    in  float4 iColourShift : TEXCOORD0,
    in  float4 iColourScale : TEXCOORD1,
    in  float2 iUV          : TEXCOORD2,
    out float4  oColour      : COLOR0 )
{
    float4 lTextureData = tex2D( DiffuseSampler, iUV.xy );
    float4 lDiffuseColour;
    {
  lDiffuseColour.rgb = (lTextureData.g * gv3OuterColour.rgb) + (lTextureData.r * gv3InnerColour.rgb);
  lDiffuseColour.a   = lTextureData.a;
    }
    lDiffuseColour *= (float4)iColourScale;
    lDiffuseColour += (float4)iColourShift;   
    oColour = lDiffuseColour;
}
