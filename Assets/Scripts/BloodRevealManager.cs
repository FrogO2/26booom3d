using System.Collections.Generic;
using UnityEngine;

public static class BloodRevealManager
{
	private static readonly int RevealCountId = Shader.PropertyToID("_RevealProjectorCount");
	private static readonly int RevealPositionsId = Shader.PropertyToID("_RevealProjectorPositions");
	private static readonly int RevealRightsId = Shader.PropertyToID("_RevealProjectorRights");
	private static readonly int RevealUpsId = Shader.PropertyToID("_RevealProjectorUps");
	private static readonly int RevealForwardsId = Shader.PropertyToID("_RevealProjectorForwards");
	private static readonly int RevealParams0Id = Shader.PropertyToID("_RevealProjectorParams0");
	private static readonly int RevealParams1Id = Shader.PropertyToID("_RevealProjectorParams1");
	private static readonly int HiddenColorId = Shader.PropertyToID("_HiddenColor");

	private static readonly Vector4[] RevealPositions = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] RevealRights = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] RevealUps = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] RevealForwards = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] RevealParams0 = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];
	private static readonly Vector4[] RevealParams1 = new Vector4[FrozenProjectorManager.ShaderMaxProjectors];

	private static readonly List<int> RevealProjectorIds = new List<int>(FrozenProjectorManager.ShaderMaxProjectors);
	private static Color hiddenColor = Color.black;

	public static void SetHiddenColor(Color color)
	{
		hiddenColor = color;
	}

	public static void AddReveal(int projectorId)
	{
		if (projectorId >= 0)
		{
			RevealProjectorIds.Add(projectorId);
		}
	}

	public static void ClearAll()
	{
		RevealProjectorIds.Clear();
	}

	public static void ApplyToMaterial(Material material)
	{
		if (material == null)
		{
			return;
		}

		int count = FrozenProjectorManager.PopulateProjectorData(
			RevealProjectorIds,
			RevealPositions,
			RevealRights,
			RevealUps,
			RevealForwards,
			RevealParams0,
			RevealParams1);

		FrozenProjectorManager.ApplySharedVisibilityData(material);
		material.SetInt(RevealCountId, count);
		material.SetVectorArray(RevealPositionsId, RevealPositions);
		material.SetVectorArray(RevealRightsId, RevealRights);
		material.SetVectorArray(RevealUpsId, RevealUps);
		material.SetVectorArray(RevealForwardsId, RevealForwards);
		material.SetVectorArray(RevealParams0Id, RevealParams0);
		material.SetVectorArray(RevealParams1Id, RevealParams1);
		material.SetColor(HiddenColorId, hiddenColor);
	}

	public static Color HiddenColor => hiddenColor;
	public static IReadOnlyList<int> RevealIds => RevealProjectorIds;
	public static bool HasActiveReveals => RevealProjectorIds.Count > 0;
}