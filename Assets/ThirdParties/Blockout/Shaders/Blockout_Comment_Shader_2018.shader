// URP-compatible rewrite (original: Amplify Shader Editor for Built-in RP)
Shader "Blockout/Blockout_Shader_Comment"
{
Properties
{
_Color("Color", Color) = (1,1,1,1)
_Drop_Value("Drop_Value", Float) = 0.5
_Gloss("Gloss", Range(0,1)) = 0.5
_Metallic("Metallic", Range(0,1)) = 0.5
_Texture("Texture", 2D) = "white" {}
_Depth_Blend("Depth_Blend", Float) = 0.1
_TilingFactor("TilingFactor", Float) = 2
_Float3("Float 3", Range(0,1)) = 0
[HideInInspector] __dirty("", Int) = 1
}

SubShader
{
Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline"
       "IgnoreProjector" = "True" "ForceNoShadowCasting" = "True" }

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
float  _Drop_Value;
float  _Metallic;
float  _Gloss;
float4 _Texture_ST;
float  _TilingFactor;
float  _Depth_Blend;
float  _Float3;
CBUFFER_END

TEXTURE2D(_Texture); SAMPLER(sampler_Texture);

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
float  fresnel = pow(1.0 - saturate(dot(nWS, vDir)), 5.0);
float  fresnelLerp = lerp(1.0, 0.5, fresnel);

// Triplanar texture
float3 blend = pow(max(abs(nWS), 0.001), 3);
blend /= blend.x + blend.y + blend.z;
float2 uvX = float2(i.positionWS.z, i.positionWS.y) * _TilingFactor;
float2 uvY = float2(i.positionWS.x, i.positionWS.z) * _TilingFactor;
float2 uvZ = float2(i.positionWS.x, i.positionWS.y) * _TilingFactor;
half4 texX = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, uvX);
half4 texY = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, uvY);
half4 texZ = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, uvZ);
half4 triColor = blend.x * texX + blend.y * texY + blend.z * texZ;
half  triAlpha = blend.x * texX.a + blend.y * texY.a + blend.z * texZ.a;

half4 combined  = clamp(_Color * triColor + triAlpha * _Drop_Value, 0, 1);
half4 finalColor = fresnelLerp * combined;

// Depth-blend alpha
float2 screenUV   = i.screenPos.xy / i.screenPos.w;
float  sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
float  surfDepth  = i.screenPos.w;
float  depthDiff  = abs(sceneDepth - surfDepth) / _Depth_Blend;
float  alpha      = _Color.a * saturate(depthDiff) * _Float3;

return half4(finalColor.rgb, alpha);
}
ENDHLSL
}
}
FallBack "Universal Render Pipeline/Lit"
}