using UnityEngine;

[RequireComponent(typeof(FirstPersonController))]
public class PlayerAudioController : MonoBehaviour
{
    public static PlayerAudioController Instance { get; private set; } 

    [Header("References")]
    [SerializeField] private FirstPersonController playerController;
    [SerializeField] private AudioSource footstepSource;
    [SerializeField] private AudioSource windSource;
    [SerializeField] private AudioSource weaponSource;

    [Header("Footsteps (脚步声)")]
    [SerializeField] private AudioClip[] walkFootsteps;
    [SerializeField] private AudioClip[] sprintFootsteps;
    [SerializeField] private float walkStepDistance = 2.0f;
    [SerializeField] private float sprintStepDistance = 2.8f;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.5f;

    [Header("Wind & Dash (破风声)")]
    [SerializeField] private float minWindSpeed = 8f;   // 开始产生破风声的最低速度
    [SerializeField] private float maxWindSpeed = 24f;  // 破风声达到最大音量的速度
    [SerializeField, Range(0f, 1f)] private float maxWindVolume = 0.8f;
    [SerializeField] private float windResponseSpeed = 5f;

    [Header("Weapon & Combat (战斗音效)")]
    [SerializeField] private AudioClip[] swingClips; // 空挥
    [SerializeField] private AudioClip[] hitClips;   // 砍中敌人肉体

    private float accumulatedDistance;
    private Vector3 lastPosition;

    private void Awake()
    {
        Instance = this;
        if (playerController == null) playerController = GetComponent<FirstPersonController>();
    }

    private void Start()
    {
        lastPosition = transform.position;

        // 确保破风声是循环播放的
        if (windSource != null)
        {
            windSource.loop = true;
            windSource.volume = 0f;
            windSource.Play();
        }
    }

    private void Update()
    {
        HandleFootsteps();
        HandleWindSound();
    }

    // ================= 处理脚步声 =================
    private void HandleFootsteps()
    {
        if (footstepSource == null || !playerController.IsGrounded || playerController.IsSliding)
        {
            lastPosition = transform.position; // 在空中或滑铲时不累积脚步距离
            return;
        }

        float distanceMoved = Vector3.Distance(transform.position, lastPosition);
        accumulatedDistance += distanceMoved;
        lastPosition = transform.position;

        float currentStepThreshold = playerController.IsSprinting ? sprintStepDistance : walkStepDistance;

        if (accumulatedDistance >= currentStepThreshold)
        {
            accumulatedDistance = 0f;
            PlayRandomFootstep();
        }
    }

    private void PlayRandomFootstep()
    {
        AudioClip[] currentClips = playerController.IsSprinting ? sprintFootsteps : walkFootsteps;
        if (currentClips.Length == 0) return;

        AudioClip clip = currentClips[Random.Range(0, currentClips.Length)];
        // 随机化音高和微调音量，增加真实感
        footstepSource.pitch = Random.Range(0.9f, 1.1f);
        footstepSource.PlayOneShot(clip, footstepVolume * Random.Range(0.9f, 1.0f));
    }

    // ================= 处理破风声 =================
    private void HandleWindSound()
    {
        if (windSource == null) return;

        // 获取玩家当前的真实物理速度
        float currentSpeed = playerController.TotalSpeed;

        // 计算目标音量和目标音高
        float speedRatio = Mathf.InverseLerp(minWindSpeed, maxWindSpeed, currentSpeed);
        float targetVolume = speedRatio * maxWindVolume;
        float targetPitch = Mathf.Lerp(0.8f, 1.5f, speedRatio);

        // 平滑过渡
        windSource.volume = Mathf.MoveTowards(windSource.volume, targetVolume, windResponseSpeed * Time.deltaTime);
        windSource.pitch = Mathf.Lerp(windSource.pitch, targetPitch, windResponseSpeed * Time.deltaTime);
    }

    // ================= 战斗相关公开接口 =================

    // 播放空挥声
    public void PlaySwingSound()
    {
        if (weaponSource == null || swingClips.Length == 0) return;
        weaponSource.pitch = Random.Range(0.95f, 1.05f); // 稍微变调
        weaponSource.PlayOneShot(swingClips[Random.Range(0, swingClips.Length)]);
    }

    public void StopWeaponSound()
    {
        if (weaponSource != null)
        {
            weaponSource.Stop();
        }
    }

    // 播放砍中声
    public void PlayHitSound()
    {
        if (weaponSource == null || hitClips.Length == 0) return;
        weaponSource.pitch = Random.Range(0.9f, 1.1f); // 砍中肉体的声音变调可以夸张一点
        weaponSource.PlayOneShot(hitClips[Random.Range(0, hitClips.Length)]);
    }
}