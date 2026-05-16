using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ArenaBakedEnemyTarget))]
[AddComponentMenu("Arena/Enemy Kill Prompt")]
public class ArenaEnemyKillPrompt : MonoBehaviour, IAttackTargetDeathListener
{
	[SerializeField] private ArenaPromptOverlay promptOverlay;
	[SerializeField] private Camera targetCamera;
	[SerializeField, TextArea(2, 4)] private string message = "TARGET DOWN";
	[SerializeField] private float duration = 2.4f;
	[SerializeField] private ArenaPromptColorMode colorMode = ArenaPromptColorMode.AdaptiveContrast;
	[SerializeField] private Color solidColor = Color.white;

	private void Reset()
	{
		AutoAssignReferences();
	}

	private void Awake()
	{
		AutoAssignReferences();
	}

	private void OnEnable()
	{
		AutoAssignReferences();
	}

	public void OnTargetKilled(ArenaBakedEnemyTarget target)
	{
		ArenaPromptEventUtility.TryShowPrompt(this, promptOverlay, targetCamera, message, duration, colorMode, solidColor);
	}

	private void AutoAssignReferences()
	{
		if (targetCamera == null)
		{
			targetCamera = ArenaPromptEventUtility.ResolveCamera();
		}
	}
}