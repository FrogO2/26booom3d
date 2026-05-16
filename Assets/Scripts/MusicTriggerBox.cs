using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Audio/Music Trigger Box")]
public class MusicTriggerBox : MonoBehaviour
{
	[Header("Track Selection Settings")]
	[Tooltip("If true, entering this zone will switch to a specific song in the playlist.")]
	public bool changeTrackOnEnter = true;

	[Tooltip("The index of the target song in the playlist.")]
	public int targetTrackIndex;

	[Header("Target Volume Weights Within This Zone")]
	[Range(0f, 1f)] public float targetVocalsWeight = 1f;
	[Range(0f, 1f)] public float targetOthersWeight = 1f;
	[Range(0f, 1f)] public float targetBassWeight = 1f;
	[Range(0f, 1f)] public float targetDrumsWeight = 1f;

	[Header("Exit Behaviour Settings")]
	[Tooltip("If true, leaving this zone will reset the mix back to exploration mode.")]
	public bool resetOnExit;

	[SerializeField] private CharacterController playerController;
	[SerializeField] private string playerTag = "Player";

	private UltimateAdaptiveMusicManager musicManager;
	private Collider triggerCollider;

	private void Awake()
	{
		EnsureColliderIsTrigger();
		AutoAssignReferences();
	}

	private void OnEnable()
	{
		EnsureColliderIsTrigger();
		AutoAssignReferences();
	}

	public void AssignManager(UltimateAdaptiveMusicManager manager)
	{
		musicManager = manager;
		if (musicManager != null && musicManager.PlayerController != null)
		{
			playerController = musicManager.PlayerController;
		}
		AutoAssignReferences();
	}

	public void ApplyDefaultSetup(int trackIndex)
	{
		changeTrackOnEnter = true;
		targetTrackIndex = trackIndex;
		targetVocalsWeight = 1f;
		targetOthersWeight = 1f;
		targetBassWeight = 1f;
		targetDrumsWeight = 1f;
		resetOnExit = false;
	}

	private void OnTriggerEnter(Collider other)
	{
		if (!ArenaPromptEventUtility.IsPlayerCollider(this, other, playerController, playerTag))
		{
			return;
		}

		SimulatePlayerEnter();
	}

	private void OnTriggerExit(Collider other)
	{
		if (!resetOnExit || !ArenaPromptEventUtility.IsPlayerCollider(this, other, playerController, playerTag))
		{
			return;
		}

		SimulatePlayerExit();
	}

	[ContextMenu("Debug: Simulate Player Enter")]
	public void SimulatePlayerEnter()
	{
		if (musicManager == null)
		{
			musicManager = UltimateAdaptiveMusicManager.EnsureInScene(this, FindAnyObjectByType<FirstPersonViewAnimationController>(), playerController);
		}

		if (musicManager == null)
		{
			Debug.LogWarning($"{nameof(MusicTriggerBox)} on {name} could not find an {nameof(UltimateAdaptiveMusicManager)}.", this);
			return;
		}

		if (changeTrackOnEnter)
		{
			musicManager.ChangeTrack(targetTrackIndex);
		}

		musicManager.SetMixWeights(targetVocalsWeight, targetOthersWeight, targetBassWeight, targetDrumsWeight);
	}

	[ContextMenu("Debug: Simulate Player Exit")]
	public void SimulatePlayerExit()
	{
		if (musicManager == null)
		{
			musicManager = FindAnyObjectByType<UltimateAdaptiveMusicManager>();
		}

		musicManager?.SetExploreMode();
	}

	private void AutoAssignReferences()
	{
		if (playerController == null)
		{
			playerController = ArenaPromptEventUtility.ResolvePlayerController(playerTag);
		}

		if (musicManager == null)
		{
			musicManager = FindAnyObjectByType<UltimateAdaptiveMusicManager>();
		}
	}

	private void EnsureColliderIsTrigger()
	{
		if (triggerCollider == null)
		{
			triggerCollider = GetComponent<Collider>();
		}

		if (triggerCollider != null)
		{
			triggerCollider.isTrigger = true;
		}
	}
}