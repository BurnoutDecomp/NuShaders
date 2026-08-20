float4x4 gWorldViewProjection;
float4   gEyePosPlusDepthBias;
void
main(
    in  float3 iPos          : POSITION,    
    in  float4 iPackedValues : COLOR0,
    out float4 oPos          : POSITION,
    out float4 oUvBlendAlpha : TEXCOORD0
    )
{
    float3 lEyeToVertex = iPos - gEyePosPlusDepthBias.xyz;
    float3 lDepthBias   = normalize( lEyeToVertex );
    float3 lAdjustedPos = iPos - ( lDepthBias * gEyePosPlusDepthBias.w );
    oPos                = mul( float4( lAdjustedPos.xyz, 1.0f ), gWorldViewProjection );
    oUvBlendAlpha = iPackedValues;
    oUvBlendAlpha.x = ( oUvBlendAlpha.x * 256.0f ) + iPackedValues.z;
}
