// URP-compatible rewrite (original: Amplify Shader Editor for Built-in RP)
Shader "Blockout/Blockout_Leaf"
{
Properties
{
_SpecColor("Specular Color", Color) = (1,1,1,1)
_Gloss("Gloss", Range(0,1)) = 0
_Spec("Spec", Range(0,1)) = 0
[HideInInspector] __dirty("", Int) = 1
}

SubShader
{
Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

Pass
{
Name "ForwardLit"
Tags { "LightMode" = "UniversalForwardOnly" }
Cull Off

HLSLPROGRAM
#pragma vertex Vert
#pragma fragment Frag
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
#pragma multi_compile_fog

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

CBUFFER_START(UnityPerMaterial)
float _Gloss;
float _Spec;
CBUFFER_END

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
float  fogCoord   : TEXCOORD2;
};

Varyings Vert(Attributes i)
{
Varyings o;
VertexPositionInputs pos = GetVertexPositionInputs(i.positionOS.xyz);
o.positionCS = pos.positionCS;
o.positionWS = pos.positionWS;
o.normalWS   = TransformObjectToWorldNormal(i.normalOS);
o.fogCoord   = ComputeFogFactor(o.positionCS.z);
return o;
}

half4 Frag(Varyings i) : SV_Target
{
half3 nWS      = normalize(i.normalWS);
half3 viewDir  = GetWorldSpaceNormalizeViewDir(i.positionWS);
half  fresnel  = pow(1.0 - saturate(dot(nWS, viewDir)), 5.0);
half4 baseCol  = half4(0.2862745, 0.4980392, 1, 1);
half3 albedo   = lerp(baseCol.rgb * 0.6, baseCol.rgb, fresnel);

InputData inputData = (InputData)0;
inputData.positionWS              = i.positionWS;
inputData.positionCS              = i.positionCS;
inputData.normalWS                = nWS;
inputData.viewDirectionWS         = viewDir;
inputData.shadowCoord             = TransformWorldToShadowCoord(i.positionWS);
inputData.fogCoord                = i.fogCoord;
inputData.bakedGI                 = SampleSH(nWS);
inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
inputData.shadowMask              = half4(1, 1, 1, 1);

SurfaceData surfaceData = (SurfaceData)0;
surfaceData.albedo     = albedo;
surfaceData.specular   = _Spec.rrr;
surfaceData.smoothness = _Gloss;
surfaceData.occlusion  = 1;
surfaceData.alpha      = 1;

half4 color = UniversalFragmentPBR(inputData, surfaceData);
color.rgb = MixFog(color.rgb, i.fogCoord);
return color;
}
ENDHLSL
}

Pass
{
Name "DepthNormalsOnly"
Tags { "LightMode" = "DepthNormalsOnly" }
ZWrite On
Cull Off

HLSLPROGRAM
#pragma vertex DepthNormalsVert
#pragma fragment DepthNormalsFrag
#pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"

struct DepthNormalsAttributes
{
float4 positionOS : POSITION;
float3 normalOS   : NORMAL;
};

struct DepthNormalsVaryings
{
float4 positionCS : SV_POSITION;
float3 normalWS   : TEXCOORD0;
};

DepthNormalsVaryings DepthNormalsVert(DepthNormalsAttributes i)
{
DepthNormalsVaryings o;
o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
o.normalWS   = TransformObjectToWorldNormal(i.normalOS);
return o;
}

void DepthNormalsFrag(
DepthNormalsVaryings i,
out half4 outNormalWS : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
, out uint outRenderingLayers : SV_Target1
#endif
)
{
float3 normalWS = NormalizeNormalPerPixel(i.normalWS);
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
Name "ShadowCaster"
Tags { "LightMode" = "ShadowCaster" }
ZWrite On
ZTest LEqual
ColorMask 0
Cull Off

HLSLPROGRAM
#pragma vertex ShadowVert
#pragma fragment ShadowFrag
#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

float3 _LightDirection;
float3 _LightPosition;

struct ShadowAttribs { float4 positionOS : POSITION; float3 normalOS : NORMAL; };

float4 ShadowVert(ShadowAttribs i) : SV_POSITION
{
float3 posWS    = TransformObjectToWorld(i.positionOS.xyz);
float3 normalWS = TransformObjectToWorldNormal(i.normalOS);
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
float3 lightDir = normalize(_LightPosition - posWS);
#else
float3 lightDir = _LightDirection;
#endif
return TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDir));
}
half4 ShadowFrag() : SV_Target { return 0; }
ENDHLSL
}

Pass
{
Name "DepthOnly"
Tags { "LightMode" = "DepthOnly" }
ZWrite On
ColorMask 0
Cull Off

HLSLPROGRAM
#pragma vertex DepthVert
#pragma fragment DepthFrag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct DepthAttribs { float4 positionOS : POSITION; };
float4 DepthVert(DepthAttribs i) : SV_POSITION { return TransformObjectToHClip(i.positionOS.xyz); }
half4 DepthFrag() : SV_Target { return 0; }
ENDHLSL
}
}
FallBack "Universal Render Pipeline/Lit"
}