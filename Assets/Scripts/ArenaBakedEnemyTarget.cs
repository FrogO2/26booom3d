using UnityEngine;
using UnityEngine.AI;

[AddComponentMenu("Arena/Enemy Target")]
public class ArenaBakedEnemyTarget : MonoBehaviour
{
	[SerializeField] private ArenaEncounterFlow encounterFlow;

	public bool IsAlive { get; private set; } = true;

	private void Awake()
	{
		ResolveEncounterFlow();
	}

	public void Initialize(ArenaEncounterFlow owner)
	{
		if (owner != null)
		{
			encounterFlow = owner;
		}

		ResolveEncounterFlow();
		IsAlive = true;
	}

	public Vector3 GetAimPoint()
	{
		return transform.position + Vector3.up * 1.2f;
	}

	public void Kill()
	{
		if (!IsAlive)
		{
			return;
		}

		IsAlive = false;

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

		encounterFlow?.NotifyEnemyKilled(this);
		Destroy(gameObject, 0.05f);
	}

	private void ResolveEncounterFlow()
	{
		if (encounterFlow == null)
		{
			encounterFlow = GetComponentInParent<ArenaEncounterFlow>();
		}
	}
}
