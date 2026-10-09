#ifndef TRANSFORM_FXH
#define TRANSFORM_FXH

#ifndef USE_SHARED_GLOBALS
float4x4  viewProjection : ViewProjection
<
 string scope = "global";
>;

#ifdef D_PLATFORM_X360
float4x4    ViewProjectionModified : register(c0)
#else
float4x4    ViewProjectionModified
#endif
<
 string scope = "global";
>;
#endif // USE_SHARED_GLOBALS

#ifdef D_PC_REFLECTION_SHADOW_DEPTH
// Native D3D9 input: cascade depth comes from the main camera that fitted the
// shadow atlas. Reflection face clip.w belongs to a different camera.
float4 ShadowMap_ViewDepthPC : register(c255);
#endif

float GetShadowReceiverDepthPC(float3 WorldSpacePosition, float faceDepth)
{
#ifdef D_PC_REFLECTION_SHADOW_DEPTH
 // Older decomp executables do not publish this optional input. Preserve their
 // original receiver path until the matching native backend supplies a plane.
 if (any(ShadowMap_ViewDepthPC.xyz != 0.0))
  return dot(float4(WorldSpacePosition, 1.0), ShadowMap_ViewDepthPC);
 return faceDepth;
#else
 return faceDepth;
#endif
}

float4 TransformWorldToProjection(float3 WorldSpacePosition)
{
 float4 hPosition;
 hPosition.x  = dot( float4(WorldSpacePosition, 1.0), ViewProjectionModified[0] );
 hPosition.y  = dot( float4(WorldSpacePosition, 1.0), ViewProjectionModified[1] );
 hPosition.z  = dot( float4(WorldSpacePosition, 1.0), ViewProjectionModified[2] );
 hPosition.zw = hPosition.zz * ViewProjectionModified[3].xz + ViewProjectionModified[3].yw;
    return hPosition;
}

float GetViewSpaceDepthFromWorldPosition(float3 WorldSpacePosition)
{
 return GetShadowReceiverDepthPC(WorldSpacePosition,
     dot(float4(WorldSpacePosition, 1.0), ViewProjectionModified[2]));
}

// Post-perspective [0,1] depth-buffer value for a world position, using the SAME projection
// as TransformWorldToProjection (ViewProjectionModified). For per-pixel depth correction (POM).
float DepthFromWorldPosition(float3 WorldSpacePosition)
{
 float rawZ  = dot( float4(WorldSpacePosition, 1.0), ViewProjectionModified[2] );
 float clipZ = rawZ * ViewProjectionModified[3].x + ViewProjectionModified[3].y;
 float clipW = rawZ * ViewProjectionModified[3].z + ViewProjectionModified[3].w;
 return clipZ / clipW;
}

#endif
