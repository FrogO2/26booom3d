// URP-compatible rewrite (original: Amplify Shader Editor for Built-in RP)
// NOTE: GrabPass (screen refraction) is not supported in URP; replaced with emissive spinner effect.
Shader "Blockout/Gate_Particle"
{
Properties
{
[Header(Refraction)]
_ChromaticAberration("Chromatic Aberration", Range(0,0.3)) = 0.1
_SpecColor("Specular Color", Color) = (1,1,1,1)
_Color_2("Color_2", Color) = (0.2980392,0.2980392,0.2980392,1)
_Color_1("Color_1", Color) = (0.909804,0.909804,0.909804,1)
_Spinner_2("Spinner_2", Float) = 0.8
_Texture0("Texture 0", 2D) = "white" {}
_Spinner_1("Spinner_1", Float) = 1
_Spinner_4("Spinner_4", Float) = 0.6
_Spinner_3("Spinner_3", Float) = 1
_Depth_Blend("Depth_Blend", Float) = 1
_Alpha("Alpha", Float) = 0.6
_Wibblyocity("Wibblyocity", Float) = 1
_Pan_Up("Pan_Up", Float) = 0.5
[HideInInspector] _texcoord("", 2D) = "white" {}
[HideInInspector] __dirty("", Int) = 1
}

SubShader
{
Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IsEmissive" = "true" }

Pass
{
Name "ForwardLit"
Tags { "LightMode" = "UniversalForward" }
Cull Off
Blend SrcAlpha OneMinusSrcAlpha
ZWrite Off

HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

CBUFFER_START(UnityPerMaterial)
float  _Wibblyocity;
float4 _Texture0_ST;
float  _Spinner_1;
float  _Spinner_2;
float  _Spinner_3;
float  _Spinner_4;
float4 _Color_1;
float4 _Color_2;
float  _Pan_Up;
float  _Alpha;
float  _Depth_Blend;
float  _ChromaticAberration;
CBUFFER_END

TEXTURE2D(_Texture0); SAMPLER(sampler_Texture0);

// Rotate UV around center
float2 RotateUV(float2 uv, float angle)
{
float c = cos(angle), s = sin(angle);
return mul(uv - 0.5, float2x2(c, -s, s, c)) + 0.5;
}

struct Attributes
{
float4 positionOS : POSITION;
float3 normalOS   : NORMAL;
float2 uv         : TEXCOORD0;
};

struct Varyings
{
float4 positionCS : SV_POSITION;
float2 uv         : TEXCOORD0;
float4 screenPos  : TEXCOORD1;
float3 positionWS : TEXCOORD2;
};

Varyings Vert(Attributes i)
{
// Vertex displacement (wibbly)
float2 rot34 = RotateUV(i.uv, _Spinner_1 * _Time.y);
float2 rot35 = RotateUV(i.uv, _Spinner_2 * _Time.y);
float2 rot36 = RotateUV(i.uv, -_Spinner_3 * _Time.y);
float2 rot37 = RotateUV(i.uv, -_Spinner_4 * _Time.y);
float  bA = SAMPLE_TEXTURE2D_LOD(_Texture0, sampler_Texture0, rot34, 0).r;
float  bB = SAMPLE_TEXTURE2D_LOD(_Texture0, sampler_Texture0, rot35, 0).g;
float  bC = SAMPLE_TEXTURE2D_LOD(_Texture0, sampler_Texture0, rot36, 0).r;
float  bD = SAMPLE_TEXTURE2D_LOD(_Texture0, sampler_Texture0, rot37, 0).g;
float  blendAB = saturate((bB > 0.5) ? (1.0 - (1.0 - 2.0*(bB-0.5))*(1.0-bA)) : (2.0*bB*bA));
float  blendCD = saturate(abs(bC - bD));
float  wibble  = saturate(0.5 - 2.0*(blendAB-0.5)*(blendCD-0.5));
float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
float4 posOS = i.positionOS + float4(_Wibblyocity * wibble * i.normalOS, 0);

Varyings o;
VertexPositionInputs pos = GetVertexPositionInputs(posOS.xyz);
o.positionCS = pos.positionCS;
o.positionWS = pos.positionWS;
o.uv         = i.uv;
o.screenPos  = ComputeScreenPos(o.positionCS);
return o;
}

half4 Frag(Varyings i) : SV_Target
{
// Radial mask
float2 centeredUV = i.uv * 2.0 - 1.0;
float  radMask    = clamp((1.0 - length(centeredUV)) * 2.33, 0, 1);

// Spinner pattern
float2 rot34 = RotateUV(i.uv, _Spinner_1 * _Time.y);
float2 rot35 = RotateUV(i.uv, _Spinner_2 * _Time.y);
float2 rot36 = RotateUV(i.uv, -_Spinner_3 * _Time.y);
float2 rot37 = RotateUV(i.uv, -_Spinner_4 * _Time.y);
float  bA = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, rot34).r;
float  bB = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, rot35).g;
float  bC = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, rot36).r;
float  bD = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, rot37).g;
float  blendAB = saturate((bB > 0.5) ? (1.0-(1.0-2.0*(bB-0.5))*(1.0-bA)) : (2.0*bB*bA));
float  blendCD = saturate(abs(bC - bD));
float  pattern = saturate(0.5 - 2.0*(blendAB-0.5)*(blendCD-0.5));

// Mask by radial and pan-up fade
float  maskA    = saturate(radMask * pattern);
float  panFade  = 1.0 - 5.0 * (i.uv.y + (-_Pan_Up));
float  blendPan = saturate((panFade > 0.5) ? (panFade + 2.0*pattern-1.0) : (panFade + 2.0*(pattern-0.5)));
float  maskB    = clamp(blendPan, 0, 1);

// Depth blend
float2 screenUV   = i.screenPos.xy / i.screenPos.w;
float  sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
float  surfDepth  = i.screenPos.w;
float  depthDiff  = abs(sceneDepth - surfDepth) / _Depth_Blend;

// Color
half3  col   = lerp(_Color_2.rgb, _Color_1.rgb, pattern);
float  alpha = maskB * _Alpha * saturate(depthDiff);

return half4(col, alpha);
}
ENDHLSL
}
}
FallBack "Universal Render Pipeline/Lit"
}