// URP-compatible rewrite (original: Amplify Shader Editor for Built-in RP)
Shader "Blockout/Editor_Trigger"
{
Properties
{
_SpecColor("Specular Color", Color) = (1,1,1,1)
_Depth_Blend("Depth_Blend", Float) = 1
_Emissive_Brightness("Emissive_Brightness", Float) = 1
_Color_1("Color_1", Color) = (0.345098,0.3686275,0.627451,1)
_Extra_Lines("Extra_Lines", Float) = 0.5
_Texture0("Texture 0", 2D) = "white" {}
[HideInInspector] __dirty("", Int) = 1
}

SubShader
{
Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IsEmissive" = "true" }

Pass
{
Name "ForwardLit"
Tags { "LightMode" = "UniversalForward" }
Cull Back
Blend One One

HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

CBUFFER_START(UnityPerMaterial)
float  _Emissive_Brightness;
float4 _Color_1;
float4 _Texture0_ST;
float  _Extra_Lines;
float  _Depth_Blend;
CBUFFER_END

TEXTURE2D(_Texture0); SAMPLER(sampler_Texture0);

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
float4 screenPos  : TEXCOORD2;
};

Varyings Vert(Attributes i)
{
Varyings o;
VertexPositionInputs pos = GetVertexPositionInputs(i.positionOS.xyz);
o.positionCS = pos.positionCS;
o.positionWS = pos.positionWS;
o.normalWS   = TransformObjectToWorldNormal(i.normalOS);
o.screenPos  = ComputeScreenPos(o.positionCS);
return o;
}

// Blend: screen (soft light)
float BlendScreen(float a, float b) { return 1.0 - (1.0 - a) * (1.0 - b); }

half4 Frag(Varyings i) : SV_Target
{
float3 nWS   = normalize(i.normalWS);
float3 vDir  = normalize(GetWorldSpaceViewDir(i.positionWS));
float  fr    = 0.0 + 0.9 * pow(1.0 - saturate(dot(nWS, vDir)), 2.0);
float  lerpFr = lerp(0.1, 0.4, fr);

float t = _Time.y * 0.8;

// X-face lines
float4 appendX  = float4(i.positionWS.y, i.positionWS.z, 0, 0);
float  texBaseX  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendX.xy).r;
float  texBlueX  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendX.xy).b;
float2 panX1 = appendX.xy * 0.5 + t * float2(-0.1, 0);
float2 panX2 = appendX.xy + t * float2(0.1, 0);
float2 panX3 = appendX.xy * 0.25 + t * float2(0, -0.1);
float2 panX4 = appendX.xy * 0.75 + t * float2(0, 0.1);
float  bX1  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panX1).g;
float  bX2  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panX2).g;
float  bX3  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panX3).g;
float  bX4  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panX4).g;
float  blendXH = saturate(round(0.5 * (bX1 + bX2)));
float  blendXV = saturate(round(0.5 * (bX3 + bX4)));
float  animX   = saturate(BlendScreen(blendXH, blendXV));
float  maskX   = clamp(texBaseX + texBlueX * _Extra_Lines + animX, 0, 1);

// Y-face lines
float4 appendY  = float4(i.positionWS.x, i.positionWS.z, 0, 0);
float2 appendYo = appendY.xy + float2(0.5, 0);
float  texBaseY  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendYo).r;
float  texBlueY  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendYo).b;
float2 panY1 = appendY.xy * 0.5 + t * float2(-0.1, 0);
float2 panY2 = appendY.xy + t * float2(0.1, 0);
float2 panY3 = appendY.xy * 0.25 + t * float2(0, -0.1);
float2 panY4 = appendY.xy * 0.75 + t * float2(0, 0.1);
float  bY1  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panY1).g;
float  bY2  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panY2).g;
float  bY3  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panY3).g;
float  bY4  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panY4).g;
float  blendYH = saturate(round(0.5 * (bY1 + bY2)));
float  blendYV = saturate(round(0.5 * (bY3 + bY4)));
float  animY   = 0.1 * saturate(BlendScreen(blendYH, blendYV));
float  maskY   = clamp(texBaseY + texBlueY * _Extra_Lines + animY, 0, 1);

// Z-face lines
float4 appendZ  = float4(i.positionWS.x, i.positionWS.y, 0, 0);
float2 appendZo = (float4(0.5, appendZ.xy, 0)).yz;
float  texBaseZ  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendZo).r;
float  texBlueZ  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendZo).b;
float2 panZ1 = appendZ.xy * 0.5 + t * float2(-0.1, 0);
float2 panZ2 = appendZ.xy + t * float2(0.1, 0);
float2 panZ3 = appendZ.xy * 0.25 + t * float2(0, -0.1);
float2 panZ4 = appendZ.xy * 0.75 + t * float2(0, 0.1);
float  bZ1  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panZ1).g;
float  bZ2  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panZ2).g;
float  bZ3  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panZ3).g;
float  bZ4  = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panZ4).g;
float  blendZH = saturate(round(0.5 * (bZ1 + bZ2)));
float  blendZV = saturate(round(0.5 * (bZ3 + bZ4)));
float  animZ   = saturate(BlendScreen(blendZH, blendZV));
float  maskZ   = clamp(texBaseZ + texBlueZ * _Extra_Lines + animZ, 0, 1);

// Combine faces by world normal weight
float3 nAbs  = pow(abs(nWS), 3);
float  total = nAbs.x + nAbs.y + nAbs.z + 0.0001;
float  mask  = (nAbs.x * maskX + nAbs.y * maskY + nAbs.z * maskZ) / total;

// Depth blend
float2 screenUV   = i.screenPos.xy / i.screenPos.w;
float  sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
float  surfDepth  = i.screenPos.w;
float  depthDiff  = abs(sceneDepth - surfDepth) / _Depth_Blend;

half3  emit  = _Emissive_Brightness * _Color_1.rgb * lerpFr * mask * saturate(depthDiff);
return half4(emit, 1);
}
ENDHLSL
}
}
FallBack "Universal Render Pipeline/Lit"
}