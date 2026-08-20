#ifndef PARALLAX_FXH
#define PARALLAX_FXH

// ===========================================================================
//  Parallax occlusion mapping (POM) with soft self-shadowing. Cross-platform
//  (SM3 X360/TUB + SM5 BPR via Platform.fxh). No precomputed tangent: the
//  tangent frame is rebuilt per-pixel from screen-space derivatives, so this
//  works on the stock vertex format (position/normal/uv) with no extra
//  interpolators. Height map convention: .r = height, 1.0 = top, 0.0 = deepest.
//  Ray-march samples use explicit LOD (valid inside flow control on SM3 + SM5).
// ===========================================================================

#ifndef D_POM_HEIGHT_CHANNEL
  #define D_POM_HEIGHT_CHANNEL r   // height map channel; override to 'a' to pack height in an existing texture's alpha
#endif

#ifndef D_POM_STEPS
  #ifdef D_PLATFORM_BPR
    #define D_POM_STEPS 32
  #else
    #define D_POM_STEPS 16
  #endif
#endif
#ifndef D_POM_SHADOW_STEPS
  #ifdef D_PLATFORM_BPR
    #define D_POM_SHADOW_STEPS 16
  #else
    #define D_POM_SHADOW_STEPS 8
  #endif
#endif

// Per-pixel cotangent frame (rows T, B, N). Multiply a world-space vector by it
// (mul(frame, v)) to transform into tangent space. After Christian Schuler,
// "Normal Mapping Without Precomputed Tangents".
float3x3 CotangentFrame( float3 N, float3 worldPos, float2 uv )
{
    float3 dp1  = ddx( worldPos );
    float3 dp2  = ddy( worldPos );
    float2 duv1 = ddx( uv );
    float2 duv2 = ddy( uv );

    float3 dp2perp = cross( dp2, N );
    float3 dp1perp = cross( N, dp1 );
    float3 T = dp2perp * duv1.x + dp1perp * duv2.x;
    float3 B = dp2perp * duv1.y + dp1perp * duv2.y;

    float invmax = rsqrt( max( dot( T, T ), dot( B, B ) ) );
    return float3x3( T * invmax, B * invmax, N );
}

// Ray-march the height field along the tangent-space view direction; return the
// parallax-offset UV and (out) the hit penetration depth in [0,1] (0 = surface top,
// 1 = deepest), for per-pixel depth correction. tsView = direction toward the eye.
float2 ParallaxOcclusion( TEX2D_PARAM(heightTex), float2 uv, float3 tsView, float scale, out float hitDepth )
{
    float3 v = normalize( tsView );
    float  layerStep = 1.0 / (float)D_POM_STEPS;
    // Total per-step UV shift; clamp z to tame the offset at grazing angles.
    float2 deltaUV = ( v.xy / max( abs( v.z ), 0.2 ) ) * scale * layerStep;

    float  curLayer = 0.0;
    float2 curUV    = uv;
    float  curDepth = 1.0 - SAMPLE2D_LOD( heightTex, curUV, 0.0 ).D_POM_HEIGHT_CHANNEL;

    for ( int i = 0; i < D_POM_STEPS; ++i )
    {
        if ( curLayer >= curDepth ) break;
        curUV    -= deltaUV;
        curDepth  = 1.0 - SAMPLE2D_LOD( heightTex, curUV, 0.0 ).D_POM_HEIGHT_CHANNEL;
        curLayer += layerStep;
    }

    // Occlusion interpolation between the last two march samples.
    float2 prevUV  = curUV + deltaUV;
    float  afterD  = curDepth - curLayer;
    float  beforeD = ( 1.0 - SAMPLE2D_LOD( heightTex, prevUV, 0.0 ).D_POM_HEIGHT_CHANNEL ) - ( curLayer - layerStep );
    float  w = saturate( afterD / ( afterD - beforeD ) );
    hitDepth = curLayer - w * layerStep;
    return lerp( curUV, prevUV, w );
}

// UV-only convenience wrapper (discards the penetration depth).
float2 ParallaxOcclusionUV( TEX2D_PARAM(heightTex), float2 uv, float3 tsView, float scale )
{
    float hd;
    return ParallaxOcclusion( TEX2D_ARG(heightTex), uv, tsView, scale, hd );
}

// Soft self-shadow at a resolved surface point: march toward the light; relief
// that rises above the light ray darkens it. tsLight = dir toward the light,
// tangent space. Returns [0,1] (1 = fully lit).
float ParallaxSelfShadowFactor( TEX2D_PARAM(heightTex), float2 uv, float3 tsLight, float scale )
{
    float3 l = normalize( tsLight );
    if ( l.z <= 0.0 ) return 1.0;

    float  surfDepth = 1.0 - SAMPLE2D_LOD( heightTex, uv, 0.0 ).D_POM_HEIGHT_CHANNEL;
    float  invSteps  = 1.0 / (float)D_POM_SHADOW_STEPS;
    float2 deltaUV   = ( l.xy / l.z ) * scale * invSteps;
    float  depthStep = surfDepth * invSteps;   // climb toward the top (depth 0)

    float  occ      = 0.0;
    float2 curUV    = uv;
    float  curDepth = surfDepth;

    for ( int i = 1; i <= D_POM_SHADOW_STEPS; ++i )
    {
        curUV    += deltaUV;
        curDepth -= depthStep;
        float sampleDepth = 1.0 - SAMPLE2D_LOD( heightTex, curUV, 0.0 ).D_POM_HEIGHT_CHANNEL;
        if ( sampleDepth < curDepth )
            occ = max( occ, ( curDepth - sampleDepth ) * ( 1.0 - (float)i * invSteps ) );
    }
    return 1.0 - saturate( occ * 2.0 );
}

#ifndef D_POM_NORMAL_EPSILON
  #define D_POM_NORMAL_EPSILON 0.002   // UV step for the height-gradient central difference
#endif

// Tangent-space normal derived from the height field (central-difference gradient),
// so the relief catches light directionally. bumpScale tunes the slope strength.
// Transform to world space with mul( result, CotangentFrame(...) ).
float3 ParallaxNormal( TEX2D_PARAM(heightTex), float2 uv, float bumpScale )
{
    float e  = D_POM_NORMAL_EPSILON;
    float hL = SAMPLE2D_LOD( heightTex, float2( uv.x - e, uv.y ), 0.0 ).D_POM_HEIGHT_CHANNEL;
    float hR = SAMPLE2D_LOD( heightTex, float2( uv.x + e, uv.y ), 0.0 ).D_POM_HEIGHT_CHANNEL;
    float hD = SAMPLE2D_LOD( heightTex, float2( uv.x, uv.y - e ), 0.0 ).D_POM_HEIGHT_CHANNEL;
    float hU = SAMPLE2D_LOD( heightTex, float2( uv.x, uv.y + e ), 0.0 ).D_POM_HEIGHT_CHANNEL;
    return normalize( float3( ( hL - hR ) * bumpScale, ( hD - hU ) * bumpScale, 1.0 ) );
}

#endif // PARALLAX_FXH
