Shader "Hidden/MyProject/BloodObjectMask"
{
	Properties
	{
		_BaseMap("Base Map", 2D) = "white" {}
		_BaseColor("Base Color", Color) = (1,1,1,1)
		_MainTex("Main Tex", 2D) = "white" {}
		_Color("Color", Color) = (1,1,1,1)
		_Cutoff("Cutoff", Range(0,1)) = 0.5
		_AlphaClip("Alpha Clip", Float) = 0
		_Cull("Cull", Float) = 2
	}

	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

		Pass
		{
			Name "BloodObjectMaskPass"
			ZWrite Off
			ZTest LEqual
			Cull [_Cull]
			Blend One One
			BlendOp Max
			ColorMask RG

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);
			TEXTURE2D(_MainTex);
			SAMPLER(sampler_MainTex);

			float4 _BaseMap_ST;
			float4 _MainTex_ST;
			float4 _BaseColor;
			float4 _Color;
			float4 _BloodMaskWriteColor;
			float _Cutoff;
			float _AlphaClip;

			struct Attributes
			{
				float4 positionOS : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float2 uvBase : TEXCOORD0;
				float2 uvMain : TEXCOORD1;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				output.uvBase = TRANSFORM_TEX(input.uv, _BaseMap);
				output.uvMain = TRANSFORM_TEX(input.uv, _MainTex);
				return output;
			}

			float SampleMaskAlpha(Varyings input)
			{
				float baseAlpha = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uvBase).a * _BaseColor.a;
				float legacyAlpha = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uvMain).a * _Color.a;
				return saturate(max(baseAlpha, legacyAlpha));
			}

			float4 Frag(Varyings input) : SV_Target
			{
				float alpha = SampleMaskAlpha(input);

				if (_AlphaClip > 0.5 || alpha <= _Cutoff)
				{
					clip(alpha - _Cutoff);
				}

				return _BloodMaskWriteColor * alpha;
			}
			ENDHLSL
		}
	}
}