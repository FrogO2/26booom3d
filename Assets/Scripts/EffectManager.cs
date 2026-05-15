using System.Collections;
using UnityEngine;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    private const float ParticlePlaneDepth = 0.42f;
    private const float MinimumEmitterDepthPadding = 0.12f;
    private const float MinimumParticleScreenSpeed = 1.35f;
    private const float MinParticleLifetime = 0.08f;
    private const float MaxParticleLifetime = 0.24f;
    private const float MinParticleSize = 0.0045f;
    private const float MaxParticleSize = 0.0135f;
    private const float MaxEmissionRate = 72f;

    [Header("全屏特效材质")]
    public Material postProcessMaterial;

    [Header("==== 击杀特效 ====")]
    public float killDuration = 0.35f;
    public AnimationCurve killCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
    private Coroutine killCoroutine;

    [Header("==== 速度线粒子 ====")]
    [Tooltip("拖入玩家身上的 CharacterController 以获取真实物理速度")]
    public CharacterController playerController;
    [Tooltip("拖入主相机，用于计算相对视野的运动方向")]
    public Transform cameraTransform;
    [Tooltip("可选：拖入 FirstPersonController，留空时会自动查找")]
    [SerializeField] private FirstPersonController firstPersonController;
    [Tooltip("速度线粒子会挂在这个相机下；留空时会自动寻找名为 Weapon Camera 的相机")]
    [SerializeField] private Camera weaponCamera;
    [Tooltip("速度线粒子预制体；留空时会自动从 Assets/Prefabs/SpeedLine.prefab 加载")]
    [SerializeField] private GameObject speedLinePrefab;
    [Tooltip("玩家达到这个速度前，速度线不会出现")]
    [SerializeField, Min(0f)] private float minimumVisibleSpeed = 6.5f;
    [Tooltip("玩家达到这个速度时，速度线会完全出现")]
    [SerializeField, Min(0f)] private float fullVisibleSpeed = 10f;
    [Tooltip("粒子速度 = 玩家速度 * 这个比例")]
    [SerializeField, Min(0f)] private float particleSpeedRatio = 0.2f;
    [Tooltip("是否让速度线朝角色移动的反方向拉伸；勾选通常更像拖尾")]
    [SerializeField] private bool reverseTrailDirection = true;
    [Tooltip("越大越贴近屏幕边缘；越小越靠近屏幕中心")]
    [SerializeField, Range(0.55f, 0.98f)] private float screenEdgePlacementRatio = 0.86f;
    [Tooltip("后向补充速度线的发射比例，避免身后完全空白")]
    [SerializeField, Range(0f, 1f)] private float rearEmissionRatio = 0.35f;
    [Tooltip("前进/后退速度映射到屏幕上下边缘的权重")]
    [SerializeField, Range(0.2f, 1.4f)] private float forwardScreenBias = 0.85f;
    [Tooltip("单条速度线基础寿命，越大线越长")]
    [SerializeField, Range(0.12f, 0.8f)] private float particleLifetime = 0.48f;
    [Tooltip("单条速度线基础粗细")]
    [SerializeField, Range(0.004f, 0.02f)] private float particleThickness = 0.009f;
    [Tooltip("速度线拉伸长度，直接影响视觉长度")]
    [SerializeField, Range(0.25f, 3f)] private float particleStretchLength = 2.2f;
    [Tooltip("额外拖尾拉伸，默认保持 0，这样速度差异主要体现在移动速度")]
    [SerializeField, Range(0f, 1f)] private float particleVelocityStretch = 0f;
    [Tooltip("粒子系统最大粒子数，越多越密集但性能开销越大")]
    [SerializeField, Range(100f, 1000f)] private float maxParticlesCount = 400f;
    [Tooltip("整体效果强度，会同时影响发射量和透明度")]
    [SerializeField, Range(0.4f, 2.2f)] private float effectStrength = 1.15f;
    [Tooltip("下落时额外增强可见性，亮背景下可以调高")]
    [SerializeField, Range(1f, 2.5f)] private float fallVisibilityBoost = 1.45f;
    [Tooltip("速度线强度变化的平滑速度")]
    [SerializeField, Min(0.1f)] private float speedLineResponse = 8f;
    [Tooltip("粒子刚开始可见时的颜色，默认偏白")]
    [SerializeField] private Color minimumSpeedColor = new Color(1f, 1f, 1f, 0.16f);
    [Tooltip("粒子完全显现时的颜色，默认深灰")]
    [SerializeField] private Color maximumSpeedColor = new Color(0.22f, 0.22f, 0.22f, 0.9f);

    private bool isSprinting;
    private float currentSpeedLineIntensity;
    private GameObject speedLineRoot;
    private ParticleSystem speedLineParticleSystem;
    private ParticleSystem speedLineRearParticleSystem;
    private ParticleSystemRenderer speedLineParticleRenderer;
    private ParticleSystemRenderer speedLineRearParticleRenderer;
    private Vector3 lastPlayerPosition;
    private bool hasLastPlayerPosition;

    private void Awake()
    {
        Instance = this;
        AutoAssignReferences();
    }

    private void Start()
    {
        ResetPostProcessState();
        EnsureSpeedLineParticles();
    }

    private void Update()
    {
        AutoAssignReferences();
        UpdateSpeedLineParticles();
    }

    public void TriggerKillEffect()
    {
        if (killCoroutine != null)
        {
            StopCoroutine(killCoroutine);
        }

        killCoroutine = StartCoroutine(PlayKillEffect());
    }

    private IEnumerator PlayKillEffect()
    {
        float elapsed = 0f;
        while (elapsed < killDuration)
        {
            elapsed += Time.deltaTime;
            float intensity = killCurve.Evaluate(elapsed / Mathf.Max(0.0001f, killDuration));
            if (postProcessMaterial != null)
            {
                postProcessMaterial.SetFloat("_Intensity", intensity);
            }

            yield return null;
        }

        if (postProcessMaterial != null)
        {
            postProcessMaterial.SetFloat("_Intensity", 0f);
        }
    }

    public void SetSprintState(bool sprinting)
    {
        isSprinting = sprinting;
    }

    private void AutoAssignReferences()
    {
        if (firstPersonController == null)
        {
            firstPersonController = FindAnyObjectByType<FirstPersonController>();
        }

        if (playerController == null && firstPersonController != null)
        {
            playerController = firstPersonController.GetComponent<CharacterController>();
        }

        if (playerController == null)
        {
            playerController = FindAnyObjectByType<CharacterController>();
        }

        if (firstPersonController == null && playerController != null)
        {
            firstPersonController = playerController.GetComponent<FirstPersonController>();
        }

        if (cameraTransform == null)
        {
            if (firstPersonController != null && firstPersonController.PlayerCamera != null)
            {
                cameraTransform = firstPersonController.PlayerCamera.transform;
            }
            else if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
        }

        if (weaponCamera == null)
        {
            weaponCamera = ResolveWeaponCamera();
        }
    }

    private Camera ResolveWeaponCamera()
    {
        if (firstPersonController != null && firstPersonController.CameraRoot != null)
        {
            Transform weaponCameraTransform = firstPersonController.CameraRoot.Find("Weapon Camera");
            if (weaponCameraTransform != null)
            {
                Camera resolvedWeaponCamera = weaponCameraTransform.GetComponent<Camera>();
                if (resolvedWeaponCamera != null)
                {
                    return resolvedWeaponCamera;
                }
            }
        }

        if (playerController != null)
        {
            Camera[] cameras = playerController.GetComponentsInChildren<Camera>(true);
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate != null && candidate.name == "Weapon Camera")
                {
                    return candidate;
                }
            }
        }

        if (firstPersonController != null)
        {
            return firstPersonController.PlayerCamera;
        }

        return null;
    }

    private void EnsureSpeedLineParticles()
    {
        if (cameraTransform == null)
        {
            return;
        }

        if (speedLineRoot == null)
        {
            speedLineRoot = new GameObject("Speed Lines");
            speedLineRoot.transform.SetParent(cameraTransform, false);
            speedLineRoot.transform.localPosition = Vector3.zero;
            speedLineRoot.transform.localRotation = Quaternion.identity;
            speedLineRoot.transform.localScale = Vector3.one;

            int speedLineLayer = ResolveSpeedLineLayer();
            SetLayerRecursively(speedLineRoot, speedLineLayer);
            speedLineParticleSystem = CreateSpeedLineParticleSystem(speedLineLayer, false);
            speedLineRearParticleSystem = CreateSpeedLineParticleSystem(speedLineLayer, true);
        }
        else if (speedLineRoot.transform.parent != cameraTransform)
        {
            speedLineRoot.transform.SetParent(cameraTransform, false);
        }

        if (speedLineParticleSystem == null)
        {
            int speedLineLayer = ResolveSpeedLineLayer();
            SetLayerRecursively(speedLineRoot, speedLineLayer);
            speedLineParticleSystem = CreateSpeedLineParticleSystem(speedLineLayer, false);
        }

        if (speedLineRearParticleSystem == null)
        {
            int speedLineLayer = ResolveSpeedLineLayer();
            SetLayerRecursively(speedLineRoot, speedLineLayer);
            speedLineRearParticleSystem = CreateSpeedLineParticleSystem(speedLineLayer, true);
        }

        speedLineRoot.transform.localPosition = Vector3.zero;
        speedLineRoot.transform.localRotation = Quaternion.identity;
        speedLineRoot.transform.localScale = Vector3.one;
        SyncSpeedLineRendererMaterialFromPrefab();
    }

    private void SyncSpeedLineRendererMaterialFromPrefab()
    {
        if (speedLinePrefab == null)
        {
            return;
        }

        ParticleSystemRenderer prefabRenderer = speedLinePrefab.GetComponent<ParticleSystemRenderer>();
        if (prefabRenderer == null || prefabRenderer.sharedMaterial == null)
        {
            return;
        }

        if (speedLineParticleRenderer != null && speedLineParticleRenderer.sharedMaterial != prefabRenderer.sharedMaterial)
        {
            speedLineParticleRenderer.sharedMaterial = prefabRenderer.sharedMaterial;
        }

        if (speedLineRearParticleRenderer != null && speedLineRearParticleRenderer.sharedMaterial != prefabRenderer.sharedMaterial)
        {
            speedLineRearParticleRenderer.sharedMaterial = prefabRenderer.sharedMaterial;
        }
    }

    private int ResolveSpeedLineLayer()
    {
        if (weaponCamera != null)
        {
            int cullingMask = weaponCamera.cullingMask;
            for (int layer = 0; layer < 32; layer++)
            {
                if ((cullingMask & (1 << layer)) != 0)
                {
                    return layer;
                }
            }
            return weaponCamera.gameObject.layer;
        }

        if (cameraTransform != null)
        {
            return cameraTransform.gameObject.layer;
        }

        return 0;
    }

    private ParticleSystem CreateSpeedLineParticleSystem(int layer, bool isRear)
    {
#if UNITY_EDITOR
        GameObject pathPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/SpeedLine.prefab");
        if (pathPrefab != null)
        {
            speedLinePrefab = pathPrefab;
        }
#endif

        if (speedLinePrefab == null)
        {
            Debug.LogError("SpeedLine 预制体未找到！请在 EffectManager Inspector 中的 'Speed Line Prefab' 字段中拖入 Assets/Prefabs/SpeedLine.prefab 预制体");
            return null;
        }

        GameObject emitterObject = Instantiate(speedLinePrefab, speedLineRoot.transform, false);
        emitterObject.name = isRear ? "SpeedLine Rear" : "SpeedLine";
        SetLayerRecursively(emitterObject, layer);

        ParticleSystem particleSystem = emitterObject.GetComponent<ParticleSystem>();
        ParticleSystemRenderer particleRenderer = emitterObject.GetComponent<ParticleSystemRenderer>();

        if (particleSystem != null)
        {
            // 保证方向主要由脚本写入的速度向量决定，避免预制体 Start Speed 抢方向
            ParticleSystem.MainModule main = particleSystem.main;
            main.startSpeed = 0f;

            // 确保速度模块启用
            ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = particleSystem.velocityOverLifetime;
            velocityOverLifetime.enabled = true;
        }

        if (particleRenderer != null)
        {
            ParticleSystemRenderer prefabRenderer = speedLinePrefab.GetComponent<ParticleSystemRenderer>();
            if (prefabRenderer != null && prefabRenderer.sharedMaterial != null)
            {
                particleRenderer.sharedMaterial = prefabRenderer.sharedMaterial;
            }
            else if (particleRenderer.sharedMaterial == null)
            {
                Debug.LogWarning("SpeedLine 预制体没有材质，请在预制体的 Particle System Renderer 上手动指定材质。");
            }
            if (isRear)
            {
                speedLineRearParticleRenderer = particleRenderer;
            }
            else
            {
                speedLineParticleRenderer = particleRenderer;
            }
        }

        if (particleSystem != null)
        {
            particleSystem.Play(true);
        }

        return particleSystem;
    }

    private void UpdateSpeedLineEmitterLayout(ParticleSystem targetSystem, Vector2 screenMotion)
    {
        if (cameraTransform == null || targetSystem == null)
        {
            return;
        }

        Camera refCamera = weaponCamera != null ? weaponCamera : Camera.main;
        if (refCamera == null)
        {
            return;
        }

        float depth = Mathf.Max(refCamera.nearClipPlane + MinimumEmitterDepthPadding, ParticlePlaneDepth);
        float halfHeight = Mathf.Tan(refCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * depth;
        float halfWidth = halfHeight * refCamera.aspect;
        Vector2 direction = screenMotion.sqrMagnitude > 0.0001f ? screenMotion.normalized : Vector2.up;
        float edgeScale = Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
        if (edgeScale < 0.0001f)
        {
            edgeScale = 1f;
        }

        Vector2 edgeDirection = direction / edgeScale;
        Transform emitterTransform = targetSystem.transform;
        emitterTransform.localPosition = new Vector3(
            edgeDirection.x * halfWidth * screenEdgePlacementRatio,
            edgeDirection.y * halfHeight * screenEdgePlacementRatio,
            depth);
        emitterTransform.localScale = Vector3.one;

        // 保持粒子系统的默认方向，由 VelocityOverLifetime 的世界空间速度控制粒子排列方向
        emitterTransform.localRotation = Quaternion.identity;
    }

    private void UpdateSpeedLineParticles()
    {
        EnsureSpeedLineParticles();

        if (playerController == null || speedLineParticleSystem == null)
        {
            ApplySpeedLineState(0f, Vector3.zero, Vector3.zero);
            return;
        }

        Transform referenceTransform = cameraTransform != null ? cameraTransform : (weaponCamera != null ? weaponCamera.transform : null);
        if (referenceTransform == null)
        {
            ApplySpeedLineState(0f, Vector3.zero, Vector3.zero);
            return;
        }

        Vector3 worldVelocity = ResolvePlayerWorldVelocity();
        Vector3 motionVector = BuildSpeedLineMotion(referenceTransform, worldVelocity);
        float sensedSpeed = worldVelocity.magnitude;
        float clampedFullSpeed = Mathf.Max(minimumVisibleSpeed + 0.01f, fullVisibleSpeed);
        float targetIntensity = Mathf.InverseLerp(minimumVisibleSpeed, clampedFullSpeed, sensedSpeed);
        currentSpeedLineIntensity = Mathf.MoveTowards(currentSpeedLineIntensity, targetIntensity, speedLineResponse * Time.deltaTime);

        ApplySpeedLineState(sensedSpeed, motionVector, worldVelocity);

        if (postProcessMaterial != null)
        {
            postProcessMaterial.SetFloat("_DashIntensity", 0f);
            postProcessMaterial.SetVector("_DashCenterOffset", Vector2.zero);
        }
    }

    private Vector3 ResolvePlayerWorldVelocity()
    {
        if (playerController == null)
        {
            hasLastPlayerPosition = false;
            return Vector3.zero;
        }

        Transform playerTransform = playerController.transform;
        Vector3 currentPosition = playerTransform.position;

        if (!hasLastPlayerPosition)
        {
            hasLastPlayerPosition = true;
            lastPlayerPosition = currentPosition;
            return playerController.velocity;
        }

        float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
        Vector3 frameVelocity = (currentPosition - lastPlayerPosition) / deltaTime;
        lastPlayerPosition = currentPosition;

        // 优先使用实际位移速度，确保方向与真实移动一致。
        return frameVelocity;
    }

    private Vector3 BuildSpeedLineMotion(Transform referenceTransform, Vector3 worldVelocity)
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(referenceTransform.forward, Vector3.up);
        if (planarForward.sqrMagnitude < 0.0001f && playerController != null)
        {
            planarForward = Vector3.ProjectOnPlane(playerController.transform.forward, Vector3.up);
        }

        if (planarForward.sqrMagnitude < 0.0001f)
        {
            planarForward = Vector3.forward;
        }

        planarForward.Normalize();

        Vector3 planarRight = Vector3.Cross(Vector3.up, planarForward);
        Vector3 planarVelocity = Vector3.ProjectOnPlane(worldVelocity, Vector3.up);

        float lateralSpeed = Vector3.Dot(planarVelocity, planarRight);
        float verticalSpeed = Vector3.Dot(worldVelocity, Vector3.up);
        float forwardSpeed = Vector3.Dot(planarVelocity, planarForward);
        return new Vector3(lateralSpeed, verticalSpeed, forwardSpeed);
    }

    private Vector2 BuildSpeedLineScreenMotion(Vector3 motionVector)
    {
        Vector2 screenMotion = new Vector2(motionVector.x, motionVector.y + (motionVector.z * forwardScreenBias));
        if (screenMotion.sqrMagnitude < 0.0001f && Mathf.Abs(motionVector.z) > 0.0001f)
        {
            screenMotion = new Vector2(0f, motionVector.z);
        }

        return screenMotion;
    }

    private void ApplySpeedLineState(float sensedSpeed, Vector3 motionVector, Vector3 worldVelocity)
    {
        if (speedLineParticleSystem == null && speedLineRearParticleSystem == null)
        {
            return;
        }

        float emissionRate = Mathf.Lerp(0f, MaxEmissionRate * effectStrength, currentSpeedLineIntensity);
        float particleSpeed = Mathf.Max(MinimumParticleScreenSpeed, sensedSpeed * particleSpeedRatio);

        Vector2 screenMotion = BuildSpeedLineScreenMotion(motionVector);
        float normalizedSpeed = Mathf.Max(sensedSpeed, 0.01f);
        float downwardShare = Mathf.Clamp01(Mathf.Max(0f, -worldVelocity.y) / normalizedSpeed);
        emissionRate *= Mathf.Lerp(1f, fallVisibilityBoost, downwardShare * 0.9f);

        UpdateSpeedLineEmitterLayout(speedLineParticleSystem, screenMotion);
        UpdateSpeedLineEmitterLayout(speedLineRearParticleSystem, -screenMotion);

        bool suppressEmission = emissionRate <= 0.01f || screenMotion.sqrMagnitude < 0.0001f;

        float primaryEmissionRate = suppressEmission ? 0f : emissionRate;
        float rearEmissionRate = suppressEmission ? 0f : emissionRate * rearEmissionRatio;

        Vector3 trailVelocity = Vector3.zero;
        if (!suppressEmission && worldVelocity.sqrMagnitude > 0.0001f)
        {
            Vector3 trailDirection = reverseTrailDirection ? -worldVelocity.normalized : worldVelocity.normalized;
            trailVelocity = trailDirection * particleSpeed;
        }

        ApplySpeedLineParticleEmission(speedLineParticleSystem, primaryEmissionRate, trailVelocity);
        ApplySpeedLineParticleEmission(speedLineRearParticleSystem, rearEmissionRate, trailVelocity);
    }

    private static void ApplySpeedLineParticleEmission(ParticleSystem targetSystem, float emissionRate, Vector3 trailVelocity)
    {
        if (targetSystem == null)
        {
            return;
        }

        ParticleSystem.EmissionModule emission = targetSystem.emission;
        emission.rateOverTime = emissionRate;

        ParticleSystem.VelocityOverLifetimeModule velocityOverLifetime = targetSystem.velocityOverLifetime;
        if (emissionRate > 0.01f && trailVelocity.sqrMagnitude > 0.0001f)
        {
            velocityOverLifetime.space = ParticleSystemSimulationSpace.World;
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(trailVelocity.x);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(trailVelocity.y);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(trailVelocity.z);
        }
        else
        {
            velocityOverLifetime.x = new ParticleSystem.MinMaxCurve(0f);
            velocityOverLifetime.y = new ParticleSystem.MinMaxCurve(0f);
            velocityOverLifetime.z = new ParticleSystem.MinMaxCurve(0f);
        }
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        if (gameObject == null)
        {
            return;
        }

        gameObject.layer = layer;
        Transform gameObjectTransform = gameObject.transform;
        for (int i = 0; i < gameObjectTransform.childCount; i++)
        {
            SetLayerRecursively(gameObjectTransform.GetChild(i).gameObject, layer);
        }
    }

    private void ResetPostProcessState()
    {
        if (postProcessMaterial != null)
        {
            postProcessMaterial.SetFloat("_Intensity", 0f);
            postProcessMaterial.SetFloat("_DashIntensity", 0f);
            postProcessMaterial.SetVector("_DashCenterOffset", Vector2.zero);
        }
    }

    private void OnDisable()
    {
        ResetPostProcessState();

        if (speedLineParticleSystem != null)
        {
            ParticleSystem.EmissionModule emission = speedLineParticleSystem.emission;
            emission.rateOverTime = 0f;
            speedLineParticleSystem.Clear(true);
        }

        if (speedLineRearParticleSystem != null)
        {
            ParticleSystem.EmissionModule rearEmission = speedLineRearParticleSystem.emission;
            rearEmission.rateOverTime = 0f;
            speedLineRearParticleSystem.Clear(true);
        }

        currentSpeedLineIntensity = 0f;
        isSprinting = false;
    }
}