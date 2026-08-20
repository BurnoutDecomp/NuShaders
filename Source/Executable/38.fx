sampler2D coronaTexture : register(s0);
struct Input
{
    float4 position : POSITION;
    float2 uv       : TEXCOORD0;
    float4 colour   : COLOR0;
};
float4
main(Input input) : COLOR0
{
    float4 colour = float4( input.colour ) * float4( tex2D(coronaTexture, input.uv) );
    return colour;
}
