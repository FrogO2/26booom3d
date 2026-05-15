using System.Collections.Generic;
using UnityEngine;

public static class BloodFxManager
{
	private static readonly int BloodCountId = Shader.PropertyToID("_BloodProjectorCount");
	private static readonly int BloodTextureId = Shader.PropertyToID("_BloodProjectorTexture");
	private static readonly int BloodPositionsId = Shader.PropertyToID("_BloodProjectorPositions");
	private static readonly int BloodRightsId = Shader.PropertyToID("_BloodProjectorRights");
	private static readonly int BloodUpsId = Shader.PropertyToID("_BloodProjectorUps");
	private static readonly int BloodForwardsId = Shader.PropertyToID("_BloodProjectorForwards");
	private static readonly int BloodParams0Id = Shader.PropertyToID("_BloodProjectorParams0");
	private static readonly int BloodParams1Id = Shader.PropertyToID("_BloodProjectorParams1");
	private static readonly int BloodColorsId = Shader.PropertyToID("_BloodProjectorColors");
	private static readonly int BloodUvTransformsId = Shader.PropertyToID("_BloodProjectorUvTransforms");
	private static readonly int BloodFlagsId = Shader.PropertyToID("_BloodProjectorFlags");

	private static readonly Vector4[] BloodPositions = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] BloodRights = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] BloodUps = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] BloodForwards = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] BloodParams0 = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] BloodParams1 = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] BloodColors = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] BloodUvTransforms = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] BloodFlags = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];

	private static readonly List<BloodFxEntry> BloodEntries = new List<BloodFxEntry>(FrozenProjectorManager.ShaderMaxProjectors);

	public static void AddBloodFx(int projectorId, Texture texture, Color tint, Vector2 uvScale, Vector2 uvOffset)
	{
		if (projectorId < 0)
		{
			return;
		}

		BloodEntries.Add(new BloodFxEntry
		{
			projectorId = projectorId,
			projectionTexture = texture,
			tint = tint,
			uvScale = uvScale,
			uvOffset = uvOffset,
		});
	}

	public static void ClearAll()
	{
		BloodEntries.Clear();
	}

	public static void ApplyToMaterial(Material material)
	{
		if (material == null)
		{
			return;
		}

		Texture sharedTexture = Texture2D.whiteTexture;
		int count = 0;
		int startIndex = Mathf.Max(0, BloodEntries.Count - FrozenProjectorManager.ShaderMaxProjectors);

        // 【新增】：获取当前主相机的视锥体平面
        Camera mainCam = Camera.main;
        Plane[] frustumPlanes = mainCam != null ? GeometryUtility.CalculateFrustumPlanes(mainCam) : null;

        for (int i = startIndex; i < BloodEntries.Count && count < FrozenProjectorManager.ShaderMaxProjectors; i++)
		{
			BloodFxEntry entry = BloodEntries[i];
			if (!FrozenProjectorManager.TryGetProjector(entry.projectorId, out FrozenProjectorManager.FrozenProjector projector))
			{
				continue;
			}
            // 【新增】：粗略计算投影仪的包围盒，并进行视锥体剔除
            if (frustumPlanes != null)
            {
                // 用一个粗略的球体或 AABB 来代表投影仪的有效范围
                Vector3 center = projector.position + projector.forward * (projector.farDistance * 0.5f);
                float radius = projector.farDistance * 0.5f;
                Bounds bounds = new Bounds(center, new Vector3(radius * 2, radius * 2, radius * 2));

                // 如果这个血迹完全不在屏幕内，直接跳过！不发给显卡！
                if (!GeometryUtility.TestPlanesAABB(frustumPlanes, bounds))
                    continue;
            }

            BloodPositions[count] = new Vector4(projector.position.x, projector.position.y, projector.position.z, 1f);
			BloodRights[count] = new Vector4(projector.right.x, projector.right.y, projector.right.z, 0f);
			BloodUps[count] = new Vector4(projector.up.x, projector.up.y, projector.up.z, 0f);
			BloodForwards[count] = new Vector4(projector.forward.x, projector.forward.y, projector.forward.z, 0f);
			BloodParams0[count] = new Vector4(projector.tanHalfFov, projector.aspect, projector.nearDistance, projector.farDistance);
			BloodParams1[count] = new Vector4(projector.edgeFeather, projector.depthSliceIndex, projector.visibleDepthBias, 0f);
			BloodColors[count] = entry.tint;
			BloodUvTransforms[count] = new Vector4(entry.uvScale.x, entry.uvScale.y, entry.uvOffset.x, entry.uvOffset.y);
			BloodFlags[count] = new Vector4(entry.projectionTexture != null ? 1f : 0f, 0f, 0f, 0f);

			if (entry.projectionTexture != null)
			{
				sharedTexture = entry.projectionTexture;
			}

			count++;
		}

		material.SetInt(BloodCountId, count);
		FrozenProjectorManager.ApplySharedVisibilityData(material);
		material.SetTexture(BloodTextureId, sharedTexture);
		material.SetVectorArray(BloodPositionsId, BloodPositions);
		material.SetVectorArray(BloodRightsId, BloodRights);
		material.SetVectorArray(BloodUpsId, BloodUps);
		material.SetVectorArray(BloodForwardsId, BloodForwards);
		material.SetVectorArray(BloodParams0Id, BloodParams0);
		material.SetVectorArray(BloodParams1Id, BloodParams1);
		material.SetVectorArray(BloodColorsId, BloodColors);
		material.SetVectorArray(BloodUvTransformsId, BloodUvTransforms);
		material.SetVectorArray(BloodFlagsId, BloodFlags);
	}

	public static IReadOnlyList<BloodFxEntry> Entries => BloodEntries;
	public static bool HasActiveEffects => BloodEntries.Count > 0;

	public struct BloodFxEntry
	{
		public int projectorId;
		public Texture projectionTexture;
		public Color tint;
		public Vector2 uvScale;
		public Vector2 uvOffset;
	}
}