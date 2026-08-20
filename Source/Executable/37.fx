sampler BaseSampler : register(s0);
sampler OverlaySampler : register(s1);
 float4 gQuincunxOffsets;  
void
main(
    in  float4 iUV_Base0_Overlay    : TEXCOORD0,
    out float4 oColour               : COLOR0 )
{
 float3 color0 = tex2D(BaseSampler, iUV_Base0_Overlay.xy - gQuincunxOffsets.xy).rgb;
 float3 color1 = tex2D(BaseSampler, iUV_Base0_Overlay.xy + gQuincunxOffsets.xy).rgb;
 float3 lBaseColour = (color0 + color1) * 0.5h;
    float4 lOverlayColour = tex2D( OverlaySampler, iUV_Base0_Overlay.zw );
    float3 lOutputColour = ( lOverlayColour.rgb + lBaseColour - ( lBaseColour * lOverlayColour.a ) );
    oColour = float4(lOutputColour, 1.0);
}
