using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Arena/Prompt Overlay")]
public class ArenaPromptOverlay : MonoBehaviour
{
	private const string PromptChannelKey = "Prompt";
	private const string CounterChannelKey = PromptChannelKey;
	private const string LegacyCanvasObjectName = "Arena Prompt UI";

	[SerializeField] private int sortingOrder = 200;
	[SerializeField] private Vector2 promptPanelPosition = new Vector2(0f, 92f);
	[SerializeField] private Vector2 promptPanelSize = new Vector2(1700f, 340f);
	[SerializeField] private Vector2 counterPanelPosition = new Vector2(0f, -132f);
	[SerializeField] private Vector2 counterPanelSize = new Vector2(1500f, 220f);

	private RuntimeTextOverlayUI overlayUi;
	private Camera targetCamera;

	private void Awake()
	{
		EnsureSceneBuilt();
	}

	public void EnsureSceneBuilt()
	{
		RemoveLegacyUi();
		overlayUi = GetComponent<RuntimeTextOverlayUI>();
		if (overlayUi == null)
		{
			overlayUi = gameObject.AddComponent<RuntimeTextOverlayUI>();
		}

		overlayUi.SetSortingOrder(sortingOrder);
		overlayUi.EnsureOverlayBuilt();
	}

	public void SetCamera(Camera camera)
	{
		targetCamera = camera != null ? camera : Camera.main;
	}

	public void ShowPrompt(string message, float duration, ArenaPromptColorMode colorMode, Color solidColor)
	{
		EnsureSceneBuilt();
		if (overlayUi == null)
		{
			return;
		}

		overlayUi.ShowText(new RuntimeTextOverlayUI.DisplayRequest
		{
			ChannelKey = PromptChannelKey,
			ExclusiveGroupKey = ArenaTextStyleUtility.SequentialOverlayGroupKey,
			Message = message,
			AnchoredPosition = promptPanelPosition,
			Size = promptPanelSize,
			FontSize = 118f,
			Duration = duration,
			FadeDuration = 0.35f,
			CharacterSpacing = 4f,
			LineSpacing = 18f,
			OutlineWidth = 0f,
			OutlineSoftness = 0f,
			FaceDilate = 0f,
			UseAdaptiveForegroundColor = false,
			AdaptiveColorCamera = targetCamera,
			Color = ArenaTextStyleUtility.DefaultForegroundColor,
			SecondaryColor = ArenaTextStyleUtility.DefaultForegroundColor,
			OutlineColor = Color.clear,
			ContrastBias = ArenaTextStyleUtility.DefaultContrastBias,
			ContrastBlendWidth = ArenaTextStyleUtility.DefaultContrastBlendWidth,
			Alignment = TMPro.TextAlignmentOptions.Center,
			FontStyle = TMPro.FontStyles.Bold,
			WordWrap = true,
			OverflowMode = TMPro.TextOverflowModes.Overflow,
		});
	}

	public void ShowCounter(string message, float duration)
	{
		EnsureSceneBuilt();
		if (overlayUi == null)
		{
			return;
		}

		overlayUi.ShowText(new RuntimeTextOverlayUI.DisplayRequest
		{
			ChannelKey = CounterChannelKey,
			ExclusiveGroupKey = ArenaTextStyleUtility.SequentialOverlayGroupKey,
			Message = message,
			AnchoredPosition = counterPanelPosition,
			Size = counterPanelSize,
			FontSize = 104f,
			Duration = duration,
			FadeDuration = 0.35f,
			CharacterSpacing = 3f,
			LineSpacing = 12f,
			OutlineWidth = 0f,
			OutlineSoftness = 0f,
			FaceDilate = 0f,
			UseAdaptiveForegroundColor = false,
			AdaptiveColorCamera = targetCamera,
			Color = ArenaTextStyleUtility.DefaultForegroundColor,
			SecondaryColor = ArenaTextStyleUtility.DefaultForegroundColor,
			OutlineColor = Color.clear,
			ContrastBias = ArenaTextStyleUtility.DefaultContrastBias,
			ContrastBlendWidth = ArenaTextStyleUtility.DefaultContrastBlendWidth,
			Alignment = TMPro.TextAlignmentOptions.Center,
			FontStyle = TMPro.FontStyles.Bold,
			WordWrap = true,
			OverflowMode = TMPro.TextOverflowModes.Overflow,
		});
	}

	private Color ResolvePromptColor(ArenaPromptColorMode colorMode, Color solidColor)
	{
		return ArenaTextStyleUtility.ResolvePromptColor(targetCamera, colorMode, solidColor);
	}

	public void HideAll()
	{
		overlayUi?.HideAllText();
	}

	private void RemoveLegacyUi()
	{
		Transform existingCanvas = transform.Find(LegacyCanvasObjectName);
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
	}
}