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
		public string ExclusiveGroupKey;
		public string Message;
		public Vector2 AnchoredPosition;
		public Vector2 Size;
		public float FontSize;
		public float Duration;
		public float FadeDuration;
		public float CharacterSpacing;
		public float LineSpacing;
		public float OutlineWidth;
		public float OutlineSoftness;
		public float FaceDilate;
		public bool UseAdaptiveForegroundColor;
		public Camera AdaptiveColorCamera;
		public Color Color;
		public Color SecondaryColor;
		public Color OutlineColor;
		public float ContrastBias;
		public float ContrastBlendWidth;
		public TextAlignmentOptions Alignment;
		public FontStyles FontStyle;
		public bool WordWrap;
		public TextOverflowModes OverflowMode;
	}

	private sealed class ChannelState
	{
		public GameObject ChannelObject;
		public RectTransform RectTransform;
		public CanvasGroup CanvasGroup;
		public TextMeshProUGUI Label;
		public Material SourceMaterial;
		public Material MaterialInstance;
		public string ExclusiveGroupKey;
		public bool UseAdaptiveForegroundColor;
		public Camera AdaptiveColorCamera;
		public Color PrimaryColor;
		public Color SecondaryColor;
		public float ContrastBias;
		public float ContrastBlendWidth;
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
	private static readonly HashSet<RuntimeTextOverlayUI> liveOverlays = new HashSet<RuntimeTextOverlayUI>();

	private void Awake()
	{
		EnsureOverlayBuilt();
	}

	private void OnEnable()
	{
		liveOverlays.Add(this);
	}

	private void OnDisable()
	{
		liveOverlays.Remove(this);
	}

	private void OnDestroy()
	{
		liveOverlays.Remove(this);
		foreach (ChannelState channel in channels.Values)
		{
			DestroyMaterialInstance(channel);
		}
	}

	private void Update()
	{
		UpdateTimedChannels();
		UpdateAdaptiveChannels();
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

		HideChannel(channel);
	}

	public void HideAllText()
	{
		foreach (ChannelState channel in channels.Values)
		{
			HideChannel(channel);
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
		if (channel.UseAdaptiveForegroundColor)
		{
			ApplyAdaptiveForeground(channel);
		}
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
				HideChannel(channel);
				continue;
			}

			float fadeDuration = Mathf.Max(0.01f, channel.FadeDuration);
			channel.CanvasGroup.alpha = remaining >= fadeDuration ? 1f : Mathf.Clamp01(remaining / fadeDuration);
		}
	}

	private void UpdateAdaptiveChannels()
	{
		if (channels.Count == 0)
		{
			return;
		}

		foreach (ChannelState channel in channels.Values)
		{
			if (!channel.UseAdaptiveForegroundColor || channel.Label == null)
			{
				continue;
			}

			if (channel.CanvasGroup == null || channel.CanvasGroup.alpha <= 0.001f)
			{
				continue;
			}

			ApplyAdaptiveForeground(channel);
		}
	}

	private void ApplyCanvasSettings()
	{
		if (canvas == null && !TryBindExistingOverlay())
		{
			return;
		}

		canvas.overrideSorting = true;
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.worldCamera = null;
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
		channel.ExclusiveGroupKey = string.IsNullOrWhiteSpace(request.ExclusiveGroupKey) ? null : request.ExclusiveGroupKey;
		if (!string.IsNullOrEmpty(channel.ExclusiveGroupKey))
		{
			HideChannelsInExclusiveGroup(channel.ExclusiveGroupKey, request.ChannelKey);
		}

		channel.UseAdaptiveForegroundColor = request.UseAdaptiveForegroundColor;
		channel.AdaptiveColorCamera = request.AdaptiveColorCamera;
		channel.PrimaryColor = request.Color;
		channel.SecondaryColor = request.SecondaryColor.a > 0f ? request.SecondaryColor : ArenaTextStyleUtility.AlertForegroundColor;
		channel.ContrastBias = request.ContrastBias;
		channel.ContrastBlendWidth = request.ContrastBlendWidth;

		channel.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
		channel.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		channel.RectTransform.pivot = new Vector2(0.5f, 0.5f);
		channel.RectTransform.anchoredPosition = request.AnchoredPosition;
		channel.RectTransform.sizeDelta = request.Size;
		channel.RectTransform.localScale = Vector3.one;

		channel.Label.font = TMP_Settings.defaultFontAsset;
		channel.Label.text = request.Message ?? string.Empty;
		channel.Label.fontSize = request.FontSize;
		channel.Label.alignment = request.Alignment;
		channel.Label.fontStyle = request.FontStyle;
		channel.Label.enableWordWrapping = request.WordWrap;
		channel.Label.overflowMode = request.OverflowMode;
		channel.Label.characterSpacing = request.CharacterSpacing;
		channel.Label.lineSpacing = request.LineSpacing;
		channel.Label.raycastTarget = false;

		EnsureMaterialInstance(channel);
		ApplyTextMaterial(channel, request);

		if (request.UseAdaptiveForegroundColor)
		{
			ApplyAdaptiveForeground(channel);
		}
		else
		{
			ApplyUniformForeground(channel.Label, request.Color);
		}

		channel.FadeDuration = request.FadeDuration > 0f ? request.FadeDuration : defaultFadeDuration;
		channel.IsTimed = request.Duration > 0f;
		channel.HideAt = channel.IsTimed ? Time.unscaledTime + request.Duration : -1f;
		channel.CanvasGroup.alpha = 1f;
	}

	private void HideChannelsInExclusiveGroup(string exclusiveGroupKey, string activeChannelKey)
	{
		foreach (RuntimeTextOverlayUI overlay in liveOverlays)
		{
			overlay.HideChannelsInExclusiveGroupLocal(exclusiveGroupKey, overlay == this ? activeChannelKey : null);
		}
	}

	private void HideChannelsInExclusiveGroupLocal(string exclusiveGroupKey, string activeChannelKey)
	{
		foreach (KeyValuePair<string, ChannelState> entry in channels)
		{
			if (!string.Equals(entry.Value.ExclusiveGroupKey, exclusiveGroupKey, StringComparison.Ordinal))
			{
				continue;
			}

			if (!string.IsNullOrEmpty(activeChannelKey) && string.Equals(entry.Key, activeChannelKey, StringComparison.Ordinal))
			{
				continue;
			}

			HideChannel(entry.Value);
		}
	}

	private static void HideChannel(ChannelState channel)
	{
		channel.IsTimed = false;
		channel.HideAt = -1f;
		channel.CanvasGroup.alpha = 0f;
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
		BindChannelsFromRoot(rootTransform);

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
			ChannelObject = channelObject,
			RectTransform = channelRect,
			CanvasGroup = channelCanvasGroup,
			Label = label,
		};

		channels[channelKey] = channel;
		return channel;
	}

	private void BindChannelsFromRoot(Transform rootTransform)
	{
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
				ChannelObject = child.gameObject,
				CanvasGroup = canvasGroup,
				RectTransform = rectTransform,
				Label = label,
			};
		}
	}

	private static void DestroyMaterialInstance(ChannelState channel)
	{
		if (channel == null || channel.MaterialInstance == null)
		{
			return;
		}

		if (Application.isPlaying)
		{
			Destroy(channel.MaterialInstance);
		}
		else
		{
			DestroyImmediate(channel.MaterialInstance);
		}

		channel.MaterialInstance = null;
		channel.SourceMaterial = null;
	}

	private static void EnsureMaterialInstance(ChannelState channel)
	{
		if (channel == null || channel.Label == null)
		{
			return;
		}

		Material sourceMaterial = channel.Label.font != null ? channel.Label.font.material : channel.Label.fontSharedMaterial;
		if (sourceMaterial == null)
		{
			return;
		}

		if (channel.MaterialInstance == null || channel.SourceMaterial != sourceMaterial)
		{
			DestroyMaterialInstance(channel);
			channel.SourceMaterial = sourceMaterial;
			channel.MaterialInstance = new Material(sourceMaterial)
			{
				name = sourceMaterial.name + " (Runtime Overlay)"
			};
		}

		channel.Label.fontSharedMaterial = channel.MaterialInstance;
	}

	private static void ApplyTextMaterial(ChannelState channel, DisplayRequest request)
	{
		if (channel?.Label == null)
		{
			return;
		}

		channel.Label.outlineWidth = Mathf.Clamp01(request.OutlineWidth);
		channel.Label.outlineColor = request.OutlineColor;

		if (channel.MaterialInstance != null)
		{
			channel.MaterialInstance.SetFloat(ShaderUtilities.ID_OutlineWidth, Mathf.Clamp01(request.OutlineWidth));
			channel.MaterialInstance.SetFloat(ShaderUtilities.ID_OutlineSoftness, Mathf.Clamp01(request.OutlineSoftness));
			channel.MaterialInstance.SetFloat(ShaderUtilities.ID_FaceDilate, Mathf.Clamp(request.FaceDilate, -1f, 1f));
			channel.MaterialInstance.SetColor(ShaderUtilities.ID_OutlineColor, request.OutlineColor);
		}

		channel.Label.UpdateMeshPadding();
	}

	private static void ApplyUniformForeground(TextMeshProUGUI label, Color color)
	{
		if (label == null)
		{
			return;
		}

		label.color = color;
		label.ForceMeshUpdate();
		ApplyVertexColors(label, (_, _) => color, null);
	}

	private static void ApplyAdaptiveForeground(ChannelState channel)
	{
		if (channel?.Label == null)
		{
			return;
		}

		channel.Label.color = Color.white;
		channel.Label.ForceMeshUpdate();
		ApplyVertexColors(
			channel.Label,
			(screenPoint, state) => ArenaTextStyleUtility.ResolveForeground(
				state.AdaptiveColorCamera,
				screenPoint,
				state.PrimaryColor,
				state.SecondaryColor,
				state.ContrastBias,
				state.ContrastBlendWidth),
			channel);
	}

	private static void ApplyVertexColors(TextMeshProUGUI label, Func<Vector2, ChannelState, Color> colorResolver, ChannelState channel)
	{
		TMP_TextInfo textInfo = label.textInfo;
		if (textInfo == null || textInfo.characterCount == 0)
		{
			return;
		}

		for (int characterIndex = 0; characterIndex < textInfo.characterCount; characterIndex++)
		{
			TMP_CharacterInfo characterInfo = textInfo.characterInfo[characterIndex];
			if (!characterInfo.isVisible)
			{
				continue;
			}

			Color32 resolvedColor = colorResolver(GetCharacterScreenPoint(label.rectTransform, characterInfo), channel);
			int materialIndex = characterInfo.materialReferenceIndex;
			int vertexIndex = characterInfo.vertexIndex;
			Color32[] colors = textInfo.meshInfo[materialIndex].colors32;
			colors[vertexIndex] = resolvedColor;
			colors[vertexIndex + 1] = resolvedColor;
			colors[vertexIndex + 2] = resolvedColor;
			colors[vertexIndex + 3] = resolvedColor;
		}

		label.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
	}

	private static Vector2 GetCharacterScreenPoint(RectTransform rectTransform, TMP_CharacterInfo characterInfo)
	{
		Vector3 localCenter = (characterInfo.bottomLeft + characterInfo.topRight) * 0.5f;
		Vector3 worldCenter = rectTransform.TransformPoint(localCenter);
		return RectTransformUtility.WorldToScreenPoint(null, worldCenter);
	}
}
