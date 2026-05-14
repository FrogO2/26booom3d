using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ArenaKillCoordinator : MonoBehaviour
{
	[Header("References")]
	[SerializeField] private FirstPersonViewAnimationController attackController;
	[SerializeField] private SprayPaint sprayPaint;
	[SerializeField] private ArenaTutorialSceneController arenaController;
	[SerializeField] private EffectManager effectManager;

	[Header("Hit Stop")]
	[SerializeField, Min(0f)] private float minimumHitStopDuration = 0.05f;
	[SerializeField, Min(0f)] private float maximumHitStopDuration = 0.18f;
	[SerializeField, Min(0.05f)] private float enemyDestroyDelay = 6f;

	[Header("Kill Direction")]
	[SerializeField, Range(0f, 1f)] private float forwardWeight = 0.35f;

	private Coroutine killSequenceCoroutine;
	private bool isSubscribed;
	private bool timeScaleOverridden;
	private float previousTimeScale = 1f;
	private float previousFixedDeltaTime = 0.02f;
	private int lastResolvedAttackSequence = -1;

	public void Initialize(ArenaTutorialSceneController controller)
	{
		arenaController = controller;
		AutoAssignReferences();
		SubscribeIfNeeded();
	}

	private void Awake()
	{
		AutoAssignReferences();
	}

	private void OnEnable()
	{
		AutoAssignReferences();
		SubscribeIfNeeded();
	}

	private void OnDisable()
	{
		Unsubscribe();
		RestoreTimeScale();
	}

	private void AutoAssignReferences()
	{
		if (attackController == null)
		{
			attackController = GetComponent<FirstPersonViewAnimationController>();
		}

		if (sprayPaint == null)
		{
			sprayPaint = GetComponent<SprayPaint>();
		}

		if (arenaController == null)
		{
			arenaController = FindAnyObjectByType<ArenaTutorialSceneController>();
		}

		if (effectManager == null)
		{
			effectManager = EffectManager.Instance != null ? EffectManager.Instance : FindAnyObjectByType<EffectManager>();
		}
	}

	private void SubscribeIfNeeded()
	{
		if (isSubscribed || attackController == null)
		{
			return;
		}

		attackController.AttackStateEnteredEvent += HandleAttackStateEntered;
		isSubscribed = true;
	}

	private void Unsubscribe()
	{
		if (!isSubscribed || attackController == null)
		{
			return;
		}

		attackController.AttackStateEnteredEvent -= HandleAttackStateEntered;
		isSubscribed = false;
	}

	private void HandleAttackStateEntered(int attackNumber, int attackSequenceId)
	{
		if (killSequenceCoroutine != null || attackSequenceId == lastResolvedAttackSequence)
		{
			return;
		}

		lastResolvedAttackSequence = attackSequenceId;

		if (arenaController == null || !arenaController.CanAttemptArenaKill)
		{
			return;
		}

		ArenaBakedEnemyTarget candidate = arenaController.GetBestKillCandidate();
		if (candidate == null)
		{
			return;
		}

		if (attackController != null)
		{
			attackController.TrySnapCurrentAttackToImpactFrame(attackNumber);
		}

		Vector3 hitPoint = candidate.GetAimPoint();
		Vector3 hitDirection = ResolveHitDirection(candidate.transform, attackNumber);
		int projectorId = sprayPaint != null ? sprayPaint.TriggerSprayFromCurrentCamera() : -1;

		if (effectManager != null)
		{
			effectManager.TriggerKillEffect();
		}

		killSequenceCoroutine = StartCoroutine(ExecuteKillSequence(candidate, attackNumber, hitPoint, hitDirection, projectorId));
	}

	private IEnumerator ExecuteKillSequence(ArenaBakedEnemyTarget target, int attackNumber, Vector3 hitPoint, Vector3 hitDirection, int projectorId)
	{
		yield return ApplyHitStop(projectorId);

		if (arenaController != null)
		{
			arenaController.TryExecuteArenaKill(target, attackNumber, hitPoint, hitDirection, enemyDestroyDelay);
		}

		killSequenceCoroutine = null;
	}

	private IEnumerator ApplyHitStop(int projectorId)
	{
		OverrideTimeScale(0f);

		float elapsed = 0f;
		while (elapsed < minimumHitStopDuration || ShouldExtendHitStop(projectorId, elapsed))
		{
			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}

		RestoreTimeScale();
	}

	private bool ShouldExtendHitStop(int projectorId, float elapsed)
	{
		if (projectorId < 0 || elapsed >= maximumHitStopDuration)
		{
			return false;
		}

		return FrozenProjectorManager.HasPendingHighResCapture(projectorId);
	}

	private void OverrideTimeScale(float timeScale)
	{
		if (!timeScaleOverridden)
		{
			previousTimeScale = Time.timeScale;
			previousFixedDeltaTime = Time.fixedDeltaTime;
			timeScaleOverridden = true;
		}

		Time.timeScale = timeScale;
		Time.fixedDeltaTime = previousFixedDeltaTime * Mathf.Max(0f, timeScale);
	}

	private void RestoreTimeScale()
	{
		if (!timeScaleOverridden)
		{
			return;
		}

		Time.timeScale = previousTimeScale;
		Time.fixedDeltaTime = previousFixedDeltaTime;
		timeScaleOverridden = false;
	}

	private Vector3 ResolveHitDirection(Transform enemyTransform, int attackNumber)
	{
		Transform referenceTransform = attackController != null && attackController.transform != null
			? attackController.transform
			: transform;

		Vector3 sideDirection = referenceTransform.right * (attackNumber == 2 ? -1f : 1f);
		Vector3 towardEnemy = enemyTransform != null ? enemyTransform.position - referenceTransform.position : referenceTransform.forward;
		towardEnemy.y = 0f;

		if (towardEnemy.sqrMagnitude <= 0.001f)
		{
			towardEnemy = referenceTransform.forward;
		}

		Vector3 blendedDirection = sideDirection.normalized + towardEnemy.normalized * Mathf.Clamp01(forwardWeight);
		return blendedDirection.sqrMagnitude > 0.001f ? blendedDirection.normalized : sideDirection.normalized;
	}
}