sampler2D   g_densitySampler;
sampler2D   g_lightSampler;
float4      g_liteColour;
float4      g_darkColour;
float4      g_layerCloudiness;
float4      g_layerInvFeather;
float4      g_layerAlphas;
struct vertexOutput 
{
    float4  hPosition       : POSITION;
    float4  domeNormal      : TEXCOORD0;
    float2  cloudUV         : TEXCOORD1;
    float3  miscValues      : TEXCOORD2;
    float3  gradient        : TEXCOORD3;
    float4  fogScattering   : TEXCOORD4;
};
float
LinearStep1Fast( float start, float invRange, float value )
{
    return saturate( ( value - start ) * invRange );
}
float4 main( vertexOutput IN,
    in float2 iScreenPos : VPOS
    ) : COLOR
{
    float    lCloudDensityTex = tex2D( g_densitySampler, IN.cloudUV ).g;
    float4   lLightTex        = tex2D( g_lightSampler, IN.cloudUV );
    float3   lDomeNormal      = normalize( float3( IN.domeNormal.xyz ) );
    float    lCloudFeathering = (float)g_layerInvFeather.x * (float)IN.domeNormal.w;
    float    lAlpha = LinearStep1Fast( (float)g_layerCloudiness.x, lCloudFeathering, lCloudDensityTex ) * (float)g_layerAlphas.x;
    lAlpha *= (float)IN.domeNormal.w;
    lLightTex = (( lLightTex - 0.5 ) * (float)IN.miscValues.x) + 0.5;
    float4   lQuadrant = saturate( float4( lDomeNormal.x, -lDomeNormal.z, -lDomeNormal.x, lDomeNormal.z ) );
    lQuadrant = lQuadrant * lQuadrant;
    float    lBrightness = dot( lQuadrant, lLightTex ) + (float)IN.miscValues.y;
    float    lNegativeAlpha = 1.0 - lAlpha;
    float    lCloudOverbrighten = (float)IN.miscValues.z * lNegativeAlpha * lNegativeAlpha;
    float3   lLighting = lerp( (float3)g_darkColour.rgb, (float3)g_liteColour.rgb, ( lBrightness + lCloudOverbrighten ) );
    float3   finalColour = lerp( float3(IN.gradient), lLighting, lAlpha );
    finalColour = (finalColour * float(IN.fogScattering.a)) + float3(IN.fogScattering.rgb);
    {
        float lfNoise = frac( ( iScreenPos.x * 0.6180339887 ) + ( iScreenPos.y * iScreenPos.y * 0.3819660112 ) );
        finalColour += ( lfNoise - 0.5f ) * ( 1.0f / 255.0f );
    }
    return float4(finalColour, 1.0f);
}
technique Default
{
    pass p0
    {    
        PixelShader  = compile ps_3_0 main();
    }
}
