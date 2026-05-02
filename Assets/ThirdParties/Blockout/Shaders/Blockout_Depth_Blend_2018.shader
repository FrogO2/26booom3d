// URP-compatible rewrite (original: Amplify Shader Editor for Built-in RP)
Shader "Blockout/Blockout_Depth_Blend"
{
Properties
{
_SpecColor("Specular Color", Color) = (1,1,1,1)
_Color("Color", Color) = (0.4980392,0.4980392,0.4980392,0.003921569)
_Depth_Blend("Depth_Blend", Float) = 1
[HideInInspector] __dirty("", Int) = 1
}

SubShader
{
Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

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
float4 _Color;
float  _Depth_Blend;
CBUFFER_END

struct Attributes
{
float4 positionOS : POSITION;
};

struct Varyings
{
float4 positionCS : SV_POSITION;
float4 screenPos  : TEXCOORD0;
};

Varyings Vert(Attributes i)
{
Varyings o;
o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
o.screenPos  = ComputeScreenPos(o.positionCS);
return o;
}

half4 Frag(Varyings i) : SV_Target
{
float2 screenUV    = i.screenPos.xy / i.screenPos.w;
float  sceneDepth  = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
float  surfDepth   = i.screenPos.w;
float  depthDiff   = abs(sceneDepth - surfDepth) / _Depth_Blend;
return half4(_Color.rgb, saturate(depthDiff));
}
ENDHLSL
}
}
FallBack "Universal Render Pipeline/Lit"
}