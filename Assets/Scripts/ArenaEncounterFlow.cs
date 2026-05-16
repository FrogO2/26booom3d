using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Arena/Encounter Flow")]
public class ArenaEncounterFlow : MonoBehaviour
{
	public event Action EncounterStarted;
	public event Action<int> RemainingEnemiesChanged;
	public event Action ExitRequestedBeforeStart;
	public event Action ExitRequestedWhileLocked;
	public event Action EncounterCleared;
	public event Action RunStarted;
	public event Action RunExitReached;

	[SerializeField] private Transform player;
	[SerializeField] private CharacterController playerController;
	[SerializeField] private Camera playerCamera;
	[SerializeField] private GameObject entryBarrier;
	[SerializeField] private GameObject exitBarrier;
	[SerializeField] private ArenaGuidanceArrow exitArrow;
	[SerializeField] private ArenaPromptOverlay promptOverlay;
	[SerializeField] private List<ArenaBakedEnemyTarget> enemyTargets = new List<ArenaBakedEnemyTarget>();
	[SerializeField] private bool showSceneIntroOnStart = true;
	[SerializeField] private bool showInitialTutorialPrompt = true;
	[SerializeField] private string initialTutorialPrompt = "Tutorial 1/3\nMove: WASD, Jump: Space, Sprint: Shift";
	[SerializeField] private float promptDuration = 4f;
	[SerializeField] private float counterDuration = 1.8f;
	[SerializeField] private Color initialTutorialPromptColor = new Color(0.26f, 0.90f, 1f);

	private readonly List<ArenaBakedEnemyTarget> arenaEnemies = new List<ArenaBakedEnemyTarget>();
	private SceneIntroOverlay sceneIntroOverlay;
	private bool usesStandalonePresentation;
	private bool arenaStarted;
	private bool arenaCleared;
	private bool runStartNotified;
	private Coroutine initialPresentationRoutine;

	public bool HasStarted => arenaStarted;
	public bool IsCleared => arenaCleared;

	private void Awake()
	{
		usesStandalonePresentation = GetComponent<ArenaTutorialSceneController>() == null;
		EnsureBindings();
	}

	private void Start()
	{
		ResetEncounter();
		if (!usesStandalonePresentation)
		{
			return;
		}

		if (showSceneIntroOnStart)
		{
			sceneIntroOverlay?.PlayIntro();
		}

		if (showInitialTutorialPrompt)
		{
			initialPresentationRoutine = StartCoroutine(ShowInitialTutorialPromptWhenReady());
		}
	}

	private IEnumerator ShowInitialTutorialPromptWhenReady()
	{
		yield return null;

		float introDelay = showSceneIntroOnStart && sceneIntroOverlay != null ? sceneIntroOverlay.IntroDuration * 0.85f : 0f;
		if (introDelay > 0.05f)
		{
			yield return new WaitForSecondsRealtime(introDelay);
		}

		initialPresentationRoutine = null;
		promptOverlay?.ShowPrompt(initialTutorialPrompt, promptDuration, ArenaPromptColorMode.AdaptiveContrast, initialTutorialPromptColor);
	}

	private void OnDestroy()
	{
		if (initialPresentationRoutine != null)
		{
			StopCoroutine(initialPresentationRoutine);
			initialPresentationRoutine = null;
		}
	}

	public void BindPlayer(Transform playerTransform, CharacterController controller, Camera camera)
	{
		if (playerTransform != null)
		{
			player = playerTransform;
		}

		if (controller != null)
		{
			playerController = controller;
		}

		if (camera != null)
		{
			playerCamera = camera;
		}

		ResolvePlayerReferences();
		EnsureArenaKillCoordinator();
	}

	public void ConfigureSceneObjects(GameObject entryBarrierObject, GameObject exitBarrierObject, ArenaGuidanceArrow exitArrowIndicator)
	{
		if (entryBarrierObject != null)
		{
			entryBarrier = entryBarrierObject;
		}

		if (exitBarrierObject != null)
		{
			exitBarrier = exitBarrierObject;
		}

		if (exitArrowIndicator != null)
		{
			exitArrow = exitArrowIndicator;
		}

		promptOverlay?.SetCamera(playerCamera);
		promptOverlay?.EnsureSceneBuilt();
		exitArrow?.EnsureSceneBuilt();
	}

	public void BindEnemies(IEnumerable<ArenaBakedEnemyTarget> enemies)
	{
		List<ArenaBakedEnemyTarget> resolvedEnemies = new List<ArenaBakedEnemyTarget>();
		if (enemies != null)
		{
			foreach (ArenaBakedEnemyTarget enemy in enemies)
			{
				if (enemy != null)
				{
					resolvedEnemies.Add(enemy);
				}
			}
		}

		arenaEnemies.Clear();
		enemyTargets.Clear();

		for (int i = 0; i < resolvedEnemies.Count; i++)
		{
			ArenaBakedEnemyTarget enemy = resolvedEnemies[i];
			enemy.Initialize();
			EnsureEncounterTargetLink(enemy);
			arenaEnemies.Add(enemy);
			enemyTargets.Add(enemy);
		}
	}

	public void EnsureBindings()
	{
		ResolvePlayerReferences();
		EnsureArenaKillCoordinator();
		ResolveEnemyTargets();
		ResolveStandalonePresentation();
		ConfigureSceneObjects(entryBarrier, exitBarrier, exitArrow);
		BindEnemies(enemyTargets);
	}

	public void ResetEncounter()
	{
		arenaStarted = false;
		arenaCleared = false;
		runStartNotified = false;

		if (entryBarrier != null)
		{
			entryBarrier.SetActive(false);
		}

		if (exitBarrier != null)
		{
			exitBarrier.SetActive(false);
		}

		exitArrow?.ResetIndicator();
		promptOverlay?.HideAll();

		for (int i = 0; i < arenaEnemies.Count; i++)
		{
			if (arenaEnemies[i] != null)
			{
				EnsureEncounterTargetLink(arenaEnemies[i]);
			}
		}
	}

	public bool IsPlayerCollider(Collider other)
	{
		if (playerController == null || other == null)
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

	public void HandleTutorialTrigger(ArenaBakedTriggerZone zone)
	{
		if (!usesStandalonePresentation || zone == null)
		{
			return;
		}

		promptOverlay?.ShowPrompt(zone.Message, promptDuration, zone.ColorMode, zone.SolidColor);
	}

	public void HandleTrigger(ArenaBakedTriggerZone zone)
	{
		if (zone == null)
		{
			return;
		}

		switch (zone.Kind)
		{
			case ArenaTriggerKind.ArenaStart:
				if (!arenaStarted)
				{
					arenaStarted = true;
					if (entryBarrier != null)
					{
						entryBarrier.SetActive(true);
					}
					if (exitBarrier != null)
					{
						exitBarrier.SetActive(true);
					}

					if (usesStandalonePresentation)
					{
						promptOverlay?.ShowPrompt("Arena active. Defeat every enemy to unlock the barriers.", promptDuration, ArenaPromptColorMode.AdaptiveContrast, new Color(1f, 0.55f, 0.55f));
						promptOverlay?.ShowCounter($"Enemies left: {GetRemainingEnemyCount()}", counterDuration);
					}

					NotifyRunStarted();
					EncounterStarted?.Invoke();
					RemainingEnemiesChanged?.Invoke(GetRemainingEnemyCount());
				}
				break;

			case ArenaTriggerKind.ArenaExit:
				if (!arenaStarted)
				{
					if (usesStandalonePresentation)
					{
						promptOverlay?.ShowPrompt("Enter the arena before heading for the exit.", promptDuration, ArenaPromptColorMode.AdaptiveContrast, Color.white);
					}

					ExitRequestedBeforeStart?.Invoke();
				}
				else if (!arenaCleared)
				{
					if (usesStandalonePresentation)
					{
						promptOverlay?.ShowPrompt("Exit locked. Defeat every enemy in the arena first.", promptDuration, ArenaPromptColorMode.AdaptiveContrast, Color.white);
					}

					ExitRequestedWhileLocked?.Invoke();
				}
				else
				{
					exitArrow?.Hide();

					if (usesStandalonePresentation)
					{
						promptOverlay?.ShowPrompt("Arena complete. Follow the path ahead.", promptDuration, ArenaPromptColorMode.AdaptiveContrast, Color.white);
					}

					RunExitReached?.Invoke();
				}
				break;
		}
	}

	public void NotifyEnemyKilled(ArenaBakedEnemyTarget enemy)
	{
		NotifyRunStarted();

		int remaining = GetRemainingEnemyCount();
		if (usesStandalonePresentation)
		{
			promptOverlay?.ShowCounter($"Enemies left: {remaining}", counterDuration);
		}

		RemainingEnemiesChanged?.Invoke(remaining);

		if (!arenaCleared && remaining <= 0)
		{
			arenaCleared = true;
			if (entryBarrier != null)
			{
				entryBarrier.SetActive(false);
			}
			if (exitBarrier != null)
			{
				exitBarrier.SetActive(false);
			}

			exitArrow?.Show();
			if (usesStandalonePresentation)
			{
				promptOverlay?.ShowPrompt("Arena cleared. Follow the ground arrow to leave.", promptDuration, ArenaPromptColorMode.AdaptiveContrast, Color.white);
			}

			EncounterCleared?.Invoke();
		}
	}

	private void NotifyRunStarted()
	{
		if (runStartNotified)
		{
			return;
		}

		runStartNotified = true;
		RunStarted?.Invoke();
	}


	private int GetRemainingEnemyCount()
	{
		int remaining = 0;
		for (int i = 0; i < arenaEnemies.Count; i++)
		{
			var enemy = arenaEnemies[i];
			if (enemy != null && enemy.IsAlive)
			{
				remaining++;
			}
		}
		return remaining;
	}

	private void ResolvePlayerReferences()
	{
		if (player == null)
		{
			GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
			if (taggedPlayer != null)
			{
				player = taggedPlayer.transform;
			}
		}

		if (playerController == null && player != null)
		{
			playerController = player.GetComponent<CharacterController>();
			if (playerController == null)
			{
				playerController = player.GetComponentInChildren<CharacterController>(true);
			}
		}

		if (playerCamera == null)
		{
			if (player != null)
			{
				playerCamera = player.GetComponentInChildren<Camera>(true);
			}

			if (playerCamera == null)
			{
				playerCamera = Camera.main;
			}
		}
	}

	private void ResolveEnemyTargets()
	{
		if (enemyTargets.Count > 0)
		{
			return;
		}

		ArenaBakedEnemyTarget[] discoveredTargets = GetComponentsInChildren<ArenaBakedEnemyTarget>(true);
		for (int i = 0; i < discoveredTargets.Length; i++)
		{
			ArenaBakedEnemyTarget target = discoveredTargets[i];
			if (target != null)
			{
				enemyTargets.Add(target);
			}
		}
	}

	private void EnsureArenaKillCoordinator()
	{
		if (player == null)
		{
			return;
		}

		ArenaKillCoordinator killCoordinator = player.GetComponent<ArenaKillCoordinator>();
		if (killCoordinator == null)
		{
			killCoordinator = player.gameObject.AddComponent<ArenaKillCoordinator>();
		}

		killCoordinator.Initialize();
	}

	private void EnsureEncounterTargetLink(ArenaBakedEnemyTarget enemy)
	{
		if (enemy == null)
		{
			return;
		}

		ArenaEncounterTargetLink link = enemy.GetComponent<ArenaEncounterTargetLink>();
		if (link == null)
		{
			link = enemy.gameObject.AddComponent<ArenaEncounterTargetLink>();
		}

		link.Initialize(this);
	}

	private void ResolveStandalonePresentation()
	{
		if (!usesStandalonePresentation)
		{
			return;
		}

		if (promptOverlay == null)
		{
			promptOverlay = GetComponent<ArenaPromptOverlay>();
			if (promptOverlay == null)
			{
				promptOverlay = gameObject.AddComponent<ArenaPromptOverlay>();
			}
		}

		promptOverlay.SetCamera(playerCamera);
		promptOverlay.EnsureSceneBuilt();

		if (!showSceneIntroOnStart)
		{
			return;
		}

		if (sceneIntroOverlay == null)
		{
			sceneIntroOverlay = GetComponent<SceneIntroOverlay>();
			if (sceneIntroOverlay == null)
			{
				sceneIntroOverlay = gameObject.AddComponent<SceneIntroOverlay>();
			}
		}

		sceneIntroOverlay.ConfigureArenaDefaults();
		sceneIntroOverlay.playOnStart = false;
		sceneIntroOverlay.EnsureSceneBuilt();
	}
}
