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
 return dot( float4(WorldSpacePosition, 1.0), ViewProjectionModified[2]);
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
