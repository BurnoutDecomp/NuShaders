float4  gvMaskUseFlags;
float4      gvMaskAPositionMinMax;
float4      gvMaskAUVStartEnd;
float4  gvMaskAUVDifference;
float4      gvMaskBPositionMinMax;
float4      gvMaskBUVStartEnd;
float4  gvMaskBUVDifference;
sampler     MaskSampler0 : register(s1);
sampler     MaskSampler1 : register(s2);
void
Generate2MaskAlpha_Fast_VertexShader( 
    in  float2      lScreenPos,
    out float4      lMaskAB_UV,
    out float4      lMaskAB_Clipping,
    out float2      lMaskAB_Disabled )
{
    float2 lMaskAB_Enabled = gvMaskUseFlags.xy;
    lMaskAB_Disabled.xy = (1.0 - lMaskAB_Enabled.xy);
    {
        float2 lDifference = lScreenPos - gvMaskAPositionMinMax.xy;
        lDifference.xy = lDifference.xy * gvMaskAUVDifference.xy;
        float2 lMaskUV = lerp( gvMaskAUVStartEnd.xy, gvMaskAUVStartEnd.zw, lDifference.xy );
        lMaskAB_UV.xy = lMaskUV;
        lMaskAB_Clipping.xy = lDifference;
    }
    {
        float2 lDifference = lScreenPos - gvMaskBPositionMinMax.xy;
        lDifference.xy = lDifference.xy * gvMaskBUVDifference.xy;
        float2 lMaskUV = lerp( gvMaskBUVStartEnd.xy, gvMaskBUVStartEnd.zw, lDifference.xy );
        lMaskAB_UV.zw = lMaskUV;
        lMaskAB_Clipping.zw = lDifference;
    }
    lMaskAB_Clipping = lMaskAB_Clipping * 2.0 - 1.0;
    lMaskAB_UV          *= lMaskAB_Enabled.xxyy;
    lMaskAB_Clipping    *= lMaskAB_Enabled.xxyy;
}
float
Generate2MaskAlpha_Fast_PixelShader(
    in float4       lMaskAB_UV,
    in float4       lMaskAB_Clipping,
    in float2       lMaskAB_Disabled)
{
    float2 lMaskAB_Alpha;
    lMaskAB_Alpha.x = tex2D( MaskSampler0, lMaskAB_UV.xy ).a;
    lMaskAB_Alpha.y = tex2D( MaskSampler1, lMaskAB_UV.zw ).a;
 float2 lIsOutsideMaskAB = max( abs(lMaskAB_Clipping.xz), abs(lMaskAB_Clipping.yw) );
    lMaskAB_Alpha.xy *= (lIsOutsideMaskAB.xy <= 1.0);
    lMaskAB_Alpha.xy = max(lMaskAB_Alpha.xy, lMaskAB_Disabled.xy);
    float lhOutputAlpha = lMaskAB_Alpha.x * lMaskAB_Alpha.y;
    return lhOutputAlpha;
}
float
Generate2MaskAlpha( float2 lScreenPos )
{
    float lhOutputAlpha = (float)1.0;
 if ( gvMaskUseFlags.x > 0.5f ) 
 {
     if ( ( ( gvMaskAPositionMinMax.x <= lScreenPos.x ) && ( lScreenPos.x <= gvMaskAPositionMinMax.z ) )
  &&   ( ( gvMaskAPositionMinMax.y >= lScreenPos.y ) && ( lScreenPos.y >= gvMaskAPositionMinMax.w ) ) )
     {
      float2 lDifference = lScreenPos - (float2)gvMaskAPositionMinMax.xy;
      lDifference.xy = lDifference.xy * (float2)gvMaskAUVDifference.xy;
      float2 lMaskUV = lerp( (float2)gvMaskAUVStartEnd.xy, (float2)gvMaskAUVStartEnd.zw, lDifference.xy );
      float  lMaskAlpha = tex2D( MaskSampler0, lMaskUV ).a;
      lhOutputAlpha *= lMaskAlpha;   
     }
     else
     {
         lhOutputAlpha = (float)0.0;
     }
    }
 if ( gvMaskUseFlags.y > 0.5f ) 
 {
     if( ( ( gvMaskBPositionMinMax.x <= lScreenPos.x ) && ( lScreenPos.x <= gvMaskBPositionMinMax.z ) )
  &&  ( ( gvMaskBPositionMinMax.y >= lScreenPos.y ) && ( lScreenPos.y >= gvMaskBPositionMinMax.w ) ) )
     {
      float2 lDifference = lScreenPos - (float2)gvMaskBPositionMinMax.xy;
      lDifference.xy = lDifference.xy * (float2)gvMaskBUVDifference.xy;
      float2 lMaskUV = lerp( (float2)gvMaskBUVStartEnd.xy, (float2)gvMaskBUVStartEnd.zw, lDifference.xy );
      float  lMaskAlpha = tex2D( MaskSampler1, lMaskUV ).a;
      lhOutputAlpha *= lMaskAlpha;   
  }
  else
  {
         lhOutputAlpha = (float)0.0;
  }
 }
 return lhOutputAlpha;
}
float4 gOffsetXYZ;
float4 gRightUp;
float4 gColourShift;
float4 gColourScale;
void
main(
    in  float2 iPos         : POSITION,
    in  float4 iColour      : COLOR0,
    in  float2 iUV          : TEXCOORD0,
    out float4 oPos             : POSITION,
    out float4 oColourShift     : TEXCOORD0,
    out float4 oColourScale     : TEXCOORD1,
    out float4 oUVAndScreenPos  : TEXCOORD2
 ,
    out float4 iMaskAB_UV  : TEXCOORD3,
    out float4 iMaskAB_Clipping : TEXCOORD4,
    out float2 iMaskAB_Disabled : TEXCOORD5
    )
{
    oPos     = float4( gOffsetXYZ.xyz, 1.0f );
    oPos.xy += ( iPos.x * gRightUp.xy ) + ( iPos.y * gRightUp.zw );
    oColourShift        = gColourShift;
    oColourScale        = iColour * gColourScale;
    oUVAndScreenPos.xy = iUV;
    oUVAndScreenPos.zw = oPos.xy;
 Generate2MaskAlpha_Fast_VertexShader( 
  oUVAndScreenPos.zw,
  iMaskAB_UV,
  iMaskAB_Clipping,
  iMaskAB_Disabled );
}
