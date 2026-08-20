#ifndef IRRADIANCE_FXH
#define IRRADIANCE_FXH

#ifndef USE_SHARED_GLOBALS
#ifdef D_PLATFORM_X360
float4x4 IrradianceQuadricA : register(c17)
#else
float4x4 IrradianceQuadricA
#endif
<
 string scope = "global";
>;

#ifdef D_PLATFORM_X360
float4x4 IrradianceQuadricB : register(c21)
#else
float4x4 IrradianceQuadricB
#endif
<
 string scope = "global";
>;
#endif // USE_SHARED_GLOBALS

float3 ComputeIrradianceFast(float3 normal)
{
 float3   irradiance_1            = float3  (IrradianceQuadricA[0].xyz);
 float3x4 irradiance_x_y_z_xx     = float3x4(IrradianceQuadricA[1], IrradianceQuadricA[2], IrradianceQuadricA[3]);
 float3x4 irradiance_xy_yz_zx_yy  = float3x4(IrradianceQuadricB[0], IrradianceQuadricB[1], IrradianceQuadricB[2]);
 float4 normal_x_y_z_xx           = float4(normal, normal.x * normal.x);
 float4 normal_xy_yz_zx_yy        = normal.xyzy * normal.yzxy;
    float3 result = irradiance_1;
 result += mul(irradiance_x_y_z_xx, normal_x_y_z_xx);
 result += mul(irradiance_xy_yz_zx_yy, normal_xy_yz_zx_yy);
 return saturate(result);
}

#endif
