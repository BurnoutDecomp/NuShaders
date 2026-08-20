#ifndef DEPTHENCODE_FXH
#define DEPTHENCODE_FXH

#ifdef D_MRT
float4 ConvertDepthToARGB( float lfDepthValue )
{
    return float4( lfDepthValue, lfDepthValue, lfDepthValue, lfDepthValue );
}
#endif

#endif
