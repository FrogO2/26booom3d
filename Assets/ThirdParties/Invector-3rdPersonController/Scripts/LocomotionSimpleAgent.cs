using UnityEngine;

[RequireComponent(typeof(UnityEngine.AI.NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class LocomotionSimpleAgent : MonoBehaviour
{
	Animator anim;
	UnityEngine.AI.NavMeshAgent agent;

	[Header("Ground Check")]
	[SerializeField] float groundCheckRadius = 0.28f;
	[SerializeField] float groundCheckOffset = -0.14f;
	[SerializeField] LayerMask groundLayers = -1;

	[Header("Turn Before Move")]
	[SerializeField] float turnThresholdAngle = 30f;
	[SerializeField] float turnSpeed = 360f;
	[SerializeField] float inPlaceTurnSpeedThreshold = 1f;

	[Header("Attack")]
	[SerializeField] float attackDuration = 0.75f;
	[SerializeField] float attackCooldown = 1.25f;
	[SerializeField] EnemyEffect enemyEffect;

	float baseMoveSpeed;
	float smoothSpeed = 0f;
	bool grounded = true;
	bool isAttacking;
	float nextAttackTime;

	// Animator parameter IDs
	int animIDInputMagnitude;
	int animIDInputVertical;
	int animIDIsGrounded;
	int animIDIsDead;
	int animIDWeakAttack;

	public bool IsAttacking => isAttacking;
	public bool IsDead { get; private set; }

	void Start()
	{
		anim = GetComponent<Animator>();
		agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
		anim.applyRootMotion = false;
		agent.updatePosition = true;
		agent.updateRotation = false;
		baseMoveSpeed = Mathf.Max(0f, agent.speed);

		animIDInputMagnitude = Animator.StringToHash("InputMagnitude");
		animIDInputVertical = Animator.StringToHash("InputVertical");
		animIDIsGrounded = Animator.StringToHash("IsGrounded");
		animIDIsDead = Animator.StringToHash("isDead");
		animIDWeakAttack = Animator.StringToHash("WeakAttack");

		if (enemyEffect == null)
		{
			enemyEffect = GetComponent<EnemyEffect>();
		}
	}

	void Update()
	{
		if (IsDead)
		{
			return;
		}

		GroundedCheck();
		UpdateLocomotionAnimation();
	}

	void GroundedCheck()
	{
		Vector3 spherePos = new Vector3(transform.position.x,
		                                transform.position.y + groundCheckOffset,
		                                transform.position.z);
		grounded = Physics.CheckSphere(spherePos, groundCheckRadius, groundLayers,
		                               QueryTriggerInteraction.Ignore);
		anim.SetBool(animIDIsGrounded, grounded);
	}

	void UpdateLocomotionAnimation()
	{
		if (agent.speed > 0.001f && !Mathf.Approximately(agent.speed, baseMoveSpeed))
		{
			baseMoveSpeed = agent.speed;
		}

		bool hasDestination = agent.remainingDistance > agent.radius;
		float currentSpeed = agent.velocity.magnitude;
		float desiredAgentSpeed = baseMoveSpeed;

		if (hasDestination)
		{
			Vector3 toTarget = (agent.steeringTarget - transform.position);
			toTarget.y = 0f;

			if (toTarget.sqrMagnitude > 0.001f)
			{
				Quaternion targetRot = Quaternion.LookRotation(toTarget);
				transform.rotation = Quaternion.RotateTowards(
					transform.rotation, targetRot, turnSpeed * Time.deltaTime);
			}

			float angle = Vector3.Angle(transform.forward, toTarget.normalized);
			bool isSlow = currentSpeed <= inPlaceTurnSpeedThreshold;
			desiredAgentSpeed = (isSlow && angle > turnThresholdAngle) ? 0f : baseMoveSpeed;
		}

		if (!Mathf.Approximately(agent.speed, desiredAgentSpeed))
		{
			agent.speed = desiredAgentSpeed;
		}

		float targetSpeed = hasDestination ? currentSpeed : 0f;
		float smooth = Mathf.Min(1.0f, Time.deltaTime / 0.15f);
		smoothSpeed = Mathf.Lerp(smoothSpeed, targetSpeed, smooth);

		float motionBlend = baseMoveSpeed > 0.001f ? Mathf.Clamp01(smoothSpeed / baseMoveSpeed) : 0f;
		anim.SetFloat(animIDInputMagnitude, motionBlend);
		anim.SetFloat(animIDInputVertical, motionBlend);
	}

	public void TriggerAttack()
	{
		if (IsDead || isAttacking || Time.time < nextAttackTime)
		{
			return;
		}

		nextAttackTime = Time.time + attackCooldown;
		StartCoroutine(AttackRoutine());
	}

	public void TriggerDeath()
	{
		if (IsDead)
		{
			return;
		}

		IsDead = true;
		isAttacking = false;

		if (agent != null)
		{
			agent.isStopped = true;
			agent.ResetPath();
			agent.enabled = false;
		}

		if (enemyEffect != null)
		{
			enemyEffect.ActivateRagdoll();
			return;
		}

		anim.SetBool(animIDIsDead, true);
	}

	System.Collections.IEnumerator AttackRoutine()
	{
		isAttacking = true;
		anim.SetTrigger(animIDWeakAttack);
		yield return new WaitForSeconds(attackDuration);
		isAttacking = false;
	}

	// OnAnimatorMove is intentionally omitted: the NavMeshAgent drives position directly.
}
