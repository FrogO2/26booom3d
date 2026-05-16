using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[AddComponentMenu("Game/Level Manager")]
public class LevelManager : MonoBehaviour
{
	public enum RetryKeyAction
	{
		[InspectorName("Disabled")]
		Disabled,
		[InspectorName("Soft Reset")]
		SoftReset,
		[InspectorName("Reload Current Level")]
		ReloadCurrentLevel,
	}

	public event Action<float> RunCompleted;

	[Header("References")]
	[SerializeField] private ArenaEncounterFlow arenaEncounterFlow;
	[SerializeField] private ArenaRunTimerDisplay runTimerDisplay;
	[SerializeField] private ArenaWallLeaderboardDisplay wallLeaderboardDisplay;
	[SerializeField] private ArenaTutorialSceneController tutorialSceneController;
	[SerializeField] private FirstPersonController firstPersonController;
	[SerializeField] private FirstPersonViewAnimationController attackController;
	[SerializeField] private LevelStartAttackGate levelStartAttackGate;
	[SerializeField] private SprayPaint sprayPaint;
	[SerializeField] private Transform playerResetPoint;
	[SerializeField] private InputActionAsset inputActions;
	[SerializeField] private string actionMapName = "Player";
	[SerializeField] private string reloadActionName = "Reload";

	[Header("Run Tracking")]
	[SerializeField] private string playerLeaderboardName = "PLAYER";

	[Header("Level Control")]
	[SerializeField, Tooltip("Choose whether pressing Reload performs a soft reset or reloads the current level.")]
	private RetryKeyAction retryKeyAction = RetryKeyAction.SoftReset;

	private bool encounterEventsBound;
	private bool sceneLoadRequested;
	private bool scoreSubmitted;
	private PlayerOneHitDeath playerOneHitDeath;
	private Transform playerTransform;
	private CharacterController playerCharacterController;
	private Camera playerCamera;
	private InputAction reloadAction;
	private Vector3 defaultPlayerResetPosition;
	private Quaternion defaultPlayerResetRotation = Quaternion.identity;
	private bool defaultPlayerResetCaptured;
	private readonly List<ArenaBakedEnemyTarget> resettableEnemyTargets = new List<ArenaBakedEnemyTarget>();

	private void Awake()
	{
		AutoAssignReferences();
		CaptureDefaultPlayerResetPoseIfNeeded();
		BindReloadAction();
	}

	private void OnEnable()
	{
		AutoAssignReferences();
		CaptureDefaultPlayerResetPoseIfNeeded();
		BindReloadAction();
		EnsureSceneBuilt();
		SubscribeEncounterEvents();
		sceneLoadRequested = false;
	}

	private void Update()
	{
		HandleRetryInput();
	}

	private void OnDisable()
	{
		DisableReloadAction();
		UnsubscribeEncounterEvents();
		PersistLeaderboardEntries();
	}

	public void Configure(ArenaEncounterFlow encounterFlow, ArenaRunTimerDisplay timerDisplay, ArenaWallLeaderboardDisplay leaderboardDisplay, string leaderboardName)
	{
		if (arenaEncounterFlow != encounterFlow)
		{
			UnsubscribeEncounterEvents();
			arenaEncounterFlow = encounterFlow;
		}

		if (timerDisplay != null)
		{
			runTimerDisplay = timerDisplay;
		}

		if (leaderboardDisplay != null)
		{
			wallLeaderboardDisplay = leaderboardDisplay;
		}

		if (!string.IsNullOrWhiteSpace(leaderboardName))
		{
			playerLeaderboardName = leaderboardName;
		}

		AutoAssignReferences();
		BindReloadAction();
		EnsureSceneBuilt();
		SubscribeEncounterEvents();
	}

	public void SoftResetLevel()
	{
		PersistLeaderboardEntries();
		scoreSubmitted = false;

		if (tutorialSceneController != null)
		{
			tutorialSceneController.SoftResetLevelRuntime();
			AutoAssignReferences();
			BindReloadAction();
			EnsureSceneBuilt();
			SubscribeEncounterEvents();
		}
		else
		{
			ResetAllEnemies();
			ResetPlayerToStart();
		}

		ResetRunState();
		arenaEncounterFlow?.ResetEncounter();
		levelStartAttackGate?.BeginGate();
	}

	public void ReloadCurrentLevel()
	{
		if (!TryBeginSceneLoad())
		{
			return;
		}

		Scene currentScene = gameObject.scene;
		if (currentScene.buildIndex >= 0)
		{
			SceneManager.LoadScene(currentScene.buildIndex);
			return;
		}

		SceneManager.LoadScene(currentScene.name);
	}

	public void LoadCurrentLevel()
	{
		ReloadCurrentLevel();
	}

	public void LoadNextLevel()
	{
		if (!TryBeginSceneLoad())
		{
			return;
		}

		Scene currentScene = gameObject.scene;
		if (currentScene.buildIndex < 0)
		{
			Debug.LogWarning($"{nameof(LevelManager)} cannot load the next level because the active scene is not in Build Settings.", this);
			return;
		}

		int nextBuildIndex = currentScene.buildIndex + 1;
		if (nextBuildIndex >= SceneManager.sceneCountInBuildSettings)
		{
			Debug.LogWarning($"{nameof(LevelManager)} cannot load the next level because there is no next scene in Build Settings.", this);
			return;
		}

		SceneManager.LoadScene(nextBuildIndex);
	}

	public void LoadLevel(int buildIndex)
	{
		if (!TryBeginSceneLoad())
		{
			return;
		}

		SceneManager.LoadScene(buildIndex);
	}

	public void LoadLevel(string sceneName)
	{
		if (string.IsNullOrWhiteSpace(sceneName))
		{
			return;
		}

		if (!TryBeginSceneLoad())
		{
			return;
		}

		SceneManager.LoadScene(sceneName);
	}

	private void HandleRetryInput()
	{
		if (retryKeyAction == RetryKeyAction.Disabled || reloadAction == null)
		{
			return;
		}

		if (!reloadAction.WasPressedThisFrame())
		{
			return;
		}

		switch (retryKeyAction)
		{
			case RetryKeyAction.SoftReset:
				SoftResetLevel();
				break;

			case RetryKeyAction.ReloadCurrentLevel:
				ReloadCurrentLevel();
				break;
		}
	}

	private void HandleRunStarted()
	{
		scoreSubmitted = false;
		if (runTimerDisplay != null && !runTimerDisplay.HasStarted)
		{
			runTimerDisplay.BeginRun();
		}
	}

	private void HandleRunExitReached()
	{
		float elapsedSeconds = 0f;
		if (runTimerDisplay != null)
		{
			if (!runTimerDisplay.HasFinished)
			{
				runTimerDisplay.FinishRun();
			}

			if (runTimerDisplay.HasStarted)
			{
				elapsedSeconds = runTimerDisplay.ElapsedSeconds;
			}
		}

		if (!scoreSubmitted && wallLeaderboardDisplay != null && elapsedSeconds > 0f)
		{
			wallLeaderboardDisplay.SubmitScore(playerLeaderboardName, elapsedSeconds);
			scoreSubmitted = true;
		}

		RunCompleted?.Invoke(elapsedSeconds);
	}

	private void AutoAssignReferences()
	{
		if (tutorialSceneController == null)
		{
			tutorialSceneController = GetComponent<ArenaTutorialSceneController>();
		}

		if (arenaEncounterFlow == null)
		{
			arenaEncounterFlow = GetComponent<ArenaEncounterFlow>();
			if (arenaEncounterFlow == null)
			{
				arenaEncounterFlow = FindAnyObjectByType<ArenaEncounterFlow>();
			}
		}

		if (runTimerDisplay == null)
		{
			runTimerDisplay = GetComponentInChildren<ArenaRunTimerDisplay>(true);
			if (runTimerDisplay == null)
			{
				runTimerDisplay = FindAnyObjectByType<ArenaRunTimerDisplay>();
			}
		}

		if (wallLeaderboardDisplay == null)
		{
			wallLeaderboardDisplay = GetComponentInChildren<ArenaWallLeaderboardDisplay>(true);
			if (wallLeaderboardDisplay == null)
			{
				wallLeaderboardDisplay = FindAnyObjectByType<ArenaWallLeaderboardDisplay>();
			}
		}

		if (attackController == null)
		{
			attackController = FindAnyObjectByType<FirstPersonViewAnimationController>();
		}

		if (firstPersonController == null)
		{
			firstPersonController = FindAnyObjectByType<FirstPersonController>();
		}

		if (firstPersonController != null)
		{
			playerTransform = firstPersonController.transform;
			playerCharacterController = firstPersonController.GetComponent<CharacterController>();
			playerCamera = firstPersonController.PlayerCamera;
			playerOneHitDeath = firstPersonController.GetComponent<PlayerOneHitDeath>();

			if (inputActions == null)
			{
				inputActions = firstPersonController.InputActions;
			}

			if (string.IsNullOrWhiteSpace(actionMapName))
			{
				actionMapName = firstPersonController.ActionMapName;
			}
		}
		else
		{
			GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
			if (taggedPlayer != null)
			{
				playerTransform = taggedPlayer.transform;
				playerCharacterController = taggedPlayer.GetComponent<CharacterController>();
				if (playerCharacterController == null)
				{
					playerCharacterController = taggedPlayer.GetComponentInChildren<CharacterController>(true);
				}

				if (playerCamera == null)
				{
					playerCamera = taggedPlayer.GetComponentInChildren<Camera>(true);
				}

				if (playerOneHitDeath == null)
				{
					playerOneHitDeath = taggedPlayer.GetComponent<PlayerOneHitDeath>();
				}
			}
		}

		if (levelStartAttackGate == null)
		{
			levelStartAttackGate = FindAnyObjectByType<LevelStartAttackGate>();
		}

		if (sprayPaint == null)
		{
			sprayPaint = playerTransform != null ? playerTransform.GetComponent<SprayPaint>() : null;
			if (sprayPaint == null)
			{
				sprayPaint = FindAnyObjectByType<SprayPaint>();
			}
		}

		if (inputActions == null)
		{
			PlayerInput playerInput = FindAnyObjectByType<PlayerInput>();
			if (playerInput != null)
			{
				inputActions = playerInput.actions;
			}
		}

		RefreshResettableEnemyTargets();
	}

	private void BindReloadAction()
	{
		DisableReloadAction();

		if (inputActions == null || string.IsNullOrWhiteSpace(actionMapName) || string.IsNullOrWhiteSpace(reloadActionName))
		{
			return;
		}

		InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);
		if (actionMap == null)
		{
			return;
		}

		reloadAction = actionMap.FindAction(reloadActionName, false);
		if (isActiveAndEnabled && reloadAction != null)
		{
			reloadAction.Enable();
		}
	}

	private void DisableReloadAction()
	{
		if (reloadAction == null)
		{
			return;
		}

		reloadAction.Disable();
		reloadAction = null;
	}

	private void ResetAllEnemies()
	{
		RefreshResettableEnemyTargets();

		for (int i = 0; i < resettableEnemyTargets.Count; i++)
		{
			if (resettableEnemyTargets[i] != null)
			{
				resettableEnemyTargets[i].ResetToInitialState();
			}
		}
	}

	private void RefreshResettableEnemyTargets()
	{
		resettableEnemyTargets.Clear();

		ArenaBakedEnemyTarget[] discoveredTargets = UnityEngine.Object.FindObjectsByType<ArenaBakedEnemyTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		for (int i = 0; i < discoveredTargets.Length; i++)
		{
			ArenaBakedEnemyTarget target = discoveredTargets[i];
			if (target == null)
			{
				continue;
			}

			Scene targetScene = target.gameObject.scene;
			if (!targetScene.IsValid() || !targetScene.isLoaded)
			{
				continue;
			}

			resettableEnemyTargets.Add(target);
		}
	}

	private void ResetPlayerToStart()
	{
		if (playerResetPoint == null)
		{
			if (!defaultPlayerResetCaptured)
			{
				Debug.LogWarning($"{nameof(LevelManager)} on {name} is missing a player reset point and could not capture a default player pose for soft reset.", this);
				return;
			}

			ResetPlayerToPose(defaultPlayerResetPosition, defaultPlayerResetRotation);
			return;
		}

		ResetPlayerToPose(playerResetPoint.position, playerResetPoint.rotation);
	}

	private void ResetPlayerToPose(Vector3 position, Quaternion rotation)
	{
		if (firstPersonController != null)
		{
			firstPersonController.ResetToSpawn(position, rotation);
			playerOneHitDeath?.ResetDeathState();
			return;
		}

		if (playerTransform == null)
		{
			Debug.LogWarning($"{nameof(LevelManager)} on {name} could not resolve a player transform for soft reset.", this);
			return;
		}

		if (playerCharacterController != null)
		{
			playerCharacterController.enabled = false;
		}

		playerTransform.SetPositionAndRotation(position, rotation);

		if (playerCharacterController != null)
		{
			playerCharacterController.enabled = true;
		}

		if (playerCamera != null && playerCamera.transform.parent != null)
		{
			playerCamera.transform.parent.localRotation = Quaternion.identity;
		}

		playerOneHitDeath?.ResetDeathState();
	}

	private void CaptureDefaultPlayerResetPoseIfNeeded()
	{
		if (defaultPlayerResetCaptured || playerTransform == null)
		{
			return;
		}

		defaultPlayerResetPosition = playerTransform.position;
		defaultPlayerResetRotation = playerTransform.rotation;
		defaultPlayerResetCaptured = true;
	}

	private void ResetRunState()
	{
		attackController?.ResetViewState();
		sprayPaint?.ClearAllSpray();
		runTimerDisplay?.ResetRun();
	}

	private void EnsureSceneBuilt()
	{
		runTimerDisplay?.EnsureSceneBuilt();
		wallLeaderboardDisplay?.EnsureSceneBuilt();
	}

	private void SubscribeEncounterEvents()
	{
		if (encounterEventsBound || arenaEncounterFlow == null)
		{
			return;
		}

		arenaEncounterFlow.RunStarted += HandleRunStarted;
		arenaEncounterFlow.RunExitReached += HandleRunExitReached;
		encounterEventsBound = true;
	}

	private void UnsubscribeEncounterEvents()
	{
		if (!encounterEventsBound || arenaEncounterFlow == null)
		{
			return;
		}

		arenaEncounterFlow.RunStarted -= HandleRunStarted;
		arenaEncounterFlow.RunExitReached -= HandleRunExitReached;
		encounterEventsBound = false;
	}

	private void PersistLeaderboardEntries()
	{
		wallLeaderboardDisplay?.PersistCurrentEntries();
	}

	private bool TryBeginSceneLoad()
	{
		if (sceneLoadRequested)
		{
			return false;
		}

		sceneLoadRequested = true;
		sprayPaint?.ClearAllSpray();
		PersistLeaderboardEntries();
		return true;
	}

	private void OnValidate()
	{
		if (string.IsNullOrWhiteSpace(playerLeaderboardName))
		{
			playerLeaderboardName = "PLAYER";
		}

		if (string.IsNullOrWhiteSpace(actionMapName))
		{
			actionMapName = "Player";
		}

		if (string.IsNullOrWhiteSpace(reloadActionName))
		{
			reloadActionName = "Reload";
		}
	}
}