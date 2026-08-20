// ---- Created with 3Dmigoto v1.3.16 on Sat Jun  6 14:33:31 2026

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
  uint v6 : SV_IsFrontFace0,
  out float4 o0 : SV_Target0)
{
  float4 r0,r1,r2,r3,r4,r5,r6,r7,r8,r9,r10;
  uint4 bitmask, uiDest;
  float4 fDest;

  r0.xyzw = DiffuseTextureSamplerTexture.Sample(DiffuseTextureSampler_s, v1.xy).xyzw;
  r1.x = cmp(0 != sampleCoverage.x);
  r1.x = r1.x ? -9.99999997e-07 : -0.499998987;
  r1.x = r1.x + r0.w;
  r1.x = cmp(r1.x < 0);
  if (r1.x != 0) discard;
  r1.x = SpecularTextureSamplerTexture.Sample(SpecularTextureSampler_s, v1.xy).y;
  r1.y = dot(v2.xyz, v2.xyz);
  r1.y = rsqrt(r1.y);
  r1.yzw = v2.xyz * r1.yyy;
  r1.y = saturate(dot(r1.yzw, -KeyLightDirection.xyz));
  r1.y = log2(r1.y);
  r1.y = SpecularPower * r1.y;
  r1.y = exp2(r1.y);
  r1.y = Specularity * r1.y;
  r1.yzw = KeyLightSpecularColour.xyz * r1.yyy;
  r1.xyz = r1.yzw * r1.xxx;
  r1.w = v6.x ? v5.w : -v5.w;
  r2.x = cmp(140 < v3.w);
  if (r2.x != 0) {
    r2.x = 0;
  } else {
    r2.yz = cmp(v3.ww < ShadowMap_Constants.yx);
    r3.xyz = r2.yyy ? ShadowMap_WorldToLight[1]._m00_m01_m02 : ShadowMap_WorldToLight[2]._m00_m01_m02;
    r4.xyz = r2.yyy ? ShadowMap_WorldToLight[1]._m30_m31_m32 : ShadowMap_WorldToLight[2]._m30_m31_m32;
    r5.xyz = r2.yyy ? ShadowMap_WorldToLight[1]._m11_m12_m10 : ShadowMap_WorldToLight[2]._m11_m12_m10;
    r6.xyz = r2.yyy ? ShadowMap_WorldToLight[1]._m22_m20_m21 : ShadowMap_WorldToLight[2]._m22_m20_m21;
    r3.xyz = r2.zzz ? ShadowMap_WorldToLight[0]._m00_m01_m02 : r3.xyz;
    r7.y = r2.z ? ShadowMap_WorldToLight[0]._m10 : r5.z;
    r2.yw = r2.zz ? ShadowMap_WorldToLight[0]._m11_m12 : r5.xy;
    r5.xz = r2.zz ? ShadowMap_WorldToLight[0]._m20_m21 : r6.yz;
    r6.z = r2.z ? ShadowMap_WorldToLight[0]._m22 : r6.x;
    r6.xyw = r2.zzz ? ShadowMap_WorldToLight[0]._m30_m31_m32 : r4.xyz;
    r4.y = ShadowMap_Constants3.x;
    r4.xzw = float3(0,0,1);
    r8.xyz = v4.xyz + r4.yzz;
    r4.xyz = v4.xyz + r4.xyz;
    r9.xyz = v4.xyz;
    r9.w = 1;
    r7.x = r3.x;
    r7.z = r5.x;
    r7.w = r6.x;
    r10.x = dot(r9.xyzw, r7.xyzw);
    r5.x = r3.y;
    r5.y = r2.y;
    r5.w = r6.y;
    r10.y = dot(r9.xyzw, r5.xyzw);
    r6.x = r3.z;
    r6.y = r2.w;
    r10.z = dot(r9.xyzw, r6.xyzw);
    r8.w = 1;
    r3.x = dot(r8.xyzw, r7.xyzw);
    r3.y = dot(r8.xyzw, r5.xyzw);
    r3.z = dot(r8.xyzw, r6.xyzw);
    r7.x = dot(r4.xyzw, r7.xyzw);
    r7.y = dot(r4.xyzw, r5.xyzw);
    r7.z = dot(r4.xyzw, r6.xyzw);
    r2.yzw = r3.xyz + -r10.xyz;
    r3.xyz = r7.xyz + -r10.xyz;
    r10.w = -ShadowMap_Constants2.z * 0.000500000024 + r10.z;
    r3.w = min(140, v3.w);
    r3.w = -r3.w * 0.00714285718 + 1;
    r3.w = 5 * r3.w;
    r3.w = dot(r3.ww, r3.ww);
    r4.xyz = float3(0,0,-1);
    while (true) {
      r4.w = cmp(1 < r4.z);
      if (r4.w != 0) break;
      r4.w = r4.z * r4.z;
      r5.xyz = r4.zzz * r3.xyz + r10.xyw;
      r6.xy = r4.xy;
      r6.z = -1;
      while (true) {
        r5.w = cmp(1 < r6.z);
        if (r5.w != 0) break;
        r5.w = r6.z * r6.z + r4.w;
        r5.w = -r5.w / r3.w;
        r5.w = 1.44269502 * r5.w;
        r5.w = exp2(r5.w);
        r7.xyz = r6.zzz * r2.yzw + r5.xyz;
        r6.w = shadowMapSamplerHighDetailTexture.SampleCmpLevelZero(shadowMapSamplerHighDetail_s, r7.xy, r7.z).x;
        r6.x = r5.w * r6.w + r6.x;
        r6.y = r6.y + r5.w;
        r6.z = 1 + r6.z;
      }
      r4.xy = r6.xy;
      r4.z = 1 + r4.z;
    }
    r2.y = 1 / r4.y;
    r2.x = saturate(r4.x * r2.y);
  }
  r2.y = -0.25 + r1.w;
  r2.y = saturate(20 * r2.y);
  r2.z = saturate(-v3.w * ShadowMap_Constants2.w + ShadowMap_Constants.w);
  r2.x = r2.x * r2.y;
  r2.x = r2.x * r2.z + 1;
  r2.x = r2.x + -r2.z;
  r1.w = saturate(r2.x * r1.w);
  r2.yzw = KeyLightColour.xyz * r1.www + v5.xyz;
  r2.yzw = materialDiffuse.xyz * r2.yzw;
  r1.xyz = r2.xxx * r1.xyz;
  r0.xyz = r0.xyz * r2.yzw + r1.xyz;
  r1.xyz = FogColourPlusWhiteLevel.xyz + -r0.xyz;
  o0.xyz = v2.www * r1.xyz + r0.xyz;
  o0.w = r0.w;
  return;
}