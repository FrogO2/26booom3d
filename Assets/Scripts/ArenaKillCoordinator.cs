using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ArenaKillCoordinator : MonoBehaviour
{
	private const string HitboxVisualizerObjectName = "Attack Hitbox";
	private const float HitStopRecoveryMaxStep = 1f / 30f;
	private const float NormalTimeScale = 1f;
	private const float DefaultFixedDeltaTime = 0.02f;

	[Header("References")]
	[SerializeField] private FirstPersonViewAnimationController attackController;
	[SerializeField] private FirstPersonController firstPersonController;
	[SerializeField] private SprayPaint sprayPaint;
	[SerializeField] private AttackTargetingService targetingService;
	[SerializeField] private LevelStartAttackGate levelStartAttackGate;

	[Header("Attack Hitbox")]
	[SerializeField] private Vector3 hitboxLocalOffset = new Vector3(0f, -0.2f, 1.8f);
	[SerializeField] private Vector3 hitboxSize = new Vector3(2.4f, 2.0f, 3.6f);
	[SerializeField] private BoxCollider hitboxVisualizer;

	[Header("Hit Stop")]
	[SerializeField, Min(0f), Tooltip("Minimum pause applied after a confirmed arena kill.")] private float minimumHitStopDuration = 0.05f;
	[SerializeField, Range(0f, 1f), Tooltip("Time scale used on the first frame after hit stop before ramping back to normal.")] private float postHitStopStartTimeScale = 0.2f;
	[SerializeField, Min(0f), Tooltip("Duration of the slow-motion ramp back to normal speed after hit stop.")] private float postHitStopRecoveryDuration = 0.12f;
	[SerializeField, Min(0f), Tooltip("Duration for suppressing look input after hit stop to discard queued mouse delta.")] private float postHitStopLookSuppressionDuration = 0.08f;
	[SerializeField, Min(0.05f)] private float enemyDestroyDelay = 6f;

	[Header("Kill Direction")]
	[SerializeField, Range(0f, 1f)] private float forwardWeight = 0.35f;

	private Coroutine killSequenceCoroutine;
	private Coroutine hitStopRecoveryCoroutine;
	private bool isSubscribed;
	private bool timeScaleOverridden;
	private float baseFixedDeltaTime = DefaultFixedDeltaTime;
	private int lastResolvedAttackSequence = -1;

	public float MinimumHitStopDuration
	{
		get => minimumHitStopDuration;
		set => minimumHitStopDuration = Mathf.Max(0f, value);
	}

	public void Initialize()
	{
		AutoAssignReferences();
		SubscribeIfNeeded();
	}

	private void Awake()
	{
		CacheBaseFixedDeltaTime();
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
		StopHitStopRecovery();
		RestoreTimeScale();
	}

	private void AutoAssignReferences()
	{
		if (attackController == null)
		{
			attackController = GetComponent<FirstPersonViewAnimationController>();
			if (attackController == null)
			{
				attackController = GetComponentInChildren<FirstPersonViewAnimationController>(true);
			}
		}

		if (sprayPaint == null)
		{
			sprayPaint = GetComponent<SprayPaint>();
			if (sprayPaint == null)
			{
				sprayPaint = GetComponentInChildren<SprayPaint>(true);
			}
		}

		if (targetingService == null)
		{
			targetingService = GetComponent<AttackTargetingService>();
			if (targetingService == null)
			{
				targetingService = gameObject.AddComponent<AttackTargetingService>();
			}
		}



		if (firstPersonController == null)
		{
			firstPersonController = GetComponent<FirstPersonController>();
			if (firstPersonController == null)
			{
				firstPersonController = GetComponentInChildren<FirstPersonController>(true);
			}
		}

		if (levelStartAttackGate == null)
		{
			levelStartAttackGate = GetComponent<LevelStartAttackGate>();
			if (levelStartAttackGate == null)
			{
				levelStartAttackGate = GetComponentInChildren<LevelStartAttackGate>(true);
			}
		}

		targetingService?.Initialize(ResolveTargetCamera());
		EnsureHitboxVisualizer();
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

		if (levelStartAttackGate != null && levelStartAttackGate.IsGateActive)
		{
			killSequenceCoroutine = StartCoroutine(ExecuteAttackAfterGateRelease(attackNumber));
			return;
		}

		TryResolveAttackKill(attackNumber);
	}

	private IEnumerator ExecuteAttackAfterGateRelease(int attackNumber)
	{
		while (levelStartAttackGate != null && levelStartAttackGate.IsGateActive)
		{
			yield return null;
		}

		TryResolveAttackKill(attackNumber);
	}

	private void TryResolveAttackKill(int attackNumber)
	{
		if (!isActiveAndEnabled)
		{
			killSequenceCoroutine = null;
			return;
		}


		ArenaBakedEnemyTarget candidate = GetBestKillCandidate();
		if (candidate == null)
		{
			killSequenceCoroutine = null;
			return;
		}

        if (PlayerAudioController.Instance != null)
        {
            PlayerAudioController.Instance.StopWeaponSound();
        }

        if (attackController != null)
		{
			attackController.TrySnapCurrentAttackToImpactFrame(attackNumber);
		}

		Vector3 hitPoint = candidate.GetAimPoint();
		Vector3 hitDirection = ResolveHitDirection(candidate.transform, attackNumber);
		int projectorId = sprayPaint != null ? sprayPaint.TriggerSprayFromCurrentCamera() : -1;



		killSequenceCoroutine = StartCoroutine(ExecuteKillSequence(candidate, attackNumber, hitPoint, hitDirection, projectorId));
	}

	private IEnumerator ExecuteKillSequence(ArenaBakedEnemyTarget target, int attackNumber, Vector3 hitPoint, Vector3 hitDirection, int projectorId)
	{
		yield return ApplyHitStop(projectorId);

		ExecuteArenaKill(target, attackNumber, hitPoint, hitDirection);

		killSequenceCoroutine = null;
	}


	private ArenaBakedEnemyTarget GetBestKillCandidate()
	{
		return targetingService == null ? null : targetingService.SelectBestTarget(hitboxLocalOffset, hitboxSize);
	}

	private void ExecuteArenaKill(ArenaBakedEnemyTarget target, int attackNumber, Vector3 hitPoint, Vector3 hitDirection)
	{
		if (targetingService == null)
		{
			return;
		}

        if (PlayerAudioController.Instance != null)
        {
            PlayerAudioController.Instance.PlayHitSound();
        }

        targetingService.TryExecuteKill(target, new ArenaEnemyKillContext
		{
			AttackNumber = attackNumber,
			HitPoint = hitPoint,
			HitDirection = hitDirection,
			DestroyDelay = enemyDestroyDelay,
		});
	}

	private IEnumerator ApplyHitStop(int projectorId)
	{
		StopHitStopRecovery();
		OverrideTimeScale(0f);

		float elapsed = 0f;
		while (elapsed < minimumHitStopDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}

		sprayPaint?.CaptureHighResForProjector(projectorId);
		firstPersonController?.SuppressLookInput(postHitStopLookSuppressionDuration);

		if (postHitStopRecoveryDuration <= 0f)
		{
			RestoreTimeScale();
			yield break;
		}

		StartHitStopRecovery();
	}

	private void StartHitStopRecovery()
	{
		StopHitStopRecovery();

		float targetTimeScale = NormalTimeScale;
		float startTimeScale = Mathf.Clamp(postHitStopStartTimeScale, 0f, targetTimeScale);
		OverrideTimeScale(startTimeScale);
		hitStopRecoveryCoroutine = StartCoroutine(RampOutOfHitStop(startTimeScale, targetTimeScale, postHitStopRecoveryDuration));
	}

	private IEnumerator RampOutOfHitStop(float startTimeScale, float targetTimeScale, float duration)
	{
		float elapsed = 0f;

		while (elapsed < duration)
		{
			elapsed += Mathf.Min(Time.unscaledDeltaTime, HitStopRecoveryMaxStep);
			float progress = Mathf.Clamp01(elapsed / duration);
			float easedProgress = progress * progress;
			OverrideTimeScale(Mathf.Lerp(startTimeScale, targetTimeScale, easedProgress));
			yield return null;
		}

		hitStopRecoveryCoroutine = null;
		RestoreTimeScale();
	}

	private void StopHitStopRecovery()
	{
		if (hitStopRecoveryCoroutine == null)
		{
			return;
		}

		StopCoroutine(hitStopRecoveryCoroutine);
		hitStopRecoveryCoroutine = null;
	}

	private void OverrideTimeScale(float timeScale)
	{
		timeScaleOverridden = true;

		Time.timeScale = timeScale;
		Time.fixedDeltaTime = baseFixedDeltaTime * Mathf.Max(0f, timeScale);
	}

	private void RestoreTimeScale()
	{
		if (!timeScaleOverridden)
		{
			return;
		}

		Time.timeScale = NormalTimeScale;
		Time.fixedDeltaTime = baseFixedDeltaTime;
		timeScaleOverridden = false;
	}

	private void CacheBaseFixedDeltaTime()
	{
		if (Time.fixedDeltaTime > 0f)
		{
			baseFixedDeltaTime = Time.fixedDeltaTime;
		}
	}

	private void OnValidate()
	{
		MinimumHitStopDuration = minimumHitStopDuration;
		postHitStopStartTimeScale = Mathf.Clamp01(postHitStopStartTimeScale);
		postHitStopRecoveryDuration = Mathf.Max(0f, postHitStopRecoveryDuration);
		postHitStopLookSuppressionDuration = Mathf.Max(0f, postHitStopLookSuppressionDuration);
		hitboxSize = new Vector3(
			Mathf.Max(0.05f, hitboxSize.x),
			Mathf.Max(0.05f, hitboxSize.y),
			Mathf.Max(0.05f, hitboxSize.z));

		if (!Application.isPlaying)
		{
			EnsureHitboxVisualizer();
		}
	}

	private Vector3 ResolveHitDirection(Transform enemyTransform, int attackNumber)
	{
		Transform referenceTransform = attackController != null && attackController.transform != null
			? attackController.transform
			: transform;

		Vector3 sideDirection = referenceTransform.right * (attackNumber == 2 ? 1f : -1f);
		Vector3 towardEnemy = enemyTransform != null ? enemyTransform.position - referenceTransform.position : referenceTransform.forward;
		towardEnemy.y = 0f;

		if (towardEnemy.sqrMagnitude <= 0.001f)
		{
			towardEnemy = referenceTransform.forward;
		}

		Vector3 blendedDirection = sideDirection.normalized + towardEnemy.normalized * Mathf.Clamp01(forwardWeight);
		return blendedDirection.sqrMagnitude > 0.001f ? blendedDirection.normalized : sideDirection.normalized;
	}

	private void OnDrawGizmosSelected()
	{
		if (!TryGetHitboxVisualizationPose(out Vector3 center, out Quaternion rotation, out Vector3 size))
		{
			return;
		}

		Matrix4x4 previousMatrix = Gizmos.matrix;
		Color previousColor = Gizmos.color;

		Gizmos.color = Color.cyan;
		Gizmos.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
		Gizmos.DrawWireCube(Vector3.zero, size);

		Gizmos.matrix = previousMatrix;
		Gizmos.color = previousColor;
	}

	private Camera ResolveTargetCamera()
	{
		if (attackController != null)
		{
			Camera controllerCamera = attackController.GetComponentInChildren<Camera>(true);
			if (controllerCamera != null)
			{
				return controllerCamera;
			}
		}

		return GetComponentInChildren<Camera>(true);
	}

	private void EnsureHitboxVisualizer()
	{
		Camera targetCamera = ResolveTargetCamera();
		if (targetCamera == null)
		{
			return;
		}

		if (hitboxVisualizer == null)
		{
			Transform existingVisualizerTransform = targetCamera.transform.Find(HitboxVisualizerObjectName);
			GameObject visualizerObject = existingVisualizerTransform != null
				? existingVisualizerTransform.gameObject
				: new GameObject(HitboxVisualizerObjectName);

			if (visualizerObject.transform.parent != targetCamera.transform)
			{
				visualizerObject.transform.SetParent(targetCamera.transform, false);
			}

			hitboxVisualizer = visualizerObject.GetComponent<BoxCollider>();
			if (hitboxVisualizer == null)
			{
				hitboxVisualizer = visualizerObject.AddComponent<BoxCollider>();
			}
		}

		Transform visualizerTransform = hitboxVisualizer.transform;
		if (visualizerTransform.parent != targetCamera.transform)
		{
			visualizerTransform.SetParent(targetCamera.transform, false);
		}

		visualizerTransform.localRotation = Quaternion.identity;
		visualizerTransform.localScale = Vector3.one;
		visualizerTransform.localPosition = hitboxLocalOffset;
		hitboxVisualizer.center = Vector3.zero;
		hitboxVisualizer.size = hitboxSize;
		hitboxVisualizer.isTrigger = true;
	}

	private bool TryGetHitboxVisualizationPose(out Vector3 center, out Quaternion rotation, out Vector3 size)
	{
		if (hitboxVisualizer != null)
		{
			center = hitboxVisualizer.transform.TransformPoint(hitboxVisualizer.center);
			rotation = hitboxVisualizer.transform.rotation;
			size = Vector3.Scale(hitboxVisualizer.size, hitboxVisualizer.transform.lossyScale);
			return true;
		}

		AttackTargetingService service = targetingService != null ? targetingService : GetComponent<AttackTargetingService>();
		if (service != null && service.TryGetHitboxPose(hitboxLocalOffset, out center, out rotation))
		{
			size = hitboxSize;
			return true;
		}

		center = Vector3.zero;
		rotation = Quaternion.identity;
		size = hitboxSize;
		return false;
	}
}