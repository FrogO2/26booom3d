using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TimerFinishLine : MonoBehaviour
{
    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player") || other.GetComponentInParent<CharacterController>() != null)
        {
            if (SpeedrunTimer.Instance != null && SpeedrunTimer.Instance.IsRunning)
            {
                SpeedrunTimer.Instance.StopTimer();

                float finalTime = SpeedrunTimer.Instance.GetFinalTime();

                ArenaWallLeaderboardDisplay board = FindAnyObjectByType<ArenaWallLeaderboardDisplay>();
                if (board != null)
                {
                    board.SubmitScore("PLAYER", finalTime);
                    Debug.Log($"[速通结束] 撞线成功！成绩 {finalTime} 已推送至 3D 榜单！");
                }
                else
                {
                    Debug.LogWarning("未在场景中找到 ArenaWallLeaderboardDisplay 组件！");
                }

                gameObject.SetActive(false);
            }
        }
    }
}