using UnityEngine;

[DisallowMultipleComponent]
public class KnifePlayerHitboxKiller : MonoBehaviour
{
	[Header("Attack hitbox delay (sec)")]
	[SerializeField] private float attackHitboxActiveDelay = 0.11f;
	[SerializeField] private BoxCollider hitbox;
	[SerializeField] private KnifePawnController knifeController;
	[SerializeField] private LocomotionSimpleAgent locomotion;
	[SerializeField] private PlayerOneHitDeath playerDeath;
	[SerializeField] private CharacterController playerCharacterController;
	[SerializeField] private Collider playerCollider;
	[SerializeField] private QueryTriggerInteraction queryTriggerInteraction = QueryTriggerInteraction.Ignore;

	private readonly Collider[] overlapResults = new Collider[8];
	private bool hitAppliedThisSwing;
	private float attackActiveTime = 0f;

	private void Awake()
	{
		AutoAssignReferences();
	}

	private void OnValidate()
	{
		AutoAssignReferences();
	}

	private void Update()
	{
		if (!TryResolveRuntimeReferences())
		{
			return;
		}

		if (!locomotion.IsAttacking)
		{
			hitAppliedThisSwing = false;
			attackActiveTime = 0f;
			return;
		}

		attackActiveTime += Time.deltaTime;
		if (hitAppliedThisSwing || playerDeath.IsDead || !hitbox.enabled)
		{
			return;
		}

		if (attackActiveTime < attackHitboxActiveDelay)
		{
			return;
		}

		if (TryApplyCharacterControllerHit())
		{
			return;
		}

		Vector3 center = hitbox.transform.TransformPoint(hitbox.center);
		Vector3 halfExtents = Vector3.Scale(hitbox.size * 0.5f, hitbox.transform.lossyScale);
		int overlapCount = Physics.OverlapBoxNonAlloc(center, halfExtents, overlapResults, hitbox.transform.rotation, ~0, queryTriggerInteraction);

		for (int index = 0; index < overlapCount; index++)
		{
			Collider candidate = overlapResults[index];
			if (candidate == null)
			{
				continue;
			}

			PlayerOneHitDeath candidateDeath = candidate.GetComponent<PlayerOneHitDeath>();
			if (candidateDeath == null)
			{
				candidateDeath = candidate.GetComponentInParent<PlayerOneHitDeath>();
			}

			if (candidateDeath == null || candidateDeath != playerDeath)
			{
				continue;
			}

			if (candidateDeath.KillFromMelee())
			{
				hitAppliedThisSwing = true;
				break;
			}
		}
	}

	public void Initialize(PlayerOneHitDeath targetPlayerDeath, KnifePawnController ownerKnifeController, LocomotionSimpleAgent ownerLocomotion)
	{
		playerDeath = targetPlayerDeath;
		knifeController = ownerKnifeController;
		locomotion = ownerLocomotion;
		AutoAssignReferences();
	}

	private void AutoAssignReferences()
	{
		if (hitbox == null)
		{
			hitbox = GetComponent<BoxCollider>();
		}

		if (knifeController == null)
		{
			knifeController = GetComponentInParent<KnifePawnController>();
		}

		if (locomotion == null)
		{
			locomotion = GetComponentInParent<LocomotionSimpleAgent>();
		}
	}

	private bool TryResolveRuntimeReferences()
	{
		AutoAssignReferences();

		if (playerDeath == null)
		{
			GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
			if (playerObject != null)
			{
				playerDeath = playerObject.GetComponent<PlayerOneHitDeath>();
				playerCharacterController = playerObject.GetComponent<CharacterController>();
				playerCollider = playerObject.GetComponent<Collider>();
			}
		}
		else if (playerCharacterController == null)
		{
			playerCharacterController = playerDeath.GetComponent<CharacterController>();
		}

		if (playerCollider == null && playerDeath != null)
		{
			playerCollider = playerDeath.GetComponent<Collider>();
		}

		return hitbox != null && knifeController != null && locomotion != null && playerDeath != null;
	}

	private bool TryApplyCharacterControllerHit()
	{
		if (playerDeath == null || hitbox == null)
		{
			return false;
		}

		if (playerCharacterController != null && hitbox.bounds.Intersects(playerCharacterController.bounds))
		{
			if (playerDeath.KillFromMelee())
			{
				hitAppliedThisSwing = true;
				return true;
			}
		}

		if (playerCollider != null && hitbox.bounds.Intersects(playerCollider.bounds))
		{
			if (playerDeath.KillFromMelee())
			{
				hitAppliedThisSwing = true;
				return true;
			}
		}

		return false;
	}
}