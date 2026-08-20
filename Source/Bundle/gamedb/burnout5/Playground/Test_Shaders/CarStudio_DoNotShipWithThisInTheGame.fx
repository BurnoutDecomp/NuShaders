#include "../../Include/Transform.fxh"
#include "../../Include/Shadow.fxh"
#include "../../Include/Fog.fxh"
#include "../../Include/Irradiance.fxh"
float3   KeyLightDirection
<
 string scope = "global";
>;
float3   ViewPosition    : cameraposition
<
 string scope = "global";
>;
float3   KeyLightColour
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
 string purpose = "none";
    string UIWidget = "rgba";
> = {1.0f, 1.0f, 1.0f, 1.0f};
texture2D DiffuseTexture : diffuse
<
 string scope = "material";
>;
texture2D LightmapTexture
<
 string scope = "material";
>;
sampler2D DiffuseTextureSampler : register(s0)
<
 string scope = "material";
 string purpose = "none";
> = sampler_state
{
 Texture = <DiffuseTexture>;
 MinFilter = Linear;
 MagFilter = Linear;
 MipFilter = Linear;
};
sampler2D LightmapTextureSampler : register(s1)
<
 string scope = "material";
 string purpose = "none";
> = sampler_state
{
 Texture = <LightmapTexture>;
 MinFilter = Linear;
 MagFilter = Linear;
 MipFilter = Linear;
};
struct vertexInput {
    float3 position    : POSITION;
    float3 normal    : NORMAL;
    float2 texCoordDiffuse  : TEXCOORD0;
    float2 atlas    : TEXCOORD1;
};
struct vertexOutput {
    float4 hPosition    : POSITION;
    float3 texCoordDiffuseAndFog : TEXCOORD0;
    float2 lightmapUV    : TEXCOORD1;
    float4 IndirectColourAndKey  : TEXCOORD3;
};
struct vertexOutputLod1 {
    float4 hPosition    : POSITION;
    float3 texCoordDiffuseAndFog : TEXCOORD0;
    float2 lightmapUV    : TEXCOORD1;
    float4 IndirectColourAndKey  : TEXCOORD2;
};
vertexOutput VS_Main(vertexInput IN) 
{
    vertexOutput OUT;
     float3 WorldSpacePosition = mul( float4( IN.position, 1.0f ), world ).xyz;           
    float3 WorldSpaceNormal  = normalize( mul( IN.normal, (float3x3)world ) );             
    OUT.hPosition    = TransformWorldToProjection( WorldSpacePosition );
    OUT.texCoordDiffuseAndFog.xy = IN.texCoordDiffuse;
  OUT.lightmapUV.xy = IN.atlas;
    OUT.IndirectColourAndKey.xyz = ComputeIrradianceFast( WorldSpaceNormal );
    OUT.IndirectColourAndKey.w = dot( WorldSpaceNormal, -KeyLightDirection ); 
    float3 lEyeToVertex    = ViewPosition.xyz - WorldSpacePosition;
    OUT.texCoordDiffuseAndFog.z = CalculateScattering( length( lEyeToVertex ) );
    return OUT;
}
float4 PS_Main( vertexOutput IN ) : COLOR
{
 float3 diffuseTexture = tex2D( DiffuseTextureSampler, IN.texCoordDiffuseAndFog.xy ).rgb;
 float lightmapTexture = tex2D( LightmapTextureSampler, IN.lightmapUV.xy ).g;
 float lf2xWhiteLevel = FogColourPlusWhiteLevel.w + FogColourPlusWhiteLevel.w;
 lightmapTexture *= lf2xWhiteLevel;
    float  lDirectLightFactor   = saturate( (float)IN.IndirectColourAndKey.w );
    float3 lDirectLightColour   = float3( KeyLightColour );
    float3 lIndirectLightColour = float3( IN.IndirectColourAndKey.xyz );
    float3 lLightColour         = max( (float3)(lightmapTexture.xxx), lerp( lIndirectLightColour, lDirectLightColour, lDirectLightFactor ) ) * float3(materialDiffuse.xyz);
    float3 lFinalColour         = (diffuseTexture * lLightColour);
    lFinalColour = lerp( lFinalColour.rgb, float3(FogColourPlusWhiteLevel.rgb), float(IN.texCoordDiffuseAndFog.z) );
    return float4(lFinalColour, 1);
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
    float lfDepth = ( IN.hPositionDepthCopy.x / IN.hPositionDepthCopy.y );
    return lfDepth.xxxx;
#else
    return float4(1,1,1,1);
#endif
}
technique ZOnlyNull
<
     string sharedName="ZOnlyNull";
>
{
    pass p0
    {  
        ZWriteEnable = False;
#ifndef D_MSAA_ENABLED
        ColorWriteEnable = 0;
#endif
  VertexShader = compile vs_3_0 VS_Main_ZOnly();
  PixelShader  = compile ps_3_0 PS_Main_ZOnly();
    }
}
