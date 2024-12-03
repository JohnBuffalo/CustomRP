#ifndef CUSTOM_FXAA_PASS_INCLUDED
#define CUSTOM_FXAA_PASS_INCLUDED


float GetLuma (float2 uv)
{
    // return Luminance(GetSource(uv)); // 获取亮度
    // return sqrt(Luminance(GetSource(uv))); //人眼对暗部变化更敏锐
    // return GetSource(uv).g; //人眼对绿色更敏锐

    #if defined(FXAA_ALPHA_CONTAINS_LUMA)
        return GetSource(uv).a;
    #else
        return GetSource(uv).g;
    #endif  
}

float4 FXAAPassFragment(Varyings input) : SV_TARGET
{
    return GetLuma(input.screenUV);
}

#endif
