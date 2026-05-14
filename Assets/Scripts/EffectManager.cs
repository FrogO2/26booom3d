using UnityEngine;
using System.Collections;

public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    [Header("ȫ����Ч����")]
    public Material postProcessMaterial;

    [Header("==== ��ɱ��Ч (˲��) ====")]
    public float killDuration = 0.35f;
    public AnimationCurve killCurve = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 0));
    private Coroutine killCoroutine;

    [Header("==== ���������Ч (��ס) ====")]
    [Tooltip("��Ч����͵������ٶȣ�ֵԽ��仯Խ��")]
    public float sprintTransitionSpeed = 5f;

    // ɾ�������� Camera ��صı�����

    // �ڲ�״̬׷��
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
            elapsed += Time.unscaledDeltaTime;
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
        currentSprintIntensity = Mathf.MoveTowards(currentSprintIntensity, targetIntensity, sprintTransitionSpeed * Time.unscaledDeltaTime);

        if (postProcessMaterial != null)
        {
            postProcessMaterial.SetFloat("_DashIntensity", currentSprintIntensity);
        }

        // ɾ�������︲�� FOV �Ĵ��룡������Ȩ���׻��� FirstPersonController��
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