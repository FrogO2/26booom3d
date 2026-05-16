using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Game/Prompt Trigger")]
public class ArenaPromptTriggerZone : MonoBehaviour
{
	[SerializeField] private ArenaPromptOverlay promptOverlay;
	[SerializeField] private CharacterController playerController;
	[SerializeField] private Camera targetCamera;
	[SerializeField] private string playerTag = "Player";
	[SerializeField, TextArea(2, 4)] private string message = "NEW OBJECTIVE";
	[SerializeField] private float duration = 3.5f;
	[SerializeField] private ArenaPromptColorMode colorMode = ArenaPromptColorMode.AdaptiveContrast;
	[SerializeField] private Color solidColor = Color.white;
	[SerializeField] private bool oneShot = true;

	private bool triggered;

	private void Reset()
	{
		EnsureTriggerCollider();
		AutoAssignReferences();
	}

	private void Awake()
	{
		EnsureTriggerCollider();
		AutoAssignReferences();
	}

	private void OnEnable()
	{
		triggered = false;
		AutoAssignReferences();
	}

	private void OnValidate()
	{
		EnsureTriggerCollider();
	}

	private void OnTriggerEnter(Collider other)
	{
		if (triggered || !ArenaPromptEventUtility.IsPlayerCollider(this, other, playerController, playerTag))
		{
			return;
		}

		bool shown = ArenaPromptEventUtility.TryShowPrompt(this, promptOverlay, targetCamera, message, duration, colorMode, solidColor);
		if (shown && oneShot)
		{
			triggered = true;
		}
	}

	private void AutoAssignReferences()
	{
		if (playerController == null)
		{
			playerController = ArenaPromptEventUtility.ResolvePlayerController(playerTag);
		}

		if (targetCamera == null)
		{
			targetCamera = ArenaPromptEventUtility.ResolveCamera();
		}
	}

	private void EnsureTriggerCollider()
	{
		Collider triggerCollider = GetComponent<Collider>();
		if (triggerCollider != null)
		{
			triggerCollider.isTrigger = true;
		}
	}
}