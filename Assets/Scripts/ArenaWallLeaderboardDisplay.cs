using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[AddComponentMenu("Arena/Wall Leaderboard Display")]
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
