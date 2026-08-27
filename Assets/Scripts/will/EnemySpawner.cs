using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class EnemySpawner : MonoBehaviour
{
    private bool summon = true;
    private float summonCooldown = 0f;

    [SerializeField] private GameObject enemyPrefab;

    private List<GameObject> enemies = new();
    [SerializeField] private int maxEnemyCount = 5;
    [SerializeField] private Image black;


    // =========================
    // 게임오버
    // =========================

    [SerializeField] private float zoomSize = 2.5f;
    [SerializeField] private float gameOverDuration = 1.5f;

    private bool gameOver = false;


    void Start()
    {
        StartCoroutine(FadeOut());
    }


    private IEnumerator FadeOut()
    {
        while (black.color.a > 0)
        {
            Color color = black.color;
            color.a -= 0.05f;
            black.color = color;

            yield return new WaitForSeconds(0.05f);
        }
    }


    void Update()
    {
        // =========================
        // 플레이어 사망 확인
        // =========================

        if (
            !gameOver &&
            Player.Instance != null &&
            Player.Instance.CurrentHealth <= 0
        )
        {
            gameOver = true;
            StartCoroutine(GameOver());
            return;
        }


        if (gameOver)
        {
            return;
        }


        if (summon)
        {
            summonCooldown -= Time.deltaTime;

            if (summonCooldown <= 0)
            {
                Summon();

                summonCooldown =
                    UnityEngine.Random.Range(3, 6);
            }
        }
    }


    private void Summon()
    {
        List<GameObject> tmp = new();

        foreach (var e in enemies)
        {
            if (e != null)
            {
                tmp.Add(e);
            }
        }

        enemies = tmp;


        if (enemies.Count > maxEnemyCount)
        {
            return;
        }


        var enemy =
            Instantiate(enemyPrefab);


        float angle =
            UnityEngine.Random.Range(0, 360);


        enemy.transform.position =
            Player.Instance.transform.position
            +
            (
                15 *
                new Vector3(
                    (float)Math.Cos(angle),
                    (float)Math.Sin(angle),
                    0
                )
            );


        enemies.Add(enemy);
    }


    // =========================
    // 게임오버
    // =========================

    private IEnumerator GameOver()
    {
        // 적 소환 중단
        summon = false;


        // =========================
        // 플레이어 죽음 애니메이션
        // =========================

        Animator playerAnimator =
            Player.Instance.GetComponentInChildren<Animator>(true);


        if (playerAnimator != null)
        {
            // 게임이 멈춰도 Animator는 계속 움직이게 함
            playerAnimator.updateMode =
                AnimatorUpdateMode.UnscaledTime;


            // 현재 플레이어 색
            ColorType currentColor =
                Player.Instance.CurrentAttackColor;


            // =====================
            // RED
            // =====================

            if (currentColor == ColorType.Red)
            {
                playerAnimator.Play(
                    "Base Layer.deathRed",
                    0,
                    0f
                );
            }


            // =====================
            // YELLOW
            // deathyellow 정확한 이름
            // =====================

            else if (currentColor == ColorType.Yellow)
            {
                playerAnimator.Play(
                    "Base Layer.deathyellow",
                    0,
                    0f
                );
            }


            // =====================
            // BLUE
            // deathblue 정확한 이름
            // =====================

            else if (currentColor == ColorType.Blue)
            {
                playerAnimator.Play(
                    "Base Layer.deathblue",
                    0,
                    0f
                );
            }


            // 바로 첫 프레임 적용
            playerAnimator.Update(0f);
        }


        // =========================
        // 카메라
        // =========================

        Camera cam =
            Camera.main;


        Vector3 cameraStartPosition =
            cam.transform.position;


        float cameraStartSize =
            cam.orthographicSize;


        float startAlpha =
            black.color.a;


        // =========================
        // 게임 정지
        // =========================

        Time.timeScale = 0f;


        float timer = 0f;


        while (timer < gameOverDuration)
        {
            // TimeScale = 0이어도 진행
            timer += Time.unscaledDeltaTime;


            float t =
                Mathf.Clamp01(
                    timer / gameOverDuration
                );


            // =========================
            // 카메라가 플레이어 쪽으로 이동
            // =========================

            Vector3 targetPosition =
                Player.Instance.transform.position;


            // 기존 카메라 Z 유지
            targetPosition.z =
                cameraStartPosition.z;


            cam.transform.position =
                Vector3.Lerp(
                    cameraStartPosition,
                    targetPosition,
                    t
                );


            // =========================
            // 줌인
            // =========================

            cam.orthographicSize =
                Mathf.Lerp(
                    cameraStartSize,
                    zoomSize,
                    t
                );


            // =========================
            // Black Fade In
            // =========================

            Color color =
                black.color;


            color.a =
                Mathf.Lerp(
                    startAlpha,
                    1f,
                    t
                );


            black.color =
                color;


            yield return null;
        }


        // =========================
        // 완전히 검게
        // =========================

        Color finalColor =
            black.color;


        finalColor.a =
            1f;


        black.color =
            finalColor;


        // =========================
        // TimeScale 복구
        // =========================

        Time.timeScale =
            1f;


        // =========================
        // Lobby 이동
        // =========================

        SceneManager.LoadScene(
            "LobbyScene"
        );
    }
}