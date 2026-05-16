using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class FirstPersonViewAnimationController : MonoBehaviour
{
	[System.Serializable]
	private struct BobSettings
	{
		[Min(0f)] public float verticalAmplitude;
		[Min(0f)] public float horizontalAmplitude;
		[Min(0f)] public float pitchAmplitude;
		[Min(0f)] public float rollAmplitude;
		[Min(0.1f)] public float frequency;
	}

	[Header("References")]
	[SerializeField] private FirstPersonController controller;
	[SerializeField] private Camera playerCamera;
	[SerializeField] private Camera weaponCamera;
	[SerializeField] private Transform cameraRoot;
	[SerializeField] private Transform weaponPivot;
	[SerializeField] private Transform weaponModel;
	[SerializeField] private Animator weaponAnimator;
	[SerializeField] private InputActionAsset inputActions;
	[SerializeField] private string actionMapName = "Player";
	[SerializeField] private string attackActionName = "Attack";

	[Header("Field Of View")]
	[SerializeField] private float baseFieldOfView = 90f;
	[SerializeField] private float sprintFieldOfView = 96f;
	[SerializeField] private float slideFieldOfView = 100f;
	[SerializeField] private float fieldOfViewLerpSpeed = 8f;

	[Header("Wall Tilt")]
	[SerializeField] private float wallRunTilt = 12f;
	[SerializeField] private float tiltLerpSpeed = 10f;

	[Header("Camera Bob")]
	[SerializeField] private float bobBlendSpeed = 10f;
	[SerializeField] private BobSettings walkBob = new BobSettings
	{
		verticalAmplitude = 0.025f,
		horizontalAmplitude = 0.012f,
		pitchAmplitude = 0.85f,
		rollAmplitude = 0.7f,
		frequency = 7.5f
	};
	[SerializeField] private BobSettings sprintBob = new BobSettings
	{
		verticalAmplitude = 0.035f,
		horizontalAmplitude = 0.018f,
		pitchAmplitude = 1.25f,
		rollAmplitude = 1f,
		frequency = 9.5f
	};
	[SerializeField] private BobSettings crouchBob = new BobSettings
	{
		verticalAmplitude = 0.014f,
		horizontalAmplitude = 0.009f,
		pitchAmplitude = 0.45f,
		rollAmplitude = 0.4f,
		frequency = 5.5f
	};
	[SerializeField] private BobSettings wallRunBob = new BobSettings
	{
		verticalAmplitude = 0.016f,
		horizontalAmplitude = 0.022f,
		pitchAmplitude = 0.5f,
		rollAmplitude = 0.5f,
		frequency = 8.25f
	};

	[Header("Slide Shake")]
	[SerializeField] private float slideShakePositionAmplitude = 0.035f;
	[SerializeField] private float slideShakeRotationAmplitude = 1.5f;
	[SerializeField] private float slideShakeFrequency = 23f;

	[Header("Airborne Speed Shake")]
	[SerializeField] private float airborneShakeSpeedThreshold = 7f;
	[SerializeField] private float airborneShakeSpeedRange = 7f;
	[SerializeField] private float airborneShakePositionAmplitude = 0.035f;
	[SerializeField] private float airborneShakeRotationAmplitude = 1.5f;
	[SerializeField] private float airborneShakeFrequency = 23f;

	[Header("Weapon Motion")]
	[SerializeField] private float weaponAnimationWeight = 1f;
	[SerializeField] private Vector3 weaponLookRotationAmount = new Vector3(1.1f, 2f, 1.35f);
	[SerializeField] private Vector3 weaponMoveOffsetAmount = new Vector3(0.035f, 0.018f, 0.025f);
	[SerializeField] private float weaponRotationLerpSpeed = 12f;
	[SerializeField] private float weaponMoveLerpSpeed = 10f;

	[Header("Attack")]
	[SerializeField, Range(0.1f, 5f)] private float attackComboWindowSeconds = 0.82f;
	[SerializeField, Range(0f, 5f)] private float attackEarliestInterruptTime = 0.2f;
	[SerializeField] private string attackStateName1 = "Attack 1 R";
	[SerializeField] private string attackStateName2 = "Attack 2 R";
	[SerializeField, Range(0f, 1f)] private float attack1ImpactNormalizedTime = 0.42f;
	[SerializeField, Range(0f, 1f)] private float attack2ImpactNormalizedTime = 0.42f;

	[Header("Death Presentation")]
	[SerializeField] private float deathCameraHeight = 0.1f;
	[SerializeField] private float deathCameraPitch = -70f;
	[SerializeField] private float deathPresentationBlendSpeed = 4f;
	[SerializeField] private string deathStateName = "Death";

	private static readonly int IsLeftWallingHash = Animator.StringToHash("isLeftWalling");
	private static readonly int IsRightWallingHash = Animator.StringToHash("isRightWalling");
	private static readonly int IsSlidingHash = Animator.StringToHash("isSliding");
	private static readonly int IsDeadHash = Animator.StringToHash("isDead");
	private static readonly int AttackNumHash = Animator.StringToHash("attackNum");

	private InputAction attackAction;
	private Vector3 cameraRootBaseLocalPosition;
	private Quaternion weaponPivotBaseLocalRotation;
	private Transform weaponMotionTarget;
	private Vector3 weaponModelBaseLocalPosition;
	private Quaternion weaponMotionBaseLocalRotation;
	private float currentCameraHeight;
	private float currentTilt;
	private float bobTime;
	private Vector3 currentCameraOffset;
	private Vector3 currentCameraRotationOffset;
	private Vector3 currentWeaponOffset;
	private Vector3 currentWeaponRotation;
	private bool attackActive;
	private bool attackStateEntered;
	private int numAttack;
	private int currentAttackNumber;
	private int attackIntentNumber;
	private float currentAttackStartTime;
	private int attackSequenceId;
	private bool attackInputLocked;
	private bool deathPresentationActive;
	private bool weaponAnimatorUpdateModeOverridden;
	private AnimatorUpdateMode previousWeaponAnimatorUpdateMode = AnimatorUpdateMode.Normal;

	public event Action<int, int> AttackStateEnteredEvent;
	public bool IsAttackActive => attackActive;
	public int CurrentAttackNumber => currentAttackNumber;
	public int AttackSequenceId => attackSequenceId;

	private void Awake()
	{
		AutoAssignReferences();
		CacheBasePose();
		BindAttackAction();
	}

	private void OnEnable()
	{
		AutoAssignReferences();
		CacheBasePose();

		if (controller != null)
		{
			controller.UseExternalViewAnimation = true;
		}

		attackAction?.Enable();
	}

	private void OnDisable()
	{
		SetWeaponAnimatorUsesUnscaledTime(false);
		attackAction?.Disable();

		if (controller != null)
		{
			controller.UseExternalViewAnimation = false;
		}

		ResetViewState();
	}

	private void LateUpdate()
	{
		if (controller == null)
		{
			return;
		}

		controller.UseExternalViewAnimation = true;

		if (deathPresentationActive)
		{
			UpdateDeathPresentation();
			UpdateWeaponAnimatorState();
			return;
		}

		UpdateCameraAnimation();
		UpdateWeaponAnimatorState();
		HandleAttackInput();
		UpdateAttackState();
		UpdateWeaponMotion();
	}

	private void AutoAssignReferences()
	{
		if (controller == null)
		{
			controller = GetComponent<FirstPersonController>();
		}

		if (playerCamera == null)
		{
			playerCamera = controller != null ? controller.PlayerCamera : GetComponentInChildren<Camera>();
		}

		if (cameraRoot == null)
		{
			cameraRoot = controller != null ? controller.CameraRoot : null;
		}

		if (cameraRoot != null && weaponPivot == null)
		{
			weaponPivot = cameraRoot.Find("Weapon Camera");
		}

		if (weaponPivot != null && weaponCamera == null)
		{
			weaponCamera = weaponPivot.GetComponent<Camera>();
		}

		if (weaponPivot != null && weaponModel == null && weaponPivot.childCount > 0)
		{
			weaponModel = weaponPivot.GetChild(0);
		}

		if (weaponModel != null && weaponAnimator == null)
		{
			weaponAnimator = weaponModel.GetComponent<Animator>();

			if (weaponAnimator == null)
			{
				weaponAnimator = weaponModel.GetComponentInParent<Animator>();
			}
		}

		ResolveWeaponMotionTarget();

		if (inputActions == null && controller != null)
		{
			inputActions = controller.InputActions;
		}

		if (string.IsNullOrWhiteSpace(actionMapName) && controller != null)
		{
			actionMapName = controller.ActionMapName;
		}
	}

	private void CacheBasePose()
	{
		ResolveWeaponMotionTarget();

		if (cameraRoot != null)
		{
			cameraRootBaseLocalPosition = cameraRoot.localPosition;
			currentCameraHeight = cameraRoot.localPosition.y;
		}

		if (weaponPivot != null)
		{
			weaponPivotBaseLocalRotation = weaponPivot.localRotation;
		}

		if (weaponMotionTarget != null)
		{
			weaponModelBaseLocalPosition = weaponMotionTarget.localPosition;
			weaponMotionBaseLocalRotation = weaponMotionTarget.localRotation;
		}

		if (playerCamera != null && baseFieldOfView <= 0f)
		{
			baseFieldOfView = playerCamera.fieldOfView;
		}
	}

	private void ResolveWeaponMotionTarget()
	{
		weaponMotionTarget = weaponAnimator != null ? weaponAnimator.transform : weaponModel;
	}

	private void BindAttackAction()
	{
		attackAction = null;

		if (inputActions == null)
		{
			return;
		}

		InputActionMap actionMap = inputActions.FindActionMap(actionMapName, false);
		if (actionMap == null)
		{
			return;
		}

		attackAction = actionMap.FindAction(attackActionName, false);
	}

	private void UpdateCameraAnimation()
	{
		if (cameraRoot == null || playerCamera == null)
		{
			return;
		}

		float deltaTime = Time.deltaTime;
		float targetFov = baseFieldOfView;

		if (controller.IsSliding)
		{
			targetFov = slideFieldOfView;
		}
		else if (controller.IsWallRunning || controller.IsSprinting)
		{
			targetFov = sprintFieldOfView;
		}

		playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFov, fieldOfViewLerpSpeed * deltaTime);

		if (weaponCamera != null)
		{
			weaponCamera.fieldOfView = playerCamera.fieldOfView;
		}

		float targetTilt = 0f;
		if (controller.IsWallRunning)
		{
			targetTilt = controller.IsLeftWalling ? -wallRunTilt : wallRunTilt;
		}

		currentTilt = Mathf.Lerp(currentTilt, targetTilt, tiltLerpSpeed * deltaTime);

		BobSettings bobSettings = GetActiveBobSettings();
		float speedFactor = GetNormalizedSpeed();
		bool shouldBob = controller.IsWallRunning || (controller.IsGrounded && !controller.IsSliding && controller.PlanarSpeed > 0.05f);

		if (shouldBob)
		{
			bobTime += deltaTime * bobSettings.frequency * Mathf.Lerp(0.35f, 1f, speedFactor);
		}

		Vector3 targetCameraOffset = Vector3.zero;
		Vector3 targetCameraRotationOffset = Vector3.zero;

		if (shouldBob)
		{
			float bobSin = Mathf.Sin(bobTime);
			float bobCos = Mathf.Cos(bobTime * 0.5f);
			targetCameraOffset = new Vector3(
				bobCos * bobSettings.horizontalAmplitude,
				Mathf.Abs(bobSin) * bobSettings.verticalAmplitude,
				0f) * speedFactor;

			targetCameraRotationOffset = new Vector3(
				Mathf.Abs(bobSin) * bobSettings.pitchAmplitude,
				0f,
				bobCos * bobSettings.rollAmplitude) * speedFactor;
		}

		GetSlideShake(out Vector3 slidePositionOffset, out Vector3 slideRotationOffset);
		GetAirborneSpeedShake(out Vector3 airbornePositionOffset, out Vector3 airborneRotationOffset);
		currentCameraOffset = Vector3.Lerp(currentCameraOffset, targetCameraOffset + slidePositionOffset + airbornePositionOffset, bobBlendSpeed * deltaTime);
		currentCameraRotationOffset = Vector3.Lerp(currentCameraRotationOffset, targetCameraRotationOffset + slideRotationOffset + airborneRotationOffset, bobBlendSpeed * deltaTime);

		float targetHeight = controller.IsCrouching ? controller.CrouchingCameraHeight : controller.StandingCameraHeight;
		currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetHeight, controller.StanceLerpSpeed * deltaTime);

		Vector3 basePosition = new Vector3(cameraRootBaseLocalPosition.x, currentCameraHeight, cameraRootBaseLocalPosition.z);
		cameraRoot.localPosition = basePosition + currentCameraOffset;
		cameraRoot.localRotation = Quaternion.Euler(
			controller.Pitch + currentCameraRotationOffset.x,
			currentCameraRotationOffset.y,
			currentTilt + currentCameraRotationOffset.z);
	}

	private BobSettings GetActiveBobSettings()
	{
		if (controller.IsWallRunning)
		{
			return wallRunBob;
		}

		if (controller.IsCrouching)
		{
			return crouchBob;
		}

		if (controller.IsSprinting)
		{
			return sprintBob;
		}

		return walkBob;
	}

	private float GetNormalizedSpeed()
	{
		float maxSpeed = GetReferenceSpeed();
		return maxSpeed > 0.01f ? Mathf.Clamp01(controller.PlanarSpeed / maxSpeed) : 0f;
	}

	private float GetReferenceSpeed()
	{
		float maxSpeed = controller.WalkSpeed;

		if (controller.IsWallRunning)
		{
			maxSpeed = controller.WallRunSpeed;
		}
		else if (controller.IsSliding)
		{
			maxSpeed = Mathf.Max(controller.SprintSpeed, controller.WalkSpeed);
		}
		else if (controller.IsCrouching)
		{
			maxSpeed = controller.CrouchSpeed;
		}
		else if (controller.IsSprinting)
		{
			maxSpeed = controller.SprintSpeed;
		}

		return maxSpeed;
	}

	private void GetSlideShake(out Vector3 positionOffset, out Vector3 rotationOffset)
	{
		positionOffset = Vector3.zero;
		rotationOffset = Vector3.zero;

		if (!controller.IsSliding)
		{
			return;
		}

		float decay = 1f - controller.SlideProgress01;
		float noiseTime = Time.time * slideShakeFrequency;
		float noiseX = (Mathf.PerlinNoise(noiseTime, 0.13f) - 0.5f) * 2f;
		float noiseY = (Mathf.PerlinNoise(0.37f, noiseTime) - 0.5f) * 2f;
		float noiseZ = (Mathf.PerlinNoise(noiseTime, 0.73f) - 0.5f) * 2f;

		positionOffset = new Vector3(noiseX, noiseY, 0f) * (slideShakePositionAmplitude * decay);
		rotationOffset = new Vector3(noiseY, noiseZ * 0.4f, noiseX) * (slideShakeRotationAmplitude * decay);
	}

	private void GetAirborneSpeedShake(out Vector3 positionOffset, out Vector3 rotationOffset)
	{
		positionOffset = Vector3.zero;
		rotationOffset = Vector3.zero;

		if (controller.IsGrounded || controller.IsSliding || controller.IsWallRunning)
		{
			return;
		}

		float speedExcess = controller.TotalSpeed - airborneShakeSpeedThreshold;
		if (speedExcess <= 0f)
		{
			return;
		}

		float strength = speedExcess / Mathf.Max(0.01f, airborneShakeSpeedRange);
		float noiseTime = Time.time * airborneShakeFrequency;
		float noiseX = (Mathf.PerlinNoise(noiseTime, 0.19f) - 0.5f) * 2f;
		float noiseY = (Mathf.PerlinNoise(0.41f, noiseTime) - 0.5f) * 2f;
		float noiseZ = (Mathf.PerlinNoise(noiseTime, 0.79f) - 0.5f) * 2f;

		positionOffset = new Vector3(noiseX, noiseY, 0f) * (airborneShakePositionAmplitude * strength);
		rotationOffset = new Vector3(noiseY, noiseZ * 0.4f, noiseX) * (airborneShakeRotationAmplitude * strength);
	}

	private void UpdateWeaponAnimatorState()
	{
		if (weaponAnimator == null)
		{
			return;
		}

		weaponAnimator.SetBool(IsLeftWallingHash, controller.IsLeftWalling);
		weaponAnimator.SetBool(IsRightWallingHash, controller.IsRightWalling);
		weaponAnimator.SetBool(IsSlidingHash, controller.IsSliding);
		weaponAnimator.SetBool(IsDeadHash, deathPresentationActive);
	}

	private void HandleAttackInput()
	{
		if (attackInputLocked || weaponAnimator == null || attackAction == null || !attackAction.WasPressedThisFrame())
		{
			return;
		}

		if (!attackActive)
		{
			StartAttack(1);
			return;
		}

		BufferAttackIntent();
	}

	public void SetAttackInputLocked(bool locked, bool clearQueuedInput = true)
	{
		attackInputLocked = locked;

		if (locked)
		{
			ClearAttackState(clearQueuedInput);
		}
	}

	private bool hasForcedDeathAnim = false;
	public void SetDeathPresentationActive(bool active)
	{
		// 只在从未死亡->死亡的那一帧强制切入死亡动画
		bool wasDead = deathPresentationActive;
		deathPresentationActive = active;
		SetWeaponAnimatorUsesUnscaledTime(deathPresentationActive);

		if (weaponAnimator != null)
		{
			weaponAnimator.SetBool(IsDeadHash, deathPresentationActive);
		}

		if (deathPresentationActive)
		{
			ClearAttackState(clearQueuedInput: true);
			currentCameraOffset = Vector3.zero;
			currentCameraRotationOffset = Vector3.zero;
			currentWeaponOffset = Vector3.zero;
			currentWeaponRotation = Vector3.zero;
			currentTilt = 0f;

			if (weaponMotionTarget != null)
			{
				weaponMotionTarget.localPosition = weaponModelBaseLocalPosition;
				weaponMotionTarget.localRotation = weaponMotionBaseLocalRotation;
			}

			if (!wasDead && !hasForcedDeathAnim)
			{
				ForceDeathAnimationImmediate();
				hasForcedDeathAnim = true;
			}
		}
		else
		{
			hasForcedDeathAnim = false;
		}
	}

	private void ForceDeathAnimationImmediate()
	{
		if (weaponAnimator == null || string.IsNullOrWhiteSpace(deathStateName))
		{
			return;
		}

		weaponAnimator.Play(deathStateName, 0, 0f);
		weaponAnimator.Update(0f);
	}

	private void UpdateDeathPresentation()
	{
		if (cameraRoot == null)
		{
			return;
		}

		float deltaTime = Time.unscaledDeltaTime;
		float blend = Mathf.Max(0f, deathPresentationBlendSpeed) * deltaTime;

		if (playerCamera != null)
		{
			playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, baseFieldOfView, fieldOfViewLerpSpeed * deltaTime);
		}

		if (weaponCamera != null && playerCamera != null)
		{
			weaponCamera.fieldOfView = playerCamera.fieldOfView;
		}

		currentCameraOffset = Vector3.zero;
		currentCameraRotationOffset = Vector3.zero;
		currentWeaponOffset = Vector3.zero;
		currentWeaponRotation = Vector3.zero;
		currentTilt = 0f;
		currentCameraHeight = Mathf.Lerp(currentCameraHeight, deathCameraHeight, blend);

		cameraRoot.localPosition = new Vector3(cameraRootBaseLocalPosition.x, currentCameraHeight, cameraRootBaseLocalPosition.z);
		cameraRoot.localRotation = Quaternion.Slerp(cameraRoot.localRotation, Quaternion.Euler(deathCameraPitch, 0f, 0f), blend);
	}

	private void StartAttack(int attackNumber)
	{
		if (weaponAnimator == null)
		{
			return;
		}


        SetAttackNumber(attackNumber);
		attackActive = true;
		attackStateEntered = false;
		currentAttackNumber = 0;
		attackIntentNumber = 0;
		currentAttackStartTime = Time.time;
	}

	private void BufferAttackIntent()
	{
		int comboSourceAttack = currentAttackNumber != 0 ? currentAttackNumber : numAttack;
		if (comboSourceAttack == 0)
		{
			return;
		}

		if (currentAttackNumber != 0)
		{
			float elapsedTime = Time.time - currentAttackStartTime;
			if (elapsedTime > attackComboWindowSeconds)
			{
				return;
			}
		}

		attackIntentNumber = comboSourceAttack == 1 ? 2 : 1;
	}

	private void SetAttackNumber(int attackNumber)
	{
		numAttack = attackNumber;
		weaponAnimator.SetInteger(AttackNumHash, numAttack);
	}

	private int GetAttackNumber(AnimatorStateInfo stateInfo)
	{
		if (stateInfo.IsName(attackStateName1))
		{
			return 1;
		}

		if (stateInfo.IsName(attackStateName2))
		{
			return 2;
		}

		return 0;
	}

	private int GetActiveAttackNumber()
	{
		AnimatorStateInfo currentState = weaponAnimator.GetCurrentAnimatorStateInfo(0);
		int attackNumber = GetAttackNumber(currentState);

		if (attackNumber == 0 && weaponAnimator.IsInTransition(0))
		{
			AnimatorStateInfo nextState = weaponAnimator.GetNextAnimatorStateInfo(0);
			attackNumber = GetAttackNumber(nextState);
		}

		return attackNumber;
	}

	private void UpdateAttackState()
	{
		if (weaponAnimator == null || !attackActive)
		{
			return;
		}

		int activeAttackNumber = GetActiveAttackNumber();

		if (activeAttackNumber != 0)
		{
			if (!attackStateEntered || currentAttackNumber != activeAttackNumber)
			{
				attackStateEntered = true;
				currentAttackNumber = activeAttackNumber;
				currentAttackStartTime = Time.time;
				attackSequenceId++;

				if (attackIntentNumber == currentAttackNumber)
				{
					attackIntentNumber = 0;
				}
                if (PlayerAudioController.Instance != null)
                {
                    PlayerAudioController.Instance.PlaySwingSound();
                }
                AttackStateEnteredEvent?.Invoke(currentAttackNumber, attackSequenceId);
			}

			float elapsedTime = Time.time - currentAttackStartTime;

			if (attackIntentNumber != 0 && elapsedTime >= attackEarliestInterruptTime)
			{
				SetAttackNumber(attackIntentNumber);
				attackIntentNumber = 0;
				return;
			}

			if (attackIntentNumber == 0 && numAttack == currentAttackNumber && elapsedTime >= attackComboWindowSeconds)
			{
				SetAttackNumber(0);
			}

			return;
		}

		if (attackStateEntered)
		{
			attackActive = false;
			attackStateEntered = false;
			currentAttackNumber = 0;
			attackIntentNumber = 0;
			currentAttackStartTime = 0f;

			if (numAttack != 0)
			{
				SetAttackNumber(0);
			}
		}
	}

	public bool TrySnapCurrentAttackToImpactFrame(int attackNumber)
	{
		if (weaponAnimator == null || !attackActive || attackNumber <= 0)
		{
			return false;
		}

		int activeAttackNumber = GetActiveAttackNumber();
		if (activeAttackNumber != attackNumber && currentAttackNumber != attackNumber)
		{
			return false;
		}

		string stateName = GetAttackStateName(attackNumber);
		if (string.IsNullOrEmpty(stateName))
		{
			return false;
		}

		weaponAnimator.Play(stateName, 0, GetImpactNormalizedTime(attackNumber));
		weaponAnimator.Update(0f);
		currentAttackNumber = attackNumber;
		return true;
	}

	public void ClearAttackState(bool clearQueuedInput = true)
	{
		attackActive = false;
		attackStateEntered = false;
		numAttack = 0;
		currentAttackNumber = 0;
		attackIntentNumber = 0;
		currentAttackStartTime = 0f;

		if (weaponAnimator != null)
		{
			SetAttackNumber(0);
			weaponAnimator.Update(0f);
		}

		if (clearQueuedInput && attackAction != null && attackAction.enabled)
		{
			attackAction.Disable();
			attackAction.Enable();
		}
	}

	public void SetWeaponAnimatorUsesUnscaledTime(bool useUnscaledTime)
	{
		if (weaponAnimator == null)
		{
			return;
		}

		if (useUnscaledTime)
		{
			if (!weaponAnimatorUpdateModeOverridden)
			{
				previousWeaponAnimatorUpdateMode = weaponAnimator.updateMode;
				weaponAnimatorUpdateModeOverridden = true;
			}

			weaponAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
			return;
		}

		if (!weaponAnimatorUpdateModeOverridden)
		{
			return;
		}

		weaponAnimator.updateMode = previousWeaponAnimatorUpdateMode;
		weaponAnimatorUpdateModeOverridden = false;
	}

	private string GetAttackStateName(int attackNumber)
	{
		switch (attackNumber)
		{
			case 1:
				return attackStateName1;
			case 2:
				return attackStateName2;
			default:
				return string.Empty;
		}
	}

	private float GetImpactNormalizedTime(int attackNumber)
	{
		return attackNumber == 2 ? attack2ImpactNormalizedTime : attack1ImpactNormalizedTime;
	}

	private void UpdateWeaponMotion()
	{
		float deltaTime = Time.deltaTime;
		float weight = Mathf.Max(0f, weaponAnimationWeight);
		Vector2 lookInput = controller.LookInput;
		float speedFactor = GetNormalizedSpeed();
		float referenceSpeed = Mathf.Max(GetReferenceSpeed(), 0.01f);
		Vector3 localPlanarVelocity = transform.InverseTransformDirection(controller.PlanarVelocity);
		Vector2 localVelocity01 = new Vector2(
			Mathf.Clamp(localPlanarVelocity.x / referenceSpeed, -1f, 1f),
			Mathf.Clamp(localPlanarVelocity.z / referenceSpeed, -1f, 1f));

		if (weaponMotionTarget != null)
		{
			Vector3 targetRotation = new Vector3(
				-lookInput.y * weaponLookRotationAmount.x,
				lookInput.x * weaponLookRotationAmount.y,
				-lookInput.x * weaponLookRotationAmount.z) * weight;

			currentWeaponRotation = Vector3.Lerp(currentWeaponRotation, targetRotation, weaponRotationLerpSpeed * deltaTime);
			float bobLift = controller.IsGrounded && !controller.IsSliding ? Mathf.Abs(Mathf.Sin(bobTime)) * weaponMoveOffsetAmount.y * speedFactor : 0f;
			Vector3 targetOffset = new Vector3(
				-localVelocity01.x * weaponMoveOffsetAmount.x,
				bobLift,
				-localVelocity01.y * weaponMoveOffsetAmount.z) * weight;

			currentWeaponOffset = Vector3.Lerp(currentWeaponOffset, targetOffset, weaponMoveLerpSpeed * deltaTime);
			weaponMotionTarget.localPosition = weaponModelBaseLocalPosition + currentWeaponOffset;
			weaponMotionTarget.localRotation = weaponMotionBaseLocalRotation * Quaternion.Euler(currentWeaponRotation);
		}
	}

	public void ResetViewState()
	{
		SetWeaponAnimatorUsesUnscaledTime(false);
		deathPresentationActive = false;
		currentTilt = 0f;
		currentCameraOffset = Vector3.zero;
		currentCameraRotationOffset = Vector3.zero;
		currentWeaponOffset = Vector3.zero;
		currentWeaponRotation = Vector3.zero;
		ClearAttackState(clearQueuedInput: false);

		if (cameraRoot != null)
		{
			cameraRoot.localPosition = cameraRootBaseLocalPosition;
			cameraRoot.localRotation = Quaternion.Euler(controller != null ? controller.Pitch : 0f, 0f, 0f);
		}

		if (weaponPivot != null)
		{
			weaponPivot.localRotation = weaponPivotBaseLocalRotation;
		}

		if (weaponMotionTarget != null)
		{
			weaponMotionTarget.localPosition = weaponModelBaseLocalPosition;
			weaponMotionTarget.localRotation = weaponMotionBaseLocalRotation;
		}

		if (playerCamera != null)
		{
			playerCamera.fieldOfView = baseFieldOfView;
		}

		if (weaponCamera != null)
		{
			weaponCamera.fieldOfView = baseFieldOfView;
		}

		if (weaponAnimator != null)
		{
			weaponAnimator.SetBool(IsLeftWallingHash, false);
			weaponAnimator.SetBool(IsRightWallingHash, false);
			weaponAnimator.SetBool(IsSlidingHash, false);
			weaponAnimator.SetBool(IsDeadHash, false);
		}
	}
}