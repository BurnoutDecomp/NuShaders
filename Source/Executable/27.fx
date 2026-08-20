sampler  DepthSampler   : register(s4);
float4x4 g_invVP;
float4x4 g_worldViewProj;
float4x4 g_ViewProjNoTranslation;
float4x4 g_invVPNoTranslation;
struct VS_OUT
{
    float4 oPos : POSITION;
    float2 UV   : TEXCOORD0;
    float4 hPos : TEXCOORD1;
};
float4
main( VS_OUT IN ) : COLOR
{
    float g_AORadiusSample = 0.4;
    float3 g_AOHemiSphereSample[] =
    { 
        float3(-0.464416, 0.050686, -0.150575     ),
        float3(-0.463939, 0.127938, -0.094447     ),
        float3(-0.463756, 0.157446, -0.003629     ),
        float3(-0.463939, 0.127938, 0.087189      ),
        float3(-0.464416, 0.050686, 0.143318      ),
        float3(-0.393131, 0.093318, -0.283137     ),
        float3(-0.392223, 0.240261, -0.176375     ),
        float3(-0.391876, 0.296389, -0.003629     ),
        float3(-0.392223, 0.240261, 0.169117      ),
        float3(-0.393131, 0.093318, 0.275880      ),
        float3(-0.282306, 0.126815, -0.388339     ),
        float3(-0.281056, 0.329065, -0.241393     ),
        float3(-0.280578, 0.406318, -0.003629     ),
        float3(-0.281056, 0.329065, 0.234135      ),
        float3(-0.282306, 0.126815, 0.381082      ),
        float3(-0.142789, 0.147899, -0.455883     ),
        float3(-0.141319, 0.385659, -0.283137     ),
        float3(-0.140757, 0.476475, -0.003629     ),
        float3(-0.141319, 0.385659, 0.275880      ),
        float3(-0.142789, 0.147899, 0.448625      ),
        float3( 0.011763, 0.154506, -0.479157     ),
        float3( 0.013309, 0.404501, -0.297521     ),
        float3( 0.013900, 0.499990, -0.003629     ),
        float3( 0.013309, 0.404501, 0.290264      ),
        float3( 0.011763, 0.154506, 0.471899      ),
        float3( 0.166222, 0.145988, -0.455883     ),
        float3( 0.167692, 0.383748, -0.283137     ),
        float3( 0.168254, 0.474564, -0.003629     ),
        float3( 0.167692, 0.383748, 0.275880      ),
        float3( 0.166222, 0.145988, 0.448625      ),
        float3( 0.305468, 0.123180, -0.388339     ),
        float3( 0.306718, 0.325431, -0.241393     ),
        float3( 0.307196, 0.402683, -0.003629     ),
        float3( 0.306718, 0.325431, 0.234135      ),
        float3( 0.305468, 0.123180, 0.381082      ),
        float3( 0.415870, 0.088315, -0.283137     ),
        float3( 0.416779, 0.235258, -0.176375     ),
        float3( 0.417126, 0.291385, -0.003629     ),
        float3( 0.416779, 0.235258, 0.169117      ),
        float3( 0.415870, 0.088315, 0.275880      ),
        float3( 0.486622, 0.044804, -0.150575     ),
        float3( 0.487100, 0.122057, -0.094447     ),
        float3( 0.487282, 0.151565, -0.003629     ),
        float3( 0.487100, 0.122057, 0.087189      ),
        float3( 0.486622, 0.044804, 0.143318      )
    };              
    float  intPart;
    float4 posScreenSample = IN.hPos;
#ifdef D_RAWZ
    float3 lDepthBuffer = tex2D( DepthSampler,IN.UV.xy ).arg;
    float3 lDepthFactor = float3(  65536.0 / 65793.0, 256.0 / 65793.0, 1.0 / 65793.0 );
    posScreenSample.z = dot( lDepthBuffer, lDepthFactor );
#elif defined(D_INTZ)
    posScreenSample.z = tex2D( DepthSampler,IN.UV.xy ).r;
#elif defined(D_MRT) || defined(D_MSAA_ENABLED)
    posScreenSample.z = frac( tex2D( DepthSampler,IN.UV.xy ).r );
#else
    posScreenSample.z = tex2D( DepthSampler,IN.UV.xy ).r;
#endif
    posScreenSample.w = 1.0;
    if ( posScreenSample.z > 0.999 )
    {
        return float4( 1.0, 1.0, 1.0, 1.0);
    }
    float4 worldSpaceWithW = mul( g_invVPNoTranslation, posScreenSample );
    worldSpaceWithW.xyz /= worldSpaceWithW.w;
    float4 worldSpaceWithWNoTranslation = mul( g_invVPNoTranslation, posScreenSample );
    worldSpaceWithWNoTranslation.xyz /= worldSpaceWithWNoTranslation.w;
    float3 cross1 = ddy( worldSpaceWithWNoTranslation.xyzw );
    float3 cross2 = ddx( worldSpaceWithWNoTranslation.xyzw );
    float3 normal = normalize( cross( cross1.xyz, cross2.xyz ) );
    worldSpaceWithW.xyz += normal * ( pow( ( 1 - ( ( 1 - max( posScreenSample.z, 0.95 ) ) / 0.05 ) ) , 10) + 0.035);
    float occludedPerc = 0.0;
    for ( int t = 0; t < 22; t++ )
    {
        float3 pointOnHemiSphere  = g_AOHemiSphereSample[t*2];
        float3 pointOnHemiSphere2 = g_AOHemiSphereSample[t*2+1];
        if ( dot( pointOnHemiSphere , normal ) < 0 )
        {
            pointOnHemiSphere = -pointOnHemiSphere;
        }
        if ( dot( pointOnHemiSphere2 , normal ) < 0 )
        {
            pointOnHemiSphere2 = -pointOnHemiSphere2;
        }
        float4 currPos  = float4( worldSpaceWithW.xyz + ( pointOnHemiSphere * g_AORadiusSample ), 1.0f );
        float4 currPos2 = float4( worldSpaceWithW.xyz + ( pointOnHemiSphere2 * g_AORadiusSample ), 1.0f );
        float4 posSample  = mul( currPos, g_ViewProjNoTranslation );
        float4 posSample2 = mul( currPos2, g_ViewProjNoTranslation );
        posScreenSample.xyz = posSample.xyz / posSample.w;
        posScreenSample.w = 1.0;
        float4 posScreenSample2;
        posScreenSample2.xyz = posSample2.xyz / posSample2.w;
        posScreenSample2.w = 1.0;
        float4 posScreenSampleCopy  = posScreenSample;
        float4 posScreenSampleCopy2 = posScreenSample2;
        posScreenSample.y  = -posScreenSample.y;
        posScreenSample.xy *= 0.5;
        posScreenSample.xy += 0.5;
        posScreenSample2.y  = -posScreenSample2.y;
        posScreenSample2.xy *= 0.5;
        posScreenSample2.xy += 0.5;
#ifdef D_RAWZ
    float  lNewDepthBuffer  = tex2D( DepthSampler, posScreenSample.xy  ).arg;
    float  lNewDepthBuffer2 = tex2D( DepthSampler, posScreenSample2.xy ).arg;
    float posScreenTextureSampleZ  = dot( lNewDepthBuffer , lDepthFactor );
    float posScreenTextureSampleZ2 = dot( lNewDepthBuffer2, lDepthFactor );
#elif defined(D_INTZ)
    float posScreenTextureSampleZ  = tex2D( DepthSampler, posScreenSample.xy ).r;
    float posScreenTextureSampleZ2 = tex2D( DepthSampler, posScreenSample2.xy ).r;
#elif defined(D_MRT) || defined(D_MSAA_ENABLED)
    float posScreenTextureSampleZ  = tex2D( DepthSampler, posScreenSample.xy  ).r;
    float posScreenTextureSampleZ2 = tex2D( DepthSampler, posScreenSample2.xy ).r;
#else
    float posScreenTextureSampleZ  = tex2D( DepthSampler, posScreenSample.xy ).r;
    float posScreenTextureSampleZ2 = tex2D( DepthSampler, posScreenSample2.xy ).r;
#endif
        float posScreenSampleZ  = posScreenSample.z;
        float posScreenSampleZ2 = posScreenSample2.z;
        posScreenSampleCopy.z  = posScreenTextureSampleZ;
        posScreenSampleCopy2.z = posScreenTextureSampleZ2;
        float4 worldSpaceSample  = mul( g_invVPNoTranslation, posScreenSampleCopy );
        float4 worldSpaceSample2 = mul( g_invVPNoTranslation, posScreenSampleCopy2 );
        worldSpaceSample.xyz  /= worldSpaceSample.w;
        worldSpaceSample2.xyz /= worldSpaceSample2.w;
        float ZDistance  = length( worldSpaceSample.xyz  - worldSpaceWithW.xyz ); 
        float ZDistance2 = length( worldSpaceSample2.xyz - worldSpaceWithW.xyz );
        float Zratio  = 1.0 - ( min( ZDistance,  ( g_AORadiusSample * 2 ) ) / ( g_AORadiusSample * 2 ) );
        float Zratio2 = 1.0 - ( min( ZDistance2, ( g_AORadiusSample * 2 ) ) / ( g_AORadiusSample * 2 ) );
        Zratio  = ( posScreenSample.z > posScreenTextureSampleZ ) ? Zratio : 0.0;
        Zratio2 = ( posScreenSample2.z > posScreenTextureSampleZ2 ) ? Zratio2 : 0.0;
        occludedPerc += Zratio  * 1.2;
        occludedPerc += Zratio2 * 1.2;
    }
    occludedPerc = pow( 1- ( occludedPerc / 22.0 ), 2.5 );
    return float4( occludedPerc.xxx, 1 );
}
