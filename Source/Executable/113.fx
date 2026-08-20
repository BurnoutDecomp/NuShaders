sampler DiffuseSampler : register(s0);
void
main(
    in  float4 iColour : COLOR0,
    in  float2 iUV     : TEXCOORD0,
    out float4  oColour : COLOR0 )
{
    oColour  = float4( tex2D( DiffuseSampler, iUV ) );
    oColour *= float4( iColour );
}
