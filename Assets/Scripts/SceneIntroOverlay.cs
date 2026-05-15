using Invector.Utils;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneIntroOverlay : MonoBehaviour, vISceneLoadListener
{
	private const string CanvasObjectName = "Intro Canvas";
	private const string RootObjectName = "Intro Root";
	private const string BackgroundWashObjectName = "Background Wash";
	private const string TitleBandObjectName = "Title Band";
	private const string TitleGlowObjectName = "Title Glow";

	[SerializeField] private string introTitle = string.Empty;
	[SerializeField] private string introSubtitle = string.Empty;
	[SerializeField] private bool useSceneNameWhenTitleEmpty = true;
	[SerializeField] public bool playOnStart = true;
	[SerializeField] private float introDuration = 5f;
	[SerializeField] private string introTitleOverride = string.Empty;
	[SerializeField] private float fadeInDuration = 0.6f;
	[SerializeField] private float fadeOutDuration = 1.35f;
	[SerializeField] private float startScale = 1.42f;
	[SerializeField] private float endScale = 1f;
	[SerializeField] private float startFovMultiplier = 0.62f;
	[SerializeField] private int sortingOrder = 240;
	[SerializeField] private Color titleColor = new Color(1f, 1f, 1f, 0.98f);
	[SerializeField] private Color shadowColor = new Color(0.42f, 0.04f, 0.04f, 0.82f);
	[SerializeField] private Color subtitleColor = new Color(1f, 1f, 1f, 0.82f);
	[SerializeField] private Color stripeColor = new Color(0.78f, 0.06f, 0.06f, 0.18f);
	[SerializeField] private Color backgroundWashColor = new Color(0.35f, 0.02f, 0.02f, 0.16f);
	[SerializeField] private Color titleBandColor = new Color(0.06f, 0.01f, 0.01f, 0.42f);
	[SerializeField] private Color titleGlowColor = new Color(1f, 1f, 1f, 0.10f);

	private CanvasGroup canvasGroup;
	private RectTransform rootRect;
	private Image backgroundWash;
	private Image titleBand;
	private Image titleGlow;
	private TextMeshProUGUI shadowLabel;
	private TextMeshProUGUI titleLabel;
	private TextMeshProUGUI subtitleLabel;
	private Image upperStripe;
	private Image lowerStripe;
	private Camera targetCamera;
	private bool isPlaying;
	private float startedAt = -1f;
	private float baseFieldOfView;

	public float IntroDuration => introDuration;

	private void Awake()
	{
		EnsureSceneBuilt();
	}

	private void Start()
	{
		if (playOnStart)
		{
			PlayIntro();
		}
	}

	private void Update()
	{
		if (!isPlaying || canvasGroup == null || rootRect == null)
		{
			return;
		}

		float elapsed = Time.unscaledTime - startedAt;
		float progress = Mathf.Clamp01(introDuration <= 0.01f ? 1f : elapsed / introDuration);
		float alpha = ResolveAlpha(progress);
		float easedProgress = EaseOutCubic(progress);
		float scale = Mathf.Lerp(startScale, endScale, easedProgress);

		canvasGroup.alpha = alpha;
		rootRect.localScale = Vector3.one * scale;

		if (backgroundWash != null)
		{
			Color wash = backgroundWashColor;
			wash.a *= Mathf.Lerp(1.3f, 0.38f, easedProgress) * alpha;
			backgroundWash.color = wash;
		}

		if (titleBand != null)
		{
			Color band = titleBandColor;
			band.a *= Mathf.Lerp(1.15f, 0.55f, easedProgress) * alpha;
			titleBand.color = band;
			titleBand.rectTransform.localScale = new Vector3(Mathf.Lerp(1.08f, 1f, easedProgress), Mathf.Lerp(1.14f, 1f, easedProgress), 1f);
		}

		if (titleGlow != null)
		{
			Color glow = titleGlowColor;
			glow.a *= Mathf.Lerp(1.3f, 0.45f, easedProgress) * alpha;
			titleGlow.color = glow;
			titleGlow.rectTransform.localScale = new Vector3(Mathf.Lerp(1.15f, 1f, easedProgress), Mathf.Lerp(1.08f, 1f, easedProgress), 1f);
		}

		if (upperStripe != null)
		{
			Color stripe = stripeColor;
			stripe.a *= Mathf.Lerp(0.8f, 1.1f, alpha);
			upperStripe.color = stripe;
		}

		if (lowerStripe != null)
		{
			Color stripe = stripeColor;
			stripe.a *= Mathf.Lerp(1.1f, 0.8f, alpha);
			lowerStripe.color = stripe;
		}

		if (shadowLabel != null)
		{
			shadowLabel.rectTransform.anchoredPosition = Vector2.Lerp(new Vector2(42f, 24f), new Vector2(14f, 10f), easedProgress);
		}

		if (titleLabel != null)
		{
			titleLabel.characterSpacing = Mathf.Lerp(-34f, -18f, easedProgress);
		}

		if (subtitleLabel != null)
		{
			subtitleLabel.characterSpacing = Mathf.Lerp(40f, 24f, easedProgress);
		}

		if (progress >= 1f)
		{
			StopIntro();
		}
	}

	private void LateUpdate()
	{
		if (!isPlaying)
		{
			return;
		}

		ResolveCamera();
		if (targetCamera == null)
		{
			return;
		}

		float elapsed = Time.unscaledTime - startedAt;
		float progress = Mathf.Clamp01(introDuration <= 0.01f ? 1f : elapsed / introDuration);
		targetCamera.fieldOfView = Mathf.Lerp(baseFieldOfView * startFovMultiplier, baseFieldOfView, EaseOutCubic(progress));
	}

	private void OnDisable()
	{
		RestoreFieldOfView();
	}

	public void EnsureSceneBuilt()
	{
		if (!TryBindExistingHierarchy())
		{
			BuildHierarchy();
		}

		ApplyVisuals();
	}

	public void ConfigureArenaDefaults()
	{
		playOnStart = false;
		useSceneNameWhenTitleEmpty = false;
		introTitle = string.Empty;
		introTitleOverride = "TUTORIAL";
		introSubtitle = string.Empty;
	}

	public void PlayIntro()
	{
		EnsureSceneBuilt();
		ResolveCamera();
		startedAt = Time.unscaledTime;
		isPlaying = true;
		if (targetCamera != null)
		{
			baseFieldOfView = targetCamera.fieldOfView;
		}

		canvasGroup.alpha = 0f;
		rootRect.localScale = Vector3.one * startScale;
	}

	public void StopIntro()
	{
		isPlaying = false;
		startedAt = -1f;
		if (canvasGroup != null)
		{
			canvasGroup.alpha = 0f;
		}

		if (rootRect != null)
		{
			rootRect.localScale = Vector3.one * endScale;
		}

		RestoreFieldOfView();
	}

	public void OnStartLoadScene(string sceneName)
	{
		StopIntro();
	}

	public void OnFinishLoadScene(string sceneName)
	{
		if (!string.IsNullOrWhiteSpace(sceneName))
		{
			introTitle = sceneName;
		}

		PlayIntro();
	}

	private bool TryBindExistingHierarchy()
	{
		Transform canvasTransform = transform.Find(CanvasObjectName);
		Transform rootTransform = canvasTransform != null ? canvasTransform.Find(RootObjectName) : null;
		if (rootTransform == null)
		{
			return false;
		}

		canvasGroup = rootTransform.GetComponent<CanvasGroup>();
		rootRect = rootTransform as RectTransform;
		shadowLabel = rootTransform.Find("Shadow")?.GetComponent<TextMeshProUGUI>();
		titleLabel = rootTransform.Find("Title")?.GetComponent<TextMeshProUGUI>();
		subtitleLabel = rootTransform.Find("Subtitle")?.GetComponent<TextMeshProUGUI>();
		upperStripe = rootTransform.Find("Upper Stripe")?.GetComponent<Image>();
		lowerStripe = rootTransform.Find("Lower Stripe")?.GetComponent<Image>();

		if (shadowLabel == null || titleLabel == null || subtitleLabel == null || upperStripe == null || lowerStripe == null)
		{
			return false;
		}

		backgroundWash = rootTransform.Find(BackgroundWashObjectName)?.GetComponent<Image>();
		titleBand = rootTransform.Find(TitleBandObjectName)?.GetComponent<Image>();
		titleGlow = rootTransform.Find(TitleGlowObjectName)?.GetComponent<Image>();

		if (backgroundWash == null || titleBand == null || titleGlow == null)
		{
			return false;
		}

		return canvasGroup != null && rootRect != null && backgroundWash != null && titleBand != null && titleGlow != null && shadowLabel != null && titleLabel != null && subtitleLabel != null && upperStripe != null && lowerStripe != null;
	}

	private void BuildHierarchy()
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

		GameObject rootObject = new GameObject(RootObjectName);
		rootObject.transform.SetParent(canvasObject.transform, false);
		rootRect = rootObject.AddComponent<RectTransform>();
		rootRect.anchorMin = new Vector2(0.5f, 0.5f);
		rootRect.anchorMax = new Vector2(0.5f, 0.5f);
		rootRect.pivot = new Vector2(0.5f, 0.5f);
		rootRect.sizeDelta = new Vector2(1920f, 1080f);

		canvasGroup = rootObject.AddComponent<CanvasGroup>();
		canvasGroup.alpha = 0f;
		canvasGroup.interactable = false;
		canvasGroup.blocksRaycasts = false;

		backgroundWash = CreatePanel(rootObject.transform, BackgroundWashObjectName, Vector2.zero, new Vector2(1920f, 1080f));
		upperStripe = CreateStripe(rootObject.transform, "Upper Stripe", new Vector2(0f, 318f), new Vector2(2150f, 220f));
		lowerStripe = CreateStripe(rootObject.transform, "Lower Stripe", new Vector2(0f, -318f), new Vector2(2150f, 220f));
		titleBand = CreatePanel(rootObject.transform, TitleBandObjectName, new Vector2(0f, -8f), new Vector2(2160f, 360f));
		titleGlow = CreatePanel(rootObject.transform, TitleGlowObjectName, new Vector2(0f, 8f), new Vector2(1820f, 500f));

		shadowLabel = CreateLabel(rootObject.transform, "Shadow", new Vector2(42f, 24f), new Vector2(2260f, 560f), 430f, shadowColor);
		shadowLabel.outlineWidth = 0f;

		titleLabel = CreateLabel(rootObject.transform, "Title", Vector2.zero, new Vector2(2200f, 540f), 400f, titleColor);
		titleLabel.outlineWidth = 0.2f;
		titleLabel.outlineColor = new Color(0.18f, 0.02f, 0.02f, 0.46f);

		subtitleLabel = CreateLabel(rootObject.transform, "Subtitle", new Vector2(0f, -278f), new Vector2(1680f, 100f), 54f, subtitleColor);
		subtitleLabel.characterSpacing = 24f;
		subtitleLabel.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
	}

	private void ApplyVisuals()
	{
		ResolveCamera();

		Canvas canvas = GetComponentInChildren<Canvas>(true);
		if (canvas != null)
		{
			canvas.renderMode = RenderMode.ScreenSpaceOverlay;
			canvas.sortingOrder = sortingOrder;
		}

		string resolvedTitle = ResolveTitle();
		string resolvedSubtitle = ResolveSubtitle();

		if (shadowLabel != null)
		{
			shadowLabel.text = resolvedTitle;
			shadowLabel.color = shadowColor;
			shadowLabel.fontSize = 430f;
			shadowLabel.characterSpacing = -24f;
		}

		if (titleLabel != null)
		{
			titleLabel.text = resolvedTitle;
			titleLabel.color = titleColor;
			titleLabel.fontSize = 400f;
			titleLabel.characterSpacing = -18f;
			titleLabel.outlineWidth = 0.2f;
			titleLabel.outlineColor = new Color(0.18f, 0.02f, 0.02f, 0.46f);
		}

		if (subtitleLabel != null)
		{
			subtitleLabel.text = resolvedSubtitle;
			subtitleLabel.color = subtitleColor;
			subtitleLabel.fontSize = 54f;
			subtitleLabel.characterSpacing = 24f;
		}

		if (backgroundWash != null)
		{
			backgroundWash.color = backgroundWashColor;
		}

		if (titleBand != null)
		{
			titleBand.color = titleBandColor;
		}

		if (titleGlow != null)
		{
			titleGlow.color = titleGlowColor;
		}

		if (upperStripe != null)
		{
			upperStripe.color = stripeColor;
		}

		if (lowerStripe != null)
		{
			lowerStripe.color = stripeColor;
		}
	}

	private string ResolveTitle()
	{
		if (!string.IsNullOrWhiteSpace(introTitleOverride))
		{
			return introTitleOverride.ToUpperInvariant();
		}

		if (!string.IsNullOrWhiteSpace(introTitle))
		{
			return introTitle.ToUpperInvariant();
		}

		if (!useSceneNameWhenTitleEmpty)
		{
			return "TUTORIAL";
		}

		return SceneManager.GetActiveScene().name.Replace('_', ' ').ToUpperInvariant();
	}

	private string ResolveSubtitle()
	{
		if (!string.IsNullOrWhiteSpace(introSubtitle))
		{
			return introSubtitle.ToUpperInvariant();
		}

		if (!string.IsNullOrWhiteSpace(introTitleOverride))
		{
			return string.Empty;
		}

		return "ENTERING AREA";
	}

	private void ResolveCamera()
	{
		if (targetCamera == null)
		{
			targetCamera = Camera.main;
		}
	}

	private void RestoreFieldOfView()
	{
		if (targetCamera != null && baseFieldOfView > 0.01f)
		{
			targetCamera.fieldOfView = baseFieldOfView;
		}
	}

	private float ResolveAlpha(float progress)
	{
		float fadeIn = fadeInDuration <= 0.01f ? 1f : Mathf.Clamp01(progress / Mathf.Clamp01(fadeInDuration / introDuration));
		float fadeOutStart = Mathf.Clamp01(1f - (fadeOutDuration / introDuration));
		float fadeOut = progress < fadeOutStart ? 1f : 1f - Mathf.Clamp01((progress - fadeOutStart) / Mathf.Max(0.0001f, 1f - fadeOutStart));
		return fadeIn * fadeOut;
	}

	private static float EaseOutCubic(float value)
	{
		float inverse = 1f - Mathf.Clamp01(value);
		return 1f - inverse * inverse * inverse;
	}

	private static Image CreateStripe(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
	{
		return CreatePanel(parent, name, anchoredPosition, size);
	}

	private static Image CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
	{
		GameObject stripeObject = new GameObject(name);
		stripeObject.transform.SetParent(parent, false);
		RectTransform rect = stripeObject.AddComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = size;
		Image image = stripeObject.AddComponent<Image>();
		image.raycastTarget = false;
		return image;
	}

	private static TextMeshProUGUI CreateLabel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize, Color color)
	{
		GameObject labelObject = new GameObject(name);
		labelObject.transform.SetParent(parent, false);
		RectTransform rect = labelObject.AddComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.anchoredPosition = anchoredPosition;
		rect.sizeDelta = size;

		TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
		label.font = TMP_Settings.defaultFontAsset;
		label.fontSize = fontSize;
		label.color = color;
		label.alignment = TextAlignmentOptions.Center;
		label.enableWordWrapping = false;
		label.overflowMode = TextOverflowModes.Overflow;
		label.fontStyle = FontStyles.Bold | FontStyles.UpperCase;
		label.characterSpacing = -6f;
		label.raycastTarget = false;
		return label;
	}
}