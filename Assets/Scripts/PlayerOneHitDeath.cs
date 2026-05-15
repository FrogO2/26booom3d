using System;
using System.Collections;
using Invector;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

[DisallowMultipleComponent]
[RequireComponent(typeof(FirstPersonController))]
public class PlayerOneHitDeath : MonoBehaviour, vIDamageReceiver
{
	public enum PlayerDeathCause
	{
		None,
		Projectile,
		Melee,
		Fall,
	}

	[Serializable]
	public sealed class PlayerDeathEvent : UnityEvent<PlayerDeathCause>
	{
	}

	[Header("References")]
	[SerializeField] private FirstPersonController firstPersonController;
	[SerializeField] private FirstPersonViewAnimationController attackController;
	[SerializeField] private RuntimeTextOverlayUI deathOverlayUi;

	[Header("Fall Death")]
	[SerializeField] private bool monitorFallDeath = true;
	[SerializeField] private float fallDeathYThreshold = -20f;

	[Header("Death Presentation")]
	[SerializeField, Min(0f)] private float deathTimeScaleToZeroDuration = 0.35f;
	[SerializeField] private int deathOverlaySortingOrder = 320;
	[SerializeField] private string restartPromptChannelKey = "PlayerDeathRestart";
	[SerializeField, TextArea] private string restartPromptMessage = "R\nRestart";
	[SerializeField] private Vector2 restartPromptAnchoredPosition = new Vector2(0f, -300f);
	[SerializeField] private Vector2 restartPromptSize = new Vector2(720f, 220f);
	[SerializeField] private float restartPromptFontSize = 72f;
	[SerializeField] private Color restartPromptColor = Color.white;

	[Header("Damage Events")]
	[SerializeField] private OnReceiveDamage onStartReceiveDamageEvent = new OnReceiveDamage();
	[SerializeField] private OnReceiveDamage onReceiveDamageEvent = new OnReceiveDamage();
	[SerializeField] private PlayerDeathEvent died = new PlayerDeathEvent();

	public event Action<PlayerDeathCause> Died;

	public bool IsDead { get; private set; }
	public PlayerDeathCause DeathCause { get; private set; }
	public float FallDeathYThreshold
	{
		get => fallDeathYThreshold;
		set => fallDeathYThreshold = value;
	}

	public bool MonitorFallDeath
	{
		get => monitorFallDeath;
		set => monitorFallDeath = value;
	}

	public OnReceiveDamage onStartReceiveDamage => onStartReceiveDamageEvent;
	public OnReceiveDamage onReceiveDamage => onReceiveDamageEvent;

	private bool timeScaleOverridden;
	private float previousTimeScale = 1f;
	private float previousFixedDeltaTime = 0.02f;
	private Coroutine deathTimeScaleCoroutine;

	private void Awake()
	{
		AutoAssignReferences();
		EnsureDeathOverlayBuilt();
	}

	private void OnValidate()
	{
		AutoAssignReferences();
	}

	private void OnDisable()
	{
		RestoreTimeScale();
	}

	private void OnDestroy()
	{
		RestoreTimeScale();
	}

	private void Update()
	{
		if (!monitorFallDeath || IsDead)
		{
			return;
		}

		if (transform.position.y < fallDeathYThreshold)
		{
			TryKill(PlayerDeathCause.Fall);
		}
	}

	public void TakeDamage(vDamage damage)
	{
		if (IsDead)
		{
			return;
		}

		onStartReceiveDamageEvent?.Invoke(damage);
		onReceiveDamageEvent?.Invoke(damage);
		TryKill(PlayerDeathCause.Projectile);
	}

	public bool KillFromMelee()
	{
		return TryKill(PlayerDeathCause.Melee);
	}

	public bool TryKill(PlayerDeathCause cause)
	{
		if (cause == PlayerDeathCause.None || IsDead)
		{
			return false;
		}

		IsDead = true;
		DeathCause = cause;

		if (firstPersonController != null)
		{
			firstPersonController.SetTraversalInputLocked(true);
			firstPersonController.SetLookInputLocked(true);
			firstPersonController.ClearInputState();
		}

		if (attackController != null)
		{
			attackController.SetAttackInputLocked(true);
			attackController.SetDeathPresentationActive(true);
		}

		ApplyDeathTimeScale();
		ShowRestartPrompt();

		died?.Invoke(cause);
		Died?.Invoke(cause);
		return true;
	}

	public void ResetDeathState()
	{
		if (!IsDead)
		{
			return;
		}

		IsDead = false;
		DeathCause = PlayerDeathCause.None;

		if (firstPersonController != null)
		{
			firstPersonController.SetTraversalInputLocked(false);
			firstPersonController.SetLookInputLocked(false);
			firstPersonController.ClearInputState();
		}

		if (attackController != null)
		{
			attackController.SetAttackInputLocked(false);
			attackController.SetDeathPresentationActive(false);
		}

		HideRestartPrompt();
		RestoreTimeScale();
	}

	private void AutoAssignReferences()
	{
		if (firstPersonController == null)
		{
			firstPersonController = GetComponent<FirstPersonController>();
		}

		if (attackController == null)
		{
			attackController = GetComponent<FirstPersonViewAnimationController>();
		}

		if (deathOverlayUi == null)
		{
			deathOverlayUi = GetComponent<RuntimeTextOverlayUI>();
		}
	}

	private void EnsureDeathOverlayBuilt()
	{
		if (deathOverlayUi == null)
		{
			deathOverlayUi = GetComponent<RuntimeTextOverlayUI>();
		}

		if (deathOverlayUi == null)
		{
			deathOverlayUi = gameObject.AddComponent<RuntimeTextOverlayUI>();
		}

		deathOverlayUi.SetSortingOrder(deathOverlaySortingOrder);
		deathOverlayUi.EnsureOverlayBuilt();
		deathOverlayUi.HideText(restartPromptChannelKey);
	}

	private void ShowRestartPrompt()
	{
		EnsureDeathOverlayBuilt();
		if (deathOverlayUi == null)
		{
			return;
		}

		deathOverlayUi.ShowText(new RuntimeTextOverlayUI.DisplayRequest
		{
			ChannelKey = restartPromptChannelKey,
			Message = restartPromptMessage,
			AnchoredPosition = restartPromptAnchoredPosition,
			Size = restartPromptSize,
			FontSize = restartPromptFontSize,
			Duration = 0f,
			FadeDuration = 0.2f,
			CharacterSpacing = 0f,
			Color = restartPromptColor,
			Alignment = TextAlignmentOptions.Center,
			FontStyle = FontStyles.Bold,
			WordWrap = true,
			OverflowMode = TextOverflowModes.Overflow,
		});
	}

	private void HideRestartPrompt()
	{
		deathOverlayUi?.HideText(restartPromptChannelKey);
	}

	private void ApplyDeathTimeScale()
	{
		if (timeScaleOverridden)
		{
			return;
		}

		previousTimeScale = Time.timeScale;
		previousFixedDeltaTime = Time.fixedDeltaTime;
		timeScaleOverridden = true;

		if (deathTimeScaleToZeroDuration <= 0f || previousTimeScale <= Mathf.Epsilon)
		{
			SetCurrentDeathTimeScale(0f);
			return;
		}

		deathTimeScaleCoroutine = StartCoroutine(RampDeathTimeScaleToZero());
	}

	private IEnumerator RampDeathTimeScaleToZero()
	{
		float elapsed = 0f;

		while (timeScaleOverridden && elapsed < deathTimeScaleToZeroDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float progress = Mathf.Clamp01(elapsed / deathTimeScaleToZeroDuration);
			SetCurrentDeathTimeScale(Mathf.Lerp(previousTimeScale, 0f, progress));
			yield return null;
		}

		if (timeScaleOverridden)
		{
			SetCurrentDeathTimeScale(0f);
		}

		deathTimeScaleCoroutine = null;
	}

	private void SetCurrentDeathTimeScale(float timeScale)
	{
		float clampedTimeScale = Mathf.Max(0f, timeScale);
		Time.timeScale = clampedTimeScale;

		if (previousTimeScale > Mathf.Epsilon)
		{
			Time.fixedDeltaTime = previousFixedDeltaTime * (clampedTimeScale / previousTimeScale);
			return;
		}

		Time.fixedDeltaTime = 0f;
	}

	private void RestoreTimeScale()
	{
		if (!timeScaleOverridden)
		{
			return;
		}

		if (deathTimeScaleCoroutine != null)
		{
			StopCoroutine(deathTimeScaleCoroutine);
			deathTimeScaleCoroutine = null;
		}

		Time.timeScale = previousTimeScale;
		Time.fixedDeltaTime = previousFixedDeltaTime;
		timeScaleOverridden = false;
	}
}