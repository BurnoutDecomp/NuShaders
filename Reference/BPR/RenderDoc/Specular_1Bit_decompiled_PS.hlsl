// ---- Created with 3Dmigoto v1.3.16 on Fri Jun  5 20:29:45 2026

cbuffer _Globals : register(b0)
{
  float4 sampleCoverage : packoffset(c0);
  row_major float4x4 viewProjection : packoffset(c1);
  row_major float4x4 ViewProjectionModified : packoffset(c5);
  row_major float4x4 ShadowMap_WorldToLight[3] : packoffset(c9);
  float4 ShadowMap_Constants : packoffset(c21);
  float4 ShadowMap_Constants2 : packoffset(c22);
  float4 ShadowMap_Constants3 : packoffset(c23);
  float4 ShadowMap_ObjectCsmSelect : packoffset(c24);
  float4 ScattCoeffs : packoffset(c25);
  float4 FogColourPlusWhiteLevel : packoffset(c26);
  row_major float4x4 IrradianceQuadricA : packoffset(c27);
  row_major float4x4 IrradianceQuadricB : packoffset(c31);
  float3 ViewPosition : packoffset(c35);
  float3 KeyLightDirection : packoffset(c36);
  float3 KeyLightSpecularColour : packoffset(c37);
  float3 KeyLightColour : packoffset(c38);
  row_major float4x4 worldViewProj : packoffset(c39);
  row_major float4x4 world : packoffset(c43);
  float4 materialDiffuse : packoffset(c47) = {1,1,1,1};
  float SpecularPower : packoffset(c48) = {12};
  float Specularity : packoffset(c48.y) = {1.5};
}

SamplerState DiffuseTextureSampler_s : register(s0);
SamplerState SpecularTextureSampler_s : register(s1);
SamplerComparisonState shadowMapSamplerHighDetail_s : register(s15);
Texture2D<float4> DiffuseTextureSamplerTexture : register(t0);
Texture2D<float4> SpecularTextureSamplerTexture : register(t1);
Texture2D<float4> shadowMapSamplerHighDetailTexture : register(t15);


// 3Dmigoto declarations
#define cmp -


void main(
  float4 v0 : SV_POSITION0,
  float4 v1 : TEXCOORD0,
  float4 v2 : TEXCOORD1,
  float4 v3 : TEXCOORD2,
  float4 v4 : TEXCOORD3,
  float4 v5 : TEXCOORD4,
  out float4 o0 : SV_Target0)
{
  float4 r0,r1,r2,r3,r4,r5,r6,r7,r8,r9,r10;
  uint4 bitmask, uiDest;
  float4 fDest;

  r0.xyz = DiffuseTextureSamplerTexture.Sample(DiffuseTextureSampler_s, v1.xy).xyz;
  r0.w = SpecularTextureSamplerTexture.Sample(SpecularTextureSampler_s, v1.xy).y;
  r1.x = dot(v2.xyz, v2.xyz);
  r1.x = rsqrt(r1.x);
  r1.xyz = v2.xyz * r1.xxx;
  r1.x = dot(r1.xyz, -KeyLightDirection.xyz);
  r1.x = max(0, r1.x);
  r1.x = log2(r1.x);
  r1.x = SpecularPower * r1.x;
  r1.x = exp2(r1.x);
  r1.x = Specularity * r1.x;
  r1.xyz = KeyLightSpecularColour.xyz * r1.xxx;
  r1.xyz = r1.xyz * r0.www;
  r0.w = cmp(140 < v3.w);
  if (r0.w != 0) {
    r0.w = 0;
  } else {
    r2.xy = cmp(v3.ww < ShadowMap_Constants.yx);
    r3.xyz = r2.xxx ? ShadowMap_WorldToLight[1]._m00_m01_m02 : ShadowMap_WorldToLight[2]._m00_m01_m02;
    r4.xyz = r2.xxx ? ShadowMap_WorldToLight[1]._m30_m31_m32 : ShadowMap_WorldToLight[2]._m30_m31_m32;
    r5.xyz = r2.xxx ? ShadowMap_WorldToLight[1]._m11_m12_m10 : ShadowMap_WorldToLight[2]._m11_m12_m10;
    r2.xzw = r2.xxx ? ShadowMap_WorldToLight[1]._m22_m20_m21 : ShadowMap_WorldToLight[2]._m22_m20_m21;
    r3.xyz = r2.yyy ? ShadowMap_WorldToLight[0]._m00_m01_m02 : r3.xyz;
    r6.y = r2.y ? ShadowMap_WorldToLight[0]._m10 : r5.z;
    r5.xy = r2.yy ? ShadowMap_WorldToLight[0]._m11_m12 : r5.xy;
    r7.xz = r2.yy ? ShadowMap_WorldToLight[0]._m20_m21 : r2.zw;
    r8.z = r2.y ? ShadowMap_WorldToLight[0]._m22 : r2.x;
    r8.xyw = r2.yyy ? ShadowMap_WorldToLight[0]._m30_m31_m32 : r4.xyz;
    r2.y = ShadowMap_Constants3.x;
    r2.xzw = float3(0,0,1);
    r4.xyz = v4.xyz + r2.yzz;
    r2.xyz = v4.xyz + r2.xyz;
    r9.xyz = v4.xyz;
    r9.w = 1;
    r6.x = r3.x;
    r6.z = r7.x;
    r6.w = r8.x;
    r10.x = dot(r9.xyzw, r6.xyzw);
    r7.x = r3.y;
    r7.y = r5.x;
    r7.w = r8.y;
    r10.y = dot(r9.xyzw, r7.xyzw);
    r8.x = r3.z;
    r8.y = r5.y;
    r10.z = dot(r9.xyzw, r8.xyzw);
    r4.w = 1;
    r3.x = dot(r4.xyzw, r6.xyzw);
    r3.y = dot(r4.xyzw, r7.xyzw);
    r3.z = dot(r4.xyzw, r8.xyzw);
    r4.x = dot(r2.xyzw, r6.xyzw);
    r4.y = dot(r2.xyzw, r7.xyzw);
    r4.z = dot(r2.xyzw, r8.xyzw);
    r2.xyz = r3.xyz + -r10.xyz;
    r3.xyz = r4.xyz + -r10.xyz;
    r1.w = min(140, v3.w);
    r1.w = -r1.w * 0.00714285718 + 1;
    r1.w = 5 * r1.w;
    r1.w = dot(r1.ww, r1.ww);
    r4.xy = float2(0,0);
    r2.w = -1;
    while (true) {
      r3.w = cmp(1 < r2.w);
      if (r3.w != 0) break;
      r3.w = r2.w * r2.w;
      r5.xyz = r2.www * r3.xyz + r10.xyz;
      r6.xy = r4.xy;
      r6.z = -1;
      while (true) {
        r4.z = cmp(1 < r6.z);
        if (r4.z != 0) break;
        r4.z = r6.z * r6.z + r3.w;
        r4.z = -r4.z / r1.w;
        r4.z = 1.44269502 * r4.z;
        r4.z = exp2(r4.z);
        r7.xyz = r6.zzz * r2.xyz + r5.xyz;
        r4.w = shadowMapSamplerHighDetailTexture.SampleCmpLevelZero(shadowMapSamplerHighDetail_s, r7.xy, r7.z).x;
        r6.x = r4.z * r4.w + r6.x;
        r6.y = r6.y + r4.z;
        r6.z = 1 + r6.z;
      }
      r4.xy = r6.xy;
      r2.w = 1 + r2.w;
    }
    r1.w = 1 / r4.y;
    r0.w = saturate(r4.x * r1.w);
  }
  r1.w = -0.25 + v5.w;
  r1.w = saturate(20 * r1.w);
  r2.x = saturate(-v3.w * ShadowMap_Constants2.w + ShadowMap_Constants.w);
  r0.w = r1.w * r0.w;
  r0.w = r0.w * r2.x + 1;
  r0.w = r0.w + -r2.x;
  r1.w = saturate(v5.w);
  r1.w = r1.w * r0.w;
  r2.xyz = KeyLightColour.xyz * r1.www + v5.xyz;
  r2.xyz = materialDiffuse.xyz * r2.xyz;
  r1.xyz = r1.xyz * r0.www;
  r0.xyz = r0.xyz * r2.xyz + r1.xyz;
  r1.xyz = FogColourPlusWhiteLevel.xyz + -r0.xyz;
  o0.xyz = v2.www * r1.xyz + r0.xyz;
  o0.w = 1;
  return;
}