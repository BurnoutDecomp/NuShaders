#ifndef SKINNING_FXH
#define SKINNING_FXH

void
DoSkinningP( 
    in float3   position,
    in float4   boneIndices,
    in float4   boneWeights,
    out float3  skinnedPosition )
{
    skinnedPosition=0;
 for (int a=0;a<4;a++)
 {
  float3x4 boneMatrix = boneMatrices[ boneIndices[a] ];
  skinnedPosition  += mul( boneMatrix, float4( position,1 ) ) * boneWeights[a];
    }
}
void
DoSkinningPN( 
    in float3   position,
    in float3   normal,
    in float4   boneIndices,
    in float4   boneWeights,
    out float3  skinnedPosition,
    out float3  skinnedNormal )
{
    skinnedPosition=0;
 skinnedNormal=0;
 for (int a=0;a<4;a++)
 {
  float3x4 boneMatrix = boneMatrices[ boneIndices[a] ];
  skinnedPosition  += mul( boneMatrix, float4( position,1 ) ) * boneWeights[a];
  skinnedNormal    += mul( (float3x3)boneMatrix, normal ) * boneWeights[a];
 }
 skinnedNormal=normalize(skinnedNormal);
}
void
DoSkinningPNT( 
    in float3   position,
    in float3   normal,
    in float3   tangent,
    in float4   boneIndices,
    in float4   boneWeights,
    out float3  skinnedPosition,
    out float3  skinnedNormal,
    out float3  skinnedTangent )
{
    skinnedPosition=0;
 skinnedNormal=0;
 skinnedTangent=0;
    for (int a=0;a<4;a++)
 {
  float3x4 boneMatrix = boneMatrices[ boneIndices[a] ];
  skinnedPosition  += mul( boneMatrix, float4( position,1 ) ) * boneWeights[a];
  skinnedNormal    += mul( (float3x3)boneMatrix, normal ) * boneWeights[a];
  skinnedTangent   += mul( (float3x3)boneMatrix, tangent ) * boneWeights[a];
 }
 skinnedNormal=normalize(skinnedNormal);
 skinnedTangent=normalize(skinnedTangent);
}

#endif
