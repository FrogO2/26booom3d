// URP-compatible rewrite (original: Amplify Shader Editor for Built-in RP)
Shader "Blockout/Area_Comment_Wireframe"
{
Properties
{
_SpecColor("Specular Color", Color) = (1,1,1,1)
_Depth_Blend("Depth_Blend", Float) = 1
_Color("Color", Color) = (0.4980392,0.4980392,0.4980392,1)
[HideInInspector] __dirty("", Int) = 1
}

SubShader
{
Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IsEmissive" = "true" }

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
float4 _Color;
float  _Depth_Blend;
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

half4 Frag(Varyings i) : SV_Target
{
float3 nWS    = normalize(i.normalWS);
float3 vDir   = normalize(GetWorldSpaceViewDir(i.positionWS));
float  fresnel = pow(1.0 - saturate(dot(nWS, vDir)), 2.0);
half4  emit    = clamp(lerp(1.4 * _Color, _Color, fresnel), 0, 1);

float2 screenUV   = i.screenPos.xy / i.screenPos.w;
float  sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
float  surfDepth  = i.screenPos.w;
float  depthDiff  = abs(sceneDepth - surfDepth) / _Depth_Blend;

return half4(emit.rgb, saturate(depthDiff));
}
ENDHLSL
}
}
FallBack "Universal Render Pipeline/Lit"
}