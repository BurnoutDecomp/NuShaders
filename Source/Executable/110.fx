float4x4 gaWorldTransforms[32];
float4x4 gViewProjection;
float3   gEyeLocation;
void
main(
    in  float4 iPosPlusTransformIndex : POSITION,
    in  float3 iNormal                : NORMAL,
    in  float2 iUV                    : TEXCOORD0,
    out float4 oPos    : POSITION,
    out float4 oColour : COLOR0,
    out float2 oUV     : TEXCOORD0,
    out float3 oNormal : TEXCOORD1,
    out float3 oToEye  : TEXCOORD2 )
{
    int      luTransformIndex = (int)iPosPlusTransformIndex.w;
    float4x4 lWorldTransform  = gaWorldTransforms[luTransformIndex];
    float4   lMaterialColour  = float4( lWorldTransform[0].w, lWorldTransform[1].w, lWorldTransform[2].w, lWorldTransform[3].w );
    float4 lWorldPos = ( lWorldTransform[0] * iPosPlusTransformIndex.x ) +
                       ( lWorldTransform[1] * iPosPlusTransformIndex.y ) +
                       ( lWorldTransform[2] * iPosPlusTransformIndex.z ) +
                       lWorldTransform[3];
    lWorldPos.w = 1.0;
 oToEye = gEyeLocation - lWorldPos.xyz;
 float3 lWorldNormal = mul( iNormal, (float3x3)lWorldTransform );
    oPos = mul( lWorldPos, gViewProjection );
    oColour = lMaterialColour;
    oNormal = lWorldNormal;
    oUV     = iUV;
}
