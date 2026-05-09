Shader "Hidden/MyProject/BloodObjectMask"
{
	SubShader
	{
		Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "Opaque" }

		Pass
		{
			Name "BloodObjectMaskPass"
			ZWrite Off
			ZTest LEqual
			Cull Back
			Blend One Zero
			ColorMask RG

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			float4 _MaskColor;

			struct Attributes
			{
				float4 positionOS : POSITION;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
			};

			Varyings Vert(Attributes input)
			{
				Varyings output;
				output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
				return output;
			}

			float4 Frag(Varyings input) : SV_Target
			{
				return _MaskColor;
			}
			ENDHLSL
		}
	}
}