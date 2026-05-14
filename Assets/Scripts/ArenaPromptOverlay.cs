using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Arena/Prompt Overlay")]
public class ArenaPromptOverlay : MonoBehaviour
{
	private const string CanvasObjectName = "Arena Prompt UI";

	[SerializeField] private int sortingOrder = 200;
	[SerializeField] private Vector2 promptPanelPosition = new Vector2(0f, 360f);
	[SerializeField] private Vector2 promptPanelSize = new Vector2(940f, 150f);
	[SerializeField] private Vector2 counterPanelPosition = new Vector2(210f, -60f);
	[SerializeField] private Vector2 counterPanelSize = new Vector2(420f, 90f);

	private CanvasGroup promptCanvasGroup;
	private CanvasGroup counterCanvasGroup;
	private Image promptBackground;
	private Image counterBackground;
	private TextMeshProUGUI promptLabel;
	private TextMeshProUGUI counterLabel;
	private Camera targetCamera;
	private ActivePromptState activePrompt;
	private float counterHideAt = -1f;

	private sealed class ActivePromptState
	{
		public string Message;
		public ArenaPromptColorMode ColorMode;
		public Color SolidColor;
		public float HideAt;
	}

	private void Awake()
	{
		EnsureSceneBuilt();
	}

	private void Update()
	{
		UpdatePromptVisuals();
		UpdateCounterVisuals();
	}

	public void EnsureSceneBuilt()
	{
		if (!TryBindExistingUi())
		{
			BuildRuntimeUi();
		}
	}

	public void SetCamera(Camera camera)
	{
		if (camera != null)
		{
			targetCamera = camera;
		}
	}

	public void ShowPrompt(string message, float duration, ArenaPromptColorMode colorMode, Color solidColor)
	{
		EnsureSceneBuilt();
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

	public void ShowCounter(string message, float duration)
	{
		EnsureSceneBuilt();
		if (counterCanvasGroup == null || counterLabel == null)
		{
			return;
		}

		counterLabel.text = message;
		counterHideAt = Time.unscaledTime + duration;
		counterCanvasGroup.alpha = 1f;
	}

	public void HideAll()
	{
		activePrompt = null;
		counterHideAt = -1f;

		if (promptCanvasGroup != null)
		{
			promptCanvasGroup.alpha = 0f;
		}

		if (counterCanvasGroup != null)
		{
			counterCanvasGroup.alpha = 0f;
		}
	}

	private void BuildRuntimeUi()
	{
		Transform existingCanvas = transform.Find(CanvasObjectName);
		if (existingCanvas != null)
		{
			if (Application.isPlaying)
			{
				Destroy(existingCanvas.gameObject);
			}
			else
			{
				DestroyImmediate(existingCanvas.gameObject);
			}
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

		promptCanvasGroup = CreatePanel(canvasObject.transform, "Prompt Panel", promptPanelPosition, promptPanelSize, out promptBackground, out promptLabel);
		promptLabel.alignment = TextAlignmentOptions.Center;
		promptLabel.fontSize = 44f;
		promptLabel.enableWordWrapping = true;

		counterCanvasGroup = CreatePanel(canvasObject.transform, "Counter Panel", counterPanelPosition, counterPanelSize, out counterBackground, out counterLabel, true);
		counterLabel.alignment = TextAlignmentOptions.Left;
		counterLabel.fontSize = 34f;
		counterLabel.enableWordWrapping = false;
	}

	private bool TryBindExistingUi()
	{
		Transform canvasTransform = transform.Find(CanvasObjectName);
		if (canvasTransform == null)
		{
			return false;
		}

		Transform promptPanel = canvasTransform.Find("Prompt Panel");
		Transform counterPanel = canvasTransform.Find("Counter Panel");
		if (promptPanel == null || counterPanel == null)
		{
			return false;
		}

		promptCanvasGroup = promptPanel.GetComponent<CanvasGroup>();
		counterCanvasGroup = counterPanel.GetComponent<CanvasGroup>();
		promptBackground = promptPanel.Find("Background")?.GetComponent<Image>();
		counterBackground = counterPanel.Find("Background")?.GetComponent<Image>();
		promptLabel = promptPanel.Find("Label")?.GetComponent<TextMeshProUGUI>();
		counterLabel = counterPanel.Find("Label")?.GetComponent<TextMeshProUGUI>();
		return promptCanvasGroup != null && counterCanvasGroup != null && promptBackground != null && counterBackground != null && promptLabel != null && counterLabel != null;
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
		Camera samplingCamera = targetCamera != null ? targetCamera : Camera.main;
		if (samplingCamera == null)
		{
			return Color.black;
		}

		Ray ray = samplingCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
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

		return samplingCamera.backgroundColor;
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
}