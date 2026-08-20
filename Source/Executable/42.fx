struct BrnSunCoronaFlareInterpolators
{
    float4 mPosition    : POSITION;
    float2 mUV          : TEXCOORD0;
};
BrnSunCoronaFlareInterpolators
main(
    in float3 iPosition : POSITION,
    in float2 iUV       : TEXCOORD0 )
{
    BrnSunCoronaFlareInterpolators lOUT;
    lOUT.mPosition  = float4( iPosition, 1.0f );
    lOUT.mUV = iUV;
    return lOUT;
}
