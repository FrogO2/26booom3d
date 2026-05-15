using UnityEngine;
using UnityEngine.AI;

public struct ArenaEnemyKillContext
{
	public int AttackNumber;
	public Vector3 HitPoint;
	public Vector3 HitDirection;
	public float DestroyDelay;
}

[AddComponentMenu("Arena/Enemy Target")]
public class ArenaBakedEnemyTarget : MonoBehaviour
{
	private const float DefaultDestroyDelay = 0.05f;

	private Vector3 initialLocalPosition;
	private Quaternion initialLocalRotation;
	private Vector3 initialLocalScale;
	private Behaviour[] cachedBehaviours = System.Array.Empty<Behaviour>();
	private bool[] cachedBehaviourEnabledStates = System.Array.Empty<bool>();
	private Renderer[] cachedRenderers = System.Array.Empty<Renderer>();
	private bool[] cachedRendererEnabledStates = System.Array.Empty<bool>();
	private Collider[] cachedColliders = System.Array.Empty<Collider>();
	private bool[] cachedColliderEnabledStates = System.Array.Empty<bool>();
	private Rigidbody[] cachedRigidbodies = System.Array.Empty<Rigidbody>();
	private bool[] cachedRigidbodyKinematicStates = System.Array.Empty<bool>();
	private bool[] cachedRigidbodyGravityStates = System.Array.Empty<bool>();
	private bool[] cachedRigidbodyDetectCollisionStates = System.Array.Empty<bool>();
	private bool initialStateCaptured;

	public bool IsAlive { get; private set; } = true;

	private void Awake()
	{
		CaptureInitialStateIfNeeded();
	}

	public void Initialize()
	{
		ResetToInitialState();
	}

	public void ResetToInitialState()
	{
		CaptureInitialStateIfNeeded();
		IsAlive = true;

		transform.localPosition = initialLocalPosition;
		transform.localRotation = initialLocalRotation;
		transform.localScale = initialLocalScale;

		for (int i = 0; i < cachedBehaviours.Length; i++)
		{
			if (cachedBehaviours[i] != null)
			{
				cachedBehaviours[i].enabled = cachedBehaviourEnabledStates[i];
			}
		}

		for (int i = 0; i < cachedRenderers.Length; i++)
		{
			if (cachedRenderers[i] != null)
			{
				cachedRenderers[i].enabled = cachedRendererEnabledStates[i];
			}
		}

		for (int i = 0; i < cachedColliders.Length; i++)
		{
			if (cachedColliders[i] != null)
			{
				cachedColliders[i].enabled = cachedColliderEnabledStates[i];
			}
		}

		for (int i = 0; i < cachedRigidbodies.Length; i++)
		{
			Rigidbody rigidbody = cachedRigidbodies[i];
			if (rigidbody == null)
			{
				continue;
			}

			rigidbody.isKinematic = cachedRigidbodyKinematicStates[i];
			rigidbody.useGravity = cachedRigidbodyGravityStates[i];
			rigidbody.detectCollisions = cachedRigidbodyDetectCollisionStates[i];
			rigidbody.linearVelocity = Vector3.zero;
			rigidbody.angularVelocity = Vector3.zero;
		}

		AudioSource[] audioSources = GetComponentsInChildren<AudioSource>(true);
		for (int i = 0; i < audioSources.Length; i++)
		{
			if (audioSources[i] != null)
			{
				audioSources[i].Stop();
			}
		}

		Animator[] animators = GetComponentsInChildren<Animator>(true);
		for (int i = 0; i < animators.Length; i++)
		{
			if (animators[i] != null)
			{
				animators[i].Rebind();
				animators[i].Update(0f);
			}
		}

		NavMeshAgent agent = GetComponent<NavMeshAgent>();
		if (agent != null && agent.enabled)
		{
			agent.isStopped = false;
			if (agent.isOnNavMesh)
			{
				agent.ResetPath();
				agent.Warp(transform.position);
			}
		}
	}

	public Vector3 GetAimPoint()
	{
		return transform.position + Vector3.up * 1.2f;
	}

	public void Kill()
	{
		TryKill(new ArenaEnemyKillContext
		{
			DestroyDelay = DefaultDestroyDelay,
		});
	}

	public void Kill(ArenaEnemyKillContext context)
	{
		TryKill(context);
	}

	public bool CanBeTargeted()
	{
		if (!IsAlive)
		{
			return false;
		}

		MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
		for (int i = 0; i < behaviours.Length; i++)
		{
			if (behaviours[i] is IAttackTargetGate gate && !gate.CanTarget(this))
			{
				return false;
			}
		}

		return true;
	}

	public bool TryKill(ArenaEnemyKillContext context)
	{
		if (!CanBeTargeted())
		{
			return false;
		}

		IsAlive = false;
		NotifyDeathListeners();

		if (TryExecuteLegacyKill(context))
		{
			return true;
		}

		ExecuteFallbackKill(context);
		return true;
	}

	private void NotifyDeathListeners()
	{
		MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
		for (int i = 0; i < behaviours.Length; i++)
		{
			if (behaviours[i] is IAttackTargetDeathListener listener)
			{
				listener.OnTargetKilled(this);
			}
		}
	}

	private bool TryExecuteLegacyKill(ArenaEnemyKillContext context)
	{
		Vector3 hitPoint = ResolveHitPoint(context);
		Vector3 hitDirection = ResolveHitDirection(context);

		KnifePawnController knifePawnController = GetComponent<KnifePawnController>();
		if (knifePawnController != null)
		{
			knifePawnController.TakeFatalDamage(hitPoint, hitDirection);
			return true;
		}

		GunPawnController gunPawnController = GetComponent<GunPawnController>();
		if (gunPawnController != null)
		{
			gunPawnController.TakeFatalDamage(hitPoint, hitDirection);
			return true;
		}

		EnemyEffect enemyEffect = GetComponent<EnemyEffect>();
		if (enemyEffect != null)
		{
			enemyEffect.PlayHitEffects(hitPoint, hitDirection);
			enemyEffect.ActivateRagdoll(hitDirection);
			return true;
		}

		return false;
	}

	private void ExecuteFallbackKill(ArenaEnemyKillContext context)
	{
		LocomotionSimpleAgent locomotionAgent = GetComponent<LocomotionSimpleAgent>();
		if (locomotionAgent != null)
		{
			locomotionAgent.enabled = false;
		}

		KnifePawnController pawnController = GetComponent<KnifePawnController>();
		if (pawnController != null)
		{
			pawnController.enabled = false;
		}

		GunPawnController gunPawnController = GetComponent<GunPawnController>();
		if (gunPawnController != null)
		{
			gunPawnController.enabled = false;
		}

		NavMeshAgent agent = GetComponent<NavMeshAgent>();
		if (agent != null)
		{
			if (agent.isOnNavMesh)
			{
				agent.isStopped = true;
				agent.ResetPath();
			}

			agent.enabled = false;
		}

		Animator animator = GetComponent<Animator>();
		if (animator != null)
		{
			animator.enabled = false;
		}

		AudioSource audioSource = GetComponent<AudioSource>();
		if (audioSource != null)
		{
			audioSource.Stop();
		}

		Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
		for (int i = 0; i < renderers.Length; i++)
		{
			renderers[i].enabled = false;
		}

		Collider[] colliders = GetComponentsInChildren<Collider>(true);
		for (int i = 0; i < colliders.Length; i++)
		{
			colliders[i].enabled = false;
		}
	}

	private void CaptureInitialStateIfNeeded()
	{
		if (initialStateCaptured)
		{
			return;
		}

		initialLocalPosition = transform.localPosition;
		initialLocalRotation = transform.localRotation;
		initialLocalScale = transform.localScale;

		cachedBehaviours = GetComponentsInChildren<Behaviour>(true);
		cachedBehaviourEnabledStates = new bool[cachedBehaviours.Length];
		for (int i = 0; i < cachedBehaviours.Length; i++)
		{
			cachedBehaviourEnabledStates[i] = cachedBehaviours[i] != null && cachedBehaviours[i].enabled;
		}

		cachedRenderers = GetComponentsInChildren<Renderer>(true);
		cachedRendererEnabledStates = new bool[cachedRenderers.Length];
		for (int i = 0; i < cachedRenderers.Length; i++)
		{
			cachedRendererEnabledStates[i] = cachedRenderers[i] != null && cachedRenderers[i].enabled;
		}

		cachedColliders = GetComponentsInChildren<Collider>(true);
		cachedColliderEnabledStates = new bool[cachedColliders.Length];
		for (int i = 0; i < cachedColliders.Length; i++)
		{
			cachedColliderEnabledStates[i] = cachedColliders[i] != null && cachedColliders[i].enabled;
		}

		cachedRigidbodies = GetComponentsInChildren<Rigidbody>(true);
		cachedRigidbodyKinematicStates = new bool[cachedRigidbodies.Length];
		cachedRigidbodyGravityStates = new bool[cachedRigidbodies.Length];
		cachedRigidbodyDetectCollisionStates = new bool[cachedRigidbodies.Length];
		for (int i = 0; i < cachedRigidbodies.Length; i++)
		{
			if (cachedRigidbodies[i] == null)
			{
				continue;
			}

			cachedRigidbodyKinematicStates[i] = cachedRigidbodies[i].isKinematic;
			cachedRigidbodyGravityStates[i] = cachedRigidbodies[i].useGravity;
			cachedRigidbodyDetectCollisionStates[i] = cachedRigidbodies[i].detectCollisions;
		}

		initialStateCaptured = true;
	}

	private Vector3 ResolveHitPoint(ArenaEnemyKillContext context)
	{
		return context.HitPoint.sqrMagnitude > 0.001f ? context.HitPoint : GetAimPoint();
	}

	private Vector3 ResolveHitDirection(ArenaEnemyKillContext context)
	{
		if (context.HitDirection.sqrMagnitude > 0.001f)
		{
			return context.HitDirection;
		}

		Vector3 fallbackDirection = transform.forward;
		fallbackDirection.y = 0f;
		return fallbackDirection.sqrMagnitude > 0.001f ? fallbackDirection.normalized : Vector3.forward;
	}
}
