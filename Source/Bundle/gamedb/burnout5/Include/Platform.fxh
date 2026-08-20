#ifndef PLATFORM_FXH
#define PLATFORM_FXH

// ===========================================================================
//  Cross-platform shader compatibility layer.
//
//  One set of .fx source compiles to three targets via these macros:
//    D_PLATFORM_BPR  -> SM5 / DX11  (Burnout Paradise Remastered)
//    D_PLATFORM_X360 -> Xenos SM3   (Xbox 360)
//    (neither)       -> DX9 SM3     (PC TUB)
//
//  SM5 differences handled here: single global cbuffer at b0 (packoffset),
//  Texture2D+SamplerState pairs, .Sample/.SampleBias/.SampleCmp, and SV_*
//  system-value semantics. The only real *logic* difference (shadow compare)
//  lives in Shadow.fxh.
//
//  Global-constant macros take the constant's register on BOTH platforms as
//  full tokens (e.g. c0 / c5) to avoid preprocessor token-pasting pitfalls.
//  Texture macros take a numeric slot (paste-friendly: t##slot / s##slot).
// ===========================================================================

#ifdef D_PLATFORM_BPR
// ----------------------------- SM5 / DX11 ----------------------------------

#define GLOBALS_BEGIN  cbuffer _Globals : register(b0) {
#define GLOBALS_END    }

// Shared globals. _R = had a forced X360 register; plain = auto on SM3.
#define GLOBAL_F4(name, bprreg)               float4 name : packoffset(bprreg);
#define GLOBAL_F3(name, bprreg)               float3 name : packoffset(bprreg);
#define GLOBAL_F4X4(name, bprreg)             row_major float4x4 name : packoffset(bprreg);
#define GLOBAL_F4_R(name, x360reg, bprreg)    float4 name : packoffset(bprreg);
#define GLOBAL_F3_R(name, x360reg, bprreg)    float3 name : packoffset(bprreg);
#define GLOBAL_F4X4_R(name, x360reg, bprreg)  row_major float4x4 name : packoffset(bprreg);
#define GLOBAL_F4X4_ARRAY_R(name, cnt, x360reg, bprreg) row_major float4x4 name[cnt] : packoffset(bprreg);

// Per-shader material constants (inside the same _Globals cbuffer).
#define MATERIAL_F4(name, bprreg, x,y,z,w)    float4 name : packoffset(bprreg);
#define MATERIAL_F(name, bprreg, dflt)        float  name : packoffset(bprreg);

// Textures / samplers (BPR pairs a SamplerState "name" with Texture2D "nameTexture").
#define DECL_TEX2D(name, slot)    Texture2D   name##Texture : register(t##slot); SamplerState name : register(s##slot)
#define DECL_TEXCUBE(name, slot)  TextureCube name##Texture : register(t##slot); SamplerState name : register(s##slot)
#define SAMPLE2D(name, uv)         name##Texture.Sample(name, uv)
#define SAMPLE2D_BIAS(name, uv, b) name##Texture.SampleBias(name, uv, b)
#define SAMPLE2D_LOD(name, uv, lod) name##Texture.SampleLevel(name, uv, lod)
#define SAMPLECUBE(name, dir)      name##Texture.Sample(name, dir)
#define SAMPLECUBE_LOD(name, dir, lod) name##Texture.SampleLevel(name, dir, lod)
// Pass a 2D texture+sampler pair to a function (and the matching call-site argument).
#define TEX2D_PARAM(name)          Texture2D name##Texture, SamplerState name
#define TEX2D_ARG(name)            name##Texture, name
#define TEXCUBE_PARAM(name)        TextureCube name##Texture, SamplerState name
#define TEXCUBE_ARG(name)          name##Texture, name

// Semantics
#define VPOS_OUT       SV_Position
#define COLOR_OUT(n)   SV_Target##n
// Conservative depth output: POM only pushes depth farther, so SV_DepthGreaterEqual keeps early-Z.
#define DEPTH_OUT      SV_DepthGreaterEqual
#define VFACE_TYPE     bool
#define VFACE_SEM      SV_IsFrontFace
// FACE_IS_FRONT mirrors the SM3 `(lfFace < 0)` test (which is the FRONT case in
// Burnout's X360 VFACE polarity). On DX11 the front face is SV_IsFrontFace == true,
// i.e. the bool itself.
#define FACE_IS_FRONT(f) (f)

#else
// --------------------------- SM3 (X360 + TUB) ------------------------------

#define GLOBALS_BEGIN
#define GLOBALS_END

#define GLOBAL_F4(name, bprreg)               float4 name;
#define GLOBAL_F3(name, bprreg)               float3 name;
#define GLOBAL_F4X4(name, bprreg)             float4x4 name;

#ifdef D_PLATFORM_X360
#define GLOBAL_F4_R(name, x360reg, bprreg)    float4 name : register(x360reg);
#define GLOBAL_F3_R(name, x360reg, bprreg)    float3 name : register(x360reg);
#define GLOBAL_F4X4_R(name, x360reg, bprreg)  float4x4 name : register(x360reg);
#define GLOBAL_F4X4_ARRAY_R(name, cnt, x360reg, bprreg) float4x4 name[cnt] : register(x360reg);
#else
#define GLOBAL_F4_R(name, x360reg, bprreg)    float4 name;
#define GLOBAL_F3_R(name, x360reg, bprreg)    float3 name;
#define GLOBAL_F4X4_R(name, x360reg, bprreg)  float4x4 name;
#define GLOBAL_F4X4_ARRAY_R(name, cnt, x360reg, bprreg) float4x4 name[cnt];
#endif

#define MATERIAL_F4(name, bprreg, x,y,z,w)    float4 name = { x, y, z, w };
#define MATERIAL_F(name, bprreg, dflt)        float  name = dflt;

#define DECL_TEX2D(name, slot)    sampler2D   name : register(s##slot)
#define DECL_TEXCUBE(name, slot)  samplerCUBE name : register(s##slot)
#define SAMPLE2D(name, uv)         tex2D(name, uv)
#define SAMPLE2D_BIAS(name, uv, b) tex2Dbias(name, float4((uv), 0, (b)))
#define SAMPLE2D_LOD(name, uv, lod) tex2Dlod(name, float4((uv), 0, (lod)))
#define SAMPLECUBE(name, dir)      texCUBE(name, dir)
#define SAMPLECUBE_LOD(name, dir, lod) texCUBElod(name, float4((dir), (lod)))
// Pass a 2D sampler to a function (and the matching call-site argument).
#define TEX2D_PARAM(name)          sampler2D name
#define TEX2D_ARG(name)            name
#define TEXCUBE_PARAM(name)        samplerCUBE name
#define TEXCUBE_ARG(name)          name

// Semantics
#define VPOS_OUT       POSITION
#define COLOR_OUT(n)   COLOR##n
#define DEPTH_OUT      DEPTH
#define VFACE_TYPE     float
#define VFACE_SEM      VFACE
#define FACE_IS_FRONT(f) ((f) < 0.0)

#endif

#endif // PLATFORM_FXH
