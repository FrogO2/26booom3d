using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Game/Level Start Attack Gate")]
public class LevelStartAttackGate : MonoBehaviour
{
	private const float NormalTimeScale = 1f;
	private const float DefaultFixedDeltaTime = 0.02f;

	[Header("References")]
	[SerializeField] private FirstPersonController firstPersonController;
	[SerializeField] private FirstPersonViewAnimationController attackController;

	[Header("Gate")]
	[SerializeField] private bool gateOnStart = true;
	[SerializeField, Min(0f)] private float releaseLookSuppressionDuration = 0.08f;

	private bool gateActive;
	private bool subscribed;
	private bool timeScaleOverridden;
	private float baseFixedDeltaTime = DefaultFixedDeltaTime;

	public bool IsGateActive => gateActive;

	private void Awake()
	{
		CacheBaseFixedDeltaTime();
		AutoAssignReferences();
	}

	private void Start()
	{
		if (gateOnStart)
		{
			BeginGate();
		}
	}

	private void OnDisable()
	{
		ReleaseGate(restoreInput: true, keepLookSuppressed: false);
	}

	public void BeginGate()
	{
		AutoAssignReferences();
		if (firstPersonController == null || attackController == null)
		{
			Debug.LogWarning($"{nameof(LevelStartAttackGate)} on {name} is missing first-person references.", this);
			return;
		}

		SubscribeIfNeeded();
		gateActive = true;
		attackController.SetWeaponAnimatorUsesUnscaledTime(true);
		attackController.ClearAttackState();
		firstPersonController.SetTraversalInputLocked(true);
		firstPersonController.SetLookInputLocked(true);
		firstPersonController.ClearInputState();
		OverrideTimeScale(0f);
	}

	public void ReleaseGate(bool restoreInput = true, bool keepLookSuppressed = true)
	{
		attackController?.SetWeaponAnimatorUsesUnscaledTime(false);
		RestoreTimeScale();

		if (restoreInput && firstPersonController != null)
		{
			firstPersonController.ClearInputState();
			firstPersonController.SetTraversalInputLocked(false);
			firstPersonController.SetLookInputLocked(false);

			if (keepLookSuppressed && releaseLookSuppressionDuration > 0f)
			{
				firstPersonController.SuppressLookInput(releaseLookSuppressionDuration);
			}
		}

		gateActive = false;
		Unsubscribe();
	}

	private void HandleAttackStateEntered(int attackNumber, int attackSequenceId)
	{
		if (!gateActive)
		{
			return;
		}

		ReleaseGate();
	}

	private void AutoAssignReferences()
	{
		if (firstPersonController == null)
		{
			firstPersonController = GetComponent<FirstPersonController>();
			if (firstPersonController == null)
			{
				firstPersonController = FindAnyObjectByType<FirstPersonController>();
			}
		}

		if (attackController == null)
		{
			attackController = GetComponent<FirstPersonViewAnimationController>();
			if (attackController == null)
			{
				attackController = FindAnyObjectByType<FirstPersonViewAnimationController>();
			}
		}
	}

	private void SubscribeIfNeeded()
	{
		if (subscribed || attackController == null)
		{
			return;
		}

		attackController.AttackStateEnteredEvent += HandleAttackStateEntered;
		subscribed = true;
	}

	private void Unsubscribe()
	{
		if (!subscribed || attackController == null)
		{
			return;
		}

		attackController.AttackStateEnteredEvent -= HandleAttackStateEntered;
		subscribed = false;
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
}