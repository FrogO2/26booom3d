using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Combat/Attack Targeting Service")]
public class AttackTargetingService : MonoBehaviour
{
	[SerializeField] private Camera playerCamera;

	public void Initialize(Camera camera)
	{
		if (camera != null)
		{
			playerCamera = camera;
		}

		AutoAssignReferences();
	}

	public ArenaBakedEnemyTarget SelectBestTarget(Vector3 hitboxLocalOffset, Vector3 hitboxSize)
	{
		if (!TryGetHitboxPose(hitboxLocalOffset, out Vector3 hitboxCenter, out Quaternion hitboxRotation))
		{
			return null;
		}

		Vector3 hitboxHalfExtents = hitboxSize * 0.5f;
		Collider[] overlaps = Physics.OverlapBox(hitboxCenter, hitboxHalfExtents, hitboxRotation, ~0, QueryTriggerInteraction.Ignore);
		ArenaBakedEnemyTarget bestCandidate = null;
		float bestScore = float.MaxValue;
		HashSet<ArenaBakedEnemyTarget> seenTargets = new HashSet<ArenaBakedEnemyTarget>();

		for (int i = 0; i < overlaps.Length; i++)
		{
			Collider overlap = overlaps[i];
			if (overlap == null)
			{
				continue;
			}

			ArenaBakedEnemyTarget candidate = overlap.GetComponent<ArenaBakedEnemyTarget>();
			if (candidate == null)
			{
				candidate = overlap.GetComponentInParent<ArenaBakedEnemyTarget>();
			}

			if (candidate == null || !seenTargets.Add(candidate) || !candidate.CanBeTargeted())
			{
				continue;
			}

			Vector3 localPoint = Quaternion.Inverse(hitboxRotation) * (candidate.GetAimPoint() - hitboxCenter);
			Vector3 normalizedPoint = new Vector3(
				hitboxSize.x > 0.001f ? localPoint.x / hitboxSize.x : 0f,
				hitboxSize.y > 0.001f ? localPoint.y / hitboxSize.y : 0f,
				hitboxSize.z > 0.001f ? localPoint.z / hitboxSize.z : 0f);
			float score = normalizedPoint.sqrMagnitude;
			if (score < bestScore)
			{
				bestScore = score;
				bestCandidate = candidate;
			}
		}

		return bestCandidate;
	}

	public bool TryGetHitboxPose(Vector3 hitboxLocalOffset, out Vector3 hitboxCenter, out Quaternion hitboxRotation)
	{
		AutoAssignReferences();
		if (playerCamera == null)
		{
			hitboxCenter = Vector3.zero;
			hitboxRotation = Quaternion.identity;
			return false;
		}

		Transform cameraTransform = playerCamera.transform;
		hitboxCenter = cameraTransform.TransformPoint(hitboxLocalOffset);
		hitboxRotation = cameraTransform.rotation;
		return true;
	}

	public bool TryExecuteKill(ArenaBakedEnemyTarget target, ArenaEnemyKillContext context)
	{
		return target != null && target.TryKill(context);
	}

	private void AutoAssignReferences()
	{
		if (playerCamera != null)
		{
			return;
		}

		playerCamera = GetComponentInChildren<Camera>(true);
		if (playerCamera == null)
		{
			playerCamera = Camera.main;
		}
	}
}