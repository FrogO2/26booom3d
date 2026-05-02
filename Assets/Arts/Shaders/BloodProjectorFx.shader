Shader "Hidden/MyProject/BloodProjectorFx"
{
	SubShader
	{
		Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
		ZWrite Off
		ZTest Always
		Cull Off

		Pass
		{
			Name "BloodProjectorFxPass"

			HLSLPROGRAM
			#pragma vertex Vert
			#pragma fragment Frag

			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
			#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

			#define MAX_PROJECTORS 16
			// Must match FrozenProjectorManager.MaxCascadesPerProjector.
			#define MAX_CASCADES_PER_PROJECTOR 4

			TEXTURE2D(_BloodProjectorTexture);
			SAMPLER(sampler_BloodProjectorTexture);
			TEXTURE2D_ARRAY(_ProjectorVisibilityAtlas);
			SAMPLER(sampler_ProjectorVisibilityAtlas);
			// High-resolution depth atlas populated asynchronously via GPU capture.
			// Alpha > 0.5 in a slot means HD data is present; checked before the standard atlas.
			TEXTURE2D_ARRAY(_ProjectorHDAtlas);
			SAMPLER(sampler_ProjectorHDAtlas);

			int _BloodProjectorCount;
			float4 _BloodProjectorPositions[MAX_PROJECTORS];
			float4 _BloodProjectorRights[MAX_PROJECTORS];
			float4 _BloodProjectorUps[MAX_PROJECTORS];
			float4 _BloodProjectorForwards[MAX_PROJECTORS];
			float4 _BloodProjectorParams0[MAX_PROJECTORS];
			float4 _BloodProjectorParams1[MAX_PROJECTORS];
			float4 _BloodProjectorColors[MAX_PROJECTORS];
			float4 _BloodProjectorUvTransforms[MAX_PROJECTORS];
			float4 _BloodProjectorFlags[MAX_PROJECTORS];
			// Per-slot cascade far distances: x/y/z/w = cascade 0/1/2/3 far distance.
			// Indexed by projector SLOT (= baseSlice / MAX_CASCADES_PER_PROJECTOR), not by active count.
			float4 _ProjectorCascadeFarDistances[MAX_PROJECTORS];

			bool EvaluateProjector(float3 worldPosition, int projectorIndex, out float2 projectorUv, out float mask)
			{
				float3 toPoint = worldPosition - _BloodProjectorPositions[projectorIndex].xyz;
				float projectedDepth = dot(toPoint, _BloodProjectorForwards[projectorIndex].xyz);
				float nearDistance = _BloodProjectorParams0[projectorIndex].z;
				float farDistance = _BloodProjectorParams0[projectorIndex].w;

				if (projectedDepth < nearDistance || projectedDepth > farDistance)
				{
					projectorUv = 0.0;
					mask = 0.0;
					return false;
				}

				float tanHalfFov = _BloodProjectorParams0[projectorIndex].x;
				float aspect = _BloodProjectorParams0[projectorIndex].y;
				float halfHeight = max(projectedDepth * tanHalfFov, 0.0001);
				float halfWidth = max(halfHeight * aspect, 0.0001);
				float projectedX = dot(toPoint, _BloodProjectorRights[projectorIndex].xyz);
				float projectedY = dot(toPoint, _BloodProjectorUps[projectorIndex].xyz);
				float2 normalizedProjection = float2(projectedX / halfWidth, projectedY / halfHeight);
				float edgeDistance = max(abs(normalizedProjection.x), abs(normalizedProjection.y));
				float feather = max(_BloodProjectorParams1[projectorIndex].x, 0.0001);

				if (edgeDistance > 1.0)
				{
					projectorUv = 0.0;
					mask = 0.0;
					return false;
				}

				projectorUv = normalizedProjection * 0.5 + 0.5;
				int baseSlice    = (int)round(_BloodProjectorParams1[projectorIndex].y);
				float depthBias  = _BloodProjectorParams1[projectorIndex].z;
				int cascadeCount = max(1, (int)round(_BloodProjectorParams1[projectorIndex].w));

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
						projectorUv = 0.0;
						mask = 0.0;
						return false;
					}
					mask = 1.0 - smoothstep(1.0 - feather, 1.0, edgeDistance);
					return true;
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
					projectorUv = 0.0;
					mask = 0.0;
					return false;
				}

				mask = 1.0 - smoothstep(1.0 - feather, 1.0, edgeDistance);
				return true;
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
					return sceneColor;
				}
				#else
				if (rawDepth >= 0.9999)
				{
					return sceneColor;
				}
				#endif

				float3 worldPosition = ComputeWorldSpacePosition(uv, rawDepth, UNITY_MATRIX_I_VP);
				float4 result = sceneColor;

				[loop]
				for (int i = 0; i < _BloodProjectorCount; i++)
				{
					float2 projectorUv;
					float mask;

					if (!EvaluateProjector(worldPosition, i, projectorUv, mask))
					{
						continue;
					}

					float2 transformedUv = projectorUv * _BloodProjectorUvTransforms[i].xy + _BloodProjectorUvTransforms[i].zw;
					float4 bloodSample = _BloodProjectorFlags[i].x > 0.5
						? SAMPLE_TEXTURE2D(_BloodProjectorTexture, sampler_BloodProjectorTexture, transformedUv)
						: float4(1.0, 1.0, 1.0, 1.0);

					float4 projectedColor = bloodSample * _BloodProjectorColors[i];
					float alpha = saturate(projectedColor.a * mask);
					result.rgb = lerp(result.rgb, projectedColor.rgb, alpha);
				}

				return result;
			}
			ENDHLSL
		}
	}
}