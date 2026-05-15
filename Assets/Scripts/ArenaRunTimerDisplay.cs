using UnityEngine;

[AddComponentMenu("Arena/Run Timer Display")]
public class ArenaRunTimerDisplay : MonoBehaviour
{
	private const string CanvasObjectName = "Run Timer Canvas";
	private bool hasStarted;
	private bool hasFinished;
	private float startedAt = -1f;
	private float finishedAt = -1f;

	private void Awake()
	{
		RemoveLegacyUi();
	}

	private void OnEnable()
	{
		RemoveLegacyUi();
	}

	public bool HasStarted => hasStarted;
	public bool HasFinished => hasFinished;
	public float ElapsedSeconds => !hasStarted ? 0f : (hasFinished ? finishedAt - startedAt : Time.unscaledTime - startedAt);

	public void BeginRun()
	{
		if (hasStarted)
		{
			return;
		}

		hasStarted = true;
		hasFinished = false;
		startedAt = Time.unscaledTime;
		finishedAt = -1f;
	}

	public void FinishRun()
	{
		if (!hasStarted || hasFinished)
		{
			return;
		}

		finishedAt = Time.unscaledTime;
		hasFinished = true;
	}

	public void ResetRun()
	{
		hasStarted = false;
		hasFinished = false;
		startedAt = -1f;
		finishedAt = -1f;
	}

	public void EnsureSceneBuilt()
	{
		RemoveLegacyUi();
	}

	public void EnsureRuntimeUi()
	{
		RemoveLegacyUi();
	}

	public static string FormatTime(float seconds)
	{
		float safeSeconds = Mathf.Max(0f, seconds);
		int minutes = Mathf.FloorToInt(safeSeconds / 60f);
		float remainingSeconds = safeSeconds - minutes * 60f;
		return $"{minutes:00}:{remainingSeconds:00.000}";
	}

	private void RemoveLegacyUi()
	{
		Transform canvasTransform = transform.Find(CanvasObjectName);
		if (canvasTransform == null)
		{
			return;
		}

		if (Application.isPlaying)
		{
			Destroy(canvasTransform.gameObject);
			return;
		}

		DestroyImmediate(canvasTransform.gameObject);
	}
}
