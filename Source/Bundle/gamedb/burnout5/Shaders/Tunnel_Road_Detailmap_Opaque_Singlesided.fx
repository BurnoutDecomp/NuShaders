#define USE_HDR_CONSTANTS
#include "../Include/Constants.fxh"
#include "../Include/NormalMapping.fxh"
#include "../Include/Transform.fxh"
// D_ROAD_X360: the shipped X360 permutation (VS C9545094 / PS 89C8D9A5) has no
// tangent-space road normal mapping -- it is the D_DISABLE_ROAD_SHADER branch,
// with no worldTangent interpolator and no s3/s4 samplers.
#if defined(D_ROAD_X360) && !defined(D_DISABLE_ROAD_SHADER)
#define D_DISABLE_ROAD_SHADER
#endif
#define SHADOW_ROAD_X360_USER
#include "../Include/Shadow.fxh"
#include "../Include/Fog.fxh"
#include "../Include/Irradiance.fxh"
#include "../Include/DepthEncode.fxh"
#ifdef D_VERSION_16
bool gbDisableAniso : register(b0);
#endif
float3   ViewPosition : cameraposition
<
 string scope = "global";
>;
float3   KeyLightDirection
<
 string scope = "global";
>;
float3   KeyLightSpecularColour
<
 string scope = "global";
>;
float3   KeyLightColour
<
 string scope = "global";
>;
float4  SkyReflectionColour
<
 string scope = "global";
>;
float4x4  worldViewProj : WorldViewProjection
<
 string scope = "object";
>;
float4x4  world : World
<
 string scope = "object";
>;
float4 materialDiffuse
<
 string scope = "material";
    string UIWidget = "rgba";
> = {1.0f, 1.0f, 1.0f, 1.0f};
float SpecularPower
<
 string scope = "material";
    string UIWidget = "slider";
    float UIMin = 1.0;
    float UIMax = 100.0;
    float UIStep = 1.0;
    string UIName = "specular power";
> = 12.0;
float Specularity
<
 string scope = "material";
    string UIWidget = "slider";
    float UIMin = 0.0;
    float UIMax = 5.0;
    float UIStep = 0.1;
    string UIName = "Specularity";
> = 1.5;
float lineUMultiplier
<
    string scope = "material";
    string UIWidget = "slider";
    float UIMin = 0.1;
    float UIMax = 32.0;
    float UIStep = 0.1;
    string UIName = "line U multiplier";
> = 4.0f;
texture2D lineMap : diffuse
<
 string scope   = "material";
 string purpose = "none";
>;
texture2D detailMap
<
 string scope   = "material";
 string purpose = "none";
>;
texture2D baseMap
<
 string scope   = "material";
 string purpose = "none";
>;
#ifndef D_ROAD_X360
texture2D NormalTexture
<
    string scope = "material";
 string purpose = "none";
>;
texture2D NormalDetailTexture
<
    string scope = "material";
 string purpose = "none";
>;
#endif
sampler2D lineMapSampler : register(s0) 
<
 string scope   = "material";
 string purpose = "none";
> = sampler_state
{
 Texture = <lineMap>;
 MinFilter = Linear;
 MagFilter = Linear;
 MipFilter = Linear;
};
sampler2D detailMapSampler : register(s1)
<
 string scope   = "material";
 string purpose = "none";
> = sampler_state
{
 Texture = <detailMap>;
 MinFilter = Linear;
 MagFilter = Linear;
 MipFilter = Linear;
};
sampler2D baseMapSampler : register(s2)
<
 string scope   = "material";
 string purpose = "none";
> = sampler_state
{
 Texture = <baseMap>;
 MinFilter = Linear;
 MagFilter = Linear;
 MipFilter = Linear;
};
#ifndef D_ROAD_X360
sampler NormalTextureSampler : register(s3)
<
    string scope = "material";
 string purpose = "none";
> = sampler_state
{
    Texture = <NormalTexture>;
#if MAYA_CGFX
    minFilter = LinearMipMapLinear;
    MagFilter = Linear;
#else
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
#endif
};
sampler NormalDetailTextureSampler : register(s4)
<
    string scope = "material";
 string purpose = "none";
> = sampler_state
{
    Texture = <NormalDetailTexture>;
#if MAYA_CGFX
    minFilter = LinearMipMapLinear;
    MagFilter = Linear;
#else
    MinFilter = Linear;
    MagFilter = Linear;
    MipFilter = Linear;
#endif
};
#endif // !D_ROAD_X360
struct vertexInput {
    float3 position    : POSITION;
    float3 normal    : NORMAL;
    float2 texCoordDiffuse  : TEXCOORD0;
};
struct vertexOutput {
    float4 hPosition                    : POSITION;
    float2 texCoordDiffuse              : TEXCOORD0;
    float4 texCoordDetailAndLine        : TEXCOORD1;
    float4 IndirectColourAndKey         : TEXCOORD2;
    SHADOWMAP_INTERPOLATORS2( 3,4 )
    float4 WorldSpaceNormalAndFog  : TEXCOORD5;
    float3 WorldSpaceViewDirection      : TEXCOORD6;
#ifdef D_MRT
    float2 hPositionDepthCopy           : TEXCOORD7;
#endif
#ifndef D_ROAD_X360
    float3 worldTangent                 : TEXCOORD8;
#endif
};
struct vertexOutputLod1 {
    float4 hPosition                    : POSITION;
    float2 texCoordDiffuse              : TEXCOORD0;
    float4 texCoordDetailAndLine        : TEXCOORD1;
    float4 IndirectColourAndKey         : TEXCOORD2;
    float4 WorldSpaceNormalAndFog  : TEXCOORD5;
    float3 WorldSpaceViewDirection      : TEXCOORD6;
};
vertexOutput VS_Main( vertexInput IN ) 
{
    vertexOutput OUT;
        float3 WorldSpacePosition = mul( float4( IN.position, 1.0f ), world ).xyz;           
    float3 WorldSpaceNormal  = normalize( mul( IN.normal, (float3x3)world ) );             
    OUT.hPosition    = TransformWorldToProjection( WorldSpacePosition );
#ifdef D_MRT
    OUT.hPositionDepthCopy = OUT.hPosition.zw;
#endif
    OUT.texCoordDiffuse.xy = IN.texCoordDiffuse;
    OUT.texCoordDetailAndLine.xy = IN.position.xz;
    OUT.texCoordDetailAndLine.z  = IN.texCoordDiffuse.x * lineUMultiplier;
    OUT.texCoordDetailAndLine.w  = IN.texCoordDiffuse.y;
    OUT.IndirectColourAndKey.rgb = ComputeIrradianceFast( WorldSpaceNormal );
    OUT.IndirectColourAndKey.w = dot( WorldSpaceNormal, -KeyLightDirection );
    float3 lVertexToEye = ViewPosition.xyz - WorldSpacePosition;
    float lFog = CalculateScattering( length( lVertexToEye ) );
    OUT.WorldSpaceNormalAndFog.xyz = WorldSpaceNormal;
    OUT.WorldSpaceNormalAndFog.w = lFog;
    OUT.WorldSpaceViewDirection = lVertexToEye;
    CALC_SHADOWMAP_INTERPOLATORS3( WorldSpacePosition, OUT.hPosition.w );
#ifndef D_ROAD_X360
    OUT.worldTangent = normalize( mul( float3(1.0, 0.0, 0.0), (float3x3)world ) );
#endif
    return OUT;
}
#ifdef D_MRT
void PS_Main( in  vertexOutput IN,
              in float4 windowPos : VPOS, 
              out float4 oColour0 : COLOR0,
              out float4 oColour1 : COLOR1 )
#else
void PS_Main( in  vertexOutput IN,
              in float4 windowPos : VPOS, 
              out float4 oColour0 : COLOR0 )
#endif
{
    float2 lDiffuseUV   = IN.texCoordDiffuse;
    float4 lDetailAndLineUV = IN.texCoordDetailAndLine;
    float4 baseTex;  
    float4 lineTex;  
    float2 detailTex;
    baseTex   = (float4)tex2D(baseMapSampler, lDiffuseUV);
    lineTex   = tex2D(lineMapSampler, lDetailAndLineUV.zw);
#ifdef D_VERSION_16
    if (gbDisableAniso) { detailTex = tex2Dbias(detailMapSampler, float4(lDetailAndLineUV.xy, 0, 0)).rg; }
    else { detailTex = tex2D(detailMapSampler, lDetailAndLineUV.xy).rg; }
#else
    detailTex = tex2D(detailMapSampler, lDetailAndLineUV.xy).rg;
#endif
    float  detailsAsphalt = detailTex.x * (float)2.0;
    float  detailsLine    = detailTex.y * (float)2.0;
    float3 asphalt = baseTex.rgb * (float3)materialDiffuse * detailsAsphalt;
    float3 lines   = lineTex.rgb * detailsLine;
    float  lineMask = lineTex.a;
    float3 diffuseTexture = lines + (asphalt * (float(1.0)-lineMask));
    float  detailsSpec     = lerp(detailsAsphalt, detailsLine, lineMask);
    float  lhSpecMap       = saturate(baseTex.a * detailsSpec);
    float3 lLightDirection = (float3)-KeyLightDirection;
    float  lhSpecularPower = (float)SpecularPower;
    float  lhSpecularity   = (float)Specularity * float(0.5);
    float3 lNormal;
#ifndef D_DISABLE_ROAD_SHADER
    float3 normalTex       = ConvertGANormalsToXYZ( tex2D( NormalTextureSampler, lDiffuseUV ).ga );
    float3 normalDetailTex = ConvertGANormalsToXYZ( tex2D( NormalDetailTextureSampler, lDetailAndLineUV.xy ).ga );
    float weight1 = 0.99;
    float weight2 = 0.5;
    float3 normalTexBlend = weight1 * (normalTex - float3(0,0,1)) + weight2 * (normalDetailTex - float3(0,0,1)) + float3(0,0,1);
    float3 innormal        = normalize( float3(IN.WorldSpaceNormalAndFog.xyz) );
    float3 intangent       = normalize( float3(-IN.worldTangent) );
    float3 binormal        = cross( intangent, innormal );
    lNormal               = TransformTangetSpaceNormalToWorldSpaceNormal( normalTexBlend, innormal, intangent, binormal );
#else
    lNormal               = normalize( (float3)IN.WorldSpaceNormalAndFog.xyz );
#endif
    float3 lViewDirection  = normalize( (float3)IN.WorldSpaceViewDirection );
    float3 lHalfVector     = normalize(lViewDirection + lLightDirection);
    float  lNdotH          = saturate( dot( lNormal, lHalfVector ) );
    float3 lSpecularColour = float3(KeyLightSpecularColour) * ( lhSpecularity * pow( lNdotH, lhSpecularPower ) );
    lSpecularColour      *= lhSpecMap;
    float  lShadowModulation    = CALC_SHADOW_FACTOR_3( (float)IN.IndirectColourAndKey.w );
#ifdef D_ROAD_X360
    // X360 (PS 89C8D9A5): the diffuse N.L is the interpolated vertex value --
    // the microcode has no per-pixel dp3 against KeyLightDirection.
    float  lDirectLightFactor   = saturate( (float)IN.IndirectColourAndKey.w * lShadowModulation );
#else
    float  lDirectLightFactor   = saturate( (float)dot( lNormal, -KeyLightDirection ) * lShadowModulation );
#endif
    float3 lDirectLightColour   = float3( KeyLightColour );
    float3 lIndirectLightColour = float3( IN.IndirectColourAndKey.xyz );
#ifdef D_ROAD_X360
    // X360: cross-fade indirect -> key light (the PC add double-counts ambient in sun)
    float3 lLightColour         = lerp( lIndirectLightColour, lDirectLightColour, lDirectLightFactor );
#else
    float3 lLightColour         = ( lIndirectLightColour + lDirectLightColour * lDirectLightFactor );
#endif
    float3 lFinalColour         = (diffuseTexture * lLightColour) + (lSpecularColour * lShadowModulation);
    lFinalColour = lerp( lFinalColour, (float3)FogColourPlusWhiteLevel.rgb, float(IN.WorldSpaceNormalAndFog.w) );
#ifdef D_ROAD_X360
    // X360 writes the direct-light factor to dest alpha, not 1
    oColour0 = float4(lFinalColour, lDirectLightFactor);
#else
    oColour0 = float4(lFinalColour, 1);
#endif
#ifdef D_MRT
    float lfDepth = ( IN.hPositionDepthCopy.x / IN.hPositionDepthCopy.y );
    oColour1 = ConvertDepthToARGB( lfDepth );
#endif
}
technique Default
{
    pass p0
    {  
  VertexShader = compile vs_3_0 VS_Main();
  PixelShader  = compile ps_3_0 PS_Main();
    }
}
struct vertexInputZOnly {
    float3 position    : POSITION;
};
struct vertexOutputZOnly {
    float4 hPosition   : POSITION;
#ifdef D_MSAA_ENABLED
    float2 hPositionDepthCopy   : TEXCOORD0;
#endif
};
vertexOutputZOnly VS_Main_ZOnly( vertexInputZOnly IN
        ) 
{ 
    vertexOutputZOnly OUT;
     float3 WorldSpacePosition = mul( float4( IN.position, 1.0f ), world ).xyz;           
    OUT.hPosition    = TransformWorldToProjection( WorldSpacePosition );
#ifdef D_MSAA_ENABLED
    OUT.hPositionDepthCopy = OUT.hPosition.zw;
#endif
    return OUT;
}
float4 PS_Main_ZOnly( vertexOutputZOnly IN ): COLOR
{
#ifdef D_MSAA_ENABLED
#ifdef D_ADD_STENCIL
    float lfDepth = ( IN.hPositionDepthCopy.x / IN.hPositionDepthCopy.y ) + 2.0f;
#else
    float lfDepth = ( IN.hPositionDepthCopy.x / IN.hPositionDepthCopy.y );
#endif
    return lfDepth.xxxx;
#else
    return float4(1,1,1,1);
#endif
}
technique ZOnlyOpaqueSingleSided
<
  string sharedName="ZOnlyOpaqueSingleSided";
>
{
    pass p0
    {  
        CullMode = cw;
#ifndef D_MSAA_ENABLED
        ColorWriteEnable = 0;
#endif
  VertexShader = compile vs_3_0 VS_Main_ZOnly();
  PixelShader  = compile ps_3_0 PS_Main_ZOnly();
    }
}

