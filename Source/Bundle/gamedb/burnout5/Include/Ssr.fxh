#ifndef SSR_FXH
#define SSR_FXH

// ===========================================================================
//  Screen-space reflection (BPR only). Marches the reflection ray in WORLD
//  space against the scene depth, compares in raw depth-buffer space (no
//  linearisation needed), binary-refines the hit, and samples the scene
//  colour there. Returns rgb = reflected colour, a = hit confidence [0,1]
//  (0 on miss / off-screen) so the caller can fall back to the cubemap.
//
//  Depth + colour are NOT exposed to shaders by the stock engine (the depth
//  is the active DSV during our draw); a companion D3D11-hook DLL binds the
//  scene-depth SRV at t8 (g_depthSampler) and scene colour at t7
//  (samplersource) for this draw. Without the mod, g_depthSampler reads 0 →
//  no hit → the caller uses the cubemap, so this is safe to ship un-modded.
//
//  Uses TransformWorldToProjection (Transform.fxh) so projection matches the VS.
// ===========================================================================

#ifndef D_SSR_STEPS
  #define D_SSR_STEPS 32          // linear march steps
#endif
#ifndef D_SSR_REFINE
  #define D_SSR_REFINE 5          // binary-search refinement steps
#endif
#ifndef D_SSR_MAX_DISTANCE
  #define D_SSR_MAX_DISTANCE 80.0 // world-units, max ray length
#endif
#ifndef D_SSR_THICKNESS
  #define D_SSR_THICKNESS 0.0012  // raw depth-buffer compare bias (tune in-game)
#endif

// World position -> screen UV [0,1] (Y-flipped for texture space) + raw depth-buffer value.
void SsrProject( float3 worldPos, out float2 uv, out float depth )
{
    float4 clip = TransformWorldToProjection( worldPos );
    float2 ndc  = clip.xy / clip.w;
    uv    = ndc * float2( 0.5, -0.5 ) + 0.5;
    depth = clip.z / clip.w;
}

// Screen-space reflection ray-march. ssrDepth = scene depth (g_depthSampler, raw),
// ssrColour = scene colour (samplersource). Returns rgb = reflected colour, a = confidence.
float4 ScreenSpaceReflection( TEX2D_PARAM(ssrDepth), TEX2D_PARAM(ssrColour), float3 worldPos, float3 reflDir )
{
    float  stepLen = D_SSR_MAX_DISTANCE / (float)D_SSR_STEPS;
    float3 prevPos = worldPos;

    [loop] for ( int i = 1; i <= D_SSR_STEPS; ++i )
    {
        float3 p = worldPos + reflDir * ( stepLen * (float)i );
        float2 uv; float rayD;
        SsrProject( p, uv, rayD );
        if ( uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0 )
            return float4( 0.0, 0.0, 0.0, 0.0 );                      // left the screen -> miss

        float sceneD = SAMPLE2D_LOD( ssrDepth, uv, 0.0 ).r;
        // sceneD == 0 => no/unbound depth (un-modded reads 0) -> never a hit -> caller uses the cubemap.
        if ( sceneD > 0.0 && rayD > sceneD + D_SSR_THICKNESS )        // valid depth + ray behind geometry -> hit
        {
            float3 lo = prevPos, hi = p;
            [unroll] for ( int k = 0; k < D_SSR_REFINE; ++k )
            {
                float3 mid = ( lo + hi ) * 0.5;
                float2 muv; float mrayD;
                SsrProject( mid, muv, mrayD );
                float msceneD = SAMPLE2D_LOD( ssrDepth, muv, 0.0 ).r;
                if ( mrayD > msceneD + D_SSR_THICKNESS ) hi = mid; else lo = mid;
            }
            float2 hitUV; float hd;
            SsrProject( ( lo + hi ) * 0.5, hitUV, hd );
            float2 e    = abs( hitUV * 2.0 - 1.0 );                   // fade out near screen edges
            float  fade = saturate( 1.0 - pow( max( e.x, e.y ), 8.0 ) );
            float3 col  = SAMPLE2D_LOD( ssrColour, hitUV, 0.0 ).rgb;
            return float4( col, fade );
        }
        prevPos = p;
    }
    return float4( 0.0, 0.0, 0.0, 0.0 );                              // no hit -> miss
}

#endif // SSR_FXH
