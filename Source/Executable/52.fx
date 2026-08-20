struct DOFBlurInterpolators
{
    float4 mPosition    : POSITION;
    float4 mUV_00_01    : TEXCOORD0;
    float4 mUV_02_03    : TEXCOORD1;
    float4 mUV_04_05    : TEXCOORD2;
};
float4 kUvOffset_00_01;
float4 kUvOffset_02_03;
float4 kUvOffset_04_05;
DOFBlurInterpolators
main(
    in float3 iPosition : POSITION,
    in float2 iUV       : TEXCOORD0 )
{
    DOFBlurInterpolators lOUT;
    lOUT.mPosition  = float4( iPosition, 1.0f );
    lOUT.mUV_00_01.xy = iUV + kUvOffset_00_01.xy;
    lOUT.mUV_00_01.zw = iUV + kUvOffset_00_01.zw;
    lOUT.mUV_02_03.xy = iUV + kUvOffset_02_03.xy;
    lOUT.mUV_02_03.zw = iUV + kUvOffset_02_03.zw;
    lOUT.mUV_04_05.xy = iUV + kUvOffset_04_05.xy;
    lOUT.mUV_04_05.zw = iUV + kUvOffset_04_05.zw;
    return lOUT;
}
