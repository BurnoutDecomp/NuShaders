struct PixelData
{
    float4 mPosition           : POSITION;
    float4 mSourceUvVignetteUv : TEXCOORD0;
};
float4 VignetteCentreXyScaleXy;
float4 VignetteAngle;
PixelData
main(
    in float3 iPosition : POSITION,
    in float2 iTexUv    : TEXCOORD0
    )
{
    PixelData lPixelData;
    lPixelData.mPosition           = float4( iPosition, 1.0f );
    float  lfVignetteAngle = VignetteAngle.x;
    float lfSin = sin( lfVignetteAngle );
    float lfCos = cos( lfVignetteAngle );
    float2 lVignetteUv = ( iTexUv.xy + iTexUv.xy ) - 1.0f;
    lVignetteUv = float2( ( lVignetteUv.x * lfCos ) + ( lVignetteUv.y * lfSin ), ( lVignetteUv.y * lfCos ) - ( lVignetteUv.x * lfSin ) );
    float2 lCentre = 1.0f - 2.0 * VignetteCentreXyScaleXy.xy;
    lVignetteUv += lCentre;
    lVignetteUv *= VignetteCentreXyScaleXy.zw;
    lPixelData.mSourceUvVignetteUv = float4( iTexUv.x, iTexUv.y, lVignetteUv.x, lVignetteUv.y );
    return lPixelData;
}
