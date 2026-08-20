sampler DiffuseSampler : register(s0);
void
main(
    in  float4 iColourShift : COLOR0,
    in  float4 iColourScale : COLOR1,
    in  float2 iUV          : TEXCOORD0,
    out float4 oColour      : COLOR0 )
{
    oColour = tex2D( DiffuseSampler, iUV );
    oColour *= iColourScale;
    oColour += iColourShift;
}
