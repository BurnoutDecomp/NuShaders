struct BrnSunCoronaOcclusionInterpolators
{
    float4 mPosition    : POSITION;
};
BrnSunCoronaOcclusionInterpolators
main(
    in float3 iPosition : POSITION,
    in float2 iUV       : TEXCOORD0 )
{
    BrnSunCoronaOcclusionInterpolators lOUT;
    lOUT.mPosition  = float4( iPosition, 1.0f );
    return lOUT;
}
