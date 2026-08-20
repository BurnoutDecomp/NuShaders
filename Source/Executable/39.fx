float4x4 viewProjectionMatrix;
float4   cameraPositionPlusBrightness;
float4   viewXyScale;
struct Input
{
    float3 position  : POSITION;
    float4 uvAndSize : TEXCOORD0;
    float4 colour    : COLOR0;
};
struct Output
{
    float4 position : POSITION;
    float2 uv       : TEXCOORD0;
    float4 colour   : COLOR0;
};
Output
main(Input input)
{
    Output result;
    float4 centre = mul( float4( input.position.xyz, 1.0f ) , viewProjectionMatrix );
    float2 sinCos = normalize( centre.xy - ( float2(0.0f, -1.5f) * centre.w ) );
    float2 size;
    size.x = (input.uvAndSize.w * sinCos.y) + (input.uvAndSize.z * sinCos.x);
    size.y = (input.uvAndSize.z * sinCos.y) - (input.uvAndSize.w * sinCos.x);
    size.xy *= viewXyScale.xy;
    result.position = centre;
    result.position.xy += size;
    result.uv = input.uvAndSize.xy;
    result.colour.rgb = input.colour.rgb * cameraPositionPlusBrightness.w;
    result.colour.a   = input.colour.a;
    return result;
}
