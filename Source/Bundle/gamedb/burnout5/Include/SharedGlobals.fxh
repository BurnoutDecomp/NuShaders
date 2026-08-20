#ifndef SHAREDGLOBALS_FXH
#define SHAREDGLOBALS_FXH

// ===========================================================================
//  Canonical shared global constants, declared ONCE for all platforms.
//
//  Must be included INSIDE a GLOBALS_BEGIN / GLOBALS_END block (see a shader).
//  On BPR these land in cbuffer _Globals : register(b0) at the exact offsets
//  the Remastered engine expects (from the RenderDoc capture). On X360 the
//  five constants that were register-forced keep their registers; the rest
//  auto-allocate (the X360 packer captures them by name into the descriptor).
//
//  Macro args: GLOBAL_*_R(name, X360reg, BPRpackoffset);  GLOBAL_*(name, BPRpackoffset)
//
//  BPR layout (packoffset):                              X360 forced reg:
// ---------------------------------------------------------------------------
GLOBAL_F4         (sampleCoverage,                 c0)
GLOBAL_F4X4       (viewProjection,                 c1)
GLOBAL_F4X4_R     (ViewProjectionModified,    c0,  c5)
GLOBAL_F4X4_ARRAY_R(ShadowMap_WorldToLight, 3, c4, c9)
GLOBAL_F4         (ShadowMap_Constants,            c21)
GLOBAL_F4         (ShadowMap_Constants2,           c22)
GLOBAL_F4         (ShadowMap_Constants3,           c23)
GLOBAL_F4         (ShadowMap_ObjectCsmSelect,      c24)
GLOBAL_F4_R       (ScattCoeffs,               c16, c25)
GLOBAL_F4         (FogColourPlusWhiteLevel,        c26)
GLOBAL_F4X4_R     (IrradianceQuadricA,        c17, c27)
GLOBAL_F4X4_R     (IrradianceQuadricB,        c21, c31)
GLOBAL_F3         (ViewPosition,                   c35)
GLOBAL_F3         (KeyLightDirection,              c36)
GLOBAL_F3         (KeyLightSpecularColour,         c37)
GLOBAL_F3         (KeyLightColour,                 c38)
GLOBAL_F4X4       (worldViewProj,                  c39)
GLOBAL_F4X4       (world,                          c43)

#endif // SHAREDGLOBALS_FXH
