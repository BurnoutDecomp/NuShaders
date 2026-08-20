float4x4    worldViewProj       : worldViewProj;
float4      ViewPositionAndSkyScale;
float4      KeyLightDirAndXZLength;
float4      TopColourDrk;
float4      HorColourPow;
float4      SunColourPow;
float3      HorBleedSclPow;
float4      g_domeRanges;
float4      g_textureScaleAndOffsets;
float4      g_scatteringCoefficients;
static const float  k_zenithBrightnessScale = 0.6;
struct vertexInput
{
    float3 direction            : POSITION;
    float2 distanceAndLength    : TEXCOORD0;
};
struct vertexOutput 
{
    float4  hPosition           : POSITION;
    float4  domeNormal          : TEXCOORD0;
    float2  cloudUV             : TEXCOORD1;
    float3  miscValues          : TEXCOORD2;
    float3  gradient            : TEXCOORD3;
    float4  fogScattering       : TEXCOORD4;
};
float3
ComputeSkyColour( float3 lSunDir, float lfSunXZ, float3 lDir, float lfDirXZ )
{
    float lfInvDen = 1.0f / ((lfDirXZ*lfSunXZ) + 0.001);
    float lfCT = dot( lDir.xz, lSunDir.xz ) * lfInvDen;
    float lfTb = pow( ( 1.001 + lfCT )*0.5, HorBleedSclPow.y )*( 1.0 - lSunDir.y );
    float lfDp = dot( lSunDir, lDir );
    float lfCs = saturate( lfDp ); 
    float lfTh = pow( lDir.y, HorColourPow.w*( 1 + lfTb*HorBleedSclPow.x ) );
    float lfTs = pow( lfCs, SunColourPow.w );
    float lfTf = pow( lfCs, HorBleedSclPow.z );
    float3 HorColour = lerp( HorColourPow.rgb, SunColourPow.rgb, lfTf );
    return 
        lerp( 1, ( 1.0 + lfDp )*0.5, TopColourDrk.w )*
        lerp( 
            lerp( HorColour, TopColourDrk.rgb, lfTh ), 
            SunColourPow.rgb, 
            lfTs );
}
float4
FogScattering( float3 viewVector, float3 skyColour )
{   
    float  viewLength               = length( viewVector );
    float3 normalisedViewVector     = viewVector / viewLength;
    float   fogBase = saturate( viewLength * g_scatteringCoefficients.x - g_scatteringCoefficients.y );
    float   fogDensity = pow( fogBase, g_scatteringCoefficients.z ) * g_scatteringCoefficients.w;
    return float4( skyColour * fogDensity, ( 1.0 - fogDensity ) ); 
}
vertexOutput 
main( vertexInput IN ) 
{
    vertexOutput OUT;
    float3  skyDomePosition = (IN.direction * ViewPositionAndSkyScale.w) + ViewPositionAndSkyScale.xyz;
    float2  domeWorldPosition = IN.direction.xz * IN.distanceAndLength.x;
    OUT.hPosition = mul( float4( skyDomePosition, 1.0 ), worldViewProj );
    float   normalisedDistance = ( IN.distanceAndLength.x - g_domeRanges.x ) * g_domeRanges.y;
    normalisedDistance = 1.0 - pow( normalisedDistance, g_domeRanges.z );
    OUT.domeNormal = float4( IN.direction, normalisedDistance );
    OUT.gradient = ComputeSkyColour( KeyLightDirAndXZLength.xyz, KeyLightDirAndXZLength.w, IN.direction, IN.distanceAndLength.y );
    OUT.fogScattering = FogScattering( IN.direction * IN.distanceAndLength.x * 0.25, OUT.gradient );
    OUT.cloudUV = ( domeWorldPosition + g_textureScaleAndOffsets.xy ) * g_textureScaleAndOffsets.zw;
    OUT.miscValues.x = ( normalisedDistance * 0.75 + 1.0 );
    OUT.miscValues.y = ( OUT.domeNormal.y * OUT.domeNormal.y ) * k_zenithBrightnessScale;
    OUT.miscValues.z = pow( saturate( dot( OUT.domeNormal.xyz, KeyLightDirAndXZLength.xyz ) ), 64.0 ) * 3.0;
    return OUT;
}
technique Default
{
    pass p0
    {        
        VertexShader = compile vs_3_0 main();        
    }
}
