using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
[AddComponentMenu("Game/Level Manager")]
public class LevelManager : MonoBehaviour
{
	public enum RetryKeyAction
	{
		Disabled,
		SoftReset,
		ReloadCurrentLevel,
	}

	public event Action<float> RunCompleted;

	[Header("References")]
	[SerializeField] private ArenaEncounterFlow arenaEncounterFlow;
	[SerializeField] private ArenaRunTimerDisplay runTimerDisplay;
	[SerializeField] private ArenaWallLeaderboardDisplay wallLeaderboardDisplay;
	[SerializeField] private ArenaTutorialSceneController tutorialSceneController;
	[SerializeField] private FirstPersonViewAnimationController attackController;
	[SerializeField] private LevelStartAttackGate levelStartAttackGate;
	[SerializeField] private SprayPaint sprayPaint;

	[Header("Run Tracking")]
	[SerializeField] private string playerLeaderboardName = "PLAYER";

	[Header("Level Control")]
	[SerializeField] private RetryKeyAction retryKeyAction = RetryKeyAction.SoftReset;
	[SerializeField] private KeyCode retryKey = KeyCode.R;

	private bool encounterEventsBound;
	private bool sceneLoadRequested;
	private bool scoreSubmitted;

	private void Awake()
	{
		AutoAssignReferences();
	}

	private void OnEnable()
	{
		AutoAssignReferences();
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
			EnsureSceneBuilt();
			SubscribeEncounterEvents();
			runTimerDisplay?.ResetRun();
			levelStartAttackGate?.BeginGate();
			return;
		}

		attackController?.ResetViewState();
		sprayPaint?.ClearAllSpray();
		runTimerDisplay?.ResetRun();
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
		if (retryKeyAction == RetryKeyAction.Disabled)
		{
			return;
		}

		bool retryPressed = Input.GetKeyDown(retryKey);
		if (!retryPressed && retryKey == KeyCode.R && Keyboard.current != null)
		{
			retryPressed = Keyboard.current.rKey.wasPressedThisFrame;
		}

		if (!retryPressed)
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

		if (levelStartAttackGate == null)
		{
			levelStartAttackGate = FindAnyObjectByType<LevelStartAttackGate>();
		}

		if (sprayPaint == null)
		{
			sprayPaint = FindAnyObjectByType<SprayPaint>();
		}
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
		PersistLeaderboardEntries();
		return true;
	}

	private void OnValidate()
	{
		if (retryKey == KeyCode.None)
		{
			retryKey = KeyCode.R;
		}
		
		if (string.IsNullOrWhiteSpace(playerLeaderboardName))
		{
			playerLeaderboardName = "PLAYER";
		}
	}
}