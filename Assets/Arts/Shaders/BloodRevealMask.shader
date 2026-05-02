Shader "Hidden/MyProject/BloodRevealMask"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off
		ZTest Always
		Cull Off

		Pass
		{
			Name "BloodRevealMaskPass"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			#define MAX_PROJECTORS 16
			// Must match FrozenProjectorManager.MaxCascadesPerProjector.
			#define MAX_CASCADES_PER_PROJECTOR 4

			TEXTURE2D_ARRAY(_ProjectorVisibilityAtlas);
			SAMPLER(sampler_ProjectorVisibilityAtlas);
			// High-resolution depth atlas populated asynchronously via GPU capture.
			// Alpha > 0.5 in a slot means HD data is present; checked before the standard atlas.
			TEXTURE2D_ARRAY(_ProjectorHDAtlas);
			SAMPLER(sampler_ProjectorHDAtlas);

			int _RevealProjectorCount;
			float4 _RevealProjectorPositions[MAX_PROJECTORS];
			float4 _RevealProjectorRights[MAX_PROJECTORS];
			float4 _RevealProjectorUps[MAX_PROJECTORS];
			float4 _RevealProjectorForwards[MAX_PROJECTORS];
			float4 _RevealProjectorParams0[MAX_PROJECTORS];
			float4 _RevealProjectorParams1[MAX_PROJECTORS];
			// Per-slot cascade far distances: x/y/z/w = cascade 0/1/2/3 far distance.
			// Indexed by projector SLOT (= baseSlice / MAX_CASCADES_PER_PROJECTOR), not by active count.
			float4 _ProjectorCascadeFarDistances[MAX_PROJECTORS];
			float4 _HiddenColor;

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

				// HD atlas check: if high-res GPU data is present for this slot it overrides
				// the standard cascade result (finer detail, no CPU raycast rounding).
				// Stored value is reverse-Z normalised: R = (far - viewZ) / (far - near).
				// Decode: viewZ = far - R * (far - near).
				float4 hdSample = SAMPLE_TEXTURE2D_ARRAY(_ProjectorHDAtlas, sampler_ProjectorHDAtlas, projectorUv, baseSlice);
				if (hdSample.a > 0.5)
				{
					float viewZ_hd = farDistance - hdSample.r * (farDistance - nearDistance);
					if (projectedDepth > viewZ_hd + depthBias)
					{
						return 0.0;
					}
					return 1.0 - smoothstep(1.0 - feather, 1.0, edgeDistance);
				}

				// Select the finest cascade whose far distance covers the projected depth.
				int projectorSlot = baseSlice / MAX_CASCADES_PER_PROJECTOR;
				float4 cascadeFars = _ProjectorCascadeFarDistances[projectorSlot];
				int cascadeIndex = cascadeCount - 1;
				if (projectedDepth <= cascadeFars.x) cascadeIndex = 0;
				else if (cascadeCount > 1 && projectedDepth <= cascadeFars.y) cascadeIndex = 1;
				else if (cascadeCount > 2 && projectedDepth <= cascadeFars.z) cascadeIndex = 2;

				int depthSliceIndex = baseSlice + cascadeIndex;
				float4 visibilitySample = SAMPLE_TEXTURE2D_ARRAY(_ProjectorVisibilityAtlas, sampler_ProjectorVisibilityAtlas, projectorUv, depthSliceIndex);

				if (visibilitySample.a <= 0.5 || projectedDepth > visibilitySample.r + depthBias)
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