struct VS_IN
{
 float4 ObjPos : POSITION;
 float2 UV : TEXCOORD0;
};
struct VS_OUT
{
 float4 oPos : POSITION;
 float2 UV   : TEXCOORD0;
 float4 hPos : TEXCOORD1;
};
VS_OUT main( VS_IN In )
{
 VS_OUT Out;
 Out.oPos = float4(In.ObjPos.xyz , 1);
 Out.hPos = Out.oPos;
 Out.UV = In.UV;
 return Out;
}
