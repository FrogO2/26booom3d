using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Arena/Prompt Overlay")]
public class ArenaPromptOverlay : MonoBehaviour
{
	private const string PromptChannelKey = "Prompt";
	private const string CounterChannelKey = "Counter";
	private const string LegacyCanvasObjectName = "Arena Prompt UI";

	[SerializeField] private int sortingOrder = 200;
	[SerializeField] private Vector2 promptPanelPosition = new Vector2(0f, 72f);
	[SerializeField] private Vector2 promptPanelSize = new Vector2(1480f, 260f);
	[SerializeField] private Vector2 counterPanelPosition = new Vector2(0f, -92f);
	[SerializeField] private Vector2 counterPanelSize = new Vector2(1320f, 160f);

	private RuntimeTextOverlayUI overlayUi;

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
			Message = message,
			AnchoredPosition = promptPanelPosition,
			Size = promptPanelSize,
			FontSize = 72f,
			Duration = duration,
			FadeDuration = 0.35f,
			CharacterSpacing = 0f,
			Color = Color.white,
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
			Message = message,
			AnchoredPosition = counterPanelPosition,
			Size = counterPanelSize,
			FontSize = 56f,
			Duration = duration,
			FadeDuration = 0.35f,
			CharacterSpacing = 0f,
			Color = Color.white,
			Alignment = TMPro.TextAlignmentOptions.Center,
			FontStyle = TMPro.FontStyles.Bold,
			WordWrap = true,
			OverflowMode = TMPro.TextOverflowModes.Overflow,
		});
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