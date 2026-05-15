using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
[AddComponentMenu("Game/Level Exit Trigger")]
public class LevelExitTrigger : MonoBehaviour
{
	[SerializeField] private LevelManager levelManager;
	[SerializeField] private CharacterController playerController;
	[SerializeField] private string playerTag = "Player";
	[SerializeField] private bool oneShot = true;

	private bool triggered;

	private void Awake()
	{
		EnsureTriggerCollider();
		AutoAssignReferences();
	}

	private void OnEnable()
	{
		triggered = false;
		AutoAssignReferences();
	}

	private void OnTriggerEnter(Collider other)
	{
		if (triggered || !IsPlayerCollider(other))
		{
			return;
		}

		if (oneShot)
		{
			triggered = true;
		}

		levelManager?.LoadNextLevel();
	}

	private void AutoAssignReferences()
	{
		if (levelManager == null)
		{
			levelManager = GetComponentInParent<LevelManager>();
			if (levelManager == null)
			{
				levelManager = FindAnyObjectByType<LevelManager>();
			}
		}

		if (playerController == null)
		{
			GameObject taggedPlayer = !string.IsNullOrWhiteSpace(playerTag) ? GameObject.FindGameObjectWithTag(playerTag) : null;
			if (taggedPlayer != null)
			{
				playerController = taggedPlayer.GetComponent<CharacterController>();
				if (playerController == null)
				{
					playerController = taggedPlayer.GetComponentInChildren<CharacterController>(true);
				}
			}

			if (playerController == null)
			{
				playerController = FindAnyObjectByType<CharacterController>();
			}
		}
	}

	private bool IsPlayerCollider(Collider other)
	{
		if (other == null)
		{
			return false;
		}

		if (playerController == null)
		{
			AutoAssignReferences();
		}

		CharacterController enteredController = other.GetComponent<CharacterController>();
		if (enteredController == playerController)
		{
			return true;
		}

		return other.GetComponentInParent<CharacterController>() == playerController;
	}

	private void EnsureTriggerCollider()
	{
		Collider triggerCollider = GetComponent<Collider>();
		if (triggerCollider != null)
		{
			triggerCollider.isTrigger = true;
		}
	}
}