float4x4 gWorldViewProjection;
float4   gUvBlendAlphaUnpack;
float4   gDepthFadeUvZOffset;
float4   gDepthFadeUvZScale;
void
main(
    in  float3 iPos          : POSITION,    
    in  float4 iUvBlendAlpha : COLOR0,
    out float4 oPos          : POSITION,
    out float4 oUvBlendAlpha : TEXCOORD0,
    out float3 oDepthFadeUvZ : TEXCOORD1
    )
{
    oPos              = mul( float4( iPos.xyz, 1.0f ), gWorldViewProjection );
    oUvBlendAlpha     = iUvBlendAlpha * gUvBlendAlphaUnpack;
    oDepthFadeUvZ     = gDepthFadeUvZOffset.xyz + ( gDepthFadeUvZScale.xyz * ( oPos.xyz / oPos.w ) );
}
