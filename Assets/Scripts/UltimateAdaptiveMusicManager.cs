using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public sealed class GameTrack
{
	public string trackName = "music0";
	public string triggerObjectName = "music0";
	public string resourceFolder = "AdaptiveMusic/music0";
	public AudioClip vocalsClip;
	public AudioClip othersClip;
	public AudioClip bassClip;
	public AudioClip drumsClip;

	public bool HasAnyClip => vocalsClip != null || othersClip != null || bassClip != null || drumsClip != null;
}

[DisallowMultipleComponent]
[AddComponentMenu("Audio/Ultimate Adaptive Music Manager")]
public class UltimateAdaptiveMusicManager : MonoBehaviour
{
	private const string ManagerObjectName = "Ultimate Adaptive Music Manager";
	private const float ScheduledPlaybackLeadTime = 0.05f;
	private static readonly string[] DefaultTrackNames = { "music0", "music1", "music2" };
	private static readonly string[] DefaultResourceFolders =
	{
		"AdaptiveMusic/music0",
		"AdaptiveMusic/music1",
		"AdaptiveMusic/music2"
	};

	[Header("References")]
	[SerializeField] private FirstPersonViewAnimationController attackController;
	[SerializeField] private CharacterController playerController;
	[SerializeField] private string playerTag = "Player";

	[Header("Audio Track Players")]
	public AudioSource vocalsSource;
	public AudioSource othersSource;
	public AudioSource bassSource;
	public AudioSource drumsSource;

	[Header("Playlist Configuration")]
	public List<GameTrack> playList = new List<GameTrack>();
	public int currentTrackIndex;

	[Header("Real-time Volume Weights (0.0 - 1.0)")]
	[Range(0f, 1f)] public float vocalsWeight = 1f;
	[Range(0f, 1f)] public float othersWeight = 1f;
	[Range(0f, 1f)] public float bassWeight = 1f;
	[Range(0f, 1f)] public float drumsWeight = 1f;
	[SerializeField, Range(0f, 1f)] private float masterVolume = 0.85f;

	[Header("Smoothing Parameters")]
	[SerializeField, Min(0f)] private float fadeSpeed = 2.5f;

	[Header("Playback Gate")]
	[SerializeField] private bool playOnFirstAttack = true;

	private float currentVocalsVolume;
	private float currentOthersVolume;
	private float currentBassVolume;
	private float currentDrumsVolume;
	private bool playbackUnlocked;
	private bool playbackStarted;
	private bool subscribedToAttackEvent;

	public CharacterController PlayerController => playerController;

	public static UltimateAdaptiveMusicManager EnsureInScene(Component host, FirstPersonViewAnimationController explicitAttackController, CharacterController explicitPlayerController = null)
	{
		UltimateAdaptiveMusicManager manager = FindAnyObjectByType<UltimateAdaptiveMusicManager>();
		if (manager == null)
		{
			if (!HasAnyNamedMusicTrigger())
			{
				return null;
			}

			GameObject managerObject = new GameObject(ManagerObjectName);
			if (host != null)
			{
				managerObject.transform.SetParent(host.transform, false);
			}

			manager = managerObject.AddComponent<UltimateAdaptiveMusicManager>();
		}

		manager.InitializeRuntime(explicitAttackController, explicitPlayerController);
		return manager;
	}

	private void Awake()
	{
		InitializeRuntime(attackController, playerController);
	}

	private void OnEnable()
	{
		InitializeRuntime(attackController, playerController);
	}

	private void OnDisable()
	{
		UnsubscribeFromAttackEvent();
	}

	private void Update()
	{
		float step = Mathf.Max(0f, fadeSpeed) * Time.unscaledDeltaTime;
		currentVocalsVolume = Mathf.MoveTowards(currentVocalsVolume, Mathf.Clamp01(vocalsWeight), step);
		currentOthersVolume = Mathf.MoveTowards(currentOthersVolume, Mathf.Clamp01(othersWeight), step);
		currentBassVolume = Mathf.MoveTowards(currentBassVolume, Mathf.Clamp01(bassWeight), step);
		currentDrumsVolume = Mathf.MoveTowards(currentDrumsVolume, Mathf.Clamp01(drumsWeight), step);

		ApplySourceVolumes();
	}

	public void InitializeRuntime(FirstPersonViewAnimationController explicitAttackController, CharacterController explicitPlayerController = null)
	{
		if (explicitAttackController != null)
		{
			attackController = explicitAttackController;
		}

		if (explicitPlayerController != null)
		{
			playerController = explicitPlayerController;
		}

		AutoAssignReferences();
		EnsureSources();
		EnsureDefaultPlaylist();
		ClampCurrentTrackIndex();
		EnsureTrackClipsLoaded(currentTrackIndex);
		AssignTrackClips(currentTrackIndex);
		EnsureMusicTriggers();
		SubscribeToAttackEvent();

		if (!playOnFirstAttack)
		{
			playbackUnlocked = true;
			StartPlaybackIfPossible();
		}
	}

	public void ChangeTrack(int index)
	{
		if (!IsValidTrackIndex(index))
		{
			return;
		}

		currentTrackIndex = index;
		EnsureTrackClipsLoaded(index);
		AssignTrackClips(index);

		if (playbackUnlocked)
		{
			StartPlaybackIfPossible(forceRestart: true);
		}
	}

	public void SetMixWeights(float targetVocalsWeight, float targetOthersWeight, float targetBassWeight, float targetDrumsWeight)
	{
		vocalsWeight = Mathf.Clamp01(targetVocalsWeight);
		othersWeight = Mathf.Clamp01(targetOthersWeight);
		bassWeight = Mathf.Clamp01(targetBassWeight);
		drumsWeight = Mathf.Clamp01(targetDrumsWeight);
	}

	public void SetExploreMode()
	{
		SetMixWeights(0f, 1f, 0f, 0f);
	}

	public bool IsPlayerCollider(Collider other)
	{
		if (other == null)
		{
			return false;
		}

		if (playerController == null)
		{
			playerController = ArenaPromptEventUtility.ResolvePlayerController(playerTag);
		}

		if (playerController == null)
		{
			return false;
		}

		CharacterController enteredController = other.GetComponent<CharacterController>();
		if (enteredController == playerController)
		{
			return true;
		}

		return other.GetComponentInParent<CharacterController>() == playerController;
	}

	private void HandleAttackStateEntered(int attackNumber, int attackSequenceId)
	{
		if (playbackUnlocked)
		{
			return;
		}

		playbackUnlocked = true;
		StartPlaybackIfPossible();
	}

	private void AutoAssignReferences()
	{
		if (attackController == null)
		{
			attackController = FindAnyObjectByType<FirstPersonViewAnimationController>();
		}

		if (playerController == null)
		{
			playerController = ArenaPromptEventUtility.ResolvePlayerController(playerTag);
		}
	}

	private void EnsureSources()
	{
		vocalsSource = GetOrCreateSource(vocalsSource, "Vocals Source");
		othersSource = GetOrCreateSource(othersSource, "Others Source");
		bassSource = GetOrCreateSource(bassSource, "Bass Source");
		drumsSource = GetOrCreateSource(drumsSource, "Drums Source");
	}

	private AudioSource GetOrCreateSource(AudioSource existingSource, string childName)
	{
		if (existingSource != null)
		{
			ConfigureSource(existingSource);
			return existingSource;
		}

		Transform child = transform.Find(childName);
		if (child == null)
		{
			child = new GameObject(childName).transform;
			child.SetParent(transform, false);
		}

		AudioSource source = child.GetComponent<AudioSource>();
		if (source == null)
		{
			source = child.gameObject.AddComponent<AudioSource>();
		}

		ConfigureSource(source);
		return source;
	}

	private static void ConfigureSource(AudioSource source)
	{
		if (source == null)
		{
			return;
		}

		source.playOnAwake = false;
		source.loop = true;
		source.spatialBlend = 0f;
		source.volume = 0f;
	}

	private void EnsureDefaultPlaylist()
	{
		if (playList.Count > 0)
		{
			return;
		}

		for (int index = 0; index < DefaultTrackNames.Length; index++)
		{
			playList.Add(new GameTrack
			{
				trackName = DefaultTrackNames[index],
				triggerObjectName = DefaultTrackNames[index],
				resourceFolder = DefaultResourceFolders[index]
			});
		}
	}

	private void EnsureMusicTriggers()
	{
		for (int index = 0; index < playList.Count; index++)
		{
			GameTrack track = playList[index];
			GameObject triggerObject = FindSceneObject(string.IsNullOrWhiteSpace(track.triggerObjectName) ? track.trackName : track.triggerObjectName);
			if (triggerObject == null)
			{
				continue;
			}

			Collider triggerCollider = triggerObject.GetComponent<Collider>();
			if (triggerCollider != null)
			{
				triggerCollider.isTrigger = true;
			}

			MusicTriggerBox triggerBox = triggerObject.GetComponent<MusicTriggerBox>();
			bool wasCreated = false;
			if (triggerBox == null)
			{
				triggerBox = triggerObject.AddComponent<MusicTriggerBox>();
				wasCreated = true;
			}

			triggerBox.AssignManager(this);
			if (wasCreated)
			{
				triggerBox.ApplyDefaultSetup(index);
			}
		}
	}

	private void EnsureTrackClipsLoaded(int index)
	{
		if (!IsValidTrackIndex(index))
		{
			return;
		}

		GameTrack track = playList[index];
		if (track.HasAnyClip)
		{
			return;
		}

		if (string.IsNullOrWhiteSpace(track.resourceFolder))
		{
			return;
		}

		AudioClip[] clips = Resources.LoadAll<AudioClip>(track.resourceFolder);
		for (int clipIndex = 0; clipIndex < clips.Length; clipIndex++)
		{
			AudioClip clip = clips[clipIndex];
			if (clip == null)
			{
				continue;
			}

			string clipName = clip.name.ToLowerInvariant();
			if (clipName.Contains("_vocals"))
			{
				track.vocalsClip = clip;
			}
			else if (clipName.Contains("_other") || clipName.Contains("_others"))
			{
				track.othersClip = clip;
			}
			else if (clipName.Contains("_bass"))
			{
				track.bassClip = clip;
			}
			else if (clipName.Contains("_drums"))
			{
				track.drumsClip = clip;
			}
		}

		if (!track.HasAnyClip)
		{
			Debug.LogWarning($"{nameof(UltimateAdaptiveMusicManager)} could not load any clips from Resources/{track.resourceFolder}.", this);
		}
	}

	private void AssignTrackClips(int index)
	{
		if (!IsValidTrackIndex(index))
		{
			return;
		}

		GameTrack track = playList[index];
		if (vocalsSource != null)
		{
			vocalsSource.clip = track.vocalsClip;
		}

		if (othersSource != null)
		{
			othersSource.clip = track.othersClip;
		}

		if (bassSource != null)
		{
			bassSource.clip = track.bassClip;
		}

		if (drumsSource != null)
		{
			drumsSource.clip = track.drumsClip;
		}
	}

	private void StartPlaybackIfPossible(bool forceRestart = false)
	{
		if (!HasAnyAssignedClip())
		{
			return;
		}

		if (playbackStarted && !forceRestart)
		{
			return;
		}

		StopAllSources();
		double scheduledTime = AudioSettings.dspTime + ScheduledPlaybackLeadTime;
		ScheduleSource(vocalsSource, scheduledTime);
		ScheduleSource(othersSource, scheduledTime);
		ScheduleSource(bassSource, scheduledTime);
		ScheduleSource(drumsSource, scheduledTime);
		playbackStarted = true;
	}

	private static void ScheduleSource(AudioSource source, double scheduledTime)
	{
		if (source == null || source.clip == null)
		{
			return;
		}

		source.PlayScheduled(scheduledTime);
	}

	private void StopAllSources()
	{
		StopSource(vocalsSource);
		StopSource(othersSource);
		StopSource(bassSource);
		StopSource(drumsSource);
	}

	private static void StopSource(AudioSource source)
	{
		if (source == null)
		{
			return;
		}

		source.Stop();
	}

	private void ApplySourceVolumes()
	{
		ApplySourceVolume(vocalsSource, currentVocalsVolume);
		ApplySourceVolume(othersSource, currentOthersVolume);
		ApplySourceVolume(bassSource, currentBassVolume);
		ApplySourceVolume(drumsSource, currentDrumsVolume);
	}

	private void ApplySourceVolume(AudioSource source, float weight)
	{
		if (source == null)
		{
			return;
		}

		source.volume = Mathf.Clamp01(weight) * Mathf.Clamp01(masterVolume);
	}

	private void SubscribeToAttackEvent()
	{
		if (subscribedToAttackEvent || attackController == null)
		{
			return;
		}

		attackController.AttackStateEnteredEvent += HandleAttackStateEntered;
		subscribedToAttackEvent = true;
	}

	private void UnsubscribeFromAttackEvent()
	{
		if (!subscribedToAttackEvent || attackController == null)
		{
			return;
		}

		attackController.AttackStateEnteredEvent -= HandleAttackStateEntered;
		subscribedToAttackEvent = false;
	}

	private void ClampCurrentTrackIndex()
	{
		if (playList.Count == 0)
		{
			currentTrackIndex = 0;
			return;
		}

		currentTrackIndex = Mathf.Clamp(currentTrackIndex, 0, playList.Count - 1);
	}

	private bool IsValidTrackIndex(int index)
	{
		return index >= 0 && index < playList.Count;
	}

	private bool HasAnyAssignedClip()
	{
		return (vocalsSource != null && vocalsSource.clip != null) ||
			(othersSource != null && othersSource.clip != null) ||
			(bassSource != null && bassSource.clip != null) ||
			(drumsSource != null && drumsSource.clip != null);
	}

	private static bool HasAnyNamedMusicTrigger()
	{
		for (int index = 0; index < DefaultTrackNames.Length; index++)
		{
			if (FindSceneObject(DefaultTrackNames[index]) != null)
			{
				return true;
			}
		}

		return false;
	}

	private static GameObject FindSceneObject(string objectName)
	{
		if (string.IsNullOrWhiteSpace(objectName))
		{
			return null;
		}

		Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		for (int index = 0; index < transforms.Length; index++)
		{
			Transform candidate = transforms[index];
			if (candidate == null || !candidate.gameObject.scene.IsValid())
			{
				continue;
			}

			if (string.Equals(candidate.name, objectName, System.StringComparison.Ordinal))
			{
				return candidate.gameObject;
			}
		}

		return null;
	}
}