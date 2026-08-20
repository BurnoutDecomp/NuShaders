sampler2D ColorTextureMapYUV : register(s0);
void
main(
    in  float4 iColourShift     : TEXCOORD0,
    in  float4 iColourScale     : TEXCOORD1,
    in  float2 iUV              : TEXCOORD2,
    out float4  oColour          : COLOR0 )
{
    float4 YUVSource = tex2D( ColorTextureMapYUV, iUV );
    float Y = YUVSource.x - ( 16.0f/256.0f); 
    float U = YUVSource.y - (128.0f/256.0f);
    float V = YUVSource.z - (128.0f/256.0f);    
    float R = (1.16894976f * (Y) - 0.00000000f * (U) + 1.60228608f * (V));
    float G = (1.16894976f * (Y) - 0.39329792f * (U) - 0.81615616f * (V));
    float B = (1.16894976f * (Y) + 2.02514176f * (U) + 0.00000000f * (V));
    oColour = float4(R,G,B,1.0f);
    oColour *= iColourScale;
    oColour += iColourShift;
}
