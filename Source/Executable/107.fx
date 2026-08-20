sampler DiffuseSampler   : register(s0);
void
main(
    in float4 iColour   : COLOR0,
    in float4 iUvUv     : TEXCOORD0,
    in float  iMiscData : TEXCOORD1,
    out float4 oColour   : COLOR0 )
{
 float4 colourMap0 = float4( tex2D( DiffuseSampler, iUvUv.xy ) );
 float4 colourMap1 = float4( tex2D( DiffuseSampler, iUvUv.zw ) );
 float4 colourMap  = lerp( colourMap0, colourMap1, iMiscData.x );
 colourMap *= float4( iColour );
    oColour = colourMap; 
}
