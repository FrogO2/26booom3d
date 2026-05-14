using UnityEngine;

[AddComponentMenu("Arena/Trigger Zone")]
public class ArenaBakedTriggerZone : MonoBehaviour
{
	[SerializeField] private ArenaTutorialSceneController tutorialController;
	[SerializeField] private ArenaEncounterFlow encounterFlow;
	[SerializeField] private ArenaTriggerKind kind;
	[SerializeField] private string message;
	[SerializeField] private ArenaPromptColorMode colorMode;
	[SerializeField] private Color solidColor = Color.white;
	private bool triggered;

	public ArenaTriggerKind Kind => kind;
	public ArenaPromptColorMode ColorMode => colorMode;
	public Color SolidColor => solidColor;
	public string Message => message;

	public void Initialize(ArenaTriggerKind zoneKind, string promptMessage, ArenaPromptColorMode zoneColorMode, Color zoneSolidColor)
	{
		kind = zoneKind;
		message = promptMessage;
		colorMode = zoneColorMode;
		solidColor = zoneSolidColor;
		triggered = false;
		ResolveOwners();
	}

	public void Bind(ArenaTutorialSceneController tutorialOwner, ArenaEncounterFlow encounterOwner)
	{
		if (tutorialOwner != null)
		{
			tutorialController = tutorialOwner;
		}

		if (encounterOwner != null)
		{
			encounterFlow = encounterOwner;
		}

		triggered = false;
	}

	private void Awake()
	{
		ResolveOwners();
	}

	private void OnEnable()
	{
		ResolveOwners();
		triggered = false;
	}

	private void OnTriggerEnter(Collider other)
	{
		if (triggered)
		{
			return;
		}

		if (kind == ArenaTriggerKind.Tutorial)
		{
			if (tutorialController != null)
			{
				if (!tutorialController.IsPlayerCollider(other))
				{
					return;
				}

				triggered = true;
				tutorialController.HandleTutorialTrigger(this);
				return;
			}

			if (encounterFlow == null || !encounterFlow.IsPlayerCollider(other))
			{
				return;
			}

			triggered = true;
			encounterFlow.HandleTutorialTrigger(this);
			return;
		}

		if (encounterFlow == null || !encounterFlow.IsPlayerCollider(other))
		{
			return;
		}

		triggered = true;
		encounterFlow.HandleTrigger(this);
	}

	private void ResolveOwners()
	{
		if (tutorialController == null)
		{
			tutorialController = GetComponentInParent<ArenaTutorialSceneController>();
		}

		if (encounterFlow == null)
		{
			encounterFlow = GetComponentInParent<ArenaEncounterFlow>();
		}
	}
}
