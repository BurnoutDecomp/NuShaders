float4 gParams;
void
main(
    in  float iAlpha  : TEXCOORD0,
    out float4 oColour : COLOR0
    )
{
    oColour = float4( gParams.x, gParams.x, gParams.x, iAlpha );
}
