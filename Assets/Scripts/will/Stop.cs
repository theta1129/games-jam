using System.Collections;
using UnityEngine;

public static class Stop
{
    private static bool isWaiting = false;
    private static MonoBehaviour runner;


    public static void Pause(float duration)
    {
        if (duration <= 0f) return;
        if (isWaiting) return;
        else isWaiting = true;
        Time.timeScale = 0.0f;
        GetRunner().StartCoroutine(Wait(duration));
    }


    private static IEnumerator Wait(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f;
        isWaiting = false;
    }

    private static MonoBehaviour GetRunner()
    {
        if (GameManager.instance != null)
        {
            return GameManager.instance;
        }

        if (runner == null)
        {
            GameObject runnerObject = new("Stop Runner");
            Object.DontDestroyOnLoad(runnerObject);
            runner = runnerObject.AddComponent<StopRunner>();
        }

        return runner;
    }

    private sealed class StopRunner : MonoBehaviour
    {
    }
}
