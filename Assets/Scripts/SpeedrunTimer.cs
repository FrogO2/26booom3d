using UnityEngine;
using TMPro;

public class SpeedrunTimer : MonoBehaviour
{
    public static SpeedrunTimer Instance { get; private set; }

    [Header("UI References")]
    [Tooltip("拖入显示当前结算时间的 TextMeshPro 组件")]
    [SerializeField] private TMP_Text timeText;

    private float startTime;
    private float endTime;
    private bool isRunning;
    private bool hasFinished;


    private static float sessionBestTime = float.MaxValue;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        Instance = this;

        if (timeText == null) timeText = GetComponent<TMP_Text>();
    }

    private void Start()
    {
        if (timeText != null)
        {
            timeText.text = "00:00.000";
            timeText.color = Color.white;
        }
    }

    private void Update()
    {
        if (!isRunning && !hasFinished && Input.GetMouseButtonDown(0))
        {
            StartTimer();
        }

        if (isRunning && timeText != null)
        {
            float elapsed = Time.time - startTime;
            timeText.text = FormatTime(elapsed);
        }
    }

    public void StartTimer()
    {
        isRunning = true;
        hasFinished = false;
        startTime = Time.time;
    }

    public void StopTimer()
    {
        if (!isRunning) return;

        isRunning = false;
        hasFinished = true;
        endTime = Time.time;

        if (timeText != null)
        {
            float finalTime = GetFinalTime();
            timeText.text = FormatTime(finalTime);
            timeText.color = Color.white; 


            if (finalTime < sessionBestTime)
            {
                sessionBestTime = finalTime;
                timeText.text += "\nNEW RECORD";
            }
        }
    }

    public float GetFinalTime()
    {
        return endTime - startTime;
    }

    private string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60F);
        float seconds = time - (minutes * 60f);
        return $"{minutes:00}:{seconds:00.000}";
    }
}