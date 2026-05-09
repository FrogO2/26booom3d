using System;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class KnifePawnController : MonoBehaviour
{
	public enum DiscoveryMode
	{
		AnyCondition,
		AllConditions
	}

	[Header("Target")]
	[SerializeField] Transform player;
	[SerializeField] string playerTag = "Player";

	[Header("Discovery")]
	[SerializeField] bool detectByVision = true;
	[SerializeField] bool detectByReachableArea = true;
	[SerializeField] DiscoveryMode discoveryMode = DiscoveryMode.AnyCondition;

	[Header("Vision")]
	[SerializeField] Transform eyePoint;
	[SerializeField] float viewDistance = 18f;
	[SerializeField, Range(1f, 180f)] float viewAngle = 90f;
	[SerializeField] LayerMask visionLayers = ~0;

	[Header("Reachable Area")]
	[SerializeField] float reachableDetectionDistance = 14f;
	[SerializeField] float reachableSampleRadius = 2f;

	[Header("Chase")]
	[SerializeField] float attackDistance = 1.4f;
	[SerializeField] float destinationRefreshInterval = 0.15f;

	NavMeshAgent agent;
	NavMeshPath path;
	float nextDestinationRefreshTime;
	bool hasDiscoveredPlayer;

	void Awake()
	{
		agent = GetComponent<NavMeshAgent>();
		path = new NavMeshPath();

		if (eyePoint == null)
			eyePoint = transform;

		agent.stoppingDistance = Mathf.Max(0f, attackDistance);
		StopMoving();
	}

	void Update()
	{
		TryResolvePlayer();

		if (player == null || !agent.isOnNavMesh)
		{
			StopMoving();
			return;
		}

		if (!hasDiscoveredPlayer && ShouldDiscoverPlayer())
			hasDiscoveredPlayer = true;

		if (hasDiscoveredPlayer)
			ChasePlayer();
		else
			StopMoving();
	}

	void TryResolvePlayer()
	{
		if (player != null)
			return;

		GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
		if (playerObject != null)
			player = playerObject.transform;
	}

	bool ShouldDiscoverPlayer()
	{
		bool visionSatisfied = !detectByVision || CanSeePlayer();
		bool reachableSatisfied = !detectByReachableArea || IsPlayerInReachableArea();

		if (discoveryMode == DiscoveryMode.AllConditions)
			return visionSatisfied && reachableSatisfied;

		bool anyEnabledConditionMatched = false;

		if (detectByVision && visionSatisfied)
			anyEnabledConditionMatched = true;

		if (detectByReachableArea && reachableSatisfied)
			anyEnabledConditionMatched = true;

		return anyEnabledConditionMatched;
	}

	bool CanSeePlayer()
	{
		Vector3 origin = eyePoint != null ? eyePoint.position : transform.position;
		Vector3 targetPoint = GetTargetPoint(player);
		Vector3 toTarget = targetPoint - origin;
		float distanceToTarget = toTarget.magnitude;

		if (distanceToTarget > viewDistance || distanceToTarget <= 0.001f)
			return false;

		float angleToTarget = Vector3.Angle(transform.forward, toTarget.normalized);
		if (angleToTarget > viewAngle * 0.5f)
			return false;

		RaycastHit[] hits = Physics.RaycastAll(origin, toTarget.normalized, distanceToTarget, visionLayers, QueryTriggerInteraction.Ignore);
		Array.Sort(hits, CompareHitDistance);

		for (int index = 0; index < hits.Length; index++)
		{
			Transform hitTransform = hits[index].collider.transform;

			if (hitTransform == transform || hitTransform.IsChildOf(transform))
				continue;

			return hitTransform == player || hitTransform.IsChildOf(player);
		}

		return true;
	}

	bool IsPlayerInReachableArea()
	{
		Vector3 targetPoint = GetTargetPoint(player);
		float distanceToPlayer = Vector3.Distance(transform.position, targetPoint);

		if (reachableDetectionDistance > 0f && distanceToPlayer > reachableDetectionDistance)
			return false;

		if (!NavMesh.SamplePosition(targetPoint, out NavMeshHit navHit, reachableSampleRadius, NavMesh.AllAreas))
			return false;

		if (!agent.CalculatePath(navHit.position, path))
			return false;

		return path.status == NavMeshPathStatus.PathComplete;
	}

	void ChasePlayer()
	{
		if (Time.time < nextDestinationRefreshTime)
			return;

		nextDestinationRefreshTime = Time.time + destinationRefreshInterval;

		Vector3 targetPoint = GetTargetPoint(player);
		if (NavMesh.SamplePosition(targetPoint, out NavMeshHit navHit, reachableSampleRadius, NavMesh.AllAreas))
			targetPoint = navHit.position;

		agent.isStopped = false;
		agent.SetDestination(targetPoint);
	}

	void StopMoving()
	{
		agent.isStopped = true;

		if (agent.hasPath)
			agent.ResetPath();
	}

	Vector3 GetTargetPoint(Transform target)
	{
		if (target == null)
			return transform.position;

		if (target.TryGetComponent(out CharacterController controller))
			return controller.bounds.center;

		if (target.TryGetComponent(out Collider targetCollider))
			return targetCollider.bounds.center;

		return target.position;
	}

	static int CompareHitDistance(RaycastHit left, RaycastHit right)
	{
		return left.distance.CompareTo(right.distance);
	}

	void OnDrawGizmosSelected()
	{
		Vector3 origin = eyePoint != null ? eyePoint.position : transform.position;

		Gizmos.color = hasDiscoveredPlayer ? Color.red : Color.yellow;
		Gizmos.DrawWireSphere(transform.position, reachableDetectionDistance);

		Vector3 leftDirection = Quaternion.Euler(0f, -viewAngle * 0.5f, 0f) * transform.forward;
		Vector3 rightDirection = Quaternion.Euler(0f, viewAngle * 0.5f, 0f) * transform.forward;

		Gizmos.color = Color.cyan;
		Gizmos.DrawLine(origin, origin + leftDirection * viewDistance);
		Gizmos.DrawLine(origin, origin + rightDirection * viewDistance);
		Gizmos.DrawWireSphere(origin, 0.1f);
	}
}