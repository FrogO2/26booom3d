using UnityEngine;
using System.Collections;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    [Header("全屏特效材质")]
    public Material postProcessMaterial;

    [Header("==== 击杀特效 ====")]
    public float killDuration = 0.35f;
    public AnimationCurve killCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
    private Coroutine killCoroutine;

    [Header("==== 持续冲刺特效 ====")]
    public float sprintTransitionSpeed = 5f;

    [Header("==== 运动方向反馈 (核心) ====")]
    [Tooltip("拖入玩家身上的 CharacterController 以获取真实物理速度")]
    public CharacterController playerController;
    [Tooltip("拖入主相机，用于计算相对视野的运动方向")]
    public Transform cameraTransform;
    [Tooltip("偏移灵敏度：值越大，左右横移时速度线偏移越夸张")]
    public float directionShiftMultiplier = 0.015f;
    [Tooltip("最大偏移限制：防止消失点跑到屏幕外面去")]
    public float maxShift = 0.3f;

    private bool isSprinting = false;
    private float currentSprintIntensity = 0f;
    // 追踪当前的消失点偏移量，用于平滑过渡
    private Vector2 currentCenterOffset = Vector2.zero;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (postProcessMaterial != null)
        {
            postProcessMaterial.SetFloat("_Intensity", 0f);
            postProcessMaterial.SetFloat("_DashIntensity", 0f);
            postProcessMaterial.SetVector("_DashCenterOffset", Vector2.zero);
        }
    }

    void Update()
    {
        UpdateSprintEffect();
    }

    public void TriggerKillEffect()
    {
        if (killCoroutine != null) StopCoroutine(killCoroutine);
        killCoroutine = StartCoroutine(PlayKillEffect());
    }

    private IEnumerator PlayKillEffect()
    {
        float elapsed = 0f;
        while (elapsed < killDuration)
        {
            elapsed += Time.deltaTime;
            float intensity = killCurve.Evaluate(elapsed / killDuration);
            if (postProcessMaterial != null) postProcessMaterial.SetFloat("_Intensity", intensity);
            yield return null;
        }
        if (postProcessMaterial != null) postProcessMaterial.SetFloat("_Intensity", 0f);
    }

    public void SetSprintState(bool sprinting)
    {
        if (sprinting && !isSprinting)
        {
            if (postProcessMaterial != null)
            {
                float randomRotation = Random.Range(0f, Mathf.PI * 2f);
                postProcessMaterial.SetFloat("_DashRotation", randomRotation);
            }
        }
        isSprinting = sprinting;
    }

    private void UpdateSprintEffect()
    {
        // 1. 处理透明度的淡入淡出
        float targetIntensity = isSprinting ? 1f : 0f;
        currentSprintIntensity = Mathf.MoveTowards(currentSprintIntensity, targetIntensity, sprintTransitionSpeed * Time.deltaTime);

        if (postProcessMaterial != null)
        {
            postProcessMaterial.SetFloat("_DashIntensity", currentSprintIntensity);
        }

        // 2. 处理动态运动方向的偏移
        if (playerController != null && cameraTransform != null)
        {
            // 将玩家的世界速度转换到相机的局部坐标系中
            // localVelocity.x 就是左右移动的速度，localVelocity.y 就是上下(跳跃/下落)的速度
            Vector3 localVelocity = cameraTransform.InverseTransformDirection(playerController.velocity);

            // 计算目标偏移量
            Vector2 targetOffset = new Vector2(localVelocity.x, localVelocity.y) * directionShiftMultiplier;

            // 限制最大偏移，防止效果穿帮
            targetOffset.x = Mathf.Clamp(targetOffset.x, -maxShift, maxShift);
            targetOffset.y = Mathf.Clamp(targetOffset.y, -maxShift, maxShift);

            // 如果玩家松开了冲刺键，让消失点快速回正到屏幕中间
            if (!isSprinting)
            {
                targetOffset = Vector2.zero;
            }

            // 平滑插值，防止突变造成的画面抖动
            currentCenterOffset = Vector2.Lerp(currentCenterOffset, targetOffset, Time.deltaTime * 8f);

            if (postProcessMaterial != null)
            {
                postProcessMaterial.SetVector("_DashCenterOffset", currentCenterOffset);
            }
        }
    }

    void OnDisable()
    {
        if (postProcessMaterial != null)
        {
            postProcessMaterial.SetFloat("_Intensity", 0f);
            postProcessMaterial.SetFloat("_DashIntensity", 0f);
            postProcessMaterial.SetVector("_DashCenterOffset", Vector2.zero);
        }
    }
}