using UnityEngine;

public class TestEffectsSpawner : MonoBehaviour
{


    void Start()
    {
        
    }

    void Update()
    {
        
        if (Input.GetMouseButtonDown(0))
        {
            //FireBloodSplash();
            EffectManager.Instance.TriggerKillEffect();
        }
        if (Input.GetKeyDown(KeyCode.LeftShift))
        {
            EffectManager.Instance.SetSprintState(true);
        }

        // ËÉ¿ª Shift ¼üµÄË²¼äÍ£Ö¹³å´Ì
        if (Input.GetKeyUp(KeyCode.LeftShift))
        {
            EffectManager.Instance.SetSprintState(false);
        }
    }



}
