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

	internal enum PromptColorMode
	{
		Solid,
		AdaptiveContrast,
		AdaptiveHueShift,
	}

	internal enum TriggerKind
	{
		Tutorial,
		ArenaStart,
		ArenaExit,
	}

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
	private readonly List<Renderer> arrowRenderers = new List<Renderer>();

	private Transform player;
	private CharacterController playerController;
	private Camera playerCamera;
	private NavMeshSurface navMeshSurface;

	private GameObject runtimeRoot;
	private GameObject entryBarrier;
	private GameObject exitBarrier;
	private GameObject exitArrow;
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
	private bool arenaStarted;
	private bool arenaCleared;
	private bool scoreSubmitted;
	private bool isRetryReloadPending;

	private sealed class ActivePromptState
	{
		public string Message;
		public PromptColorMode ColorMode;
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

		EnsureKillCoordinator();
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
		AnimateExitArrow();
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

	private void EnsureKillCoordinator()
	{
		if (player == null)
		{
			return;
		}

		ArenaKillCoordinator coordinator = player.GetComponent<ArenaKillCoordinator>();
		if (coordinator == null)
		{
			coordinator = player.gameObject.AddComponent<ArenaKillCoordinator>();
		}

		coordinator.Initialize(this);
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
		EnsureSceneIntroOverlay();
		EnsurePromptUi();
		EnsureGeneratedContent();
		EnsureRunTimerDisplay();
		EnsureLeaderboardDisplay();
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
		exitArrow = FindDescendantByName(runtimeRoot.transform, ExitArrowObjectName)?.gameObject;
		NormalizeExitArrowGeometry();
		EnsureEncounterZones();

		arrowRenderers.Clear();
		if (exitArrow != null)
		{
			Renderer[] renderers = exitArrow.GetComponentsInChildren<Renderer>(true);
			for (int i = 0; i < renderers.Length; i++)
			{
				if (renderers[i] != null)
				{
					arrowRenderers.Add(renderers[i]);
				}
			}
		}

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
		arenaStarted = false;
		arenaCleared = false;
		scoreSubmitted = false;
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

		if (entryBarrier != null)
		{
			entryBarrier.SetActive(false);
		}

		if (exitBarrier != null)
		{
			exitBarrier.SetActive(false);
		}

		if (exitArrow != null)
		{
			exitArrow.SetActive(false);
			exitArrow.transform.position = courseOrigin + ExitArrowOffset;
		}

		for (int i = 0; i < arenaEnemies.Count; i++)
		{
			if (arenaEnemies[i] != null)
			{
				arenaEnemies[i].Initialize(this);
			}
		}

		if (runTimerDisplay != null)
		{
			runTimerDisplay.ResetRun();
		}
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
		ShowPrompt("Tutorial 1/3\nMove: WASD, Jump: Space, Sprint: Shift", promptDuration, PromptColorMode.Solid, new Color(0.26f, 0.90f, 1f));
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
		exitArrow.SetActive(false);
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

	private void CreateTutorialZone(Vector3 position, Vector3 size, string message, PromptColorMode colorMode, Color solidColor)
	{
		CreateEncounterZone(position, size, TriggerKind.Tutorial, message, colorMode, solidColor);
	}

	private void CreateEncounterZone(Vector3 position, Vector3 size, TriggerKind kind, string message, PromptColorMode colorMode, Color solidColor)
	{
		GameObject zoneObject = new GameObject(kind + " Zone");
		zoneObject.transform.SetParent(runtimeRoot.transform, false);
		zoneObject.transform.position = position;

		BoxCollider boxCollider = zoneObject.AddComponent<BoxCollider>();
		boxCollider.isTrigger = true;
		boxCollider.size = size;

		ArenaBakedTriggerZone zone = zoneObject.AddComponent<ArenaBakedTriggerZone>();
		zone.Initialize(this, kind, message, colorMode, solidColor);
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

		if (enemyInstance.GetComponent<EnemyEffect>() == null)
		{
			enemyInstance.AddComponent<EnemyEffect>();
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
		enemyTarget.Initialize(this);
		arenaEnemies.Add(enemyTarget);
		return enemyInstance;
	}

	private void BuildEncounterZones()
	{
		CreateTutorialZone(courseOrigin + new Vector3(0f, 1.4f, 7f), new Vector3(14f, 3f, 5f), "Tutorial 1/3\nMove: WASD, Jump: Space, Sprint: Shift.", PromptColorMode.Solid, new Color(0.26f, 0.90f, 1f));
		CreateTutorialZone(courseOrigin + new Vector3(0f, 1.4f, 18f), new Vector3(14f, 3f, 6f), "Tutorial 2/3\nLeft Click swings the blade. Kill-confirmed hits trigger blood and hit-stop.", PromptColorMode.AdaptiveContrast, Color.white);
		CreateTutorialZone(courseOrigin + new Vector3(0f, 1.4f, 29f), new Vector3(14f, 3f, 6f), "Tutorial 3/3\nEnter the arena ahead. Defeat every enemy before leaving.", PromptColorMode.AdaptiveHueShift, Color.white);

		CreateEncounterZone(courseOrigin + new Vector3(0f, 1.5f, 37f), new Vector3(14f, 3.2f, 6f), TriggerKind.ArenaStart, null, PromptColorMode.Solid, new Color(1f, 0.4f, 0.4f));
		CreateEncounterZone(courseOrigin + new Vector3(0f, 1.5f, 74f), new Vector3(14f, 3.2f, 6f), TriggerKind.ArenaExit, null, PromptColorMode.Solid, new Color(0.8f, 1f, 0.8f));
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
				triggerZones[i].Bind(this);
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

			enemyTarget.Initialize(this);
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

	private GameObject CreateArrow(Transform parent, Vector3 position, Material material)
	{
		GameObject root = new GameObject("Exit Arrow");
		root.transform.SetParent(parent, false);
		root.transform.position = position;

		GameObject shaft = CreateBox(root.transform, "Arrow Shaft", position + new Vector3(0f, 0f, -0.12f), new Vector3(0.9f, 0.12f, 3.3f), material);
		DisableCollider(shaft);
		RegisterArrowRenderer(shaft);

		GameObject headLeft = CreateBox(root.transform, "Arrow Head Left", position + new Vector3(-0.52f, 0f, 1.34f), new Vector3(0.64f, 0.12f, 1.72f), material);
		headLeft.transform.rotation = Quaternion.Euler(0f, 38f, 0f);
		DisableCollider(headLeft);
		RegisterArrowRenderer(headLeft);

		GameObject headRight = CreateBox(root.transform, "Arrow Head Right", position + new Vector3(0.52f, 0f, 1.34f), new Vector3(0.64f, 0.12f, 1.72f), material);
		headRight.transform.rotation = Quaternion.Euler(0f, -38f, 0f);
		DisableCollider(headRight);
		RegisterArrowRenderer(headRight);

		return root;
	}

	private void NormalizeExitArrowGeometry()
	{
		if (exitArrow == null)
		{
			return;
		}

		Transform shaft = exitArrow.transform.Find("Arrow Shaft");
		if (shaft != null)
		{
			shaft.position = exitArrow.transform.position + new Vector3(0f, 0f, -0.12f);
			shaft.rotation = Quaternion.identity;
			shaft.localScale = new Vector3(0.9f, 0.12f, 3.3f);
			DisableCollider(shaft.gameObject);
		}

		Transform headLeft = exitArrow.transform.Find("Arrow Head Left");
		if (headLeft != null)
		{
			headLeft.position = exitArrow.transform.position + new Vector3(-0.52f, 0f, 1.34f);
			headLeft.rotation = Quaternion.Euler(0f, 38f, 0f);
			headLeft.localScale = new Vector3(0.64f, 0.12f, 1.72f);
			DisableCollider(headLeft.gameObject);
		}

		Transform headRight = exitArrow.transform.Find("Arrow Head Right");
		if (headRight != null)
		{
			headRight.position = exitArrow.transform.position + new Vector3(0.52f, 0f, 1.34f);
			headRight.rotation = Quaternion.Euler(0f, -38f, 0f);
			headRight.localScale = new Vector3(0.64f, 0.12f, 1.72f);
			DisableCollider(headRight.gameObject);
		}
	}

	private static void DisableCollider(GameObject gameObject)
	{
		Collider collider = gameObject.GetComponent<Collider>();
		if (collider != null)
		{
			collider.enabled = false;
		}
	}

	private void RegisterArrowRenderer(GameObject gameObject)
	{
		Renderer renderer = gameObject.GetComponent<Renderer>();
		if (renderer != null)
		{
			arrowRenderers.Add(renderer);
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

	internal void HandleTrigger(ArenaBakedTriggerZone zone)
	{
		switch (zone.Kind)
		{
			case TriggerKind.Tutorial:
				ShowPrompt(zone.Message, promptDuration, zone.ColorMode, zone.SolidColor);
				break;

			case TriggerKind.ArenaStart:
				if (!arenaStarted)
				{
					arenaStarted = true;
					entryBarrier.SetActive(true);
					exitBarrier.SetActive(true);
					ShowPrompt("Arena active. Defeat every enemy to unlock the barriers.", promptDuration, PromptColorMode.Solid, new Color(1f, 0.55f, 0.55f));
					ShowCounter($"Enemies left: {GetRemainingEnemyCount()}", counterDuration);
				}
				break;

			case TriggerKind.ArenaExit:
				if (!arenaStarted)
				{
					ShowPrompt("Enter the arena before heading for the exit.", promptDuration, PromptColorMode.Solid, Color.white);
				}
				else if (!arenaCleared)
				{
					ShowPrompt("Exit locked. Defeat every enemy in the arena first.", promptDuration, PromptColorMode.AdaptiveContrast, Color.white);
				}
				else
				{
					if (exitArrow != null)
					{
						exitArrow.SetActive(false);
					}

					CompleteRun();
					string timeText = runTimerDisplay != null && runTimerDisplay.HasStarted ? $"\nTime: {ArenaRunTimerDisplay.FormatTime(runTimerDisplay.ElapsedSeconds)}" : string.Empty;
					ShowPrompt($"Arena complete.{timeText}\nFollow the path ahead.", promptDuration, PromptColorMode.AdaptiveHueShift, Color.white);
				}
				break;
		}
	}

	internal bool CanAttemptArenaKill => arenaStarted && !arenaCleared;

	internal ArenaBakedEnemyTarget GetBestKillCandidate()
	{
		if (!CanAttemptArenaKill)
		{
			return null;
		}

		return SelectKillCandidate();
	}

	internal bool TryExecuteArenaKill(ArenaBakedEnemyTarget target, int attackNumber, Vector3 hitPoint, Vector3 hitDirection, float destroyDelay)
	{
		if (!CanAttemptArenaKill || target == null || !target.IsAlive)
		{
			return false;
		}

		target.Kill(new ArenaEnemyKillContext
		{
			AttackNumber = attackNumber,
			HitPoint = hitPoint,
			HitDirection = hitDirection,
			DestroyDelay = destroyDelay,
			PlayEffects = true,
		});

		return true;
	}

	private ArenaBakedEnemyTarget SelectKillCandidate()
	{
		if (playerCamera == null)
		{
			return null;
		}

		Vector3 origin = playerCamera.transform.position;
		Vector3 forward = playerCamera.transform.forward;
		ArenaBakedEnemyTarget bestCandidate = null;
		float bestScore = float.MaxValue;

		for (int i = 0; i < arenaEnemies.Count; i++)
		{
			ArenaBakedEnemyTarget enemy = arenaEnemies[i];
			if (enemy == null || !enemy.IsAlive)
			{
				continue;
			}

			Vector3 targetPoint = enemy.GetAimPoint();
			Vector3 toTarget = targetPoint - origin;
			float distance = toTarget.magnitude;
			if (distance > fakeKillRange || distance <= 0.01f)
			{
				continue;
			}

			float angle = Vector3.Angle(forward, toTarget.normalized);
			if (angle > fakeKillAngle)
			{
				continue;
			}

			float score = angle * 4f + distance;
			if (score < bestScore)
			{
				bestScore = score;
				bestCandidate = enemy;
			}
		}

		return bestCandidate;
	}

	internal void NotifyEnemyKilled(ArenaBakedEnemyTarget enemy)
	{
		if (runTimerDisplay != null && !runTimerDisplay.HasStarted)
		{
			runTimerDisplay.BeginRun();
		}

		int remaining = GetRemainingEnemyCount();
		ShowCounter($"Enemies left: {remaining}", counterDuration);

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
			if (exitArrow != null)
			{
				exitArrow.SetActive(true);
			}

			ShowPrompt("Arena cleared. Follow the ground arrow to leave.", promptDuration, PromptColorMode.AdaptiveHueShift, Color.white);
		}
	}

	private void CompleteRun()
	{
		if (runTimerDisplay != null && !runTimerDisplay.HasFinished)
		{
			runTimerDisplay.FinishRun();
		}

		if (scoreSubmitted || wallLeaderboardDisplay == null || runTimerDisplay == null || !runTimerDisplay.HasStarted)
		{
			return;
		}

		wallLeaderboardDisplay.SubmitScore(playerLeaderboardName, runTimerDisplay.ElapsedSeconds);
		scoreSubmitted = true;
	}

	private int GetRemainingEnemyCount()
	{
		int remaining = 0;
		for (int i = 0; i < arenaEnemies.Count; i++)
		{
			if (arenaEnemies[i] != null && arenaEnemies[i].IsAlive)
			{
				remaining++;
			}
		}

		return remaining;
	}

	private void ShowPrompt(string message, float duration, PromptColorMode colorMode, Color solidColor)
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

	private Color ResolvePromptColor(PromptColorMode colorMode, Color solidColor)
	{
		if (colorMode == PromptColorMode.Solid)
		{
			return solidColor;
		}

		Color backgroundColor = SampleSceneColor();
		Color.RGBToHSV(backgroundColor, out float hue, out float saturation, out float value);

		float baseHue = Mathf.Repeat(hue + 0.5f, 1f);
		if (colorMode == PromptColorMode.AdaptiveHueShift)
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

	private void AnimateExitArrow()
	{
		if (exitArrow == null || !exitArrow.activeSelf)
		{
			return;
		}

		float hue = Mathf.Repeat(Time.unscaledTime * 0.22f, 1f);
		Color animatedColor = Color.HSVToRGB(hue, 0.85f, 1f);
		float bob = Mathf.Sin(Time.unscaledTime * 2.2f) * 0.18f;
		exitArrow.transform.position = courseOrigin + new Vector3(0f, 0.18f + bob, 68f);

		for (int i = 0; i < arrowRenderers.Count; i++)
		{
			Renderer renderer = arrowRenderers[i];
			if (renderer == null)
			{
				continue;
			}

			Material material = renderer.sharedMaterial;
			if (material == null)
			{
				continue;
			}

			if (material.HasProperty("_BaseColor"))
			{
				material.SetColor("_BaseColor", animatedColor);
			}
			else if (material.HasProperty("_Color"))
			{
				material.SetColor("_Color", animatedColor);
			}

			if (material.HasProperty("_EmissionColor"))
			{
				material.SetColor("_EmissionColor", animatedColor * 0.6f);
			}
		}
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
		arrowRenderers.Clear();
	}


}

internal struct ArenaEnemyKillContext
{
	public int AttackNumber;
	public Vector3 HitPoint;
	public Vector3 HitDirection;
	public float DestroyDelay;
	public bool PlayEffects;
}

public class ArenaBakedTriggerZone : MonoBehaviour
{
	private ArenaTutorialSceneController controller;
	[SerializeField] private ArenaTutorialSceneController.TriggerKind kind;
	[SerializeField] private string message;
	[SerializeField] private ArenaTutorialSceneController.PromptColorMode colorMode;
	[SerializeField] private Color solidColor = Color.white;
	private bool triggered;

	internal ArenaTutorialSceneController.TriggerKind Kind => kind;
	internal ArenaTutorialSceneController.PromptColorMode ColorMode => colorMode;
	public Color SolidColor => solidColor;
	public string Message => message;

	internal void Initialize(ArenaTutorialSceneController owner, ArenaTutorialSceneController.TriggerKind zoneKind, string promptMessage, ArenaTutorialSceneController.PromptColorMode zoneColorMode, Color zoneSolidColor)
	{
		controller = owner;
		kind = zoneKind;
		message = promptMessage;
		colorMode = zoneColorMode;
		solidColor = zoneSolidColor;
		triggered = false;
	}

	public void Bind(ArenaTutorialSceneController owner)
	{
		controller = owner;
		triggered = false;
	}

	private void OnEnable()
	{
		triggered = false;
	}

	private void OnTriggerEnter(Collider other)
	{
		if (controller == null || triggered || !controller.IsPlayerCollider(other))
		{
			return;
		}

		triggered = true;
		controller.HandleTrigger(this);
	}
}

public class ArenaBakedEnemyTarget : MonoBehaviour
{
	private ArenaTutorialSceneController controller;

	public bool IsAlive { get; private set; } = true;

	public void Initialize(ArenaTutorialSceneController owner)
	{
		controller = owner;
		IsAlive = true;
	}

	public Vector3 GetAimPoint()
	{
		return transform.position + Vector3.up * 1.2f;
	}

	public void Kill()
	{
		Kill(new ArenaEnemyKillContext
		{
			AttackNumber = 0,
			HitPoint = GetAimPoint(),
			HitDirection = -transform.forward,
			DestroyDelay = 0.05f,
			PlayEffects = false,
		});
	}

	internal void Kill(ArenaEnemyKillContext killContext)
	{
		if (!IsAlive)
		{
			return;
		}

		IsAlive = false;
		Vector3 hitPoint = killContext.HitPoint.sqrMagnitude > 0.001f ? killContext.HitPoint : GetAimPoint();
		Vector3 hitDirection = killContext.HitDirection.sqrMagnitude > 0.001f ? killContext.HitDirection : -transform.forward;
		EnemyEffect enemyEffect = GetComponent<EnemyEffect>();
		bool useDeathEffects = killContext.PlayEffects && enemyEffect != null;

		LocomotionSimpleAgent locomotionAgent = GetComponent<LocomotionSimpleAgent>();
		if (locomotionAgent != null)
		{
			locomotionAgent.enabled = false;
		}

		KnifePawnController pawnController = GetComponent<KnifePawnController>();
		if (pawnController != null)
		{
			pawnController.enabled = false;
		}

		GunPawnController gunPawnController = GetComponent<GunPawnController>();
		if (gunPawnController != null)
		{
			gunPawnController.enabled = false;
		}

		NavMeshAgent agent = GetComponent<NavMeshAgent>();
		if (agent != null)
		{
			if (agent.isOnNavMesh)
			{
				agent.isStopped = true;
				agent.ResetPath();
			}

			agent.enabled = false;
		}

		AudioSource audioSource = GetComponent<AudioSource>();
		if (audioSource != null)
		{
			audioSource.Stop();
		}

		if (useDeathEffects)
		{
			enemyEffect.PlayHitEffects(hitPoint, hitDirection);
			enemyEffect.ActivateRagdoll(hitDirection);
		}
		else
		{
			Animator animator = GetComponent<Animator>();
			if (animator != null)
			{
				animator.enabled = false;
			}

			Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
			for (int i = 0; i < renderers.Length; i++)
			{
				renderers[i].enabled = false;
			}

			Collider[] colliders = GetComponentsInChildren<Collider>(true);
			for (int i = 0; i < colliders.Length; i++)
			{
				colliders[i].enabled = false;
			}
		}

		controller?.NotifyEnemyKilled(this);
		Destroy(gameObject, useDeathEffects ? Mathf.Max(0.5f, killContext.DestroyDelay) : Mathf.Max(0.05f, killContext.DestroyDelay));
	}
}

public class ArenaRunTimerDisplay : MonoBehaviour
{
	private const string CanvasObjectName = "Run Timer Canvas";
	private const string PanelObjectName = "Run Timer Panel";

	[SerializeField] private string timerTitle = "RUN TIME";
	[SerializeField] private Vector2 panelAnchoredPosition = new Vector2(0f, -76f);
	[SerializeField] private Vector2 panelSize = new Vector2(560f, 132f);
	[SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.48f);
	[SerializeField] private Color titleColor = new Color(1f, 1f, 1f, 0.72f);
	[SerializeField] private Color valueColor = Color.white;
	[SerializeField] private int sortingOrder = 215;

	private CanvasGroup panelCanvasGroup;
	private TextMeshProUGUI titleLabel;
	private TextMeshProUGUI valueLabel;
	private bool hasStarted;
	private bool hasFinished;
	private float startedAt = -1f;
	private float finishedAt = -1f;

	private void Awake()
	{
		TryBindExistingUi();
	}

	private void Update()
	{
		if (hasStarted && !hasFinished)
		{
			UpdateTimerLabel();
		}
	}

	public bool HasStarted => hasStarted;
	public bool HasFinished => hasFinished;
	public float ElapsedSeconds => !hasStarted ? 0f : (hasFinished ? finishedAt - startedAt : Time.unscaledTime - startedAt);

	public void BeginRun()
	{
		EnsureRuntimeUi();
		if (hasStarted)
		{
			return;
		}

		hasStarted = true;
		hasFinished = false;
		startedAt = Time.unscaledTime;
		finishedAt = -1f;
		panelCanvasGroup.alpha = 1f;
		UpdateTimerLabel();
	}

	public void FinishRun()
	{
		if (!hasStarted || hasFinished)
		{
			return;
		}

		finishedAt = Time.unscaledTime;
		hasFinished = true;
		UpdateTimerLabel();
	}

	public void ResetRun()
	{
		hasStarted = false;
		hasFinished = false;
		startedAt = -1f;
		finishedAt = -1f;

		if (panelCanvasGroup != null)
		{
			panelCanvasGroup.alpha = 0f;
		}
	}

	public void EnsureSceneBuilt()
	{
		EnsureRuntimeUi();
		ApplyVisuals();
		UpdateTimerLabel();
	}

	public void EnsureRuntimeUi()
	{
		if (panelCanvasGroup != null || TryBindExistingUi())
		{
			ApplyVisuals();
			return;
		}

		GameObject canvasObject = new GameObject(CanvasObjectName);
		canvasObject.transform.SetParent(transform, false);

		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = sortingOrder;

		CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920f, 1080f);
		scaler.matchWidthOrHeight = 0.5f;

		canvasObject.AddComponent<GraphicRaycaster>();

		GameObject panelObject = new GameObject(PanelObjectName);
		panelObject.transform.SetParent(canvasObject.transform, false);

		RectTransform panelRect = panelObject.AddComponent<RectTransform>();
		panelRect.anchorMin = new Vector2(0.5f, 1f);
		panelRect.anchorMax = new Vector2(0.5f, 1f);
		panelRect.pivot = new Vector2(0.5f, 1f);
		panelRect.anchoredPosition = panelAnchoredPosition;
		panelRect.sizeDelta = panelSize;

		panelCanvasGroup = panelObject.AddComponent<CanvasGroup>();
		panelCanvasGroup.alpha = 0f;
		panelCanvasGroup.interactable = false;
		panelCanvasGroup.blocksRaycasts = false;

		GameObject backgroundObject = new GameObject("Background");
		backgroundObject.transform.SetParent(panelObject.transform, false);
		RectTransform backgroundRect = backgroundObject.AddComponent<RectTransform>();
		backgroundRect.anchorMin = Vector2.zero;
		backgroundRect.anchorMax = Vector2.one;
		backgroundRect.offsetMin = Vector2.zero;
		backgroundRect.offsetMax = Vector2.zero;
		Image background = backgroundObject.AddComponent<Image>();
		background.color = panelColor;
		background.raycastTarget = false;

		titleLabel = CreateLabel(panelObject.transform, "Title", new Vector2(0f, -28f), new Vector2(500f, 36f), 28f, TextAlignmentOptions.Center);
		titleLabel.text = timerTitle;
		titleLabel.color = titleColor;
		titleLabel.characterSpacing = 16f;
		titleLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;

		valueLabel = CreateLabel(panelObject.transform, "Value", new Vector2(0f, -80f), new Vector2(500f, 62f), 56f, TextAlignmentOptions.Center);
		valueLabel.color = valueColor;
		valueLabel.fontStyle = FontStyles.Bold;
		valueLabel.text = FormatTime(0f);

		ApplyVisuals();
	}

	public static string FormatTime(float seconds)
	{
		float safeSeconds = Mathf.Max(0f, seconds);
		int minutes = Mathf.FloorToInt(safeSeconds / 60f);
		float remainingSeconds = safeSeconds - minutes * 60f;
		return $"{minutes:00}:{remainingSeconds:00.000}";
	}

	private void UpdateTimerLabel()
	{
		if (valueLabel == null)
		{
			return;
		}

		valueLabel.text = FormatTime(ElapsedSeconds);
	}

	private bool TryBindExistingUi()
	{
		Transform canvasTransform = transform.Find(CanvasObjectName);
		if (canvasTransform == null)
		{
			return false;
		}

		Transform panelTransform = canvasTransform.Find(PanelObjectName);
		if (panelTransform == null)
		{
			return false;
		}

		panelCanvasGroup = panelTransform.GetComponent<CanvasGroup>();
		titleLabel = panelTransform.Find("Title")?.GetComponent<TextMeshProUGUI>();
		valueLabel = panelTransform.Find("Value")?.GetComponent<TextMeshProUGUI>();
		return panelCanvasGroup != null && titleLabel != null && valueLabel != null;
	}

	private void ApplyVisuals()
	{
		if (panelCanvasGroup != null)
		{
			Image background = panelCanvasGroup.transform.Find("Background")?.GetComponent<Image>();
			if (background != null)
			{
				background.color = panelColor;
				background.raycastTarget = false;
			}
		}

		Canvas canvas = GetComponentInChildren<Canvas>(true);
		if (canvas != null)
		{
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = sortingOrder;
		}

		if (titleLabel != null)
		{
			titleLabel.text = timerTitle;
			titleLabel.color = titleColor;
		}

		if (valueLabel != null)
		{
			valueLabel.color = valueColor;
			if (!hasStarted)
			{
				valueLabel.text = FormatTime(0f);
			}
		}
	}

	private static TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
	{
		GameObject labelObject = new GameObject(name);
		labelObject.transform.SetParent(parent, false);

		RectTransform labelRect = labelObject.AddComponent<RectTransform>();
		labelRect.anchorMin = new Vector2(0.5f, 1f);
		labelRect.anchorMax = new Vector2(0.5f, 1f);
		labelRect.pivot = new Vector2(0.5f, 0.5f);
		labelRect.anchoredPosition = anchoredPosition;
		labelRect.sizeDelta = size;

		TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
		label.font = TMP_Settings.defaultFontAsset;
		label.fontSize = fontSize;
		label.enableAutoSizing = false;
		label.alignment = alignment;
		label.enableWordWrapping = false;
		label.raycastTarget = false;
		return label;
	}
}

public class ArenaWallLeaderboardDisplay : MonoBehaviour
{
	[Serializable]
	public struct EntryData
	{
		public string playerName;
		public float seconds;
	}

	private sealed class RuntimeEntry
	{
		public string PlayerName;
		public float Seconds;
		public bool IsPlayer;
	}

	private struct PersistedEntryData
	{
		public string PlayerName;
		public float Seconds;
		public bool IsPlayer;
	}

	[SerializeField] private string leaderboardTitle = "BEST TIMES";
	[SerializeField] private string leaderboardSubtitle = "SPRAY THE WALL";
	[SerializeField] private Vector2 boardSize = new Vector2(3f, 2f);
	[SerializeField] private float boardThickness = 0.08f;
	[SerializeField] private Color boardColor = new Color(0.12f, 0.13f, 0.15f, 1f);
	[SerializeField] private Color titleColor = new Color(1f, 1f, 1f, 0.98f);
	[SerializeField] private Color subtitleColor = new Color(1f, 1f, 1f, 0.76f);
	[SerializeField] private Color entryColor = new Color(1f, 1f, 1f, 0.94f);
	[SerializeField] private Color highlightColor = new Color(1f, 0.94f, 0.84f, 1f);
	[SerializeField] private int maxEntries = 6;
	[SerializeField] private Vector2 canvasResolution = new Vector2(1180f, 860f);
	[SerializeField] private float canvasScale = 0.0024f;
	[SerializeField] private List<EntryData> seedEntries = new List<EntryData>
	{
		new EntryData { playerName = "CREATOR 01", seconds = -1f },
		new EntryData { playerName = "CREATOR 02", seconds = -1f },
		new EntryData { playerName = "CREATOR 03", seconds = -1f },
		new EntryData { playerName = "CREATOR 04", seconds = -1f },
	};

	private static readonly Dictionary<string, List<PersistedEntryData>> persistedEntriesByKey = new Dictionary<string, List<PersistedEntryData>>(StringComparer.Ordinal);
	private readonly List<RuntimeEntry> runtimeEntries = new List<RuntimeEntry>();
	private readonly List<TextMeshProUGUI> rowLabels = new List<TextMeshProUGUI>();
	private MeshRenderer boardRenderer;
	private Material boardMaterial;
	private TextMeshProUGUI titleLabel;
	private TextMeshProUGUI subtitleLabel;
	private bool built;

	private void Awake()
	{
		EnsureBuilt();
		ApplySeedEntriesIfNeeded();
		RefreshVisuals();
	}

	public void EnsureSceneBuilt()
	{
		EnsureBuilt();
		ApplySeedEntriesIfNeeded();
		RefreshVisuals();
	}

	public void PersistCurrentEntries()
	{
		PersistRuntimeEntries();
	}

	public void Configure(string title, string subtitle, IReadOnlyList<EntryData> entries)
	{
		leaderboardTitle = title;
		leaderboardSubtitle = subtitle;
		ReplaceSeedEntries(entries);
	}

	public void ReplaceSeedEntries(IReadOnlyList<EntryData> entries)
	{
		runtimeEntries.Clear();

		if (entries != null)
		{
			for (int i = 0; i < entries.Count; i++)
			{
				EntryData entry = entries[i];
				if (string.IsNullOrWhiteSpace(entry.playerName))
				{
					continue;
				}

				runtimeEntries.Add(new RuntimeEntry
				{
					PlayerName = entry.playerName,
					Seconds = entry.seconds,
					IsPlayer = false,
				});
			}
		}

		SortEntries();
		PersistRuntimeEntries();
		RefreshVisuals();
	}

	public void SubmitScore(string playerName, float seconds)
	{
		if (string.IsNullOrWhiteSpace(playerName))
		{
			return;
		}

		EnsureBuilt();
		ApplySeedEntriesIfNeeded();

		RuntimeEntry existingEntry = runtimeEntries.Find(entry => string.Equals(entry.PlayerName, playerName, StringComparison.OrdinalIgnoreCase));
		if (existingEntry != null)
		{
			existingEntry.Seconds = existingEntry.Seconds >= 0f ? Mathf.Min(existingEntry.Seconds, seconds) : seconds;
			existingEntry.IsPlayer = true;
		}
		else
		{
			runtimeEntries.Add(new RuntimeEntry
			{
				PlayerName = playerName,
				Seconds = seconds,
				IsPlayer = true,
			});
		}

		SortEntries();
		if (runtimeEntries.Count > maxEntries)
		{
			runtimeEntries.RemoveRange(maxEntries, runtimeEntries.Count - maxEntries);
		}

		PersistRuntimeEntries();
		RefreshVisuals();
	}

	private void EnsureBuilt()
	{
		if (built)
		{
			return;
		}

		if (TryBindExistingHierarchy())
		{
			ApplyUnpaintableLayer();
			built = true;
			return;
		}

		built = true;

		GameObject plaqueObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
		plaqueObject.name = "Leaderboard Plaque";
		plaqueObject.transform.SetParent(transform, false);
		plaqueObject.transform.localPosition = Vector3.back * (boardThickness * 0.5f);
		plaqueObject.transform.localScale = new Vector3(boardSize.x, boardSize.y, boardThickness);

		Collider plaqueCollider = plaqueObject.GetComponent<Collider>();
		if (plaqueCollider != null)
		{
			plaqueCollider.enabled = false;
		}

		boardRenderer = plaqueObject.GetComponent<MeshRenderer>();
		if (boardRenderer != null)
		{
			boardMaterial = CreateBoardMaterial();
			boardRenderer.sharedMaterial = boardMaterial;
		}

		ApplyUnpaintableLayer();

		GameObject canvasObject = new GameObject("Leaderboard Canvas");
		canvasObject.transform.SetParent(transform, false);
		canvasObject.transform.localPosition = new Vector3(0.17f, 1.81f, -0.308f);
		ApplyUnpaintableLayer();

		Canvas canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.WorldSpace;

		RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
		canvasRect.sizeDelta = canvasResolution;
		canvasRect.localScale = Vector3.one * canvasScale;

		canvasObject.AddComponent<GraphicRaycaster>();

		titleLabel = CreateTextLabel(canvasObject.transform, "Title", new Vector2(0f, -92f), new Vector2(920f, 92f), 66f, TextAlignmentOptions.Center);
		titleLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
		titleLabel.color = titleColor;
		titleLabel.characterSpacing = 18f;

		subtitleLabel = CreateTextLabel(canvasObject.transform, "Subtitle", new Vector2(0f, -156f), new Vector2(920f, 40f), 24f, TextAlignmentOptions.Center);
		subtitleLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
		subtitleLabel.color = subtitleColor;
		subtitleLabel.characterSpacing = 10f;

		for (int i = 0; i < maxEntries; i++)
		{
			TextMeshProUGUI rowLabel = CreateTextLabel(canvasObject.transform, $"Row {i + 1}", new Vector2(0f, -258f - i * 76f), new Vector2(960f, 52f), 36f, TextAlignmentOptions.Left);
			rowLabel.fontStyle = FontStyles.Bold;
			rowLabel.color = entryColor;
			rowLabels.Add(rowLabel);
		}
	}

	private void ApplySeedEntriesIfNeeded()
	{
		if (runtimeEntries.Count > 0)
		{
			return;
		}

		if (TryRestorePersistedEntries())
		{
			return;
		}

		ReplaceSeedEntries(seedEntries);
	}

	private bool TryRestorePersistedEntries()
	{
		if (!persistedEntriesByKey.TryGetValue(GetPersistenceKey(), out List<PersistedEntryData> persistedEntries) || persistedEntries == null || persistedEntries.Count == 0)
		{
			return false;
		}

		runtimeEntries.Clear();
		for (int i = 0; i < persistedEntries.Count; i++)
		{
			PersistedEntryData entry = persistedEntries[i];
			if (string.IsNullOrWhiteSpace(entry.PlayerName))
			{
				continue;
			}

			runtimeEntries.Add(new RuntimeEntry
			{
				PlayerName = entry.PlayerName,
				Seconds = entry.Seconds,
				IsPlayer = entry.IsPlayer,
			});
		}

		SortEntries();
		return runtimeEntries.Count > 0;
	}

	private void PersistRuntimeEntries()
	{
		List<PersistedEntryData> persistedEntries = new List<PersistedEntryData>(runtimeEntries.Count);
		for (int i = 0; i < runtimeEntries.Count; i++)
		{
			RuntimeEntry entry = runtimeEntries[i];
			persistedEntries.Add(new PersistedEntryData
			{
				PlayerName = entry.PlayerName,
				Seconds = entry.Seconds,
				IsPlayer = entry.IsPlayer,
			});
		}

		persistedEntriesByKey[GetPersistenceKey()] = persistedEntries;
	}

	private string GetPersistenceKey()
	{
		string sceneKey = string.IsNullOrEmpty(gameObject.scene.path) ? gameObject.scene.name : gameObject.scene.path;
		return sceneKey + ":" + GetHierarchyPath(transform);
	}

	private static string GetHierarchyPath(Transform current)
	{
		string path = current.name;
		while (current.parent != null)
		{
			current = current.parent;
			path = current.name + "/" + path;
		}

		return path;
	}

	private void RefreshVisuals()
	{
		if (!built)
		{
			return;
		}

		if (boardRenderer != null)
		{
			boardMaterial = boardRenderer.sharedMaterial;
			if (boardMaterial != null)
			{
				if (boardMaterial.HasProperty("_BaseColor"))
				{
					boardMaterial.SetColor("_BaseColor", boardColor);
				}
				else if (boardMaterial.HasProperty("_Color"))
				{
					boardMaterial.SetColor("_Color", boardColor);
				}

				if (boardMaterial.HasProperty("_EmissionColor"))
				{
					boardMaterial.EnableKeyword("_EMISSION");
					boardMaterial.SetColor("_EmissionColor", boardColor * 0.06f);
				}
			}
		}

		if (titleLabel != null)
		{
			titleLabel.text = leaderboardTitle;
		}

		if (subtitleLabel != null)
		{
			subtitleLabel.text = leaderboardSubtitle;
		}

		for (int i = 0; i < rowLabels.Count; i++)
		{
			TextMeshProUGUI rowLabel = rowLabels[i];
			if (i >= runtimeEntries.Count)
			{
				rowLabel.text = string.Empty;
				continue;
			}

			RuntimeEntry entry = runtimeEntries[i];
			string timeText = entry.Seconds >= 0f ? ArenaRunTimerDisplay.FormatTime(entry.Seconds) : "--:--.---";
			rowLabel.text = $"{i + 1}.  {entry.PlayerName.ToUpperInvariant(),-12}  {timeText}";
			rowLabel.color = entry.IsPlayer ? highlightColor : entryColor;
		}
	}

	private bool TryBindExistingHierarchy()
	{
		Transform plaqueTransform = transform.Find("Leaderboard Plaque");
		Transform canvasTransform = transform.Find("Leaderboard Canvas");
		if (plaqueTransform == null || canvasTransform == null)
		{
			return false;
		}

		boardRenderer = plaqueTransform.GetComponent<MeshRenderer>();
		boardMaterial = boardRenderer != null ? boardRenderer.sharedMaterial : null;
		titleLabel = canvasTransform.Find("Title")?.GetComponent<TextMeshProUGUI>();
		subtitleLabel = canvasTransform.Find("Subtitle")?.GetComponent<TextMeshProUGUI>();

		rowLabels.Clear();
		for (int i = 0; i < maxEntries; i++)
		{
			TextMeshProUGUI rowLabel = canvasTransform.Find($"Row {i + 1}")?.GetComponent<TextMeshProUGUI>();
			if (rowLabel == null)
			{
				rowLabels.Clear();
				return false;
			}

			rowLabels.Add(rowLabel);
		}

		return boardRenderer != null && titleLabel != null && subtitleLabel != null;
	}

	private void ApplyUnpaintableLayer()
	{
		int unpaintableLayer = LayerMask.NameToLayer("Unpaintable");
		if (unpaintableLayer < 0)
		{
			return;
		}

		SetLayerRecursively(gameObject, unpaintableLayer);
	}

	private void SortEntries()
	{
		runtimeEntries.Sort(static (left, right) =>
		{
			bool leftHasTime = left.Seconds >= 0f;
			bool rightHasTime = right.Seconds >= 0f;

			if (leftHasTime != rightHasTime)
			{
				return leftHasTime ? -1 : 1;
			}

			if (!leftHasTime)
			{
				return string.Compare(left.PlayerName, right.PlayerName, StringComparison.OrdinalIgnoreCase);
			}

			int timeCompare = left.Seconds.CompareTo(right.Seconds);
			if (timeCompare != 0)
			{
				return timeCompare;
			}

			if (left.IsPlayer != right.IsPlayer)
			{
				return left.IsPlayer ? -1 : 1;
			}

			return string.Compare(left.PlayerName, right.PlayerName, StringComparison.OrdinalIgnoreCase);
		});
	}

	private Material CreateBoardMaterial()
	{
		Shader shader = Shader.Find("Universal Render Pipeline/Lit");
		if (shader == null)
		{
			shader = Shader.Find("Standard");
		}

		Material material = new Material(shader)
		{
			name = "Leaderboard Plaque Material",
		};

		if (material.HasProperty("_BaseColor"))
		{
			material.SetColor("_BaseColor", boardColor);
		}
		else if (material.HasProperty("_Color"))
		{
			material.SetColor("_Color", boardColor);
		}

		if (material.HasProperty("_EmissionColor"))
		{
			material.EnableKeyword("_EMISSION");
			material.SetColor("_EmissionColor", boardColor * 0.06f);
		}

		return material;
	}

	private static TextMeshProUGUI CreateTextLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment)
	{
		GameObject labelObject = new GameObject(name);
		labelObject.transform.SetParent(parent, false);
		labelObject.layer = parent.gameObject.layer;

		RectTransform labelRect = labelObject.AddComponent<RectTransform>();
		labelRect.anchorMin = new Vector2(0.5f, 0f);
		labelRect.anchorMax = new Vector2(0.5f, 0f);
		labelRect.pivot = new Vector2(0.5f, 1f);
		labelRect.anchoredPosition = anchoredPosition;
		labelRect.sizeDelta = size;

		TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
		label.font = TMP_Settings.defaultFontAsset;
		label.fontSize = fontSize;
		label.enableWordWrapping = false;
		label.alignment = alignment;
		label.raycastTarget = false;
		label.outlineWidth = 0.12f;
		label.outlineColor = new Color(1f, 1f, 1f, 0.08f);
		return label;
	}

	private static void SetLayerRecursively(GameObject root, int layer)
	{
		root.layer = layer;
		for (int i = 0; i < root.transform.childCount; i++)
		{
			SetLayerRecursively(root.transform.GetChild(i).gameObject, layer);
		}
	}
}