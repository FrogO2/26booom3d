using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class RuntimeTextOverlayUI : MonoBehaviour
{
	private const string CanvasObjectName = "Runtime Text Overlay Canvas";
	private const string RootObjectName = "Runtime Text Overlay Root";

	[Serializable]
	public struct DisplayRequest
	{
		public string ChannelKey;
		public string Message;
		public Vector2 AnchoredPosition;
		public Vector2 Size;
		public float FontSize;
		public float Duration;
		public float FadeDuration;
		public float CharacterSpacing;
		public Color Color;
		public TextAlignmentOptions Alignment;
		public FontStyles FontStyle;
		public bool WordWrap;
		public TextOverflowModes OverflowMode;
	}

	private sealed class ChannelState
	{
		public RectTransform RectTransform;
		public CanvasGroup CanvasGroup;
		public TextMeshProUGUI Label;
		public float HideAt = -1f;
		public float FadeDuration = 0.35f;
		public bool IsTimed;
	}

	[SerializeField] private int sortingOrder = 200;
	[SerializeField] private Vector2 referenceResolution = new Vector2(1920f, 1080f);
	[SerializeField] private float canvasMatchWidthOrHeight = 0.5f;
	[SerializeField] private float defaultFadeDuration = 0.35f;

	private Canvas canvas;
	private RectTransform rootRect;
	private readonly Dictionary<string, ChannelState> channels = new Dictionary<string, ChannelState>();

	private void Awake()
	{
		EnsureOverlayBuilt();
	}

	private void Update()
	{
		UpdateTimedChannels();
	}

	public void SetSortingOrder(int value)
	{
		sortingOrder = value;
		ApplyCanvasSettings();
	}

	public void EnsureOverlayBuilt()
	{
		if (!TryBindExistingOverlay())
		{
			BuildOverlay();
		}

		ApplyCanvasSettings();
	}

	public void ShowText(DisplayRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.ChannelKey))
		{
			return;
		}

		EnsureOverlayBuilt();
		ChannelState channel = GetOrCreateChannel(request.ChannelKey);
		ApplyRequest(channel, request);
	}

	public void HideText(string channelKey)
	{
		if (!TryGetChannel(channelKey, out ChannelState channel))
		{
			return;
		}

		channel.IsTimed = false;
		channel.HideAt = -1f;
		channel.CanvasGroup.alpha = 0f;
	}

	public void HideAllText()
	{
		foreach (ChannelState channel in channels.Values)
		{
			channel.IsTimed = false;
			channel.HideAt = -1f;
			channel.CanvasGroup.alpha = 0f;
		}
	}

	public void SetChannelAlpha(string channelKey, float alpha)
	{
		if (!TryGetChannel(channelKey, out ChannelState channel))
		{
			return;
		}

		channel.CanvasGroup.alpha = Mathf.Clamp01(alpha);
	}

	public void SetChannelScale(string channelKey, Vector3 scale)
	{
		if (!TryGetChannel(channelKey, out ChannelState channel))
		{
			return;
		}

		channel.RectTransform.localScale = scale;
	}

	public void SetChannelCharacterSpacing(string channelKey, float spacing)
	{
		if (!TryGetChannel(channelKey, out ChannelState channel))
		{
			return;
		}

		channel.Label.characterSpacing = spacing;
	}

	private void UpdateTimedChannels()
	{
		if (channels.Count == 0)
		{
			return;
		}

		foreach (ChannelState channel in channels.Values)
		{
			if (!channel.IsTimed)
			{
				continue;
			}

			float remaining = channel.HideAt - Time.unscaledTime;
			if (remaining <= 0f)
			{
				channel.IsTimed = false;
				channel.HideAt = -1f;
				channel.CanvasGroup.alpha = 0f;
				continue;
			}

			float fadeDuration = Mathf.Max(0.01f, channel.FadeDuration);
			channel.CanvasGroup.alpha = remaining >= fadeDuration ? 1f : Mathf.Clamp01(remaining / fadeDuration);
		}
	}

	private void ApplyCanvasSettings()
	{
		if (canvas == null)
		{
			return;
		}

		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = sortingOrder;

		CanvasScaler scaler = canvas.GetComponent<CanvasScaler>();
		if (scaler != null)
		{
			scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
			scaler.referenceResolution = referenceResolution;
			scaler.matchWidthOrHeight = canvasMatchWidthOrHeight;
		}
	}

	private void ApplyRequest(ChannelState channel, DisplayRequest request)
	{
		channel.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
		channel.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		channel.RectTransform.pivot = new Vector2(0.5f, 0.5f);
		channel.RectTransform.anchoredPosition = request.AnchoredPosition;
		channel.RectTransform.sizeDelta = request.Size;
		channel.RectTransform.localScale = Vector3.one;

		channel.Label.font = TMP_Settings.defaultFontAsset;
		channel.Label.text = request.Message ?? string.Empty;
		channel.Label.fontSize = request.FontSize;
		channel.Label.color = request.Color;
		channel.Label.alignment = request.Alignment;
		channel.Label.fontStyle = request.FontStyle;
		channel.Label.enableWordWrapping = request.WordWrap;
		channel.Label.overflowMode = request.OverflowMode;
		channel.Label.characterSpacing = request.CharacterSpacing;
		channel.Label.outlineWidth = 0f;
		channel.Label.raycastTarget = false;

		channel.FadeDuration = request.FadeDuration > 0f ? request.FadeDuration : defaultFadeDuration;
		channel.IsTimed = request.Duration > 0f;
		channel.HideAt = channel.IsTimed ? Time.unscaledTime + request.Duration : -1f;
		channel.CanvasGroup.alpha = 1f;
	}

	private bool TryBindExistingOverlay()
	{
		Transform canvasTransform = transform.Find(CanvasObjectName);
		Transform rootTransform = canvasTransform != null ? canvasTransform.Find(RootObjectName) : null;
		if (canvasTransform == null || rootTransform == null)
		{
			return false;
		}

		canvas = canvasTransform.GetComponent<Canvas>();
		rootRect = rootTransform as RectTransform;
		channels.Clear();

		for (int index = 0; index < rootTransform.childCount; index++)
		{
			Transform child = rootTransform.GetChild(index);
			CanvasGroup canvasGroup = child.GetComponent<CanvasGroup>();
			RectTransform rectTransform = child as RectTransform;
			TextMeshProUGUI label = child.Find("Label")?.GetComponent<TextMeshProUGUI>();
			if (canvasGroup == null || rectTransform == null || label == null)
			{
				continue;
			}

			channels[child.name] = new ChannelState
			{
				CanvasGroup = canvasGroup,
				RectTransform = rectTransform,
				Label = label,
			};
		}

		return canvas != null && rootRect != null;
	}

	private void BuildOverlay()
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

		canvas = canvasObject.AddComponent<Canvas>();
		canvasObject.AddComponent<GraphicRaycaster>();
		canvasObject.AddComponent<CanvasScaler>();

		GameObject rootObject = new GameObject(RootObjectName);
		rootObject.transform.SetParent(canvasObject.transform, false);
		rootRect = rootObject.AddComponent<RectTransform>();
		rootRect.anchorMin = new Vector2(0.5f, 0.5f);
		rootRect.anchorMax = new Vector2(0.5f, 0.5f);
		rootRect.pivot = new Vector2(0.5f, 0.5f);
		rootRect.sizeDelta = referenceResolution;
		channels.Clear();
	}

	private bool TryGetChannel(string channelKey, out ChannelState channel)
	{
		EnsureOverlayBuilt();
		return channels.TryGetValue(channelKey, out channel);
	}

	private ChannelState GetOrCreateChannel(string channelKey)
	{
		if (channels.TryGetValue(channelKey, out ChannelState existingChannel))
		{
			return existingChannel;
		}

		GameObject channelObject = new GameObject(channelKey);
		channelObject.transform.SetParent(rootRect, false);

		RectTransform channelRect = channelObject.AddComponent<RectTransform>();
		CanvasGroup channelCanvasGroup = channelObject.AddComponent<CanvasGroup>();
		channelCanvasGroup.alpha = 0f;
		channelCanvasGroup.interactable = false;
		channelCanvasGroup.blocksRaycasts = false;

		GameObject labelObject = new GameObject("Label");
		labelObject.transform.SetParent(channelObject.transform, false);
		RectTransform labelRect = labelObject.AddComponent<RectTransform>();
		labelRect.anchorMin = Vector2.zero;
		labelRect.anchorMax = Vector2.one;
		labelRect.offsetMin = Vector2.zero;
		labelRect.offsetMax = Vector2.zero;

		TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
		label.enableAutoSizing = false;
		label.raycastTarget = false;

		ChannelState channel = new ChannelState
		{
			RectTransform = channelRect,
			CanvasGroup = channelCanvasGroup,
			Label = label,
		};

		channels[channelKey] = channel;
		return channel;
	}
}
