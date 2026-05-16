using UnityEngine;

public static class ArenaPromptEventUtility
{
	private const string GeneratedOverlayHostName = "Runtime Prompt Overlay Host";

	public static bool TryShowPrompt(Component context, ArenaPromptOverlay explicitOverlay, Camera explicitCamera, string message, float duration, ArenaPromptColorMode colorMode, Color solidColor)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return false;
		}

		ArenaPromptOverlay overlay = ResolvePromptOverlay(context, explicitOverlay);
		if (overlay == null)
		{
			Debug.LogWarning($"{nameof(ArenaPromptEventUtility)} could not resolve an {nameof(ArenaPromptOverlay)} for '{message}'.", context);
			return false;
		}

		overlay.SetCamera(explicitCamera != null ? explicitCamera : ResolveCamera());
		overlay.EnsureSceneBuilt();
		overlay.ShowPrompt(message, duration, colorMode, solidColor);
		return true;
	}

	public static bool IsPlayerCollider(Component context, Collider other, CharacterController explicitPlayerController, string playerTag)
	{
		if (other == null)
		{
			return false;
		}

		ArenaEncounterFlow encounterFlow = context != null ? context.GetComponentInParent<ArenaEncounterFlow>() : null;
		if (encounterFlow != null && encounterFlow.IsPlayerCollider(other))
		{
			return true;
		}

		ArenaTutorialSceneController tutorialController = context != null ? context.GetComponentInParent<ArenaTutorialSceneController>() : null;
		if (tutorialController != null && tutorialController.IsPlayerCollider(other))
		{
			return true;
		}

		CharacterController playerController = explicitPlayerController != null ? explicitPlayerController : ResolvePlayerController(playerTag);
		if (playerController == null)
		{
			return false;
		}

		CharacterController enteredController = other.GetComponent<CharacterController>();
		if (enteredController == playerController)
		{
			return true;
		}

		return other.GetComponentInParent<CharacterController>() == playerController;
	}

	public static CharacterController ResolvePlayerController(string playerTag)
	{
		GameObject taggedPlayer = FindTaggedPlayer(playerTag);
		if (taggedPlayer != null)
		{
			CharacterController taggedController = taggedPlayer.GetComponent<CharacterController>();
			if (taggedController != null)
			{
				return taggedController;
			}

			taggedController = taggedPlayer.GetComponentInChildren<CharacterController>(true);
			if (taggedController != null)
			{
				return taggedController;
			}
		}

		return Object.FindAnyObjectByType<CharacterController>();
	}

	public static Camera ResolveCamera()
	{
		if (Camera.main != null)
		{
			return Camera.main;
		}

		return Object.FindAnyObjectByType<Camera>();
	}

	private static ArenaPromptOverlay ResolvePromptOverlay(Component context, ArenaPromptOverlay explicitOverlay)
	{
		if (explicitOverlay != null)
		{
			return explicitOverlay;
		}

		ArenaPromptOverlay overlay = context != null ? context.GetComponent<ArenaPromptOverlay>() : null;
		if (overlay != null)
		{
			return overlay;
		}

		overlay = context != null ? context.GetComponentInParent<ArenaPromptOverlay>() : null;
		if (overlay != null)
		{
			return overlay;
		}

		overlay = Object.FindAnyObjectByType<ArenaPromptOverlay>();
		if (overlay != null)
		{
			return overlay;
		}

		ArenaEncounterFlow encounterFlow = context != null ? context.GetComponentInParent<ArenaEncounterFlow>() : null;
		if (encounterFlow == null)
		{
			encounterFlow = Object.FindAnyObjectByType<ArenaEncounterFlow>();
		}

		if (encounterFlow != null)
		{
			return GetOrAddOverlay(encounterFlow.gameObject);
		}

		ArenaTutorialSceneController tutorialController = context != null ? context.GetComponentInParent<ArenaTutorialSceneController>() : null;
		if (tutorialController == null)
		{
			tutorialController = Object.FindAnyObjectByType<ArenaTutorialSceneController>();
		}

		if (tutorialController != null)
		{
			return GetOrAddOverlay(tutorialController.gameObject);
		}

		LevelManager levelManager = context != null ? context.GetComponentInParent<LevelManager>() : null;
		if (levelManager == null)
		{
			levelManager = Object.FindAnyObjectByType<LevelManager>();
		}

		if (levelManager != null)
		{
			return GetOrAddOverlay(levelManager.gameObject);
		}

		GameObject host = GameObject.Find(GeneratedOverlayHostName);
		if (host == null)
		{
			host = new GameObject(GeneratedOverlayHostName);
		}

		return GetOrAddOverlay(host);
	}

	private static ArenaPromptOverlay GetOrAddOverlay(GameObject host)
	{
		if (host == null)
		{
			return null;
		}

		ArenaPromptOverlay overlay = host.GetComponent<ArenaPromptOverlay>();
		if (overlay == null)
		{
			overlay = host.AddComponent<ArenaPromptOverlay>();
		}

		return overlay;
	}

	private static GameObject FindTaggedPlayer(string playerTag)
	{
		if (string.IsNullOrWhiteSpace(playerTag))
		{
			return null;
		}

		try
		{
			return GameObject.FindGameObjectWithTag(playerTag);
		}
		catch (UnityException)
		{
			return null;
		}
	}
}