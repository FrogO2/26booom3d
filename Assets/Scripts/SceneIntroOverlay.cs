using Invector.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneIntroOverlay : MonoBehaviour, vISceneLoadListener
{
	private const string LegacyCanvasObjectName = "Intro Canvas";
	private const string TitleChannelKey = "Title";
	private const string SubtitleChannelKey = "Subtitle";

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
	[SerializeField] private Color subtitleColor = new Color(1f, 1f, 1f, 0.82f);

	private RuntimeTextOverlayUI overlayUi;
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
		if (!isPlaying || overlayUi == null)
		{
			return;
		}

		float elapsed = Time.unscaledTime - startedAt;
		float progress = Mathf.Clamp01(introDuration <= 0.01f ? 1f : elapsed / introDuration);
		float alpha = ResolveAlpha(progress);
		float easedProgress = EaseOutCubic(progress);
		float scale = Mathf.Lerp(startScale, endScale, easedProgress);
		float subtitleAlpha = string.IsNullOrWhiteSpace(ResolveSubtitle()) ? 0f : alpha;

		overlayUi.SetChannelAlpha(TitleChannelKey, alpha);
		overlayUi.SetChannelScale(TitleChannelKey, Vector3.one * scale);
		overlayUi.SetChannelCharacterSpacing(TitleChannelKey, Mathf.Lerp(-16f, -6f, easedProgress));
		overlayUi.SetChannelAlpha(SubtitleChannelKey, subtitleAlpha);
		overlayUi.SetChannelScale(SubtitleChannelKey, Vector3.one * scale);
		overlayUi.SetChannelCharacterSpacing(SubtitleChannelKey, Mathf.Lerp(20f, 8f, easedProgress));

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
		RemoveLegacyHierarchy();
		overlayUi = GetComponent<RuntimeTextOverlayUI>();
		if (overlayUi == null)
		{
			overlayUi = gameObject.AddComponent<RuntimeTextOverlayUI>();
		}

		overlayUi.SetSortingOrder(sortingOrder);
		overlayUi.EnsureOverlayBuilt();
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

		SetDisplayState(0f, startScale, -16f, 20f);
	}

	public void StopIntro()
	{
		isPlaying = false;
		startedAt = -1f;
		SetDisplayState(0f, endScale, -6f, 8f);

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

	private void ApplyVisuals()
	{
		ResolveCamera();
		if (overlayUi == null)
		{
			return;
		}

		string resolvedTitle = ResolveTitle();
		string resolvedSubtitle = ResolveSubtitle();

		overlayUi.ShowText(new RuntimeTextOverlayUI.DisplayRequest
		{
			ChannelKey = TitleChannelKey,
			Message = resolvedTitle,
			AnchoredPosition = new Vector2(0f, 70f),
			Size = new Vector2(1760f, 320f),
			FontSize = 190f,
			Duration = 0f,
			FadeDuration = 0.35f,
			CharacterSpacing = -6f,
			Color = titleColor,
			Alignment = TMPro.TextAlignmentOptions.Center,
			FontStyle = TMPro.FontStyles.Bold | TMPro.FontStyles.UpperCase,
			WordWrap = true,
			OverflowMode = TMPro.TextOverflowModes.Overflow,
		});

		overlayUi.ShowText(new RuntimeTextOverlayUI.DisplayRequest
		{
			ChannelKey = SubtitleChannelKey,
			Message = resolvedSubtitle,
			AnchoredPosition = new Vector2(0f, -120f),
			Size = new Vector2(1600f, 160f),
			FontSize = 64f,
			Duration = 0f,
			FadeDuration = 0.35f,
			CharacterSpacing = 8f,
			Color = subtitleColor,
			Alignment = TMPro.TextAlignmentOptions.Center,
			FontStyle = TMPro.FontStyles.Bold | TMPro.FontStyles.UpperCase,
			WordWrap = true,
			OverflowMode = TMPro.TextOverflowModes.Overflow,
		});

		if (!isPlaying)
		{
			SetDisplayState(0f, endScale, -6f, 8f);
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

	private void SetDisplayState(float alpha, float scale, float titleSpacing, float subtitleSpacing)
	{
		if (overlayUi == null)
		{
			return;
		}

		float subtitleAlpha = string.IsNullOrWhiteSpace(ResolveSubtitle()) ? 0f : alpha;
		Vector3 channelScale = Vector3.one * scale;
		overlayUi.SetChannelAlpha(TitleChannelKey, alpha);
		overlayUi.SetChannelScale(TitleChannelKey, channelScale);
		overlayUi.SetChannelCharacterSpacing(TitleChannelKey, titleSpacing);
		overlayUi.SetChannelAlpha(SubtitleChannelKey, subtitleAlpha);
		overlayUi.SetChannelScale(SubtitleChannelKey, channelScale);
		overlayUi.SetChannelCharacterSpacing(SubtitleChannelKey, subtitleSpacing);
	}

	private void RemoveLegacyHierarchy()
	{
		Transform child = transform.Find(LegacyCanvasObjectName);
		if (child == null)
		{
			return;
		}

		if (Application.isPlaying)
		{
			Destroy(child.gameObject);
			return;
		}

		DestroyImmediate(child.gameObject);
	}
}