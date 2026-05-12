using System.Collections.Generic;
using TMPro;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ArenaTutorialSceneController : MonoBehaviour
{
	private enum PromptColorMode
	{
		Solid,
		AdaptiveContrast,
		AdaptiveHueShift,
	}

	private enum TriggerKind
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

	private readonly List<ArenaEnemyTarget> arenaEnemies = new List<ArenaEnemyTarget>();
	private readonly List<Renderer> arrowRenderers = new List<Renderer>();

	private Transform player;
	private CharacterController playerController;
	private Camera playerCamera;
	private NavMeshSurface navMeshSurface;

	private GameObject runtimeRoot;
	private GameObject entryBarrier;
	private GameObject exitBarrier;
	private GameObject exitArrow;

	private CanvasGroup promptCanvasGroup;
	private CanvasGroup counterCanvasGroup;
	private Image promptBackground;
	private Image counterBackground;
	private TextMeshProUGUI promptLabel;
	private TextMeshProUGUI counterLabel;

	private ActivePromptState activePrompt;
	private float counterHideAt = -1f;
	private bool arenaStarted;
	private bool arenaCleared;

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

		CreateRuntimeUi();
		BuildRuntimeCourse();
		RepositionPlayer();
		ShowPrompt("Tutorial 1/3\nMove: WASD, Jump: Space, Sprint: Shift", promptDuration, PromptColorMode.Solid, new Color(0.26f, 0.90f, 1f));
	}

	private void Update()
	{
		UpdatePromptVisuals();
		UpdateCounterVisuals();
		HandleFakeAttackKill();
		AnimateExitArrow();
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

	private void CreateRuntimeUi()
	{
		GameObject canvasObject = new GameObject("Arena Tutorial UI");
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
		runtimeRoot = new GameObject("Arena Tutorial Runtime");
		runtimeRoot.transform.SetParent(transform, false);

		Material floorMaterial = CreateMaterial("Floor", new Color(0.18f, 0.21f, 0.24f), 0f, true);
		Material wallMaterial = CreateMaterial("Wall", new Color(0.10f, 0.12f, 0.16f), 0f, true);
		Material accentMaterial = CreateMaterial("Accent", new Color(0.23f, 0.42f, 0.35f), 0f, true);
		Material barrierMaterial = CreateMaterial("Barrier", new Color(0.85f, 0.16f, 0.18f), 0.6f, false);
		Material arrowMaterial = CreateMaterial("Arrow", new Color(0.10f, 1f, 0.72f), 0f, false);

		Vector3 courseCenter = courseOrigin + new Vector3(0f, -0.5f, 42f);
		CreateBox(runtimeRoot.transform, "Course Floor", courseCenter, new Vector3(18f, 1f, 96f), floorMaterial);
		CreateBox(runtimeRoot.transform, "Left Wall", courseOrigin + new Vector3(-9.5f, 2f, 42f), new Vector3(1f, 4f, 96f), wallMaterial);
		CreateBox(runtimeRoot.transform, "Right Wall", courseOrigin + new Vector3(9.5f, 2f, 42f), new Vector3(1f, 4f, 96f), wallMaterial);
		CreateBox(runtimeRoot.transform, "Arena Back Wall", courseOrigin + new Vector3(0f, 2f, 82f), new Vector3(20f, 4f, 1f), wallMaterial);
		CreateBox(runtimeRoot.transform, "Start Accent", courseOrigin + new Vector3(0f, 0.02f, 7f), new Vector3(10f, 0.04f, 4f), accentMaterial);
		CreateBox(runtimeRoot.transform, "Tutorial Accent", courseOrigin + new Vector3(0f, 0.02f, 21f), new Vector3(10f, 0.04f, 6f), accentMaterial);

		CreateTutorialZone(courseOrigin + new Vector3(0f, 1.4f, 7f), new Vector3(14f, 3f, 5f), "Tutorial 1/3\nMove: WASD, Jump: Space, Sprint: Shift.", PromptColorMode.Solid, new Color(0.26f, 0.90f, 1f));
		CreateTutorialZone(courseOrigin + new Vector3(0f, 1.4f, 18f), new Vector3(14f, 3f, 6f), "Tutorial 2/3\nLeft Click triggers spray and a temporary kill check.", PromptColorMode.AdaptiveContrast, Color.white);
		CreateTutorialZone(courseOrigin + new Vector3(0f, 1.4f, 29f), new Vector3(14f, 3f, 6f), "Tutorial 3/3\nEnter the arena ahead. Defeat every enemy before leaving.", PromptColorMode.AdaptiveHueShift, Color.white);

		CreateEncounterZone(courseOrigin + new Vector3(0f, 1.5f, 37f), new Vector3(14f, 3.2f, 6f), TriggerKind.ArenaStart, null, PromptColorMode.Solid, new Color(1f, 0.4f, 0.4f));
		CreateEncounterZone(courseOrigin + new Vector3(0f, 1.5f, 74f), new Vector3(14f, 3.2f, 6f), TriggerKind.ArenaExit, null, PromptColorMode.Solid, new Color(0.8f, 1f, 0.8f));

		entryBarrier = CreateBox(runtimeRoot.transform, "Arena Entry Barrier", courseOrigin + new Vector3(0f, 1.6f, 34f), new Vector3(16f, 3.2f, 1.2f), barrierMaterial);
		exitBarrier = CreateBox(runtimeRoot.transform, "Arena Exit Barrier", courseOrigin + new Vector3(0f, 1.6f, 66f), new Vector3(16f, 3.2f, 1.2f), barrierMaterial);
		entryBarrier.SetActive(false);
		exitBarrier.SetActive(false);

		navMeshSurface = runtimeRoot.GetComponent<NavMeshSurface>();
		if (navMeshSurface == null)
		{
			navMeshSurface = runtimeRoot.AddComponent<NavMeshSurface>();
		}

		navMeshSurface.collectObjects = CollectObjects.Children;
		navMeshSurface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
		navMeshSurface.ignoreNavMeshAgent = true;
		navMeshSurface.ignoreNavMeshObstacle = true;
		navMeshSurface.BuildNavMesh();

		SpawnArenaEnemy(knifeEnemyPrefab, courseOrigin + new Vector3(-4f, 0.1f, 49f), Quaternion.identity);
		SpawnArenaEnemy(gunEnemyPrefab, courseOrigin + new Vector3(4f, 0.1f, 52f), Quaternion.Euler(0f, 180f, 0f));
		SpawnArenaEnemy(knifeEnemyPrefab, courseOrigin + new Vector3(-2f, 0.1f, 58f), Quaternion.Euler(0f, 180f, 0f));
		SpawnArenaEnemy(gunEnemyPrefab, courseOrigin + new Vector3(3f, 0.1f, 61f), Quaternion.identity);

		exitArrow = CreateArrow(runtimeRoot.transform, courseOrigin + new Vector3(0f, 0.08f, 68f), arrowMaterial);
		exitArrow.SetActive(false);
	}

	private void RepositionPlayer()
	{
		Vector3 startPosition = courseOrigin + new Vector3(0f, 1.05f, 4f);
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

		RuntimeTriggerZone zone = zoneObject.AddComponent<RuntimeTriggerZone>();
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

		if (enemyInstance.GetComponent<LocomotionSimpleAgent>() == null)
		{
			enemyInstance.AddComponent<LocomotionSimpleAgent>();
		}

		KnifePawnController controller = enemyInstance.GetComponent<KnifePawnController>();
		if (controller == null)
		{
			controller = enemyInstance.AddComponent<KnifePawnController>();
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

		ArenaEnemyTarget enemyTarget = enemyInstance.GetComponent<ArenaEnemyTarget>();
		if (enemyTarget == null)
		{
			enemyTarget = enemyInstance.AddComponent<ArenaEnemyTarget>();
		}
		enemyTarget.Initialize(this);
		arenaEnemies.Add(enemyTarget);
		return enemyInstance;
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

		GameObject shaft = CreateBox(root.transform, "Arrow Shaft", position, new Vector3(1f, 0.12f, 3.6f), material);
		DisableCollider(shaft);
		RegisterArrowRenderer(shaft);

		GameObject headLeft = CreateBox(root.transform, "Arrow Head Left", position + new Vector3(-0.9f, 0f, 1.4f), new Vector3(0.9f, 0.12f, 1.8f), material);
		headLeft.transform.rotation = Quaternion.Euler(0f, -42f, 0f);
		DisableCollider(headLeft);
		RegisterArrowRenderer(headLeft);

		GameObject headRight = CreateBox(root.transform, "Arrow Head Right", position + new Vector3(0.9f, 0f, 1.4f), new Vector3(0.9f, 0.12f, 1.8f), material);
		headRight.transform.rotation = Quaternion.Euler(0f, 42f, 0f);
		DisableCollider(headRight);
		RegisterArrowRenderer(headRight);

		return root;
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

	private void HandleTrigger(RuntimeTriggerZone zone)
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
					exitArrow.SetActive(false);
					ShowPrompt("Arena complete. Follow the path ahead.", promptDuration, PromptColorMode.AdaptiveHueShift, Color.white);
				}
				break;
		}
	}

	private void HandleFakeAttackKill()
	{
		if (!arenaStarted || arenaCleared || Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
		{
			return;
		}

		ArenaEnemyTarget candidate = SelectKillCandidate();
		if (candidate != null)
		{
			candidate.Kill();
		}
	}

	private ArenaEnemyTarget SelectKillCandidate()
	{
		if (playerCamera == null)
		{
			return null;
		}

		Vector3 origin = playerCamera.transform.position;
		Vector3 forward = playerCamera.transform.forward;
		ArenaEnemyTarget bestCandidate = null;
		float bestScore = float.MaxValue;

		for (int i = 0; i < arenaEnemies.Count; i++)
		{
			ArenaEnemyTarget enemy = arenaEnemies[i];
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

	private void NotifyEnemyKilled(ArenaEnemyTarget enemy)
	{
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
		counterLabel.text = message;
		counterHideAt = Time.unscaledTime + duration;
		counterCanvasGroup.alpha = 1f;
	}

	private void UpdatePromptVisuals()
	{
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

	private sealed class RuntimeTriggerZone : MonoBehaviour
	{
		private ArenaTutorialSceneController controller;
		private string message;
		private bool triggered;

		public TriggerKind Kind { get; private set; }
		public PromptColorMode ColorMode { get; private set; }
		public Color SolidColor { get; private set; }

		public void Initialize(ArenaTutorialSceneController owner, TriggerKind kind, string promptMessage, PromptColorMode colorMode, Color solidColor)
		{
			controller = owner;
			Kind = kind;
			message = promptMessage;
			ColorMode = colorMode;
			SolidColor = solidColor;
		}

		public string Message => message;

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

	private sealed class ArenaEnemyTarget : MonoBehaviour
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
			if (!IsAlive)
			{
				return;
			}

			IsAlive = false;

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

			Animator animator = GetComponent<Animator>();
			if (animator != null)
			{
				animator.enabled = false;
			}

			AudioSource audioSource = GetComponent<AudioSource>();
			if (audioSource != null)
			{
				audioSource.Stop();
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

			controller.NotifyEnemyKilled(this);
			Destroy(gameObject, 0.05f);
		}
	}
}