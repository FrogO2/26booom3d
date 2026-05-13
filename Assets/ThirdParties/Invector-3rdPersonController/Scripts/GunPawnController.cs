using UnityEngine;
using Invector;
using Invector.IK;
using Invector.vCharacterController;
using Invector.vShooter;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public class GunPawnController : MonoBehaviour
{
    enum AimReferenceAxis
    {
        ForwardZ,
        RightX,
        UpY
    }

    [Header("References")]
    [SerializeField] Transform player;
    [SerializeField] vShooterWeapon weapon;
    [SerializeField] EnemyEffect enemyEffect;

    [Header("Detection")]
    [SerializeField] float detectionRange = 20f;
    [SerializeField] float fieldOfView = 150f;
    [SerializeField] string playerTag = "Player";

    [Header("Shooting")]
    // Note: set weapon.isInfinityAmmo = true in the Inspector for unlimited ammo
    [SerializeField, FormerlySerializedAs("shootInterval"), Min(0.01f)] float continuousFireInterval = 2f;
    [SerializeField, Min(0f)] float fallbackAimReadyDelay = 0.2f;
    [SerializeField] bool aimAtTargetTransform = true;
    [SerializeField] float aimHeightOffset = 1.4f;
	[SerializeField, Min(0.01f)] float projectileSpeedMultiplier = 1f;

    [Header("Rotation")]
    [SerializeField] float turnSpeed = 5f;

    [Header("IK")]
    [SerializeField] float ikSmoothIn = 5f;
    [SerializeField] float ikSmoothOut = 10f;
    [SerializeField] float onlyArmsLayerSpeed = 25f;
    [SerializeField] float armAlignmentWeightSmooth = 24f;
    [SerializeField, Range(0f, 1f)] float lookAtBodyWeight = 0.35f;
    [SerializeField, Range(0f, 1f)] float lookAtHeadWeight = 0.85f;
    [SerializeField, Range(0f, 1f)] float lookAtEyesWeight = 0f;
    [SerializeField, Range(0f, 1f)] float lookAtClampWeight = 0.5f;
    [SerializeField] bool useHeadTrack = true;
    [SerializeField] bool manualHeadTrackUpdate = true;
    [SerializeField] bool ignoreHeadTrackAngleLimit = true;
    [SerializeField] float armAimRotationSmooth = 30f;
    [SerializeField] float maxVerticalArmAimAngle = 60f;
    [SerializeField] float maxHorizontalArmAimAngle = 20f;
    [SerializeField, Min(0.5f)] float armAlignmentMinDistance = 8f;
    [SerializeField] bool smoothArmAlignmentPoint = true;
    [SerializeField] AimReferenceAxis aimReferenceAxis = AimReferenceAxis.RightX;
    [SerializeField] vWeaponIKAdjustList weaponIKAdjustList;
    [SerializeField] bool useRightHandIKSolver = true;
    [SerializeField] float rightHandIKWeight = 1f;
    [SerializeField] float rightHandIKSmooth = 20f;
    [SerializeField] bool useLeftHandIK = true;
    [SerializeField] bool useLeftHandIKSolver = true;
    [SerializeField] float leftHandIKWeight = 1f;
    [SerializeField] float leftHandIKSmoothIn = 10f;
    [SerializeField] float leftHandIKSmoothOut = 25f;
    [SerializeField] bool forceLeftHandIKOffset = true;
    [SerializeField] Vector3 leftHandIKLocalPosition = new Vector3(0.019f, -0.072f, -0.001f);
    [SerializeField] Vector3 leftHandIKLocalEuler = new Vector3(342.311188f, 183.349838f, 169.321396f);

    Animator animator;
    bool isDead;
    bool playerDetected;
    float shootTimer;
    float currentIKWeight;
    float onlyArmsLayerWeight;
    float armAlignmentWeight;
    float aimReadyTimer;
    float rightHandIKCurrentWeight;
    float leftHandIKCurrentWeight;
    string shotTriggerName;
    vArmAimAlign rightArmAim;
    vIKSolver rightHandIKSolver;
    vIKSolver leftHandIKSolver;
    vHeadTrack headTrack;
    vThirdPersonInput thirdPersonInput;
    int onlyArmsLayer = -1;
    int shotLayer = -1;
    int upperBodyLayer = -1;
    bool hasCanAimParameter;

    static readonly int HashMoveSetID     = Animator.StringToHash("MoveSet_ID");
    static readonly int HashUpperBodyID   = Animator.StringToHash("UpperBody_ID");
    static readonly int HashShotID        = Animator.StringToHash("Shot_ID");
    static readonly int HashCanAim        = Animator.StringToHash("CanAim");
    static readonly int HashIsAiming      = Animator.StringToHash("IsAiming");
    static readonly int HashIsDead        = Animator.StringToHash("isDead");
    static readonly int HashInputMagnitude = Animator.StringToHash("InputMagnitude");
    static readonly int HashIsGrounded    = Animator.StringToHash("IsGrounded");
    static readonly int HashInputHorizontal = Animator.StringToHash("InputHorizontal");

    void Awake()
    {
        animator = GetComponent<Animator>();
        headTrack = GetComponent<vHeadTrack>();
        thirdPersonInput = GetComponent<vThirdPersonInput>();
        onlyArmsLayer = animator.GetLayerIndex("OnlyArms");
        shotLayer = animator.GetLayerIndex("Shot");
        upperBodyLayer = animator.GetLayerIndex("UpperBody");
        hasCanAimParameter = HasBoolParameter("CanAim");

        // Invector variants may use different shot trigger names depending on controller version.
        shotTriggerName = ResolveShotTriggerName();

        if (headTrack != null)
        {
            headTrack.followCamera = false;
            headTrack.alwaysFollowCamera = false;
            if (ignoreHeadTrackAngleLimit)
            {
                headTrack.cancelTrackOutOfAngle = false;
            }
        }
    }

    void Start()
    {
        EnsureAimAngleReference();
        TryResolvePlayer();

        if (weapon != null)
        {
			ApplyWeaponRuntimeSettings();
            animator.SetFloat(HashMoveSetID,   weapon.moveSetID);
            animator.SetFloat(HashUpperBodyID,  weapon.upperBodyID);
            animator.SetFloat(HashShotID,       weapon.shotID);
        }

        animator.SetBool(HashIsGrounded,    true);
        animator.SetFloat(HashInputMagnitude, 0f);
        animator.SetFloat(HashInputHorizontal, 0f);
        animator.SetBool(HashIsAiming,      false);
        if (hasCanAimParameter)
        {
            animator.SetBool(HashCanAim, false);
        }

        if (onlyArmsLayer >= 0)
        {
            animator.SetLayerWeight(onlyArmsLayer, 0f);
        }

        if (shotLayer >= 0)
        {
            animator.SetLayerWeight(shotLayer, 1f);
        }

        if (weapon != null)
        {
            weapon.SetActiveAim(false);
            weapon.SetActiveScope(false);
        }

        // Start ready to shoot
        shootTimer = continuousFireInterval;
    }

    void Update()
    {
        if (isDead) return;

        UpdateDetection();

        if (playerDetected)
        {
            FacePlayer();
            UpdateShootingTimer();
            aimReadyTimer += Time.deltaTime;
        }
        else
        {
            animator.SetFloat(HashInputHorizontal, 0f, 0.1f, Time.deltaTime);
            aimReadyTimer = 0f;
        }

        // Smooth IK weight
        float targetWeight = playerDetected ? 1f : 0f;
        float smoothSpeed = targetWeight > currentIKWeight ? ikSmoothIn : ikSmoothOut;
        currentIKWeight = Mathf.MoveTowards(currentIKWeight, targetWeight, smoothSpeed * Time.deltaTime);
    }

    void LateUpdate()
    {
        if (isDead || weapon == null)
        {
            return;
        }

        UpdateShooterAnimationState();
        UpdateAimRig();
        TryShoot();
    }

    void UpdateDetection()
    {
        if (player == null)
        {
            TryResolvePlayer();
            playerDetected = false;
            return;
        }

        Vector3 toPlayer = player.position - transform.position;
        if (toPlayer.sqrMagnitude > detectionRange * detectionRange)
        {
            playerDetected = false;
            return;
        }

        float angle = Vector3.Angle(transform.forward, toPlayer);
        playerDetected = angle <= fieldOfView * 0.5f;
    }

    void TryResolvePlayer()
    {
        if (player != null || string.IsNullOrWhiteSpace(playerTag))
        {
            return;
        }

        GameObject playerObject = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    public void SetPlayer(Transform target)
    {
        player = target;
    }

    void FacePlayer()
    {
        Vector3 dir = player.position - transform.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) return;

        // Drive turn animation blend
        float signedAngle = Vector3.SignedAngle(transform.forward, dir.normalized, Vector3.up);
        float turnBlend = Mathf.Clamp(signedAngle / 90f, -1f, 1f);
        animator.SetFloat(HashInputHorizontal, turnBlend, 0.1f, Time.deltaTime);

        // Rotate body toward player
        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
    }

    void UpdateShootingTimer()
    {
        shootTimer -= Time.deltaTime;
    }

    void Fire()
    {
        if (weapon == null) return;

		ApplyWeaponRuntimeSettings();

        Vector3 aimTarget = GetAimTargetPosition();
        weapon.Shoot(aimTarget, transform);

        if (!string.IsNullOrEmpty(shotTriggerName))
        {
            animator.SetTrigger(shotTriggerName);
        }
    }

    void ApplyWeaponRuntimeSettings()
    {
        if (weapon == null)
        {
            return;
        }

        weapon.shootFrequency = continuousFireInterval;
        weapon.velocityMultiplierMod = projectileSpeedMultiplier - 1f;
    }

    void UpdateShooterAnimationState()
    {
        ApplyWeaponRuntimeSettings();

        animator.SetFloat(HashMoveSetID, weapon.moveSetID, 0.1f, Time.deltaTime);
        animator.SetFloat(HashUpperBodyID, weapon.upperBodyID, 0.1f, Time.deltaTime);
        animator.SetFloat(HashShotID, weapon.shotID, 0.1f, Time.deltaTime);
        animator.SetBool(HashIsAiming, playerDetected);
        if (hasCanAimParameter)
        {
            animator.SetBool(HashCanAim, playerDetected);
        }

        onlyArmsLayerWeight = Mathf.Lerp(onlyArmsLayerWeight, playerDetected ? 1f : 0f, onlyArmsLayerSpeed * Time.deltaTime);
        if (onlyArmsLayer >= 0)
        {
            animator.SetLayerWeight(onlyArmsLayer, onlyArmsLayerWeight);
        }

        if (shotLayer >= 0)
        {
            animator.SetLayerWeight(shotLayer, 1f);
        }

        weapon.SetActiveAim(playerDetected);
        weapon.SetActiveScope(false);
    }

    void UpdateAimRig()
    {
        if (player == null)
        {
            armAlignmentWeight = 0f;
            return;
        }

        if (useHeadTrack && headTrack != null)
        {
            if (playerDetected)
            {
                headTrack.SetLookTarget(player);
            }
            else
            {
                headTrack.RemoveLookTarget(player);
            }

            if (manualHeadTrackUpdate || thirdPersonInput == null)
            {
                headTrack.UpdateHeadTrack();
            }
        }

        if (!playerDetected)
        {
            armAlignmentWeight = 0f;
            return;
        }

        Vector3 aimTarget = GetAimTargetPosition();
        Transform aimRef = weapon.aimReference != null ? weapon.aimReference : weapon.transform;
        Vector3 armAlignmentTarget = GetArmAlignmentTargetPosition(aimRef, aimTarget);

        UpdateArmAlignmentWeight();
        UpdateRightHandIK();
        AlignWeaponArmToAim(aimRef, armAlignmentTarget, armAlignmentWeight);
        UpdateLeftHandIK();
    }

    void UpdateArmAlignmentWeight()
    {
        float targetWeight = 0f;
        if (CanRotateAimArm())
        {
            targetWeight = Mathf.Clamp01(GetUpperBodyStateInfo().normalizedTime);
        }
        else if (HasFallbackAimReadiness())
        {
            targetWeight = 1f;
        }

        armAlignmentWeight = Mathf.Lerp(armAlignmentWeight, targetWeight, armAlignmentWeightSmooth * Time.deltaTime);
        armAlignmentWeight = Mathf.Min(armAlignmentWeight, currentIKWeight);
    }

    void TryShoot()
    {
        if (!playerDetected || shootTimer > 0f || !CanShoot())
        {
            return;
        }

        Fire();
        shootTimer = continuousFireInterval;
    }

    bool CanShoot()
    {
        if (weapon == null || isDead)
        {
            return false;
        }

        if (upperBodyLayer < 0)
        {
            return armAlignmentWeight >= 0.5f && currentIKWeight >= 0.5f;
        }

        return armAlignmentWeight >= 0.5f && (GetUpperBodyStateInfo().IsTag("Upperbody Pose") || HasFallbackAimReadiness());
    }

    bool CanRotateAimArm()
    {
        if (upperBodyLayer < 0)
        {
            return playerDetected;
        }

        AnimatorStateInfo stateInfo = GetUpperBodyStateInfo();
        return stateInfo.IsTag("Upperbody Pose") && stateInfo.normalizedTime > 0.5f;
    }

    bool HasFallbackAimReadiness()
    {
        return playerDetected && currentIKWeight >= 0.5f && aimReadyTimer >= fallbackAimReadyDelay;
    }

    AnimatorStateInfo GetUpperBodyStateInfo()
    {
        if (upperBodyLayer >= 0)
        {
            return animator.GetCurrentAnimatorStateInfo(upperBodyLayer);
        }

        return animator.GetCurrentAnimatorStateInfo(0);
    }

    void OnAnimatorIK(int layerIndex)
    {
        ResetAnimatorAimIK();

        if (isDead || weapon == null || player == null) return;

        if (headTrack == null || !useHeadTrack)
        {
            ApplyUpperBodyLookAt();
        }

        if (currentIKWeight < 0.01f) return;

        if (!useLeftHandIKSolver && useLeftHandIK)
        {
            ApplyLeftHandIKOffset();

            Transform leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform leftHandTarget = weapon.handIKTargetOffset != null ? weapon.handIKTargetOffset : weapon.handIKTarget;
            if (leftHandBone != null && leftHandTarget != null)
            {
                float supportWeight = Mathf.Clamp01(leftHandIKCurrentWeight);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, supportWeight);
                animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget.position);
                animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, supportWeight);
                animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandTarget.rotation);
            }
        }
    }

    Vector3 GetAimTargetPosition()
    {
        if (player == null)
        {
            return transform.position + transform.forward;
        }

        return ResolveAimTargetPosition(player);
    }

    Vector3 GetArmAlignmentTargetPosition(Transform aimRef, Vector3 aimTarget)
    {
        if (aimRef == null)
        {
            return aimTarget;
        }

        Vector3 toTarget = aimTarget - aimRef.position;
        Vector3 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : GetAimAxisDirection(aimRef);
        float distance = Mathf.Max(armAlignmentMinDistance, toTarget.magnitude);
        return aimRef.position + direction * distance;
    }

    Vector3 ResolveAimTargetPosition(Transform target)
    {
        if (target == null)
        {
            return transform.position + transform.forward;
        }

        Camera targetCamera = target.GetComponentInChildren<Camera>(true);
        if (targetCamera != null)
        {
            return targetCamera.transform.position;
        }

        vLookTarget lookTarget = target.GetComponentInChildren<vLookTarget>();
        if (lookTarget != null)
        {
            return lookTarget.lookPoint;
        }

        CharacterController characterController = target.GetComponent<CharacterController>();
        if (characterController != null)
        {
            return target.TransformPoint(characterController.center);
        }

        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.center;
        }

        Vector3 targetPosition = target.position;
        if (!aimAtTargetTransform)
        {
            targetPosition += Vector3.up * aimHeightOffset;
        }

        return targetPosition;
    }

    void ApplyUpperBodyLookAt()
    {
        if (currentIKWeight < 0.01f)
        {
            animator.SetLookAtWeight(0f);
            return;
        }

        animator.SetLookAtWeight(currentIKWeight, lookAtBodyWeight, lookAtHeadWeight, lookAtEyesWeight, lookAtClampWeight);
        animator.SetLookAtPosition(GetAimTargetPosition());
    }

    void ResetAnimatorAimIK()
    {
        animator.SetLookAtWeight(0f);
        animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
        animator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
    }

    void UpdateLeftHandIK()
    {
        float targetWeight = 0f;
        Transform leftHandTarget = null;

        if (useLeftHandIK && playerDetected)
        {
            ApplyLeftHandIKOffset();
            leftHandTarget = weapon.handIKTargetOffset != null ? weapon.handIKTargetOffset : weapon.handIKTarget;
            if (leftHandTarget != null)
            {
                targetWeight = Mathf.Clamp01(currentIKWeight * leftHandIKWeight);
            }
        }

        float smooth = targetWeight > leftHandIKCurrentWeight ? leftHandIKSmoothIn : leftHandIKSmoothOut;
        leftHandIKCurrentWeight = Mathf.Lerp(leftHandIKCurrentWeight, targetWeight, smooth * Time.deltaTime);

        if (!useLeftHandIKSolver || leftHandTarget == null || leftHandIKCurrentWeight <= 0.001f)
        {
            return;
        }

        if (leftHandIKSolver == null || !leftHandIKSolver.isValidBones)
        {
            leftHandIKSolver = new vIKSolver(animator, AvatarIKGoal.LeftHand);
        }

        if (!leftHandIKSolver.isValidBones)
        {
            return;
        }

        leftHandIKSolver.UpdateIK();
        leftHandIKSolver.SetIKWeight(leftHandIKCurrentWeight);
        leftHandIKSolver.SetIKPosition(leftHandTarget.position);
        leftHandIKSolver.SetIKRotation(leftHandTarget.rotation);
    }

    void UpdateRightHandIK()
    {
        float targetWeight = 0f;
        IKAdjust currentAdjust = null;

        if (useRightHandIKSolver && playerDetected)
        {
            currentAdjust = GetCurrentWeaponIKAdjust();
            if (currentAdjust != null)
            {
                targetWeight = Mathf.Clamp01(currentIKWeight * rightHandIKWeight);
            }
        }

        rightHandIKCurrentWeight = Mathf.Lerp(rightHandIKCurrentWeight, targetWeight, rightHandIKSmooth * Time.deltaTime);
        if (rightHandIKCurrentWeight <= 0.001f)
        {
            return;
        }

        if (rightHandIKSolver == null || !rightHandIKSolver.isValidBones)
        {
            rightHandIKSolver = new vIKSolver(animator, AvatarIKGoal.RightHand);
        }

        if (!rightHandIKSolver.isValidBones || currentAdjust == null)
        {
            return;
        }

        rightHandIKSolver.SetIKWeight(rightHandIKCurrentWeight);
        ApplyOffsetToSolver(rightHandIKSolver.endBoneOffset, currentAdjust.weaponHandOffset, rightHandIKCurrentWeight);
        ApplyOffsetToSolver(rightHandIKSolver.middleBoneOffset, currentAdjust.weaponHintOffset, rightHandIKCurrentWeight);
        rightHandIKSolver.AnimationToIK();
    }

    void ApplyLeftHandIKOffset()
    {
        Transform offset = weapon.handIKTargetOffset;
        if (offset == null)
        {
            return;
        }

        if (weaponIKAdjustList != null)
        {
            offset.localPosition = weaponIKAdjustList.ikTargetPositionOffsetL;
            offset.localEulerAngles = weaponIKAdjustList.ikTargetRotationOffsetL;
            return;
        }

        if (forceLeftHandIKOffset)
        {
            offset.localPosition = leftHandIKLocalPosition;
            offset.localEulerAngles = leftHandIKLocalEuler;
        }
    }

    IKAdjust GetCurrentWeaponIKAdjust()
    {
        if (weaponIKAdjustList == null || weapon == null)
        {
            return null;
        }

        vWeaponIKAdjust weaponIKAdjust = weaponIKAdjustList.GetWeaponIK(weapon.weaponCategory);
        if (weaponIKAdjust == null)
        {
            return null;
        }

        return weaponIKAdjust.GetIKAdjust(playerDetected, false, weapon.isLeftWeapon);
    }

    void ApplyOffsetToSolver(Transform target, IKOffsetTransform offset, float weight)
    {
        if (target == null || offset == null)
        {
            return;
        }

        target.localPosition = Vector3.Lerp(target.localPosition, offset.position, rightHandIKSmooth * Time.deltaTime * Mathf.Max(weight, 0.01f));
        target.localRotation = Quaternion.Lerp(target.localRotation, Quaternion.Euler(offset.eulerAngles), rightHandIKSmooth * Time.deltaTime * Mathf.Max(weight, 0.01f));
    }

    void AlignWeaponArmToAim(Transform aimRef, Vector3 aimTarget, float weight)
    {
        if (aimRef == null || weight <= 0.001f || (!weapon.alignRightUpperArmToAim && !weapon.alignRightHandToAim))
        {
            return;
        }

        Transform upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
        Transform foreArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
        Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        if (upperArm == null || foreArm == null || hand == null)
        {
            return;
        }

        if (rightArmAim == null)
        {
            rightArmAim = new vArmAimAlign(upperArm, foreArm, hand);
        }

        if (!rightArmAim.IsValid)
        {
            return;
        }

        rightArmAim.aimReference = aimRef;
        rightArmAim.smoothIKAlignmentPoint = smoothArmAlignmentPoint;
        rightArmAim.smooth = armAimRotationSmooth;
        rightArmAim.maxVerticalAligmentAngle = maxVerticalArmAimAngle;
        rightArmAim.maxHorizontalAligmentAngle = maxHorizontalArmAimAngle;
        rightArmAim.UpdateDefaultAlignment();
        rightArmAim.AlignToArmToPosition(aimTarget, weight, weapon.alignRightUpperArmToAim, weapon.alignRightHandToAim);
    }

    bool HasBoolParameter(string parameterName)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Bool && parameter.name == parameterName)
            {
                return true;
            }
        }

        return false;
    }

    Vector3 GetAimAxisDirection(Transform axisReference)
    {
        if (axisReference == null)
        {
            return Vector3.forward;
        }

        switch (aimReferenceAxis)
        {
            case AimReferenceAxis.RightX:
                return axisReference.right;
            case AimReferenceAxis.UpY:
                return axisReference.up;
            default:
                return axisReference.forward;
        }
    }

    Vector3 GetAimAxisUp(Transform axisReference, Vector3 axisDirection)
    {
        if (axisReference == null)
        {
            return Vector3.up;
        }

        Vector3 rawUp = aimReferenceAxis == AimReferenceAxis.UpY ? axisReference.forward : axisReference.up;
        Vector3 up = Vector3.ProjectOnPlane(rawUp, axisDirection);

        if (up.sqrMagnitude < 0.0001f)
        {
            up = Vector3.ProjectOnPlane(Vector3.up, axisDirection);
            if (up.sqrMagnitude < 0.0001f)
            {
                up = Vector3.ProjectOnPlane(Vector3.right, axisDirection);
            }
        }

        return up.normalized;
    }

    string ResolveShotTriggerName()
    {
        if (animator == null || animator.runtimeAnimatorController == null)
        {
            return string.Empty;
        }

        AnimatorControllerParameter[] parameters = animator.parameters;
        string[] candidates = { "Shot", "Shoot", "TriggerShot", "Attack" };

        for (int i = 0; i < candidates.Length; i++)
        {
            string candidate = candidates[i];
            for (int j = 0; j < parameters.Length; j++)
            {
                AnimatorControllerParameter p = parameters[j];
                if (p.type == AnimatorControllerParameterType.Trigger && p.name == candidate)
                {
                    return candidate;
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Call this from a bullet/projectile hit to kill the enemy instantly.
    /// </summary>
    public void TakeDamage(Vector3 hitPoint, Vector3 hitDirection)
    {
        if (isDead) return;
        isDead = true;

        animator.SetBool(HashIsAiming, false);
        animator.SetBool(HashIsDead, true);

        if (enemyEffect != null)
        {
            enemyEffect.PlayHitEffects(hitPoint, hitDirection);
            enemyEffect.ActivateRagdoll(hitDirection);
        }
    }
}
