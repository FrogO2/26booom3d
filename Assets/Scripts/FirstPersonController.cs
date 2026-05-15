using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
	private const float LookSuppressionMaxStep = 1f / 30f;

	[Header("References")]
	[SerializeField] private Camera playerCamera;
	[SerializeField] private Transform cameraRoot;
	[SerializeField] private InputActionAsset inputActions;
	[SerializeField] private string actionMapName = "Player";

	[Header("Look")]
	[SerializeField] private float lookSensitivity = 180f;
	[SerializeField] private float minPitch = -85f;
	[SerializeField] private float maxPitch = 85f;
	[SerializeField] private bool lockCursor = true;

	[Header("Movement")]
	[SerializeField] private float walkSpeed = 6f;
	[SerializeField] private float sprintSpeed = 9f;
	[SerializeField] private float crouchSpeed = 3.5f;
	[SerializeField] private float acceleration = 24f;
	[SerializeField] private float deceleration = 20f;
	[SerializeField, Range(0f, 1f)] private float airControl = 0.45f;
	[SerializeField] private float jumpHeight = 1.35f;
	[SerializeField] private float gravity = 30f;
	[SerializeField] private float terminalFallSpeed = 45f;
	[SerializeField] private float groundedStickForce = 5f;

	[Header("Stance")]
	[SerializeField] private float standingHeight = 1.8f;
	[SerializeField] private float crouchingHeight = 1.1f;
	[SerializeField] private float stanceLerpSpeed = 12f;
	[SerializeField] private float standingCameraHeight = 0.78f;
	[SerializeField] private float crouchingCameraHeight = 0.38f;

	[Header("Assist")]
	[SerializeField] private float coyoteTime = 0.15f;
	[SerializeField] private float jumpBufferTime = 0.2f;
	[SerializeField] private int extraAirJumps = 1;

	[Header("Slide")]
	[SerializeField] private float slideSpeed = 12f;
	[SerializeField] private float slideDuration = 0.65f;
	[SerializeField] private float slideCooldown = 0.25f;

	[Header("Wall Run")]
	[SerializeField] private float wallRunSpeed = 9.5f;
	[SerializeField] private float wallRunGravity = 6f;
	[SerializeField] private float wallCheckDistance = 0.9f;
	[SerializeField] private float wallCheckHeight = 1.1f;
	[SerializeField] private float wallJumpHorizontalForce = 8f;
	[SerializeField] private float wallJumpVerticalForce = 8.5f;

	[Header("World Detection")]
	[SerializeField] private LayerMask environmentMask = ~0;
	[SerializeField] private float groundProbeDistance = 0.3f;
	[SerializeField] private float headroomProbePadding = 0.05f;

	[Header("Camera Feedback")]
	[SerializeField] private float baseFieldOfView = 90f;
	[SerializeField] private float sprintFieldOfView = 96f;
	[SerializeField] private float slideFieldOfView = 100f;
	[SerializeField] private float fieldOfViewLerpSpeed = 8f;
	[SerializeField] private float wallRunTilt = 12f;
	[SerializeField] private float tiltLerpSpeed = 10f;

	private CharacterController characterController;
	private InputAction moveAction;
	private InputAction lookAction;
	private InputAction jumpAction;
	private InputAction sprintAction;
	private InputAction crouchAction;

	private Vector2 moveInput;
	private Vector2 lookInput;
	private Vector3 planarVelocity;
	private Vector3 slideDirection;
	private Vector3 wallNormal;
	private Vector3 wallRunDirection;
	private float pitch;
	private float verticalVelocity;
	private float coyoteTimer;
	private float jumpBufferTimer;
	private float slideTimer;
	private float slideCooldownTimer;
	private float currentCameraTilt;
	private int airJumpsUsed;
	private bool isGrounded;
	private bool isCrouching;
	private bool isSliding;
	private bool isWallRunning;
	private bool wallOnLeft;
	private bool wallOnRight;
	private bool moveInputLocked;
	private bool lookInputLocked;
	private bool jumpInputLocked;
	private bool sprintInputLocked;
	private bool crouchInputLocked;
	private bool moveActionSuppressedByController;
	private bool lookActionSuppressedByController;
	private bool jumpActionSuppressedByController;
	private bool sprintActionSuppressedByController;
	private bool crouchActionSuppressedByController;
	private float lookInputSuppressionRemaining;
	private RaycastHit leftWallHit;
	private RaycastHit rightWallHit;

	public Camera PlayerCamera => playerCamera;
	public Transform CameraRoot => cameraRoot;
	public InputActionAsset InputActions => inputActions;
	public string ActionMapName => actionMapName;
	public Vector2 MoveInput => moveInput;
	public Vector2 LookInput => lookInput;
	public Vector3 PlanarVelocity => planarVelocity;
	public Vector3 WorldVelocity => planarVelocity + Vector3.up * verticalVelocity;
	public Vector3 WallNormal => wallNormal;
	public float Pitch => pitch;
	public float StandingCameraHeight => standingCameraHeight;
	public float CrouchingCameraHeight => crouchingCameraHeight;
	public float StanceLerpSpeed => stanceLerpSpeed;
	public float WalkSpeed => walkSpeed;
	public float SprintSpeed => sprintSpeed;
	public float CrouchSpeed => crouchSpeed;
	public float WallRunSpeed => wallRunSpeed;
	public float SlideDuration => slideDuration;
	public float PlanarSpeed => planarVelocity.magnitude;
	public float TotalSpeed => WorldVelocity.magnitude;
	public float SlideProgress01 => isSliding ? 1f - Mathf.Clamp01(slideTimer / Mathf.Max(0.01f, slideDuration)) : 0f;
	public bool IsGrounded => isGrounded;
	public bool IsCrouching => isCrouching;
	public bool IsSliding => isSliding;
	public bool IsWallRunning => isWallRunning;
	public bool IsLeftWalling => isWallRunning && wallOnLeft;
	public bool IsRightWalling => isWallRunning && wallOnRight;
	public bool IsSprinting => sprintAction != null && sprintAction.IsPressed() && moveInput.y > 0.1f && isGrounded && !isCrouching && !isSliding;
	public bool UseExternalViewAnimation { get; set; }

	private void Awake()
	{
		characterController = GetComponent<CharacterController>();

		if (cameraRoot == null && playerCamera != null)
		{
			cameraRoot = playerCamera.transform.parent != null ? playerCamera.transform.parent : playerCamera.transform;
		}

		if (playerCamera == null)
		{
			playerCamera = GetComponentInChildren<Camera>();
		}

		if (cameraRoot == null && playerCamera != null)
		{
			cameraRoot = playerCamera.transform;
		}

		characterController.height = standingHeight;
		characterController.center = Vector3.up * (standingHeight * 0.5f);

		if (playerCamera != null && baseFieldOfView <= 0f)
		{
			baseFieldOfView = playerCamera.fieldOfView;
		}

		BindInputActions();
	}

	private void OnEnable()
	{
		EnableActions();
		UpdateActionLockStates();

		if (lockCursor)
		{
			Cursor.lockState = CursorLockMode.Locked;
			Cursor.visible = false;
		}
	}

	private void OnDisable()
	{
		DisableActions();

		if (lockCursor)
		{
			Cursor.lockState = CursorLockMode.None;
			Cursor.visible = true;
		}
	}

	private void Update()
	{
		SampleInput();
		UpdateTimers();
		ProbeGround();
		ProbeWalls();
		HandleLook();
		HandleSlide();
		HandleWallRun();
		HandleJump();
		HandleMovement();
		HandleStance();

		if (!UseExternalViewAnimation)
		{
			HandleCameraEffects();
		}
        // Debug.Log($"Crouch: {isCrouching}, Grounded: {isGrounded}, Sliding: {isSliding}, WallRunning: {isWallRunning}, Vertical Velocity: {verticalVelocity:F2}");
	}

	private void BindInputActions()
	{
		if (inputActions == null)
		{
			Debug.LogWarning($"{nameof(FirstPersonController)} on {name} has no InputActionAsset assigned.", this);
			return;
		}

		InputActionMap actionMap = inputActions.FindActionMap(actionMapName, true);
		moveAction = actionMap.FindAction("Move", true);
		lookAction = actionMap.FindAction("Look", true);
		jumpAction = actionMap.FindAction("Jump", true);
		sprintAction = actionMap.FindAction("Sprint", true);
		crouchAction = actionMap.FindAction("Crouch", true);
	}

	private void EnableActions()
	{
		moveAction?.Enable();
		lookAction?.Enable();
		jumpAction?.Enable();
		sprintAction?.Enable();
		crouchAction?.Enable();
	}

	private void DisableActions()
	{
		moveAction?.Disable();
		lookAction?.Disable();
		jumpAction?.Disable();
		sprintAction?.Disable();
		crouchAction?.Disable();
	}

	private void SampleInput()
	{
		UpdateActionLockStates();
		moveInput = moveInputLocked || moveAction == null ? Vector2.zero : moveAction.ReadValue<Vector2>();
		lookInput = IsLookInputSuppressed() || lookAction == null ? Vector2.zero : lookAction.ReadValue<Vector2>();

		if (!jumpInputLocked && jumpAction != null && jumpAction.WasPressedThisFrame())
		{
			jumpBufferTimer = jumpBufferTime;
		}
	}

	public void SuppressLookInput(float duration)
	{
		lookInputSuppressionRemaining = Mathf.Max(lookInputSuppressionRemaining, Mathf.Max(0f, duration));
		lookInput = Vector2.zero;
		UpdateActionLockStates();
	}

	public void SetMoveInputLocked(bool locked, bool clearInputState = true)
	{
		moveInputLocked = locked;
		if (clearInputState)
		{
			moveInput = Vector2.zero;
			RefreshActionState(moveAction, !moveInputLocked, ref moveActionSuppressedByController, pulseWhenEnabled: !moveInputLocked);
		}
		else
		{
			RefreshActionState(moveAction, !moveInputLocked, ref moveActionSuppressedByController);
		}
	}

	public void SetLookInputLocked(bool locked, bool clearInputState = true)
	{
		lookInputLocked = locked;
		lookInput = Vector2.zero;

		if (clearInputState)
		{
			RefreshActionState(lookAction, !IsLookInputSuppressed(), ref lookActionSuppressedByController, pulseWhenEnabled: !IsLookInputSuppressed());
		}
		else
		{
			RefreshActionState(lookAction, !IsLookInputSuppressed(), ref lookActionSuppressedByController);
		}
	}

	public void SetJumpInputLocked(bool locked, bool clearInputState = true)
	{
		jumpInputLocked = locked;
		jumpBufferTimer = 0f;

		if (clearInputState)
		{
			RefreshActionState(jumpAction, !jumpInputLocked, ref jumpActionSuppressedByController, pulseWhenEnabled: !jumpInputLocked);
		}
		else
		{
			RefreshActionState(jumpAction, !jumpInputLocked, ref jumpActionSuppressedByController);
		}
	}

	public void SetSprintInputLocked(bool locked, bool clearInputState = true)
	{
		sprintInputLocked = locked;
		RefreshActionState(sprintAction, !sprintInputLocked, ref sprintActionSuppressedByController, clearInputState && !sprintInputLocked);
	}

	public void SetCrouchInputLocked(bool locked, bool clearInputState = true)
	{
		crouchInputLocked = locked;
		RefreshActionState(crouchAction, !crouchInputLocked, ref crouchActionSuppressedByController, clearInputState && !crouchInputLocked);
	}

	public void SetTraversalInputLocked(bool locked, bool clearInputState = true)
	{
		SetMoveInputLocked(locked, clearInputState);
		SetJumpInputLocked(locked, clearInputState);
		SetSprintInputLocked(locked, clearInputState);
		SetCrouchInputLocked(locked, clearInputState);
	}

	public void ClearInputState(bool clearMovement = true, bool clearLook = true)
	{
		if (clearMovement)
		{
			moveInput = Vector2.zero;
			jumpBufferTimer = 0f;
			RefreshActionState(moveAction, !moveInputLocked, ref moveActionSuppressedByController, pulseWhenEnabled: !moveInputLocked);
			RefreshActionState(jumpAction, !jumpInputLocked, ref jumpActionSuppressedByController, pulseWhenEnabled: !jumpInputLocked);
			RefreshActionState(sprintAction, !sprintInputLocked, ref sprintActionSuppressedByController, pulseWhenEnabled: !sprintInputLocked);
			RefreshActionState(crouchAction, !crouchInputLocked, ref crouchActionSuppressedByController, pulseWhenEnabled: !crouchInputLocked);
		}

		if (clearLook)
		{
			lookInput = Vector2.zero;
			RefreshActionState(lookAction, !IsLookInputSuppressed(), ref lookActionSuppressedByController, pulseWhenEnabled: !IsLookInputSuppressed());
		}
	}

	public void ResetToSpawn(Vector3 worldPosition, Quaternion worldRotation)
	{
		ClearInputState();
		moveInput = Vector2.zero;
		lookInput = Vector2.zero;
		planarVelocity = Vector3.zero;
		slideDirection = Vector3.zero;
		wallNormal = Vector3.zero;
		wallRunDirection = Vector3.zero;
		verticalVelocity = 0f;
		coyoteTimer = 0f;
		jumpBufferTimer = 0f;
		slideTimer = 0f;
		slideCooldownTimer = 0f;
		currentCameraTilt = 0f;
		airJumpsUsed = 0;
		isGrounded = false;
		isCrouching = false;
		isSliding = false;
		isWallRunning = false;
		wallOnLeft = false;
		wallOnRight = false;
		leftWallHit = default;
		rightWallHit = default;
		lookInputSuppressionRemaining = 0f;

		if (characterController != null)
		{
			characterController.enabled = false;
		}

		transform.SetPositionAndRotation(worldPosition, worldRotation);
		pitch = 0f;

		if (characterController != null)
		{
			characterController.height = standingHeight;
			characterController.center = Vector3.up * (standingHeight * 0.5f);
			characterController.enabled = true;
		}

		if (cameraRoot != null)
		{
			Vector3 localPosition = cameraRoot.localPosition;
			localPosition.y = standingCameraHeight;
			cameraRoot.localPosition = localPosition;
			cameraRoot.localRotation = Quaternion.Euler(0f, 0f, 0f);
		}

		ProbeGround();
	}

	private void UpdateActionLockStates()
	{
		if (lookInputSuppressionRemaining > 0f)
		{
			lookInputSuppressionRemaining = Mathf.Max(0f, lookInputSuppressionRemaining - Mathf.Min(Time.unscaledDeltaTime, LookSuppressionMaxStep));
		}

		RefreshActionState(moveAction, !moveInputLocked, ref moveActionSuppressedByController);
		RefreshActionState(lookAction, !IsLookInputSuppressed(), ref lookActionSuppressedByController);
		RefreshActionState(jumpAction, !jumpInputLocked, ref jumpActionSuppressedByController);
		RefreshActionState(sprintAction, !sprintInputLocked, ref sprintActionSuppressedByController);
		RefreshActionState(crouchAction, !crouchInputLocked, ref crouchActionSuppressedByController);

		if (IsLookInputSuppressed())
		{
			lookInput = Vector2.zero;
		}
	}

	private bool IsLookInputSuppressed()
	{
		return lookInputLocked || lookInputSuppressionRemaining > 0f;
	}

	private void RefreshActionState(InputAction action, bool shouldEnable, ref bool suppressedByController, bool pulseWhenEnabled = false)
	{
		if (action == null)
		{
			return;
		}

		if (!shouldEnable)
		{
			if (action.enabled)
			{
				action.Disable();
				suppressedByController = true;
			}

			return;
		}

		if (suppressedByController && isActiveAndEnabled)
		{
			action.Enable();
			suppressedByController = false;

			if (pulseWhenEnabled)
			{
				action.Disable();
				action.Enable();
			}
		}
		else if (pulseWhenEnabled && action.enabled)
		{
			action.Disable();
			action.Enable();
		}
	}

	private void UpdateTimers()
	{
		float deltaTime = Time.deltaTime;

		coyoteTimer -= deltaTime;
		jumpBufferTimer -= deltaTime;
		slideTimer -= deltaTime;
		slideCooldownTimer -= deltaTime;
	}

	private void HandleLook()
	{
		float lookScale = lookSensitivity * Time.deltaTime;
		float yawDelta = lookInput.x * lookScale;
		float pitchDelta = lookInput.y * lookScale;

		transform.Rotate(Vector3.up * yawDelta);

		pitch = Mathf.Clamp(pitch - pitchDelta, minPitch, maxPitch);

		if (!UseExternalViewAnimation && cameraRoot != null)
		{
			cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, currentCameraTilt);
		}
	}

	private void HandleSlide()
	{
		bool wantsCrouch = crouchAction != null && crouchAction.IsPressed();
		bool wantsSprint = sprintAction != null && sprintAction.IsPressed();
        // Debug.Log($"WantsCrouch: {wantsCrouch}");

		if (!isSliding && slideCooldownTimer <= 0f && isGrounded && wantsCrouch && wantsSprint && moveInput.y > 0.1f)
		{
			isSliding = true;
			slideTimer = slideDuration;
			slideCooldownTimer = slideDuration + slideCooldown;

			Vector3 desiredDirection = GetMoveDirection();
			slideDirection = desiredDirection.sqrMagnitude > 0.001f ? desiredDirection : transform.forward;
			slideDirection.y = 0f;
			slideDirection.Normalize();
		}

		if (isSliding && (!isGrounded || slideTimer <= 0f))
		{
			isSliding = false;
		}
	}

	private void HandleWallRun()
	{
		bool canWallRun = !isGrounded && !isSliding && moveInput.y > 0.1f && verticalVelocity <= 0.5f;
		bool touchingWall = wallOnLeft || wallOnRight;

		if (canWallRun && touchingWall)
		{
			if (!isWallRunning)
			{
				isWallRunning = true;
				verticalVelocity = 0f;
			}

			wallRunDirection = Vector3.Cross(wallNormal, Vector3.up).normalized;

			if (Vector3.Dot(wallRunDirection, transform.forward) < 0f)
			{
				wallRunDirection *= -1f;
			}
		}
		else
		{
			isWallRunning = false;
		}
	}

	private void HandleJump()
	{
		if (jumpBufferTimer <= 0f)
		{
			return;
		}

		if (isWallRunning)
		{
			isWallRunning = false;
			jumpBufferTimer = 0f;
			Vector3 wallJumpDirection = Vector3.ProjectOnPlane(wallNormal, Vector3.up).normalized;
			planarVelocity += wallJumpDirection * wallJumpHorizontalForce;
			verticalVelocity = Mathf.Max(verticalVelocity + wallJumpVerticalForce, wallJumpVerticalForce);
			return;
		}

		if (isGrounded || coyoteTimer > 0f)
		{
			jumpBufferTimer = 0f;
			coyoteTimer = 0f;
			isGrounded = false;
			verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
			return;
		}

		if (airJumpsUsed < extraAirJumps)
		{
			jumpBufferTimer = 0f;
			airJumpsUsed++;
			verticalVelocity = Mathf.Sqrt(2f * gravity * jumpHeight);
		}
	}

	private void HandleMovement()
	{
		float deltaTime = Time.deltaTime;
		Vector3 desiredVelocity = Vector3.zero;
		float control = isGrounded ? 1f : airControl;

		if (isSliding)
		{
			float slideProgress = 1f - Mathf.Clamp01(slideTimer / Mathf.Max(0.01f, slideDuration));
			float currentSlideSpeed = Mathf.Lerp(slideSpeed, crouchSpeed, slideProgress);
			desiredVelocity = slideDirection * currentSlideSpeed;
		}
		else if (isWallRunning)
		{
			desiredVelocity = wallRunDirection * wallRunSpeed;
		}
		else
		{
			float targetSpeed = GetTargetSpeed();
			desiredVelocity = GetMoveDirection() * targetSpeed;
		}

		float changeRate = desiredVelocity.sqrMagnitude > 0.01f ? acceleration : deceleration;
		planarVelocity = Vector3.MoveTowards(planarVelocity, desiredVelocity, changeRate * control * deltaTime);

		if (isGrounded && verticalVelocity < 0f)
		{
			verticalVelocity = -groundedStickForce;
		}
		else if (isWallRunning)
		{
			verticalVelocity = Mathf.Max(verticalVelocity - wallRunGravity * deltaTime, -wallRunGravity);
		}
		else
		{
			verticalVelocity = Mathf.Max(verticalVelocity - gravity * deltaTime, -terminalFallSpeed);
		}

		Vector3 totalVelocity = planarVelocity + Vector3.up * verticalVelocity;
		CollisionFlags collisionFlags = characterController.Move(totalVelocity * deltaTime);

		if ((collisionFlags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
		{
			verticalVelocity = 0f;
		}

		bool wasGrounded = isGrounded;
		isGrounded = (collisionFlags & CollisionFlags.Below) != 0 || CheckGrounded();

		if (isGrounded)
		{
			coyoteTimer = coyoteTime;
			airJumpsUsed = 0;
		}
		else if (wasGrounded)
		{
			coyoteTimer = coyoteTime;
		}
	}

	private void HandleStance()
	{
		bool wantsCrouch = crouchAction != null && crouchAction.IsPressed();
		bool targetCrouch = isSliding || wantsCrouch;

		if (!targetCrouch && !CanStandUp())
		{
			targetCrouch = true;
		}

		isCrouching = targetCrouch;

		float targetHeight = isCrouching ? crouchingHeight : standingHeight;
		float nextHeight = Mathf.Lerp(characterController.height, targetHeight, stanceLerpSpeed * Time.deltaTime);
		characterController.height = nextHeight;
		characterController.center = Vector3.up * (nextHeight * 0.5f);

		if (!UseExternalViewAnimation && cameraRoot != null)
		{
			Vector3 localPosition = cameraRoot.localPosition;
			float targetCameraHeight = isCrouching ? crouchingCameraHeight : standingCameraHeight;
			localPosition.y = Mathf.Lerp(localPosition.y, targetCameraHeight, stanceLerpSpeed * Time.deltaTime);
			cameraRoot.localPosition = localPosition;
		}
	}

	private void HandleCameraEffects()
	{
		if (playerCamera == null)
		{
			return;
		}

		float targetFov = baseFieldOfView;

		if (isSliding)
		{
			targetFov = slideFieldOfView;
		}
		else if (isWallRunning || (sprintAction != null && sprintAction.IsPressed() && moveInput.y > 0.1f && isGrounded))
		{
			targetFov = sprintFieldOfView;
		}

		playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, fieldOfViewLerpSpeed * Time.deltaTime);

		float targetTilt = 0f;

		if (isWallRunning)
		{
			targetTilt = wallOnLeft ? -wallRunTilt : wallRunTilt;
		}

		currentCameraTilt = Mathf.Lerp(currentCameraTilt, targetTilt, tiltLerpSpeed * Time.deltaTime);

		if (cameraRoot != null)
		{
			cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, currentCameraTilt);
		}
	}

	private void ProbeGround()
	{
		bool wasGrounded = isGrounded;
		isGrounded = CheckGrounded();

		if (isGrounded)
		{
			coyoteTimer = coyoteTime;
			airJumpsUsed = 0;
		}
	}

	private void ProbeWalls()
	{
		Vector3 origin = transform.position + Vector3.up * wallCheckHeight;

		wallOnLeft = Physics.Raycast(origin, -transform.right, out leftWallHit, wallCheckDistance, environmentMask, QueryTriggerInteraction.Ignore);
		wallOnRight = Physics.Raycast(origin, transform.right, out rightWallHit, wallCheckDistance, environmentMask, QueryTriggerInteraction.Ignore);

		Debug.DrawRay(origin, -transform.right * wallCheckDistance, wallOnLeft ? Color.green : Color.red);
		Debug.DrawRay(origin, transform.right * wallCheckDistance, wallOnRight ? Color.green : Color.red);

		if (wallOnLeft)
		{
			wallNormal = leftWallHit.normal;
		}
		else if (wallOnRight)
		{
			wallNormal = rightWallHit.normal;
		}
		else
		{
			wallNormal = Vector3.zero;
		}
	}

	private Vector3 GetMoveDirection()
	{
		Vector3 direction = transform.right * moveInput.x + transform.forward * moveInput.y;

		if (direction.sqrMagnitude > 1f)
		{
			direction.Normalize();
		}

		direction.y = 0f;
		return direction.normalized;
	}

	private float GetTargetSpeed()
	{
		bool wantsSprint = sprintAction != null && sprintAction.IsPressed();

		if (!isGrounded)
		{
			return wantsSprint && moveInput.y > 0.1f ? sprintSpeed : walkSpeed;
		}

		if (isCrouching)
		{
			return crouchSpeed;
		}

		return wantsSprint && moveInput.y > 0.1f ? sprintSpeed : walkSpeed;
	}

	private bool CheckGrounded()
	{
		Vector3 center = transform.position + characterController.center;
		float sphereOffset = (characterController.height * 0.5f) - characterController.radius + 0.02f;
		Vector3 sphereOrigin = center - Vector3.up * sphereOffset;
		float radius = Mathf.Max(0.05f, characterController.radius * 0.92f);

		return Physics.SphereCast(
			sphereOrigin,
			radius,
			Vector3.down,
			out _,
			groundProbeDistance,
			environmentMask,
			QueryTriggerInteraction.Ignore);
	}

	private bool CanStandUp()
	{
		GetHeadroomCapsule(characterController, out Vector3 bottomPoint, out Vector3 topPoint, out float radius);

		return !Physics.CheckCapsule(bottomPoint, topPoint, radius, environmentMask, QueryTriggerInteraction.Ignore);
	}

	private void GetHeadroomCapsule(CharacterController controller, out Vector3 bottomPoint, out Vector3 topPoint, out float radius)
	{
		radius = Mathf.Max(0.05f, controller.radius - headroomProbePadding);
		Vector3 standingCenter = transform.position + Vector3.up * (standingHeight * 0.5f);
		float cylinderHalfHeight = Mathf.Max(0f, (standingHeight * 0.5f) - radius);
		bottomPoint = standingCenter - Vector3.up * cylinderHalfHeight;
		topPoint = standingCenter + Vector3.up * cylinderHalfHeight;
	}

	private void OnDrawGizmosSelected()
	{
		CharacterController controller = characterController != null ? characterController : GetComponent<CharacterController>();

		if (controller == null)
		{
			return;
		}

		Vector3 groundCenter = transform.position + controller.center;
		float sphereOffset = (controller.height * 0.5f) - controller.radius + 0.02f;
		Vector3 sphereOrigin = groundCenter - Vector3.up * sphereOffset;
		float groundRadius = Mathf.Max(0.05f, controller.radius * 0.92f);

		Gizmos.color = Color.green;
		Gizmos.DrawWireSphere(sphereOrigin, groundRadius);
		Gizmos.DrawLine(sphereOrigin, sphereOrigin + Vector3.down * groundProbeDistance);

		Vector3 wallOrigin = transform.position + Vector3.up * wallCheckHeight;
		Gizmos.color = wallOnRight ? Color.green : Color.red;
		Gizmos.DrawLine(wallOrigin, wallOrigin + transform.right * wallCheckDistance);
		Gizmos.color = wallOnLeft ? Color.green : Color.red;
		Gizmos.DrawLine(wallOrigin, wallOrigin - transform.right * wallCheckDistance);

		if (wallOnLeft)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireSphere(leftWallHit.point, 0.08f);
			Gizmos.color = Color.blue;
			Gizmos.DrawLine(leftWallHit.point, leftWallHit.point + leftWallHit.normal * 0.35f);
		}

		if (wallOnRight)
		{
			Gizmos.color = Color.green;
			Gizmos.DrawWireSphere(rightWallHit.point, 0.08f);
			Gizmos.color = Color.blue;
			Gizmos.DrawLine(rightWallHit.point, rightWallHit.point + rightWallHit.normal * 0.35f);
		}

		GetHeadroomCapsule(controller, out Vector3 headroomBottom, out Vector3 headroomTop, out float headroomRadius);
		bool headroomBlocked = Physics.CheckCapsule(headroomBottom, headroomTop, headroomRadius, environmentMask, QueryTriggerInteraction.Ignore);

		Gizmos.color = headroomBlocked ? Color.red : Color.yellow;
		Gizmos.DrawWireSphere(headroomBottom, headroomRadius);
		Gizmos.DrawWireSphere(headroomTop, headroomRadius);
		Gizmos.DrawLine(headroomBottom + transform.forward * headroomRadius, headroomTop + transform.forward * headroomRadius);
		Gizmos.DrawLine(headroomBottom - transform.forward * headroomRadius, headroomTop - transform.forward * headroomRadius);
		Gizmos.DrawLine(headroomBottom + transform.right * headroomRadius, headroomTop + transform.right * headroomRadius);
		Gizmos.DrawLine(headroomBottom - transform.right * headroomRadius, headroomTop - transform.right * headroomRadius);
	}
}