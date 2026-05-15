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

	public bool IsAlive { get; private set; } = true;

	public void Initialize()
	{
		IsAlive = true;
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
			ScheduleDestroy(context.DestroyDelay);
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

		ScheduleDestroy(context.DestroyDelay);
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

	private void ScheduleDestroy(float destroyDelay)
	{
		Destroy(gameObject, Mathf.Max(DefaultDestroyDelay, destroyDelay));
	}
}
