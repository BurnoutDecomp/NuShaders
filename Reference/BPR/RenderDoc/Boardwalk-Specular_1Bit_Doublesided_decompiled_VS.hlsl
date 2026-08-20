// ---- Created with 3Dmigoto v1.3.16 on Sat Jun  6 14:42:59 2026

cbuffer _Globals : register(b0)
{
  float4 sampleCoverage : packoffset(c0);
  float4 HDRConstants : packoffset(c1);
  row_major float4x4 viewProjection : packoffset(c2);
  row_major float4x4 ViewProjectionModified : packoffset(c6);
  row_major float4x4 ShadowMap_WorldToLight[3] : packoffset(c10);
  float4 ShadowMap_Constants : packoffset(c22);
  float4 ShadowMap_Constants2 : packoffset(c23);
  float4 ShadowMap_Constants3 : packoffset(c24);
  float4 ShadowMap_ObjectCsmSelect : packoffset(c25);
  float4 ScattCoeffs : packoffset(c26);
  float4 FogColourPlusWhiteLevel : packoffset(c27);
  row_major float4x4 IrradianceQuadricA : packoffset(c28);
  row_major float4x4 IrradianceQuadricB : packoffset(c32);
  float3 ViewPosition : packoffset(c36);
  float3 KeyLightDirection : packoffset(c37);
  float3 KeyLightSpecularColour : packoffset(c38);
  float3 KeyLightColour : packoffset(c39);
  row_major float4x4 worldViewProj : packoffset(c40);
  row_major float4x4 world : packoffset(c44);
  float4 materialDiffuse : packoffset(c48) = {1,1,1,1};
  float SpecularPower : packoffset(c49) = {12};
  float Specularity : packoffset(c49.y) = {1.5};
}



// 3Dmigoto declarations
#define cmp -


void main(
  float3 v0 : POSITION0,
  float3 v1 : NORMAL0,
  float2 v2 : TEXCOORD0,
  out float4 o0 : SV_POSITION0,
  out float4 o1 : TEXCOORD0,
  out float4 o2 : TEXCOORD1,
  out float4 o3 : TEXCOORD2,
  out float4 o4 : TEXCOORD3,
  out float4 o5 : TEXCOORD4)
{
  float4 r0,r1,r2,r3;
  uint4 bitmask, uiDest;
  float4 fDest;

  r0.xyz = world._m10_m11_m12 * v0.yyy;
  r0.xyz = v0.xxx * world._m00_m01_m02 + r0.xyz;
  r0.xyz = v0.zzz * world._m20_m21_m22 + r0.xyz;
  r0.xyz = world._m30_m31_m32 + r0.xyz;
  r0.w = 1;
  r1.x = dot(r0.xyzw, ViewProjectionModified._m20_m21_m22_m23);
  r1.xy = r1.xx * ViewProjectionModified._m30_m32 + ViewProjectionModified._m31_m33;
  o0.zw = r1.xy;
  o3.w = r1.y;
  o0.x = dot(r0.xyzw, ViewProjectionModified._m00_m01_m02_m03);
  o0.y = dot(r0.xyzw, ViewProjectionModified._m10_m11_m12_m13);
  o4.xyzw = r0.xyzw;
  o1.xy = v2.xy;
  r1.xyz = ViewPosition.xyz + -r0.xyz;
  r0.w = dot(r1.xyz, r1.xyz);
  r0.w = sqrt(r0.w);
  r0.w = saturate(r0.w * ScattCoeffs.x + -ScattCoeffs.y);
  r0.w = log2(r0.w);
  r0.w = ScattCoeffs.z * r0.w;
  r0.w = exp2(r0.w);
  o2.w = ScattCoeffs.w * r0.w;
  r2.xyz = cmp(v1.xyz == float3(0,0,0));
  r0.w = r2.y ? r2.x : 0;
  r0.w = r2.z ? r0.w : 0;
  r2.xyz = r0.www ? float3(0,1,0) : v1.xyz;
  r3.xyz = world._m10_m11_m12 * r2.yyy;
  r2.xyw = r2.xxx * world._m00_m01_m02 + r3.xyz;
  r2.xyz = r2.zzz * world._m20_m21_m22 + r2.xyw;
  r0.w = dot(r2.xyz, r2.xyz);
  r0.w = rsqrt(r0.w);
  r2.xyz = r2.xyz * r0.www;
  r0.w = dot(r1.xyz, r2.xyz);
  r0.w = r0.w + r0.w;
  o2.xyz = r2.xyz * r0.www + -r1.xyz;
  r1.xyz = ShadowMap_WorldToLight[0]._m10_m11_m12 * r0.yyy;
  r0.xyw = r0.xxx * ShadowMap_WorldToLight[0]._m00_m01_m02 + r1.xyz;
  r0.xyz = r0.zzz * ShadowMap_WorldToLight[0]._m20_m21_m22 + r0.xyw;
  r0.xyz = ShadowMap_WorldToLight[0]._m30_m31_m32 + r0.xyz;
  o3.z = -ShadowMap_Constants2.z * 0.000500000024 + r0.z;
  o3.xy = r0.xy;
  r2.w = r2.x * r2.x;
  r0.x = dot(IrradianceQuadricA._m10_m11_m12_m13, r2.xyzw);
  r0.y = dot(IrradianceQuadricA._m20_m21_m22_m23, r2.xyzw);
  r0.z = dot(IrradianceQuadricA._m30_m31_m32_m33, r2.xyzw);
  r0.xyz = IrradianceQuadricA._m00_m01_m02 + r0.xyz;
  r1.xyzw = r2.xyzy * r2.yzxy;
  o5.w = dot(r2.xyz, -KeyLightDirection.xyz);
  r2.x = dot(IrradianceQuadricB._m00_m01_m02_m03, r1.xyzw);
  r2.y = dot(IrradianceQuadricB._m10_m11_m12_m13, r1.xyzw);
  r2.z = dot(IrradianceQuadricB._m20_m21_m22_m23, r1.xyzw);
  o5.xyz = saturate(r2.xyz + r0.xyz);
  return;
}