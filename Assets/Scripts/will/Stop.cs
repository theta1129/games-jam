using System.Collections;
using UnityEngine;

public static class Stop
{
    private static bool isWaiting = false;


    public static void Pause(float duration)
    {
        if (isWaiting) return;
        else isWaiting = true;
        Time.timeScale = 0.0f;
        GameManager.instance.StartCoroutine(Wait(duration));
    }


    private static IEnumerator Wait(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1.0f;
        isWaiting = false;
    }

}
