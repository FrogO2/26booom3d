using UnityEngine;
using Invector.IK;
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
    [SerializeField] Transform aimAngleReference;

    [Header("Detection")]
    [SerializeField] float detectionRange = 20f;
    [SerializeField] float fieldOfView = 150f;

    [Header("Shooting")]
    // Note: set weapon.isInfinityAmmo = true in the Inspector for unlimited ammo
    [SerializeField, FormerlySerializedAs("shootInterval"), Min(0.01f)] float continuousFireInterval = 2f;
    [SerializeField] bool aimAtTargetTransform = true;
    [SerializeField] float aimHeightOffset = 1.4f;
	[SerializeField, Min(0.01f)] float projectileSpeedMultiplier = 1f;

    [Header("Rotation")]
    [SerializeField] float turnSpeed = 5f;

    [Header("IK")]
    [SerializeField] float ikSmoothIn = 5f;
    [SerializeField] float ikSmoothOut = 10f;
    [SerializeField, Range(0f, 1f)] float lookAtBodyWeight = 0.35f;
    [SerializeField, Range(0f, 1f)] float lookAtHeadWeight = 0.85f;
    [SerializeField, Range(0f, 1f)] float lookAtEyesWeight = 0f;
    [SerializeField, Range(0f, 1f)] float lookAtClampWeight = 0.5f;
    [SerializeField] float armAimRotationSmooth = 30f;
    [SerializeField] float maxVerticalArmAimAngle = 60f;
    [SerializeField] float maxHorizontalArmAimAngle = 20f;
    [SerializeField] bool smoothArmAlignmentPoint = true;
    [SerializeField] AimReferenceAxis aimReferenceAxis = AimReferenceAxis.RightX;
    [SerializeField] bool useLeftHandIK = true;
    [SerializeField] float leftHandIKWeight = 1f;
    [SerializeField] bool forceLeftHandIKOffset = true;
    [SerializeField] Vector3 leftHandIKLocalPosition = new Vector3(0.019f, -0.072f, -0.001f);
    [SerializeField] Vector3 leftHandIKLocalEuler = new Vector3(342.311188f, 183.349838f, 169.321396f);

    Animator animator;
    bool isDead;
    bool playerDetected;
    float shootTimer;
    float currentIKWeight;
    string shotTriggerName;
    vArmAimAlign rightArmAim;

    static readonly int HashMoveSetID     = Animator.StringToHash("MoveSet_ID");
    static readonly int HashUpperBodyID   = Animator.StringToHash("UpperBody_ID");
    static readonly int HashShotID        = Animator.StringToHash("Shot_ID");
    static readonly int HashIsAiming      = Animator.StringToHash("IsAiming");
    static readonly int HashIsDead        = Animator.StringToHash("isDead");
    static readonly int HashInputMagnitude = Animator.StringToHash("InputMagnitude");
    static readonly int HashIsGrounded    = Animator.StringToHash("IsGrounded");
    static readonly int HashInputHorizontal = Animator.StringToHash("InputHorizontal");

    void Awake()
    {
        animator = GetComponent<Animator>();

        // Invector variants may use different shot trigger names depending on controller version.
        shotTriggerName = ResolveShotTriggerName();
    }

    void Start()
    {
        EnsureAimAngleReference();

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
        animator.SetBool(HashIsAiming,      true);

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
            UpdateShooting();
        }
        else
        {
            animator.SetFloat(HashInputHorizontal, 0f, 0.1f, Time.deltaTime);
        }

        // Smooth IK weight
        float targetWeight = playerDetected ? 1f : 0f;
        float smoothSpeed = targetWeight > currentIKWeight ? ikSmoothIn : ikSmoothOut;
        currentIKWeight = Mathf.MoveTowards(currentIKWeight, targetWeight, smoothSpeed * Time.deltaTime);
    }

    void UpdateDetection()
    {
        if (player == null)
        {
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

    void UpdateShooting()
    {
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0f)
        {
            Fire();
            shootTimer = continuousFireInterval;
        }
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

    void OnAnimatorIK(int layerIndex)
    {
        ResetAnimatorAimIK();

        if (isDead || weapon == null || player == null) return;

        UpdateAimAngleReference();

        ApplyUpperBodyLookAt();

        if (currentIKWeight < 0.01f) return;

        if (forceLeftHandIKOffset)
        {
            Transform offset = weapon.handIKTargetOffset;
            if (offset != null)
            {
                offset.localPosition = leftHandIKLocalPosition;
                offset.localEulerAngles = leftHandIKLocalEuler;
            }
        }

        Vector3 aimTarget = GetAimTargetPosition();
        Transform aimRef = weapon.aimReference != null ? weapon.aimReference : weapon.transform;
        Transform leftHandBone = animator.GetBoneTransform(HumanBodyBones.LeftHand);
        AlignWeaponArmToAim(aimRef, aimTarget);

        // Support hand IK (left hand for right-handed weapon), following the weapon grip target.
        if (useLeftHandIK && leftHandBone != null)
        {
            Transform leftHandTarget = weapon.handIKTargetOffset != null ? weapon.handIKTargetOffset : weapon.handIKTarget;
            if (leftHandTarget != null)
            {
                float supportWeight = Mathf.Clamp01(currentIKWeight * leftHandIKWeight);
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

        Vector3 targetPosition = player.position;
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

    void EnsureAimAngleReference()
    {
        if (aimAngleReference != null)
        {
            return;
        }

        Transform headBone = animator != null ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        GameObject helper = new GameObject("aimAngleReference");
        helper.tag = "Ignore Ragdoll";
        aimAngleReference = helper.transform;
        if (headBone != null)
        {
            aimAngleReference.SetParent(headBone);
        }
        else
        {
            aimAngleReference.SetParent(transform);
        }

        aimAngleReference.localPosition = Vector3.zero;
        aimAngleReference.rotation = transform.rotation;
    }

    void UpdateAimAngleReference()
    {
        if (aimAngleReference == null)
        {
            return;
        }

        aimAngleReference.rotation = transform.rotation;
    }

    void AlignWeaponArmToAim(Transform aimRef, Vector3 aimTarget)
    {
        if (aimRef == null || (!weapon.alignRightUpperArmToAim && !weapon.alignRightHandToAim))
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
        rightArmAim.AlignToArmToPosition(aimTarget, currentIKWeight, weapon.alignRightUpperArmToAim, weapon.alignRightHandToAim);
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
