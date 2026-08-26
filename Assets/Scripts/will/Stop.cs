using System.Collections;
using UnityEngine;

public static class Stop
{
    private static MonoBehaviour runner;

    private static Coroutine timeRoutine;


    // 완전 정지 종료 시각
    private static float pauseEndRealtime = -1f;

    // 슬로우 종료 시각
    private static float slowEndRealtime = -1f;

    // 현재 적용할 슬로우 배율
    private static float slowScale = 1f;


    // =========================================================
    // 완전 정지
    // =========================================================

    public static void Pause(
        float duration
    )
    {
        if (duration <= 0f)
        {
            return;
        }


        float newEndTime =
            Time.unscaledTime +
            duration;


        pauseEndRealtime =
            Mathf.Max(
                pauseEndRealtime,
                newEndTime
            );


        EnsureTimeRoutine();
    }


    // =========================================================
    // 슬로우 모션
    //
    // duration = 실제 시간
    // scale = 게임 속도
    //
    // 예:
    // Stop.Slow(0.1f, 0.5f);
    // =========================================================

    public static void Slow(
        float duration,
        float scale
    )
    {
        if (duration <= 0f)
        {
            return;
        }


        float safeScale =
            Mathf.Clamp(
                scale,
                0.05f,
                1f
            );


        float now =
            Time.unscaledTime;


        // 이전 슬로우가 이미 끝난 상태라면
        // 새 슬로우 배율 사용
        if (now >= slowEndRealtime)
        {
            slowScale =
                safeScale;
        }
        else
        {
            // 여러 슬로우가 겹치면
            // 더 느린 쪽을 우선
            slowScale =
                Mathf.Min(
                    slowScale,
                    safeScale
                );
        }


        slowEndRealtime =
            Mathf.Max(
                slowEndRealtime,
                now + duration
            );


        EnsureTimeRoutine();
    }


    // =========================================================
    // Coroutine 시작
    // =========================================================

    private static void EnsureTimeRoutine()
    {
        if (timeRoutine != null)
        {
            return;
        }


        timeRoutine =
            GetRunner()
                .StartCoroutine(
                    TimeRoutine()
                );
    }


    // =========================================================
    // 실제 TimeScale 관리
    // =========================================================

    private static IEnumerator TimeRoutine()
    {
        while (true)
        {
            float now =
                Time.unscaledTime;


            bool pauseActive =
                now <
                pauseEndRealtime;


            bool slowActive =
                now <
                slowEndRealtime;


            // 아무 효과도 남지 않음
            if (
                !pauseActive &&
                !slowActive
            )
            {
                break;
            }


            // ==========================================
            // Pause가 Slow보다 우선
            // ==========================================

            if (pauseActive)
            {
                Time.timeScale =
                    0f;
            }
            else if (slowActive)
            {
                Time.timeScale =
                    slowScale;
            }


            yield return null;
        }


        // 정상 속도 복귀
        Time.timeScale =
            1f;


        pauseEndRealtime =
            -1f;


        slowEndRealtime =
            -1f;


        slowScale =
            1f;


        timeRoutine =
            null;
    }


    // =========================================================
    // Runner
    // =========================================================

    private static MonoBehaviour GetRunner()
    {
        if (GameManager.instance != null)
        {
            return
                GameManager.instance;
        }


        if (runner == null)
        {
            GameObject runnerObject =
                new(
                    "Stop Runner"
                );


            Object.DontDestroyOnLoad(
                runnerObject
            );


            runner =
                runnerObject
                    .AddComponent<StopRunner>();
        }


        return runner;
    }


    private sealed class StopRunner
        : MonoBehaviour
    {
    }
}