#ifndef VEHICLEDEFORMATION_FXH
#define VEHICLEDEFORMATION_FXH

float3
VerletOffset(
    int2    boneIndices,
    float2  boneWeights )
{
    return g_verletOffsets[ boneIndices.x ].xyz * boneWeights.x +
           g_verletOffsets[ boneIndices.y ].xyz * boneWeights.y;
}

#ifdef VEHICLE_USE_SCRATCHES
float4
VerletOffsetPlusScratches(
    int2    boneIndices,
    float2  boneWeights )
{
    return g_verletOffsets[ boneIndices.x ].xyzw * boneWeights.x +
           g_verletOffsets[ boneIndices.y ].xyzw * boneWeights.y;
}
#endif

#endif
