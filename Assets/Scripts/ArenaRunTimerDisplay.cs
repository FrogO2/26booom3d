using TMPro;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Arena/Run Timer Display")]
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
