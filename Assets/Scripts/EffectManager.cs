using UnityEngine;
using System.Collections;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    [Header("全屏特效材质")]
    public Material postProcessMaterial;

    [Header("==== 击杀特效 (瞬间) ====")]
    public float killDuration = 0.35f;
    public AnimationCurve killCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
    private Coroutine killCoroutine;

    [Header("==== 持续冲刺特效 (按住) ====")]
    [Tooltip("特效淡入和淡出的速度，值越大变化越快")]
    public float sprintTransitionSpeed = 5f;

    // 删除了所有 Camera 相关的变量！

    // 内部状态追踪
    private bool isSprinting = false;
    private float currentSprintIntensity = 0f;

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
        float targetIntensity = isSprinting ? 1f : 0f;
        currentSprintIntensity = Mathf.MoveTowards(currentSprintIntensity, targetIntensity, sprintTransitionSpeed * Time.deltaTime);

        if (postProcessMaterial != null)
        {
            postProcessMaterial.SetFloat("_DashIntensity", currentSprintIntensity);
        }

        // 删除了这里覆盖 FOV 的代码！将控制权彻底还给 FirstPersonController！
    }

    void OnDisable()
    {
        if (postProcessMaterial != null)
        {
            postProcessMaterial.SetFloat("_Intensity", 0f);
            postProcessMaterial.SetFloat("_DashIntensity", 0f);
        }
    }
}