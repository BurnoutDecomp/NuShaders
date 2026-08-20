float3 gLightDirection;
float3 gLightColour;
float4 gShinyParams;
sampler DiffuseSampler   : register(s0);
void
main(
    in float4 iColour      : COLOR0,
    in float2 iUV          : TEXCOORD0,
    in float3 iWorldNormal : TEXCOORD1,
    in float3 iToEye       : TEXCOORD2,
    out float4 oColour      : COLOR0 )
{
    float3 lNormalisedToEye  = normalize( float3(iToEye) );
    float3 lNormalisedNormal = normalize( float3(iWorldNormal) );
    float3 lReflectedLight = reflect( float3(gLightDirection), lNormalisedNormal );
    float3 lSpecular = pow( saturate( dot( lReflectedLight, lNormalisedToEye ) ), float(gShinyParams.z) ) * float3(gLightColour.xyz) * float(gShinyParams.w);
    float4 lTextureColour = float4( tex2D( DiffuseSampler, float2(iUV) ) );
    float3 lBaseColour = float3(iColour.xyz) * lTextureColour.xyz;
    float  lDirectionalLight = saturate( dot( lNormalisedNormal, float3(gLightDirection) ) );
    float  lAmbientLight     = float( gShinyParams.y );
    float3 lShadedColour     = lBaseColour * ( ( float3( gLightColour.xyz ) * lDirectionalLight ) + lAmbientLight );
    oColour = float4( saturate( lShadedColour * lTextureColour.w ) + lSpecular, lTextureColour.w ) * float( iColour.w );
}
