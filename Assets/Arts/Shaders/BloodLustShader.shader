Shader "MyCustom/BloodLust"
{
    Properties
    {
        // --- 击杀特效属性 ---
        _Intensity ("Effect Intensity", Range(0, 1)) = 0
        _VignetteColor ("Vignette Color", Color) = (0.8, 0.0, 0.0, 1.0)
        _VignetteInner ("Kill Vignette Inner", Range(-0.5, 1)) = 0.2
        _VignetteOuter ("Kill Vignette Outer", Range(0, 1.5)) = 0.8

        // --- 冲刺速度线属性 ---
        _SpeedLineTex ("Speed Line Texture", 2D) = "black" {}
        _DashIntensity ("Dash Intensity", Range(0, 1)) = 0
        _DashColor ("Dash Color", Color) = (1.0, 1.0, 1.0, 1.0)
        _DashRotation ("Dash Rotation", Float) = 0
        
        // 新增：控制冲刺速度线的中心渐变范围
        _DashInner ("Dash Mask Inner ", Range(0, 1)) = 0.1
        _DashOuter ("Dash Mask Outer ", Range(0, 1.5)) = 0.6
    }

    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    // 击杀变量
    float _Intensity;
    float4 _VignetteColor;
    float _VignetteInner;
    float _VignetteOuter;

    // 冲刺变量
    TEXTURE2D(_SpeedLineTex);
    SAMPLER(sampler_SpeedLineTex);
    float _DashIntensity;
    float4 _DashColor;
    float _DashRotation;
    // 新增变量声明
    float _DashInner;
    float _DashOuter;

    float2 RotateUV(float2 uv, float angle)
    {
        float s, c;
        sincos(angle, s, c);
        float2x2 rot = float2x2(c, -s, s, c);
        return mul(rot, uv - 0.5) + 0.5;
    }

    float4 Fragment(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);

        // 我们提前算出当前像素到屏幕中心的距离，击杀和冲刺都能复用这个值！
        float dist = distance(uv, float2(0.5, 0.5));

        // ==========================================
        // 1. 击杀褪色与晕影逻辑 (BloodLust)
        // ==========================================
        float luminance = dot(color.rgb, float3(0.299, 0.587, 0.114));
        float redFactor = saturate((color.r - max(color.g, color.b)) * 2.0);
        float3 bwColor = lerp(float3(luminance, luminance, luminance), color.rgb, redFactor);
        
        float3 finalColor = lerp(color.rgb, bwColor, _Intensity);

        float killVignette = smoothstep(_VignetteInner, _VignetteOuter, dist) * _Intensity; 
        finalColor = lerp(finalColor, _VignetteColor.rgb, killVignette * _VignetteColor.a);

       // ==========================================
        // 2. 冲刺速度线逻辑 (持续流逝版)
        // ==========================================
        
        float2 speedUV = RotateUV(uv, _DashRotation);
        
        // 速度线的流动速度 (如果觉得太快或太慢，修改这个值)
        float flowSpeed = 1.5; 
        float time = _Time.y * flowSpeed;

        // 计算两个互相错开的循环周期 (0 到 1)
        float t1 = frac(time);
        float t2 = frac(time + 0.5); 

        // 计算两层的缩放：从 1.0 缩小到 0.2（UV缩小等于画面放大，产生冲刺感）
        float scale1 = lerp(1.0, 0.2, t1);
        float scale2 = lerp(1.0, 0.2, t2);

        // 计算两层的透明度：使用三角波平滑淡入淡出，完美隐藏跳变的瞬间
        // 公式运算结果为：0 -> 1 -> 0
        float alpha1 = 1.0 - abs(t1 * 2.0 - 1.0);
        float alpha2 = 1.0 - abs(t2 * 2.0 - 1.0);

        // 分别计算两层的 UV
        float2 uv1 = (speedUV - 0.5) * scale1 + 0.5;
        float2 uv2 = (speedUV - 0.5) * scale2 + 0.5;

        // 采样两层图片，并乘上各自的透明度
        float3 sample1 = SAMPLE_TEXTURE2D(_SpeedLineTex, sampler_SpeedLineTex, uv1).rgb * alpha1;
        float3 sample2 = SAMPLE_TEXTURE2D(_SpeedLineTex, sampler_SpeedLineTex, uv2).rgb * alpha2;
        
        // 将两层无缝叠加
        float3 seamlessSpeedLine = sample1 + sample2;

        // 保持之前的遮罩逻辑不变：中心淡出 + 边缘淡出
        float dashMask = smoothstep(_DashInner, _DashOuter, dist);
        float outerFadeMask = 1.0 - smoothstep(0.45, 0.7, dist); 

        // 最终混合到画面上
        finalColor += seamlessSpeedLine * _DashIntensity * _DashColor.rgb * dashMask * outerFadeMask;

        return float4(finalColor, color.a);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZWrite Off Cull Off
        
        Pass
        {
            Name "BloodLustPass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fragment
            ENDHLSL
        }
    }
}