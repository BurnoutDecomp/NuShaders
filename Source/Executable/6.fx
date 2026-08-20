sampler DiffuseSampler : register(s0);
void
main(
    in  float4 iColourShift : TEXCOORD0,
    in  float4 iColourScale : TEXCOORD1,
    in  float2 iUV          : TEXCOORD2,
    out float4  oColour      : COLOR0 )
{
    float4 Tex = tex2D( DiffuseSampler, iUV ) - float4( 0.5f, -16.f / 256.f, 0.5f, 0.f );
    float4 lDiffuseColour;
    lDiffuseColour = Tex.ggga * (float)1.164;
    lDiffuseColour.r +=  Tex.r * (float)1.596;
    lDiffuseColour.g += -Tex.b * (float)0.391 - Tex.r * (float)0.813;
    lDiffuseColour.b +=  Tex.b * (float)2.018;
    lDiffuseColour.a  = (float)1.0;
    lDiffuseColour *= (float4)iColourScale;
    lDiffuseColour += (float4)iColourShift;    
    oColour = lDiffuseColour;
}
