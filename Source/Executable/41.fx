struct BrnSunCoronaOcclusionInterpolators
{
    float4 mPosition    : POSITION;
};
float4 kUvStartAndOffset;
sampler   SamplerSource : register(s0);
float4
main(
    BrnSunCoronaOcclusionInterpolators lInput ) : COLOR
{
    float3 kDepthFactor = float3( 65536.0 / 65793.0, 256.0 / 65793.0, 1.0 / 65793.0 );
    float2 lUv = kUvStartAndOffset.xy - (3.0 * kUvStartAndOffset.zw);
    float   lhDepthTotal = 0.0;
    int i, j;
    for(j = 0; j < 7; j++)
    {
        lUv.x = kUvStartAndOffset.x;
        for(i = 0; i < 7; i++)
        {
            float3 lDepthTap;
         float   lhDepth;
            lDepthTap = tex2D( SamplerSource, lUv ).arg;
            lhDepth = (float)dot( lDepthTap, kDepthFactor );
            lhDepth = (lhDepth < 1.0) ? 0.0 : 1.0;
            lhDepthTotal += lhDepth;
            lUv.x += kUvStartAndOffset.z;
        }
        lUv.y += kUvStartAndOffset.w;
    }
    float lhDepthAverage = lhDepthTotal * float(1.0f / 49.0f);
    return float4( lhDepthAverage, lhDepthAverage, lhDepthAverage, float(1.0) );
}
