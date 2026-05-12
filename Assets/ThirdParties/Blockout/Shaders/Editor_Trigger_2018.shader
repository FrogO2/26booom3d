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
}

SubShader
{
Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IsEmissive" = "true" }

Pass
{
Name "ForwardLit"
Tags { "LightMode" = "UniversalForwardOnly" }
Cull Back
Blend One One

HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

CBUFFER_START(UnityPerMaterial)
float _Emissive_Brightness;
float4 _Color_1;
float4 _Texture0_ST;
float _Extra_Lines;
float _Depth_Blend;
CBUFFER_END

TEXTURE2D(_Texture0);
SAMPLER(sampler_Texture0);

struct Attributes
{
float4 positionOS : POSITION;
float3 normalOS : NORMAL;
};

struct Varyings
{
float4 positionCS : SV_POSITION;
float3 normalWS : TEXCOORD0;
float3 positionWS : TEXCOORD1;
float4 screenPos : TEXCOORD2;
};

Varyings Vert(Attributes input)
{
Varyings output;
VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
output.positionCS = position.positionCS;
output.positionWS = position.positionWS;
output.normalWS = TransformObjectToWorldNormal(input.normalOS);
output.screenPos = ComputeScreenPos(output.positionCS);
return output;
}

float BlendScreen(float left, float right)
{
return 1.0 - (1.0 - left) * (1.0 - right);
}

half4 Frag(Varyings input) : SV_Target
{
float3 normalWS = normalize(input.normalWS);
float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
float fresnel = 0.9 * pow(1.0 - saturate(dot(normalWS, viewDir)), 2.0);
float fresnelLerp = lerp(0.1, 0.4, fresnel);
float timeValue = _Time.y * 0.8;

float2 appendX = input.positionWS.yz;
float texBaseX = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendX).r;
float texBlueX = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendX).b;
float2 panX1 = appendX * 0.5 + timeValue * float2(-0.1, 0.0);
float2 panX2 = appendX + timeValue * float2(0.1, 0.0);
float2 panX3 = appendX * 0.25 + timeValue * float2(0.0, -0.1);
float2 panX4 = appendX * 0.75 + timeValue * float2(0.0, 0.1);
float blendXH = saturate(round(0.5 * (SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panX1).g + SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panX2).g)));
float blendXV = saturate(round(0.5 * (SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panX3).g + SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panX4).g)));
float animX = saturate(BlendScreen(blendXH, blendXV));
float maskX = clamp(texBaseX + texBlueX * _Extra_Lines + animX, 0.0, 1.0);

float2 appendY = input.positionWS.xz;
float2 appendYo = appendY + float2(0.5, 0.0);
float texBaseY = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendYo).r;
float texBlueY = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendYo).b;
float2 panY1 = appendY * 0.5 + timeValue * float2(-0.1, 0.0);
float2 panY2 = appendY + timeValue * float2(0.1, 0.0);
float2 panY3 = appendY * 0.25 + timeValue * float2(0.0, -0.1);
float2 panY4 = appendY * 0.75 + timeValue * float2(0.0, 0.1);
float blendYH = saturate(round(0.5 * (SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panY1).g + SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panY2).g)));
float blendYV = saturate(round(0.5 * (SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panY3).g + SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panY4).g)));
float animY = 0.1 * saturate(BlendScreen(blendYH, blendYV));
float maskY = clamp(texBaseY + texBlueY * _Extra_Lines + animY, 0.0, 1.0);

float2 appendZ = input.positionWS.xy;
float2 appendZo = float2(0.5, appendZ.y);
float texBaseZ = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendZo).r;
float texBlueZ = SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, appendZo).b;
float2 panZ1 = appendZ * 0.5 + timeValue * float2(-0.1, 0.0);
float2 panZ2 = appendZ + timeValue * float2(0.1, 0.0);
float2 panZ3 = appendZ * 0.25 + timeValue * float2(0.0, -0.1);
float2 panZ4 = appendZ * 0.75 + timeValue * float2(0.0, 0.1);
float blendZH = saturate(round(0.5 * (SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panZ1).g + SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panZ2).g)));
float blendZV = saturate(round(0.5 * (SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panZ3).g + SAMPLE_TEXTURE2D(_Texture0, sampler_Texture0, panZ4).g)));
float animZ = saturate(BlendScreen(blendZH, blendZV));
float maskZ = clamp(texBaseZ + texBlueZ * _Extra_Lines + animZ, 0.0, 1.0);

float3 absNormal = pow(abs(normalWS), 3.0);
float totalWeight = absNormal.x + absNormal.y + absNormal.z + 0.0001;
float mask = (absNormal.x * maskX + absNormal.y * maskY + absNormal.z * maskZ) / totalWeight;

float2 screenUV = input.screenPos.xy / input.screenPos.w;
float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
float surfaceDepth = input.screenPos.w;
float depthDiff = abs(sceneDepth - surfaceDepth) / _Depth_Blend;

half3 emission = _Emissive_Brightness * _Color_1.rgb * fresnelLerp * mask * saturate(depthDiff);
return half4(emission, 1.0);
}
ENDHLSL
}

Pass
{
Name "DepthNormalsOnly"
Tags { "LightMode" = "DepthNormalsOnly" }
ZWrite On
Cull Back

HLSLPROGRAM
#pragma vertex DepthNormalsVert
#pragma fragment DepthNormalsFrag
#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

struct DepthNormalsAttributes
{
float4 positionOS : POSITION;
float3 normalOS : NORMAL;
};

struct DepthNormalsVaryings
{
float4 positionCS : SV_POSITION;
float3 normalWS : TEXCOORD0;
};

DepthNormalsVaryings DepthNormalsVert(DepthNormalsAttributes input)
{
DepthNormalsVaryings output;
output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
output.normalWS = TransformObjectToWorldNormal(input.normalOS);
return output;
}

void DepthNormalsFrag(
DepthNormalsVaryings input,
out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
, out uint outRenderingLayers : SV_Target1
#endif
)
{
float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
half3 packedNormalWS = half3(PackFloat2To888(remappedOctNormalWS));
outNormalWS = half4(packedNormalWS, 0.0);
#else
outNormalWS = half4(normalWS, 0.0);
#endif
#ifdef _WRITE_RENDERING_LAYERS
outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}
ENDHLSL
}

Pass
{
Name "DepthOnly"
Tags { "LightMode" = "DepthOnly" }
ZWrite On
ColorMask 0
Cull Back

HLSLPROGRAM
#pragma vertex DepthVert
#pragma fragment DepthFrag

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct DepthAttribs
{
float4 positionOS : POSITION;
};

float4 DepthVert(DepthAttribs input) : SV_POSITION
{
return TransformObjectToHClip(input.positionOS.xyz);
}

half4 DepthFrag() : SV_Target
{
return 0;
}
ENDHLSL
}
}

FallBack "Universal Render Pipeline/Lit"
}