using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ArenaTutorialSceneController : MonoBehaviour
{
	private const string RuntimeRootObjectName = "Arena Tutorial Runtime";
	private const string PromptUiObjectName = "Arena Tutorial UI";
	private const string RunTimerObjectName = "Run Timer System";
	private const string LeaderboardObjectName = "Wall Leaderboard";
	private const string SceneIntroObjectName = "Scene Intro Overlay";
	private const string CourseFloorObjectName = "Course Floor";
	private const string EntryBarrierObjectName = "Arena Entry Barrier";
	private const string ExitBarrierObjectName = "Arena Exit Barrier";
	private const string ExitArrowObjectName = "Exit Arrow";
	private static readonly Vector3 CourseFloorOffset = new Vector3(0f, -0.5f, 42f);
	private static readonly Vector3 PlayerStartOffset = new Vector3(0f, 1.05f, 4f);
	private static readonly Vector3 ExitArrowOffset = new Vector3(0f, 0.08f, 68f);

	[Header("Prefabs")]
	[SerializeField] private GameObject knifeEnemyPrefab;
	[SerializeField] private GameObject gunEnemyPrefab;

	[Header("UI")]
	[SerializeField] private float promptDuration = 3f;
	[SerializeField] private float counterDuration = 3f;

	[Header("Layout")]
	[SerializeField] private Vector3 courseOrigin = new Vector3(120f, 0f, 0f);

	[Header("Fake Kill")]
	[SerializeField] private float fakeKillRange = 10f;
	[SerializeField, Range(5f, 60f)] private float fakeKillAngle = 22f;

	[Header("Run Tracking")]
	[SerializeField] private string playerLeaderboardName = "PLAYER";
	[SerializeField] private Vector3 leaderboardOffset = new Vector3(0f, 2.18f, 80.6f);
	[SerializeField] private Vector3 leaderboardEulerAngles = new Vector3(0f, 180f, 0f);

	private readonly List<ArenaBakedEnemyTarget> arenaEnemies = new List<ArenaBakedEnemyTarget>();

	private Transform player;
	private CharacterController playerController;
	private Camera playerCamera;
	private NavMeshSurface navMeshSurface;
	private ArenaEncounterFlow arenaEncounterFlow;

	private GameObject runtimeRoot;
	private GameObject entryBarrier;
	private GameObject exitBarrier;
	private ArenaGuidanceArrow exitArrow;
	private ArenaRunTimerDisplay runTimerDisplay;
	private ArenaWallLeaderboardDisplay wallLeaderboardDisplay;
	private SceneIntroOverlay sceneIntroOverlay;

	private CanvasGroup promptCanvasGroup;
	private CanvasGroup counterCanvasGroup;
	private Image promptBackground;
	private Image counterBackground;
	private TextMeshProUGUI promptLabel;
	private TextMeshProUGUI counterLabel;

	private ActivePromptState activePrompt;
	private float counterHideAt = -1f;
	private float initialPromptAt = -1f;
	private bool isRetryReloadPending;
	private bool encounterEventsBound;

	private sealed class ActivePromptState
	{
		public string Message;
		public ArenaPromptColorMode ColorMode;
		public Color SolidColor;
		public float HideAt;
	}

	private void Start()
	{
		if (!TryResolvePlayer())
		{
			Debug.LogError($"{nameof(ArenaTutorialSceneController)} could not find the player in scene.", this);
			enabled = false;
			return;
		}

		EnsureArenaEncounterFlow();
		sceneIntroOverlay = null;
		EnsureSceneBuilt();
		ResetArenaState();
		RepositionPlayer();
		QueueInitialPrompt();
		StartCoroutine(PlaySceneIntroAfterSetup());
	}

	private IEnumerator PlaySceneIntroAfterSetup()
	{
		yield return null;

		if (sceneIntroOverlay != null)
		{
			sceneIntroOverlay.PlayIntro();
		}
	}

	private void Update()
	{
		HandleRetryInput();
		TryShowInitialPrompt();
		UpdatePromptVisuals();
		UpdateCounterVisuals();
		HandleFakeAttackKill();
	}

	private void OnDestroy()
	{
		if (!encounterEventsBound || arenaEncounterFlow == null)
		{
			return;
		}

		arenaEncounterFlow.EncounterStarted -= HandleArenaEncounterStarted;
		arenaEncounterFlow.RemainingEnemiesChanged -= HandleArenaRemainingEnemiesChanged;
		arenaEncounterFlow.ExitRequestedBeforeStart -= HandleArenaExitRequestedBeforeStart;
		arenaEncounterFlow.ExitRequestedWhileLocked -= HandleArenaExitRequestedWhileLocked;
		arenaEncounterFlow.EncounterCleared -= HandleArenaEncounterCleared;
		arenaEncounterFlow.RunCompleted -= HandleArenaRunCompleted;
	}

	private void HandleRetryInput()
	{
		bool retryPressed = Input.GetKeyDown(KeyCode.R);
		if (!retryPressed && Keyboard.current != null)
		{
			retryPressed = Keyboard.current.rKey.wasPressedThisFrame;
		}

		if (!retryPressed || isRetryReloadPending)
		{
			return;
		}

		RetryLevel();
	}

	private void RetryLevel()
	{
		isRetryReloadPending = true;

		if (wallLeaderboardDisplay != null)
		{
			wallLeaderboardDisplay.PersistCurrentEntries();
		}

		Scene currentScene = gameObject.scene;
		if (currentScene.buildIndex >= 0)
		{
			SceneManager.LoadScene(currentScene.buildIndex);
			return;
		}

		SceneManager.LoadScene(currentScene.name);
	}

	private void EnsureArenaEncounterFlow()
	{
		if (arenaEncounterFlow == null)
		{
			arenaEncounterFlow = GetComponent<ArenaEncounterFlow>();
			if (arenaEncounterFlow == null)
			{
				arenaEncounterFlow = gameObject.AddComponent<ArenaEncounterFlow>();
			}
		}

		arenaEncounterFlow.BindPlayer(player, playerController, playerCamera);

		if (encounterEventsBound)
		{
			return;
		}

		arenaEncounterFlow.EncounterStarted += HandleArenaEncounterStarted;
		arenaEncounterFlow.RemainingEnemiesChanged += HandleArenaRemainingEnemiesChanged;
		arenaEncounterFlow.ExitRequestedBeforeStart += HandleArenaExitRequestedBeforeStart;
		arenaEncounterFlow.ExitRequestedWhileLocked += HandleArenaExitRequestedWhileLocked;
		arenaEncounterFlow.EncounterCleared += HandleArenaEncounterCleared;
		arenaEncounterFlow.RunCompleted += HandleArenaRunCompleted;
		encounterEventsBound = true;
	}

	private void HandleArenaEncounterStarted()
	{
		ShowPrompt("Arena active. Defeat every enemy to unlock the barriers.", promptDuration, ArenaPromptColorMode.Solid, new Color(1f, 0.55f, 0.55f));
	}

	private void HandleArenaRemainingEnemiesChanged(int remainingEnemies)
	{
		ShowCounter($"Enemies left: {remainingEnemies}", counterDuration);
	}

	private void HandleArenaExitRequestedBeforeStart()
	{
		ShowPrompt("Enter the arena before heading for the exit.", promptDuration, ArenaPromptColorMode.Solid, Color.white);
	}

	private void HandleArenaExitRequestedWhileLocked()
	{
		ShowPrompt("Exit locked. Defeat every enemy in the arena first.", promptDuration, ArenaPromptColorMode.AdaptiveContrast, Color.white);
	}

	private void HandleArenaEncounterCleared()
	{
		ShowPrompt("Arena cleared. Follow the ground arrow to leave.", promptDuration, ArenaPromptColorMode.AdaptiveHueShift, Color.white);
	}

	private void HandleArenaRunCompleted(float elapsedSeconds)
	{
		string timeText = elapsedSeconds > 0f ? $"\nTime: {ArenaRunTimerDisplay.FormatTime(elapsedSeconds)}" : string.Empty;
		ShowPrompt($"Arena complete.{timeText}\nFollow the path ahead.", promptDuration, ArenaPromptColorMode.AdaptiveHueShift, Color.white);
	}

	private bool TryResolvePlayer()
	{
		playerController = FindFirstObjectByType<CharacterController>();
		if (playerController == null)
		{
			return false;
		}

		player = playerController.transform;
		playerCamera = player.GetComponentInChildren<Camera>(true);
		player.tag = "Player";
		return true;
	}

	public void BakeGeneratedContentIntoScene()
	{
		ClearGeneratedContentFromScene();
		EnsureSceneBuilt();
		ResetArenaState();
	}

	public void ClearGeneratedContentFromScene()
	{
		DestroyGeneratedObject(FindGeneratedObject(SceneIntroObjectName));
		DestroyGeneratedObject(FindGeneratedObject(RunTimerObjectName));
		DestroyGeneratedObject(FindGeneratedObject(PromptUiObjectName));
		DestroyGeneratedObject(FindGeneratedObject(LeaderboardObjectName));
		DestroyGeneratedObject(FindGeneratedObject(RuntimeRootObjectName));
		ClearCachedReferences();
	}

	private void EnsureSceneBuilt()
	{
		EnsureArenaEncounterFlow();
		EnsureSceneIntroOverlay();
		EnsurePromptUi();
		EnsureGeneratedContent();
		EnsureRunTimerDisplay();
		EnsureLeaderboardDisplay();
		BindArenaEncounterFlow();
	}

	private void BindArenaEncounterFlow()
	{
		if (arenaEncounterFlow == null)
		{
			return;
		}

		arenaEncounterFlow.BindPlayer(player, playerController, playerCamera);
		arenaEncounterFlow.ConfigureSceneObjects(entryBarrier, exitBarrier, exitArrow, runTimerDisplay, wallLeaderboardDisplay, playerLeaderboardName);
		arenaEncounterFlow.BindEnemies(arenaEnemies);
	}

	private void EnsurePromptUi()
	{
		if (!TryBindPromptUi())
		{
			CreateRuntimeUi();
		}

		ApplyPromptUiVisuals();
	}

	private bool TryBindPromptUi()
	{
		Transform uiRoot = FindGeneratedTransform(PromptUiObjectName);
		if (uiRoot == null)
		{
			return false;
		}

		Transform promptPanel = uiRoot.Find("Prompt Panel");
		Transform counterPanel = uiRoot.Find("Counter Panel");
		promptCanvasGroup = promptPanel != null ? promptPanel.GetComponent<CanvasGroup>() : null;
		counterCanvasGroup = counterPanel != null ? counterPanel.GetComponent<CanvasGroup>() : null;
		promptBackground = promptPanel?.Find("Background")?.GetComponent<Image>();
		counterBackground = counterPanel?.Find("Background")?.GetComponent<Image>();
		promptLabel = promptPanel?.Find("Label")?.GetComponent<TextMeshProUGUI>();
		counterLabel = counterPanel?.Find("Label")?.GetComponent<TextMeshProUGUI>();
		return promptCanvasGroup != null && counterCanvasGroup != null && promptBackground != null && counterBackground != null && promptLabel != null && counterLabel != null;
	}

	private void ApplyPromptUiVisuals()
	{
		Canvas canvas = FindGeneratedTransform(PromptUiObjectName)?.GetComponent<Canvas>();
		if (canvas != null)
		{
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = 200;
		}

		if (promptLabel != null)
		{
			promptLabel.alignment = TextAlignmentOptions.Center;
			promptLabel.fontSize = 44f;
			promptLabel.enableWordWrapping = true;
		}

		if (counterLabel != null)
		{
			counterLabel.alignment = TextAlignmentOptions.Left;
			counterLabel.fontSize = 34f;
			counterLabel.enableWordWrapping = false;
		}
	}

	private void EnsureGeneratedContent()
	{
		runtimeRoot = FindGeneratedObject(RuntimeRootObjectName);
		if (runtimeRoot == null)
		{
			BuildRuntimeCourse();
		}

		BindGeneratedContentReferences();
	}

	private void EnsureRunTimerDisplay()
	{
		GameObject timerObject = FindGeneratedObject(RunTimerObjectName);
		if (timerObject == null)
		{
			timerObject = new GameObject(RunTimerObjectName);
			timerObject.transform.SetParent(transform, false);
		}

		runTimerDisplay = timerObject.GetComponent<ArenaRunTimerDisplay>();
		if (runTimerDisplay == null)
		{
			runTimerDisplay = timerObject.AddComponent<ArenaRunTimerDisplay>();
		}

		runTimerDisplay.EnsureSceneBuilt();
	}

	private void EnsureLeaderboardDisplay()
	{
		GameObject leaderboardObject = FindGeneratedObject(LeaderboardObjectName);
		if (leaderboardObject == null)
		{
			Transform parent = runtimeRoot != null ? runtimeRoot.transform : transform;
			leaderboardObject = new GameObject(LeaderboardObjectName);
			leaderboardObject.transform.SetParent(parent, false);
			leaderboardObject.transform.position = courseOrigin + leaderboardOffset;
			leaderboardObject.transform.rotation = Quaternion.Euler(leaderboardEulerAngles);
		}

		wallLeaderboardDisplay = leaderboardObject.GetComponent<ArenaWallLeaderboardDisplay>();
		if (wallLeaderboardDisplay == null)
		{
			wallLeaderboardDisplay = leaderboardObject.AddComponent<ArenaWallLeaderboardDisplay>();
		}

		wallLeaderboardDisplay.EnsureSceneBuilt();
	}

	private void EnsureSceneIntroOverlay()
	{
		GameObject introObject = FindGeneratedObject(SceneIntroObjectName);
		if (introObject == null)
		{
			introObject = new GameObject(SceneIntroObjectName);
		}

		if (introObject.transform.parent != transform)
		{
			introObject.transform.SetParent(transform, false);
		}

		introObject.transform.localPosition = Vector3.zero;
		introObject.transform.localRotation = Quaternion.identity;
		introObject.transform.localScale = Vector3.one;
		introObject.SetActive(true);

		sceneIntroOverlay = introObject.GetComponent<SceneIntroOverlay>();
		if (sceneIntroOverlay == null)
		{
			sceneIntroOverlay = introObject.AddComponent<SceneIntroOverlay>();
		}

		sceneIntroOverlay.ConfigureArenaDefaults();
		sceneIntroOverlay.EnsureSceneBuilt();
	}

	private void BindGeneratedContentReferences()
	{
		if (runtimeRoot == null)
		{
			return;
		}

		SyncCourseOriginFromRuntimeRoot();

		navMeshSurface = runtimeRoot.GetComponent<NavMeshSurface>();
		if (navMeshSurface == null)
		{
			navMeshSurface = runtimeRoot.AddComponent<NavMeshSurface>();
		}

		ConfigureNavMeshSurface(navMeshSurface);
		entryBarrier = FindDescendantByName(runtimeRoot.transform, EntryBarrierObjectName)?.gameObject;
		exitBarrier = FindDescendantByName(runtimeRoot.transform, ExitBarrierObjectName)?.gameObject;
		Transform exitArrowTransform = FindDescendantByName(runtimeRoot.transform, ExitArrowObjectName);
		exitArrow = exitArrowTransform != null ? exitArrowTransform.GetComponent<ArenaGuidanceArrow>() : null;
		if (exitArrowTransform != null && exitArrow == null)
		{
			exitArrow = exitArrowTransform.gameObject.AddComponent<ArenaGuidanceArrow>();
		}
		if (exitArrow != null)
		{
			exitArrow.SetBasePosition(courseOrigin + ExitArrowOffset);
			exitArrow.EnsureSceneBuilt();
		}

		EnsureEncounterZones();

		EnsureArenaEnemyBindings();
	}

	private static void ConfigureNavMeshSurface(NavMeshSurface surface)
	{
		surface.collectObjects = CollectObjects.Children;
		surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
		surface.ignoreNavMeshAgent = true;
		surface.ignoreNavMeshObstacle = true;
	}

	private void ResetArenaState()
	{
		counterHideAt = -1f;
		activePrompt = null;

		if (promptCanvasGroup != null)
		{
			promptCanvasGroup.alpha = 0f;
		}

		if (counterCanvasGroup != null)
		{
			counterCanvasGroup.alpha = 0f;
		}

		arenaEncounterFlow?.ResetEncounter();
	}

	private void QueueInitialPrompt()
	{
		float introDuration = sceneIntroOverlay != null ? sceneIntroOverlay.IntroDuration : 0f;
		if (introDuration <= 0.05f)
		{
			ShowInitialPrompt();
			return;
		}

		initialPromptAt = Time.unscaledTime + introDuration * 0.85f;
	}

	private void TryShowInitialPrompt()
	{
		if (initialPromptAt <= 0f || Time.unscaledTime < initialPromptAt)
		{
			return;
		}

		initialPromptAt = -1f;
		ShowInitialPrompt();
	}

	private void ShowInitialPrompt()
	{
		ShowPrompt("Tutorial 1/3\nMove: WASD, Jump: Space, Sprint: Shift", promptDuration, ArenaPromptColorMode.Solid, new Color(0.26f, 0.90f, 1f));
	}

	private void CreateRuntimeUi()
	{
		GameObject canvasObject = new GameObject(PromptUiObjectName);
		canvasObject.transform.SetParent(transform, false);

		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 200;

		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;

		canvasObject.AddComponent<GraphicRaycaster>();

		promptCanvasGroup = CreatePanel(canvasObject.transform, "Prompt Panel", new Vector2(0f, 360f), new Vector2(940f, 150f), out promptBackground, out promptLabel);
		promptLabel.alignment = TextAlignmentOptions.Center;
		promptLabel.fontSize = 44f;
		promptLabel.enableWordWrapping = true;

		counterCanvasGroup = CreatePanel(canvasObject.transform, "Counter Panel", new Vector2(210f, -60f), new Vector2(420f, 90f), out counterBackground, out counterLabel, topLeftAnchored: true);
		counterLabel.alignment = TextAlignmentOptions.Left;
		counterLabel.fontSize = 34f;
		counterLabel.enableWordWrapping = false;
	}

	private static CanvasGroup CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, out Image background, out TextMeshProUGUI label, bool topLeftAnchored = false)
	{
		GameObject root = new GameObject(name);
		root.transform.SetParent(parent, false);

		RectTransform rootRect = root.AddComponent<RectTransform>();
		if (topLeftAnchored)
		{
			rootRect.anchorMin = new Vector2(0f, 1f);
			rootRect.anchorMax = new Vector2(0f, 1f);
			rootRect.pivot = new Vector2(0f, 1f);
		}
		else
		{
			rootRect.anchorMin = new Vector2(0.5f, 0.5f);
			rootRect.anchorMax = new Vector2(0.5f, 0.5f);
			rootRect.pivot = new Vector2(0.5f, 0.5f);
		}
		rootRect.anchoredPosition = anchoredPosition;
		rootRect.sizeDelta = size;

		CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
		canvasGroup.alpha = 0f;
		canvasGroup.interactable = false;
		canvasGroup.blocksRaycasts = false;

		GameObject backgroundObject = new GameObject("Background");
		backgroundObject.transform.SetParent(root.transform, false);
		RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
		backgroundRect.anchorMin = Vector2.zero;
		backgroundRect.anchorMax = Vector2.one;
		backgroundRect.offsetMin = Vector2.zero;
		backgroundRect.offsetMax = Vector2.zero;
		background = backgroundObject.AddComponent<Image>();
		background.color = new Color(0f, 0f, 0f, 0.62f);

		GameObject labelObject = new GameObject("Label");
		labelObject.transform.SetParent(root.transform, false);
		RectTransform labelRect = labelObject.AddComponent<RectTransform>();
		labelRect.anchorMin = Vector2.zero;
		labelRect.anchorMax = Vector2.one;
		labelRect.offsetMin = new Vector2(28f, 18f);
		labelRect.offsetMax = new Vector2(-28f, -18f);
		label = labelObject.AddComponent<TextMeshProUGUI>();
		label.font = TMP_Settings.defaultFontAsset;
		label.color = Color.white;

		return canvasGroup;
	}

	private void BuildRuntimeCourse()
	{
		runtimeRoot = new GameObject(RuntimeRootObjectName);
		runtimeRoot.transform.SetParent(transform, false);

		Material floorMaterial = CreateMaterial("Floor", new Color(0.18f, 0.21f, 0.24f), 0f, true);
		Material wallMaterial = CreateMaterial("Wall", new Color(0.10f, 0.12f, 0.16f), 0f, true);
		Material accentMaterial = CreateMaterial("Accent", new Color(0.23f, 0.42f, 0.35f), 0f, true);
		Material barrierMaterial = CreateMaterial("Barrier", new Color(0.85f, 0.16f, 0.18f), 0.6f, false);
		Material arrowMaterial = CreateMaterial("Arrow", new Color(0.10f, 1f, 0.72f), 0f, false);

		Vector3 courseCenter = courseOrigin + CourseFloorOffset;
		CreateBox(runtimeRoot.transform, "Course Floor", courseCenter, new Vector3(18f, 1f, 96f), floorMaterial);
		CreateBox(runtimeRoot.transform, "Left Wall", courseOrigin + new Vector3(-9.5f, 2f, 42f), new Vector3(1f, 4f, 96f), wallMaterial);
		CreateBox(runtimeRoot.transform, "Right Wall", courseOrigin + new Vector3(9.5f, 2f, 42f), new Vector3(1f, 4f, 96f), wallMaterial);
		CreateBox(runtimeRoot.transform, "Arena Back Wall", courseOrigin + new Vector3(0f, 2f, 82f), new Vector3(20f, 4f, 1f), wallMaterial);
		CreateBox(runtimeRoot.transform, "Start Accent", courseOrigin + new Vector3(0f, 0.02f, 7f), new Vector3(10f, 0.04f, 4f), accentMaterial);
		CreateBox(runtimeRoot.transform, "Tutorial Accent", courseOrigin + new Vector3(0f, 0.02f, 21f), new Vector3(10f, 0.04f, 6f), accentMaterial);

		BuildEncounterZones();

		entryBarrier = CreateBox(runtimeRoot.transform, EntryBarrierObjectName, courseOrigin + new Vector3(0f, 1.6f, 34f), new Vector3(16f, 3.2f, 1.2f), barrierMaterial);
		exitBarrier = CreateBox(runtimeRoot.transform, ExitBarrierObjectName, courseOrigin + new Vector3(0f, 1.6f, 66f), new Vector3(16f, 3.2f, 1.2f), barrierMaterial);
		entryBarrier.SetActive(false);
		exitBarrier.SetActive(false);

		navMeshSurface = runtimeRoot.GetComponent<NavMeshSurface>();
		if (navMeshSurface == null)
		{
			navMeshSurface = runtimeRoot.AddComponent<NavMeshSurface>();
		}

		ConfigureNavMeshSurface(navMeshSurface);
		navMeshSurface.BuildNavMesh();

		SpawnArenaEnemy(knifeEnemyPrefab, courseOrigin + new Vector3(-4f, 0.1f, 49f), Quaternion.identity);
		SpawnArenaEnemy(gunEnemyPrefab, courseOrigin + new Vector3(4f, 0.1f, 52f), Quaternion.Euler(0f, 180f, 0f));
		SpawnArenaEnemy(knifeEnemyPrefab, courseOrigin + new Vector3(-2f, 0.1f, 58f), Quaternion.Euler(0f, 180f, 0f));
		SpawnArenaEnemy(gunEnemyPrefab, courseOrigin + new Vector3(3f, 0.1f, 61f), Quaternion.identity);

		exitArrow = CreateArrow(runtimeRoot.transform, courseOrigin + ExitArrowOffset, arrowMaterial);
		exitArrow.Hide();
	}

	private void RepositionPlayer()
	{
		Vector3 startPosition = courseOrigin + PlayerStartOffset;
		Quaternion startRotation = Quaternion.Euler(0f, 0f, 0f);

		if (playerController != null)
		{
			playerController.enabled = false;
		}

		player.SetPositionAndRotation(startPosition, startRotation);

		if (playerCamera != null && playerCamera.transform.parent != null)
		{
			playerCamera.transform.parent.localRotation = Quaternion.identity;
		}

		if (playerController != null)
		{
			playerController.enabled = true;
		}

		SprayPaint sprayPaint = player.GetComponent<SprayPaint>();
		if (sprayPaint != null)
		{
			sprayPaint.ClearAllSpray();
		}
	}

	private void CreateTutorialZone(Vector3 position, Vector3 size, string message, ArenaPromptColorMode colorMode, Color solidColor)
	{
		CreateEncounterZone(position, size, ArenaTriggerKind.Tutorial, message, colorMode, solidColor);
	}

	private void CreateEncounterZone(Vector3 position, Vector3 size, ArenaTriggerKind kind, string message, ArenaPromptColorMode colorMode, Color solidColor)
	{
		GameObject zoneObject = new GameObject(kind + " Zone");
		zoneObject.transform.SetParent(runtimeRoot.transform, false);
		zoneObject.transform.position = position;

		BoxCollider boxCollider = zoneObject.AddComponent<BoxCollider>();
		boxCollider.isTrigger = true;
		boxCollider.size = size;

		ArenaBakedTriggerZone zone = zoneObject.AddComponent<ArenaBakedTriggerZone>();
		zone.Initialize(kind, message, colorMode, solidColor);
		zone.Bind(this, arenaEncounterFlow);
	}

	private GameObject SpawnArenaEnemy(GameObject prefab, Vector3 position, Quaternion rotation)
	{
		if (prefab == null)
		{
			Debug.LogError($"{nameof(ArenaTutorialSceneController)} is missing an enemy prefab reference.", this);
			return null;
		}

		GameObject enemyInstance = Instantiate(prefab, position, rotation, runtimeRoot.transform);
		enemyInstance.name = prefab.name;

		if (enemyInstance.GetComponent<AudioSource>() == null)
		{
			enemyInstance.AddComponent<AudioSource>();
		}

		if (enemyInstance.GetComponent<LocomotionSimpleAgent>() == null)
		{
			enemyInstance.AddComponent<LocomotionSimpleAgent>();
		}

		KnifePawnController knifeController = enemyInstance.GetComponent<KnifePawnController>();
		GunPawnController gunController = enemyInstance.GetComponent<GunPawnController>();
		if (knifeController == null && gunController == null)
		{
			knifeController = enemyInstance.AddComponent<KnifePawnController>();
		}

		if (knifeController != null && player != null)
		{
			knifeController.SetPlayer(player);
		}

		if (gunController != null && player != null)
		{
			gunController.SetPlayer(player);
		}

		NavMeshAgent agent = enemyInstance.GetComponent<NavMeshAgent>();
		if (agent != null && NavMesh.SamplePosition(position, out NavMeshHit navHit, 6f, NavMesh.AllAreas))
		{
			agent.Warp(navHit.position);
		}

		if (enemyInstance.GetComponent<Collider>() == null)
		{
			CapsuleCollider capsuleCollider = enemyInstance.AddComponent<CapsuleCollider>();
			capsuleCollider.center = new Vector3(0f, 0.9f, 0f);
			capsuleCollider.height = 1.8f;
			capsuleCollider.radius = 0.35f;
		}

		ArenaBakedEnemyTarget enemyTarget = enemyInstance.GetComponent<ArenaBakedEnemyTarget>();
		if (enemyTarget == null)
		{
			enemyTarget = enemyInstance.AddComponent<ArenaBakedEnemyTarget>();
		}
		enemyTarget.Initialize(arenaEncounterFlow);
		arenaEnemies.Add(enemyTarget);
		return enemyInstance;
	}

	private void BuildEncounterZones()
	{
		CreateTutorialZone(courseOrigin + new Vector3(0f, 1.4f, 7f), new Vector3(14f, 3f, 5f), "Tutorial 1/3\nMove: WASD, Jump: Space, Sprint: Shift.", ArenaPromptColorMode.Solid, new Color(0.26f, 0.90f, 1f));
		CreateTutorialZone(courseOrigin + new Vector3(0f, 1.4f, 18f), new Vector3(14f, 3f, 6f), "Tutorial 2/3\nLeft Click triggers spray and a temporary kill check.", ArenaPromptColorMode.AdaptiveContrast, Color.white);
		CreateTutorialZone(courseOrigin + new Vector3(0f, 1.4f, 29f), new Vector3(14f, 3f, 6f), "Tutorial 3/3\nEnter the arena ahead. Defeat every enemy before leaving.", ArenaPromptColorMode.AdaptiveHueShift, Color.white);

		CreateEncounterZone(courseOrigin + new Vector3(0f, 1.5f, 37f), new Vector3(14f, 3.2f, 6f), ArenaTriggerKind.ArenaStart, null, ArenaPromptColorMode.Solid, new Color(1f, 0.4f, 0.4f));
		CreateEncounterZone(courseOrigin + new Vector3(0f, 1.5f, 74f), new Vector3(14f, 3.2f, 6f), ArenaTriggerKind.ArenaExit, null, ArenaPromptColorMode.Solid, new Color(0.8f, 1f, 0.8f));
	}

	private void EnsureEncounterZones()
	{
		ArenaBakedTriggerZone[] triggerZones = runtimeRoot.GetComponentsInChildren<ArenaBakedTriggerZone>(true);
		if (triggerZones.Length != 5)
		{
			ClearExistingEncounterZones();
			BuildEncounterZones();
			triggerZones = runtimeRoot.GetComponentsInChildren<ArenaBakedTriggerZone>(true);
		}

		for (int i = 0; i < triggerZones.Length; i++)
		{
			if (triggerZones[i] != null)
			{
				triggerZones[i].Bind(this, arenaEncounterFlow);
			}
		}
	}

	private void ClearExistingEncounterZones()
	{
		List<GameObject> zoneObjects = new List<GameObject>();
		Transform[] transforms = runtimeRoot.GetComponentsInChildren<Transform>(true);
		for (int i = 0; i < transforms.Length; i++)
		{
			Transform current = transforms[i];
			if (current == null || current == runtimeRoot.transform)
			{
				continue;
			}

			if (!current.name.EndsWith(" Zone", StringComparison.Ordinal))
			{
				continue;
			}

			Collider collider = current.GetComponent<Collider>();
			if (collider != null && collider.isTrigger)
			{
				zoneObjects.Add(current.gameObject);
			}
		}

		for (int i = 0; i < zoneObjects.Count; i++)
		{
			DestroyGeneratedObject(zoneObjects[i]);
		}
	}

	private void EnsureArenaEnemyBindings()
	{
		arenaEnemies.Clear();
		HashSet<GameObject> enemyRoots = new HashSet<GameObject>();

		KnifePawnController[] knifeEnemies = runtimeRoot.GetComponentsInChildren<KnifePawnController>(true);
		for (int i = 0; i < knifeEnemies.Length; i++)
		{
			if (knifeEnemies[i] == null)
			{
				continue;
			}

			knifeEnemies[i].SetPlayer(player);
			enemyRoots.Add(knifeEnemies[i].gameObject);
		}

		GunPawnController[] gunEnemies = runtimeRoot.GetComponentsInChildren<GunPawnController>(true);
		for (int i = 0; i < gunEnemies.Length; i++)
		{
			if (gunEnemies[i] == null)
			{
				continue;
			}

			gunEnemies[i].SetPlayer(player);
			enemyRoots.Add(gunEnemies[i].gameObject);
		}

		foreach (GameObject enemyRoot in enemyRoots)
		{
			if (enemyRoot == null)
			{
				continue;
			}

			ArenaBakedEnemyTarget enemyTarget = enemyRoot.GetComponent<ArenaBakedEnemyTarget>();
			if (enemyTarget == null)
			{
				enemyTarget = enemyRoot.AddComponent<ArenaBakedEnemyTarget>();
			}

			enemyTarget.Initialize(arenaEncounterFlow);
			arenaEnemies.Add(enemyTarget);
		}
	}

	private static GameObject CreateBox(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
	{
		GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
		box.name = name;
		box.transform.SetParent(parent, false);
		box.transform.position = position;
		box.transform.localScale = scale;

		Renderer renderer = box.GetComponent<Renderer>();
		if (renderer != null)
		{
			renderer.sharedMaterial = material;
		}

		return box;
	}

	private ArenaGuidanceArrow CreateArrow(Transform parent, Vector3 position, Material material)
	{
		GameObject root = new GameObject("Exit Arrow");
		root.transform.SetParent(parent, false);
		root.transform.position = position;

		GameObject shaft = CreateBox(root.transform, "Arrow Shaft", position + new Vector3(0f, 0f, -0.12f), new Vector3(0.9f, 0.12f, 3.3f), material);
		DisableCollider(shaft);

		GameObject headLeft = CreateBox(root.transform, "Arrow Head Left", position + new Vector3(-0.52f, 0f, 1.34f), new Vector3(0.64f, 0.12f, 1.72f), material);
		headLeft.transform.rotation = Quaternion.Euler(0f, 38f, 0f);
		DisableCollider(headLeft);

		GameObject headRight = CreateBox(root.transform, "Arrow Head Right", position + new Vector3(0.52f, 0f, 1.34f), new Vector3(0.64f, 0.12f, 1.72f), material);
		headRight.transform.rotation = Quaternion.Euler(0f, -38f, 0f);
		DisableCollider(headRight);

		ArenaGuidanceArrow guidanceArrow = root.GetComponent<ArenaGuidanceArrow>();
		if (guidanceArrow == null)
		{
			guidanceArrow = root.AddComponent<ArenaGuidanceArrow>();
		}

		guidanceArrow.SetBasePosition(position);
		guidanceArrow.EnsureSceneBuilt();
		return guidanceArrow;
	}

	private static void DisableCollider(GameObject gameObject)
	{
		Collider collider = gameObject.GetComponent<Collider>();
		if (collider != null)
		{
			collider.enabled = false;
		}
	}

	private static Material CreateMaterial(string name, Color color, float alpha, bool useLitShader)
	{
		string shaderName = useLitShader ? "Universal Render Pipeline/Lit" : "Universal Render Pipeline/Unlit";
		Shader shader = Shader.Find(shaderName);
		if (shader == null)
		{
			shader = Shader.Find("Standard");
		}

		Material material = new Material(shader)
		{
			name = name,
		};

		Color tintedColor = color;
		if (alpha > 0f)
		{
			tintedColor.a = alpha;
		}

		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", tintedColor);
		}
		else if (material.HasProperty("_Color"))
		{
			material.SetColor("_Color", tintedColor);
		}

		if (material.HasProperty("_Surface") && alpha > 0f)
		{
			material.SetFloat("_Surface", 1f);
			material.SetFloat("_Blend", 0f);
			material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
			material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
			material.SetFloat("_ZWrite", 0f);
			material.DisableKeyword("_ALPHATEST_ON");
			material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
			material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
		}

		if (material.HasProperty("_EmissionColor"))
		{
			material.EnableKeyword("_EMISSION");
			material.SetColor("_EmissionColor", color * 0.4f);
		}

		return material;
	}

	internal void HandleTutorialTrigger(ArenaBakedTriggerZone zone)
	{
		if (zone == null || zone.Kind != ArenaTriggerKind.Tutorial)
		{
			return;
		}

		ShowPrompt(zone.Message, promptDuration, zone.ColorMode, zone.SolidColor);
	}

	private void HandleFakeAttackKill()
	{
		if (arenaEncounterFlow == null || !arenaEncounterFlow.HasStarted || arenaEncounterFlow.IsCleared || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
		{
			return;
		}

		ArenaBakedEnemyTarget candidate = arenaEncounterFlow.SelectKillCandidate(fakeKillRange, fakeKillAngle);
		if (candidate != null)
		{
			candidate.Kill();
		}
	}

	private void ShowPrompt(string message, float duration, ArenaPromptColorMode colorMode, Color solidColor)
	{
		if (promptCanvasGroup == null || promptLabel == null)
		{
			return;
		}

		activePrompt = new ActivePromptState
		{
			Message = message,
			ColorMode = colorMode,
			SolidColor = solidColor,
			HideAt = Time.unscaledTime + duration,
		};

		promptLabel.text = message;
		promptCanvasGroup.alpha = 1f;
	}

	private void ShowCounter(string message, float duration)
	{
		if (counterCanvasGroup == null || counterLabel == null)
		{
			return;
		}

		counterLabel.text = message;
		counterHideAt = Time.unscaledTime + duration;
		counterCanvasGroup.alpha = 1f;
	}

	private void UpdatePromptVisuals()
	{
		if (promptCanvasGroup == null || promptLabel == null || promptBackground == null)
		{
			return;
		}

		if (activePrompt == null)
		{
			promptCanvasGroup.alpha = 0f;
			return;
		}

		float remaining = activePrompt.HideAt - Time.unscaledTime;
		if (remaining <= 0f)
		{
			activePrompt = null;
			promptCanvasGroup.alpha = 0f;
			return;
		}

		promptCanvasGroup.alpha = Mathf.Clamp01(remaining / 0.35f);
		promptLabel.color = ResolvePromptColor(activePrompt.ColorMode, activePrompt.SolidColor);
		promptBackground.color = new Color(0f, 0f, 0f, 0.62f);
	}

	private void UpdateCounterVisuals()
	{
		if (counterCanvasGroup == null || counterLabel == null || counterBackground == null)
		{
			return;
		}

		if (counterHideAt <= 0f)
		{
			counterCanvasGroup.alpha = 0f;
			return;
		}

		float remaining = counterHideAt - Time.unscaledTime;
		if (remaining <= 0f)
		{
			counterHideAt = -1f;
			counterCanvasGroup.alpha = 0f;
			return;
		}

		counterCanvasGroup.alpha = Mathf.Clamp01(remaining / 0.35f);
		counterLabel.color = Color.white;
		counterBackground.color = new Color(0f, 0f, 0f, 0.62f);
	}

	private Color ResolvePromptColor(ArenaPromptColorMode colorMode, Color solidColor)
	{
		if (colorMode == ArenaPromptColorMode.Solid)
		{
			return solidColor;
		}

		Color backgroundColor = SampleSceneColor();
		Color.RGBToHSV(backgroundColor, out float hue, out float saturation, out float value);

		float baseHue = Mathf.Repeat(hue + 0.5f, 1f);
		if (colorMode == ArenaPromptColorMode.AdaptiveHueShift)
		{
			baseHue = Mathf.Repeat(baseHue + Time.unscaledTime * 0.18f, 1f);
		}

		float targetSaturation = Mathf.Clamp01(0.65f + (1f - saturation) * 0.35f);
		float targetValue = value > 0.55f ? 0.12f : 1f;
		return Color.HSVToRGB(baseHue, targetSaturation, targetValue);
	}

	private Color SampleSceneColor()
	{
		if (playerCamera == null)
		{
			return Color.black;
		}

		Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
		if (Physics.Raycast(ray, out RaycastHit hit, 250f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
		{
			Renderer renderer = hit.collider.GetComponentInParent<Renderer>();
			if (renderer != null)
			{
				Material sharedMaterial = renderer.sharedMaterial;
				if (sharedMaterial != null)
				{
					if (sharedMaterial.HasProperty("_BaseColor"))
					{
						return sharedMaterial.GetColor("_BaseColor");
					}
					if (sharedMaterial.HasProperty("_Color"))
					{
						return sharedMaterial.GetColor("_Color");
					}
				}
			}
		}

		return RenderSettings.ambientSkyColor.maxColorComponent > 0f ? RenderSettings.ambientSkyColor : playerCamera.backgroundColor;
	}

	internal bool IsPlayerCollider(Collider other)
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

	private void SyncCourseOriginFromRuntimeRoot()
	{
		if (runtimeRoot == null)
		{
			return;
		}

		Transform floor = FindDescendantByName(runtimeRoot.transform, CourseFloorObjectName);
		if (floor != null)
		{
			courseOrigin = floor.position - CourseFloorOffset;
		}
	}

	private GameObject FindGeneratedObject(string objectName)
	{
		Transform directChild = transform.Find(objectName);
		if (directChild != null)
		{
			return directChild.gameObject;
		}

		Transform[] transforms = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);
		Transform bestMatch = null;
		float bestDistance = float.MaxValue;
		for (int i = 0; i < transforms.Length; i++)
		{
			Transform candidate = transforms[i];
			if (candidate == null || candidate.gameObject.scene != gameObject.scene || candidate.name != objectName)
			{
				continue;
			}

			if (candidate.IsChildOf(transform))
			{
				return candidate.gameObject;
			}

			float distance = (candidate.position - transform.position).sqrMagnitude;
			if (distance < bestDistance)
			{
				bestDistance = distance;
				bestMatch = candidate;
			}
		}

		return bestMatch != null ? bestMatch.gameObject : null;
	}

	private Transform FindGeneratedTransform(string objectName)
	{
		GameObject generatedObject = FindGeneratedObject(objectName);
		return generatedObject != null ? generatedObject.transform : null;
	}

	private static Transform FindDescendantByName(Transform root, string objectName)
	{
		if (root == null)
		{
			return null;
		}

		for (int i = 0; i < root.childCount; i++)
		{
			Transform child = root.GetChild(i);
			if (child.name == objectName)
			{
				return child;
			}

			Transform nestedChild = FindDescendantByName(child, objectName);
			if (nestedChild != null)
			{
				return nestedChild;
			}
		}

		return null;
	}

	private static void DestroyGeneratedObject(GameObject generatedObject)
	{
		if (generatedObject == null)
		{
			return;
		}

		if (Application.isPlaying)
		{
			Destroy(generatedObject);
		}
		else
		{
			DestroyImmediate(generatedObject);
		}
	}

	private void ClearCachedReferences()
	{
		runtimeRoot = null;
		entryBarrier = null;
		exitBarrier = null;
		exitArrow = null;
		navMeshSurface = null;
		runTimerDisplay = null;
		wallLeaderboardDisplay = null;
		sceneIntroOverlay = null;
		promptCanvasGroup = null;
		counterCanvasGroup = null;
		promptBackground = null;
		counterBackground = null;
		promptLabel = null;
		counterLabel = null;
		arenaEnemies.Clear();
	}


}



