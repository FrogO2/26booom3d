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

	[Header("Fall Settings")]
	[SerializeField] float fallTimeout = 0.15f;

	float maxSpeed;
	float smoothSpeed = 0f;
	float verticalVelocity = 0f;
	float fallTimeoutDelta;
	bool grounded = true;

	// Animator parameter IDs
	int animIDSpeed;
	int animIDGrounded;
	int animIDJump;
	int animIDFreeFall;
	int animIDMotionSpeed;

	void Start()
	{
		anim = GetComponent<Animator>();
		agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
		agent.updatePosition = true;
		agent.updateRotation = false;
		maxSpeed = agent.speed;

		fallTimeoutDelta = fallTimeout;

		animIDSpeed       = Animator.StringToHash("Speed");
		animIDGrounded    = Animator.StringToHash("Grounded");
		animIDJump        = Animator.StringToHash("Jump");
		animIDFreeFall    = Animator.StringToHash("FreeFall");
		animIDMotionSpeed = Animator.StringToHash("MotionSpeed");
	}

	void Update()
	{
		GroundedCheck();
		UpdateGravity();
		UpdateLocomotionAnimation();
	}

	void GroundedCheck()
	{
		Vector3 spherePos = new Vector3(transform.position.x,
		                                transform.position.y + groundCheckOffset,
		                                transform.position.z);
		grounded = Physics.CheckSphere(spherePos, groundCheckRadius, groundLayers,
		                               QueryTriggerInteraction.Ignore);
		anim.SetBool(animIDGrounded, grounded);
	}

	void UpdateGravity()
	{
		if (grounded)
		{
			fallTimeoutDelta = fallTimeout;
			verticalVelocity = -2f; // keep pressed to ground
			anim.SetBool(animIDFreeFall, false);
		}
		else
		{
			if (fallTimeoutDelta >= 0f)
				fallTimeoutDelta -= Time.deltaTime;
			else
				anim.SetBool(animIDFreeFall, true);

			verticalVelocity += Physics.gravity.y * Time.deltaTime;
		}

		// AI never jumps
		anim.SetBool(animIDJump, false);
	}

	void UpdateLocomotionAnimation()
	{
		bool hasDestination = agent.remainingDistance > agent.radius;

		if (hasDestination)
		{
			// Get direction from agent to next steering target (flat)
			Vector3 toTarget = (agent.steeringTarget - transform.position);
			toTarget.y = 0f;

			if (toTarget.sqrMagnitude > 0.001f)
			{
				// Rotate agent towards that direction each frame
				Quaternion targetRot = Quaternion.LookRotation(toTarget);
				transform.rotation = Quaternion.RotateTowards(
					transform.rotation, targetRot, turnSpeed * Time.deltaTime);
			}

			float angle = Vector3.Angle(transform.forward, toTarget.normalized);
			bool isSlow = agent.velocity.magnitude <= inPlaceTurnSpeedThreshold;
			agent.speed = (isSlow && angle > turnThresholdAngle) ? 0f : maxSpeed;
		}
		else
		{
			agent.speed = maxSpeed;
		}

		float targetSpeed = hasDestination ? agent.velocity.magnitude : 0f;

		// Smooth speed changes
		float smooth = Mathf.Min(1.0f, Time.deltaTime / 0.15f);
		smoothSpeed = Mathf.Lerp(smoothSpeed, targetSpeed, smooth);

		// MotionSpeed: 1 when moving, 0 when idle (controls animation blend rate)
		float motionSpeed = hasDestination ? 1f : 0f;

		anim.SetFloat(animIDSpeed, smoothSpeed);
		anim.SetFloat(animIDMotionSpeed, motionSpeed);
	}

	// OnAnimatorMove is intentionally omitted: new animations are in-place (no root motion),
	// so NavMeshAgent drives position directly via agent.updatePosition = true.
}
