sampler DiffuseSampler : register(s0);
void
main(in float4 iColour : COLOR0,
     in float2 iUV0    : TEXCOORD0,
     out float4 oColour : COLOR0 )
{
 float4 colourMap0 = float4( tex2D( DiffuseSampler, iUV0 ) );
    oColour = colourMap0 * float4( iColour );
}
