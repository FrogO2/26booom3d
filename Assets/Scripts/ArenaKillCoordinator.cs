using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class ArenaKillCoordinator : MonoBehaviour
{
	private const string HitboxVisualizerObjectName = "Attack Hitbox";

	[Header("References")]
	[SerializeField] private FirstPersonViewAnimationController attackController;
	[SerializeField] private SprayPaint sprayPaint;
	[SerializeField] private AttackTargetingService targetingService;
	[SerializeField] private EffectManager effectManager;

	[Header("Attack Hitbox")]
	[SerializeField] private Vector3 hitboxLocalOffset = new Vector3(0f, -0.2f, 1.8f);
	[SerializeField] private Vector3 hitboxSize = new Vector3(2.4f, 2.0f, 3.6f);
	[SerializeField] private BoxCollider hitboxVisualizer;

	[Header("Hit Stop")]
	[SerializeField, Min(0f), Tooltip("Minimum pause applied after a confirmed arena kill.")] private float minimumHitStopDuration = 0.05f;
	[SerializeField, Min(0.05f)] private float enemyDestroyDelay = 6f;

	[Header("Kill Direction")]
	[SerializeField, Range(0f, 1f)] private float forwardWeight = 0.35f;

	private Coroutine killSequenceCoroutine;
	private bool isSubscribed;
	private bool timeScaleOverridden;
	private float previousTimeScale = 1f;
	private float previousFixedDeltaTime = 0.02f;
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

		if (effectManager == null)
		{
			effectManager = EffectManager.Instance != null ? EffectManager.Instance : FindAnyObjectByType<EffectManager>();
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


		ArenaBakedEnemyTarget candidate = GetBestKillCandidate();
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
		OverrideTimeScale(0f);

		float elapsed = 0f;
		while (elapsed < minimumHitStopDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			yield return null;
		}

		sprayPaint?.CaptureHighResForProjector(projectorId);

		RestoreTimeScale();
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

	private void OnValidate()
	{
		MinimumHitStopDuration = minimumHitStopDuration;
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