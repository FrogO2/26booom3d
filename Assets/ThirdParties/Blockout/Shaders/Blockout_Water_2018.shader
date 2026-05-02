// URP-compatible rewrite (original: Amplify Shader Editor for Built-in RP)
Shader "Blockout/Blockout_Water"
{
Properties
{
_SpecColor("Specular Color", Color) = (1,1,1,1)
_Gloss("Gloss", Range(0,1)) = 0.5
_Spec("Spec", Range(0,1)) = 0.5
_Depth_Fade("Depth_Fade", Float) = 1
_Water_Colour_Power("Water_Colour_Power", Float) = 2
_Speed("Speed", Float) = 1
_Tex_World_Scale("Tex_World_Scale", Float) = 0.5
_Alpha("Alpha", Float) = 0.5
_Tex("Tex", 2D) = "white" {}
_Color_2("Color_2", Color) = (0.9333333,0.9333333,0.9333333,0.003921569)
_Depth_Color("Depth_Color", Color) = (0.627451,0.654902,0.6784314,0.003921569)
_Color_1("Color_1", Color) = (0.7215686,0.5411765,0.3764706,0.003921569)
_Fresnel_Colour_Bias("Fresnel_Colour_Bias", Float) = 0
_Fresnel_Colour_Scale("Fresnel_Colour_Scale", Float) = 1
[HideInInspector] __dirty("", Int) = 1
}

SubShader
{
Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

Pass
{
Name "ForwardLit"
Tags { "LightMode" = "UniversalForward" }
Cull Back
Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off

HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

CBUFFER_START(UnityPerMaterial)
float4 _Depth_Color;
float4 _Color_2;
float  _Fresnel_Colour_Bias;
float  _Fresnel_Colour_Scale;
float  _Water_Colour_Power;
float4 _Color_1;
float  _Depth_Fade;
float  _Speed;
float  _Tex_World_Scale;
float4 _Tex_ST;
float  _Spec;
float  _Gloss;
float  _Alpha;
CBUFFER_END

TEXTURE2D(_Tex); SAMPLER(sampler_Tex);

struct Attributes
{
float4 positionOS : POSITION;
float3 normalOS   : NORMAL;
};

struct Varyings
{
float4 positionCS : SV_POSITION;
float3 normalWS   : TEXCOORD0;
float3 positionWS : TEXCOORD1;
float3 viewDirWS  : TEXCOORD2;
float4 screenPos  : TEXCOORD3;
};

Varyings Vert(Attributes i)
{
Varyings o;
VertexPositionInputs pos = GetVertexPositionInputs(i.positionOS.xyz);
o.positionCS = pos.positionCS;
o.positionWS = pos.positionWS;
o.normalWS   = TransformObjectToWorldNormal(i.normalOS);
o.viewDirWS  = GetWorldSpaceViewDir(pos.positionWS);
o.screenPos  = ComputeScreenPos(o.positionCS);
return o;
}

half4 Frag(Varyings i) : SV_Target
{
half3 nWS     = normalize(i.normalWS);
half3 viewDir = normalize(i.viewDirWS);
float fresnel = _Fresnel_Colour_Bias + _Fresnel_Colour_Scale * pow(1.0 - saturate(dot(nWS, viewDir)), _Water_Colour_Power);

// Depth-based color
float2 screenUV   = i.screenPos.xy / i.screenPos.w;
float  sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
float  surfDepth  = i.screenPos.w;
float  depthFade  = abs(sceneDepth - surfDepth) / _Depth_Fade;
float  depthFade2 = abs(sceneDepth - surfDepth); // scale 1

half4 colorA = lerp(_Depth_Color, _Color_2, fresnel);
half4 colorB = lerp(_Color_1, _Color_2, fresnel);
half4 baseColor = lerp(colorA, colorB, saturate(depthFade));

// Panning texture noise for foam/waves
float  t    = _Speed * _Time.y;
float2 worldXZ = float2(i.positionWS.x, i.positionWS.z) * _Tex_World_Scale;
float2 panner36 = worldXZ + t * float2(0, 0.55);
float2 panner35 = worldXZ * 1.2 + t * float2(0.4, 0);
float2 panner34 = worldXZ * 1.4 + t * float2(0, -0.45);
float2 panner33 = worldXZ * 0.8 + t * float2(0.35, 0);

float foam1 = saturate(min(SAMPLE_TEXTURE2D(_Tex, sampler_Tex, panner36).r,
                          SAMPLE_TEXTURE2D(_Tex, sampler_Tex, panner35).r));
float foam2 = saturate(min(SAMPLE_TEXTURE2D(_Tex, sampler_Tex, panner34).r,
                          SAMPLE_TEXTURE2D(_Tex, sampler_Tex, panner33).r));
float foamBlend = clamp(foam1 + foam2, 0.498, 1.0);

float depthFade2N = abs(sceneDepth - surfDepth);
half3 finalColor = (baseColor + (1.0 - depthFade2N) * step(depthFade2N * foamBlend, 0.5)).rgb;

// Alpha based on edge depth fade
float depthFadeAlpha = abs(sceneDepth - surfDepth);
float alpha = clamp(pow(depthFadeAlpha * 3.0, 3.0), 0, 1) * _Alpha;

return half4(finalColor, alpha);
}
ENDHLSL
}
}
FallBack "Universal Render Pipeline/Lit"
}