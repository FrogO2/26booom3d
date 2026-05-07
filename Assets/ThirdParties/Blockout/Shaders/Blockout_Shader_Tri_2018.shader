// URP-compatible rewrite (original: Amplify Shader Editor for Built-in RP)
Shader "Blockout/Blockout_Shader_Tri"
{
	Properties
	{
		_Color("Color", Color) = (1,1,1,1)
		_Locked_Drop("Locked_Drop", Float) = 0.7
		_Drop_Value("Drop_Value", Float) = 0.5
		_Texture("Texture", 2D) = "white" {}
		_Gloss("Gloss", Range( 0 , 1)) = 0.227
		_Metallic("Metallic", Range( 0 , 1)) = 0.087
		[HideInInspector] __dirty( "", Int ) = 1
	}

	SubShader
	{
		Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

		Pass
		{
			Name "ForwardLit"
			Tags { "LightMode" = "UniversalForwardOnly" }
			Cull Back

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
			#pragma multi_compile _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
			#pragma multi_compile_fog

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			CBUFFER_START(UnityPerMaterial)
				float4 _Color;
				float  _Locked_Drop;
				float4 _Texture_ST;
				float  _Drop_Value;
				float  _Gloss;
				float  _Metallic;
			CBUFFER_END

			TEXTURE2D(_Texture); SAMPLER(sampler_Texture);

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS   : NORMAL;
				float4 color      : COLOR;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float4 color      : TEXCOORD0;
				float3 normalWS   : TEXCOORD1;
				float3 positionWS : TEXCOORD2;
				float  fogCoord   : TEXCOORD3;
			};

			Varyings Vert(Attributes i)
			{
				Varyings o;
				VertexPositionInputs pos = GetVertexPositionInputs(i.positionOS.xyz);
				o.positionCS = pos.positionCS;
				o.positionWS = pos.positionWS;
				o.color      = i.color;
				o.normalWS   = TransformObjectToWorldNormal(i.normalOS);
				o.fogCoord   = ComputeFogFactor(o.positionCS.z);
				return o;
			}

			half4 Frag(Varyings i) : SV_Target
			{
				float3 nWS = normalize(i.normalWS);

				// Triplanar weights (clamped to positive, power 3)
				float3 blend = pow(max(abs(nWS), 0.001), 3);
				float  blendSum = blend.x + blend.y + blend.z;
				blend /= blendSum;

				// Sample texture from each axis
				half4 texX = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.positionWS.yz);
				half4 texY = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.positionWS.xz);
				half4 texZ = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, i.positionWS.xy);

				half4 triColor = blend.x * texX + blend.y * texY + blend.z * texZ;
				half  triAlpha = blend.x * texX.a + blend.y * texY.a + blend.z * texZ.a;

				half4 combined = clamp(_Color * triColor + triAlpha * _Drop_Value, 0, 1);
				half4 albedo   = lerp(combined * _Locked_Drop, combined, i.color.r);

				InputData inputData = (InputData)0;
				inputData.positionWS              = i.positionWS;
				inputData.positionCS              = i.positionCS;
				inputData.normalWS                = nWS;
				inputData.viewDirectionWS         = GetWorldSpaceNormalizeViewDir(i.positionWS);
				inputData.shadowCoord             = TransformWorldToShadowCoord(i.positionWS);
				inputData.fogCoord                = i.fogCoord;
				inputData.bakedGI                 = SampleSH(nWS);
				inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
				inputData.shadowMask              = half4(1, 1, 1, 1);

				SurfaceData surfaceData = (SurfaceData)0;
				surfaceData.albedo     = albedo.rgb;
				surfaceData.metallic   = _Metallic;
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
			Cull Back

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
			Cull Back

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
