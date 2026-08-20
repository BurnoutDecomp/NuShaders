float4x4 worldViewProj   : worldViewProj;
void
main(
    in  float3 iPos    : POSITION,
    out float4 oPos    : POSITION
)
{
    oPos    = mul( float4( iPos, 1.0f ), worldViewProj );
}
