Shader "Hidden/MyProject/BloodRevealMask_WebGL"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off
		ZTest Always
		Cull Off

		Pass
		{
			Name "BloodRevealMaskPass_WebGL"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag
			#pragma target 3.0

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			#define MAX_PROJECTORS 16
			#define MAX_CASCADES_PER_PROJECTOR 4

			// WebGL: 16 张独立 Texture2D 替代 Texture2DArray。
			#define DECLARE_VIS_SLICE(i) TEXTURE2D(_ProjectorVisibilityAtlas##i); SAMPLER(sampler_ProjectorVisibilityAtlas##i);
			#define DECLARE_HD_SLICE(i) TEXTURE2D(_ProjectorHDAtlas##i); SAMPLER(sampler_ProjectorHDAtlas##i);

			DECLARE_VIS_SLICE(0)  DECLARE_VIS_SLICE(1)  DECLARE_VIS_SLICE(2)  DECLARE_VIS_SLICE(3)
			DECLARE_VIS_SLICE(4)  DECLARE_VIS_SLICE(5)  DECLARE_VIS_SLICE(6)  DECLARE_VIS_SLICE(7)
			DECLARE_VIS_SLICE(8)  DECLARE_VIS_SLICE(9)  DECLARE_VIS_SLICE(10) DECLARE_VIS_SLICE(11)
			DECLARE_VIS_SLICE(12) DECLARE_VIS_SLICE(13) DECLARE_VIS_SLICE(14) DECLARE_VIS_SLICE(15)

			DECLARE_HD_SLICE(0)  DECLARE_HD_SLICE(1)  DECLARE_HD_SLICE(2)  DECLARE_HD_SLICE(3)
			DECLARE_HD_SLICE(4)  DECLARE_HD_SLICE(5)  DECLARE_HD_SLICE(6)  DECLARE_HD_SLICE(7)
			DECLARE_HD_SLICE(8)  DECLARE_HD_SLICE(9)  DECLARE_HD_SLICE(10) DECLARE_HD_SLICE(11)
			DECLARE_HD_SLICE(12) DECLARE_HD_SLICE(13) DECLARE_HD_SLICE(14) DECLARE_HD_SLICE(15)

			TEXTURE2D(_BloodObjectMaskTex);
			SAMPLER(sampler_BloodObjectMaskTex);

			int _RevealProjectorCount;
			float4 _RevealProjectorPositions[MAX_PROJECTORS];
			float4 _RevealProjectorRights[MAX_PROJECTORS];
			float4 _RevealProjectorUps[MAX_PROJECTORS];
			float4 _RevealProjectorForwards[MAX_PROJECTORS];
			float4 _RevealProjectorParams0[MAX_PROJECTORS];
			float4 _RevealProjectorParams1[MAX_PROJECTORS];
			float4 _ProjectorCascadeFarDistances[MAX_PROJECTORS];
			float4 _HiddenColor;

			float4 SampleVisibilityAtlas(int slice, float2 uv)
			{
				if (slice == 0)  return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas0,  sampler_ProjectorVisibilityAtlas0,  uv);
				if (slice == 1)  return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas1,  sampler_ProjectorVisibilityAtlas1,  uv);
				if (slice == 2)  return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas2,  sampler_ProjectorVisibilityAtlas2,  uv);
				if (slice == 3)  return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas3,  sampler_ProjectorVisibilityAtlas3,  uv);
				if (slice == 4)  return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas4,  sampler_ProjectorVisibilityAtlas4,  uv);
				if (slice == 5)  return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas5,  sampler_ProjectorVisibilityAtlas5,  uv);
				if (slice == 6)  return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas6,  sampler_ProjectorVisibilityAtlas6,  uv);
				if (slice == 7)  return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas7,  sampler_ProjectorVisibilityAtlas7,  uv);
				if (slice == 8)  return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas8,  sampler_ProjectorVisibilityAtlas8,  uv);
				if (slice == 9)  return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas9,  sampler_ProjectorVisibilityAtlas9,  uv);
				if (slice == 10) return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas10, sampler_ProjectorVisibilityAtlas10, uv);
				if (slice == 11) return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas11, sampler_ProjectorVisibilityAtlas11, uv);
				if (slice == 12) return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas12, sampler_ProjectorVisibilityAtlas12, uv);
				if (slice == 13) return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas13, sampler_ProjectorVisibilityAtlas13, uv);
				if (slice == 14) return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas14, sampler_ProjectorVisibilityAtlas14, uv);
				return SAMPLE_TEXTURE2D(_ProjectorVisibilityAtlas15, sampler_ProjectorVisibilityAtlas15, uv);
			}

			float4 SampleHDAtlas(int slice, float2 uv)
			{
				if (slice == 0)  return SAMPLE_TEXTURE2D(_ProjectorHDAtlas0,  sampler_ProjectorHDAtlas0,  uv);
				if (slice == 1)  return SAMPLE_TEXTURE2D(_ProjectorHDAtlas1,  sampler_ProjectorHDAtlas1,  uv);
				if (slice == 2)  return SAMPLE_TEXTURE2D(_ProjectorHDAtlas2,  sampler_ProjectorHDAtlas2,  uv);
				if (slice == 3)  return SAMPLE_TEXTURE2D(_ProjectorHDAtlas3,  sampler_ProjectorHDAtlas3,  uv);
				if (slice == 4)  return SAMPLE_TEXTURE2D(_ProjectorHDAtlas4,  sampler_ProjectorHDAtlas4,  uv);
				if (slice == 5)  return SAMPLE_TEXTURE2D(_ProjectorHDAtlas5,  sampler_ProjectorHDAtlas5,  uv);
				if (slice == 6)  return SAMPLE_TEXTURE2D(_ProjectorHDAtlas6,  sampler_ProjectorHDAtlas6,  uv);
				if (slice == 7)  return SAMPLE_TEXTURE2D(_ProjectorHDAtlas7,  sampler_ProjectorHDAtlas7,  uv);
				if (slice == 8)  return SAMPLE_TEXTURE2D(_ProjectorHDAtlas8,  sampler_ProjectorHDAtlas8,  uv);
				if (slice == 9)  return SAMPLE_TEXTURE2D(_ProjectorHDAtlas9,  sampler_ProjectorHDAtlas9,  uv);
				if (slice == 10) return SAMPLE_TEXTURE2D(_ProjectorHDAtlas10, sampler_ProjectorHDAtlas10, uv);
				if (slice == 11) return SAMPLE_TEXTURE2D(_ProjectorHDAtlas11, sampler_ProjectorHDAtlas11, uv);
				if (slice == 12) return SAMPLE_TEXTURE2D(_ProjectorHDAtlas12, sampler_ProjectorHDAtlas12, uv);
				if (slice == 13) return SAMPLE_TEXTURE2D(_ProjectorHDAtlas13, sampler_ProjectorHDAtlas13, uv);
				if (slice == 14) return SAMPLE_TEXTURE2D(_ProjectorHDAtlas14, sampler_ProjectorHDAtlas14, uv);
				return SAMPLE_TEXTURE2D(_ProjectorHDAtlas15, sampler_ProjectorHDAtlas15, uv);
			}

			float EvaluateProjectorMask(float3 worldPosition, int projectorIndex)
			{
				float3 toPoint = worldPosition - _RevealProjectorPositions[projectorIndex].xyz;
				float projectedDepth = dot(toPoint, _RevealProjectorForwards[projectorIndex].xyz);
				float nearDistance = _RevealProjectorParams0[projectorIndex].z;
				float farDistance = _RevealProjectorParams0[projectorIndex].w;

				if (projectedDepth < nearDistance || projectedDepth > farDistance)
				{
					return 0.0;
				}

				float tanHalfFov = _RevealProjectorParams0[projectorIndex].x;
				float aspect = _RevealProjectorParams0[projectorIndex].y;
				float halfHeight = max(projectedDepth * tanHalfFov, 0.0001);
				float halfWidth = max(halfHeight * aspect, 0.0001);
				float projectedX = dot(toPoint, _RevealProjectorRights[projectorIndex].xyz);
				float projectedY = dot(toPoint, _RevealProjectorUps[projectorIndex].xyz);
				float2 normalizedProjection = float2(projectedX / halfWidth, projectedY / halfHeight);
				float edgeDistance = max(abs(normalizedProjection.x), abs(normalizedProjection.y));
				float feather = max(_RevealProjectorParams1[projectorIndex].x, 0.0001);

				if (edgeDistance > 1.0)
				{
					return 0.0;
				}

				float2 projectorUv = normalizedProjection * 0.5 + 0.5;
				int baseSlice    = (int)round(_RevealProjectorParams1[projectorIndex].y);
				float depthBias  = _RevealProjectorParams1[projectorIndex].z;
				int cascadeCount = max(1, (int)round(_RevealProjectorParams1[projectorIndex].w));

				// HD atlas (WebGL 上一般为 stub a=0,自然跳过)
				float4 hdSample = SampleHDAtlas(baseSlice, projectorUv);
				if (hdSample.a > 0.5)
				{
					float viewZ_hd = farDistance - hdSample.r * (farDistance - nearDistance);
					if (projectedDepth > viewZ_hd + depthBias)
					{
						return 0.0;
					}
					return 1.0 - smoothstep(1.0 - feather, 1.0, edgeDistance);
				}

				int projectorSlot = baseSlice / MAX_CASCADES_PER_PROJECTOR;
				float4 cascadeFars = _ProjectorCascadeFarDistances[projectorSlot];
				int cascadeIndex = cascadeCount - 1;
				if (projectedDepth <= cascadeFars.x) cascadeIndex = 0;
				else if (cascadeCount > 1 && projectedDepth <= cascadeFars.y) cascadeIndex = 1;
				else if (cascadeCount > 2 && projectedDepth <= cascadeFars.z) cascadeIndex = 2;

				int depthSliceIndex = baseSlice + cascadeIndex;
				float4 visibilitySample = SampleVisibilityAtlas(depthSliceIndex, projectorUv);

				// WebGL: 解码 R 为 viewZ = far - R*(far - near)
				float visibleViewZ = farDistance - visibilitySample.r * (farDistance - nearDistance);
				if (visibilitySample.a <= 0.5 || projectedDepth > visibleViewZ + depthBias)
				{
					return 0.0;
				}

				return 1.0 - smoothstep(1.0 - feather, 1.0, edgeDistance);
			}

			float4 Frag(Varyings input) : SV_Target0
			{
				UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

				float2 uv = input.texcoord.xy;
				float4 sceneColor = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);

				float revealWithoutBlood = SAMPLE_TEXTURE2D(_BloodObjectMaskTex, sampler_BloodObjectMaskTex, uv).r;
				if (revealWithoutBlood >= 0.999)
				{
					return sceneColor;
				}

				float rawDepth = SampleSceneDepth(uv);

				#if UNITY_REVERSED_Z
				if (rawDepth <= 0.0001)
				{
					return _HiddenColor;
				}
				#else
				if (rawDepth >= 0.9999)
				{
					return _HiddenColor;
				}
				#endif

				float3 worldPosition = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);

				float visibility = 0.0;

				[loop]
				for (int i = 0; i < _RevealProjectorCount; i++)
				{
					visibility = max(visibility, EvaluateProjectorMask(worldPosition, i));
				}

				return lerp(_HiddenColor, sceneColor, saturate(visibility));
			}
			ENDHLSL
		}
	}
}
