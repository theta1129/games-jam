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
    // 게임오버 연출
    // =========================

    private IEnumerator GameOver()
    {
        // 적 소환 중지
        summon = false;


        Camera cam =
            Camera.main;


        Vector3 cameraStartPosition =
            cam.transform.position;


        float cameraStartSize =
            cam.orthographicSize;


        float startAlpha =
            black.color.a;


        // 게임 정지
        Time.timeScale = 0f;


        float timer = 0f;


        while (timer < gameOverDuration)
        {
            // timeScale이 0이므로
            // unscaledDeltaTime 사용
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


            // 카메라 Z값 유지
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
            // 검은 화면
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


        // 확실하게 알파 1
        Color finalColor =
            black.color;

        finalColor.a = 1f;

        black.color =
            finalColor;


        // 다음 씬에서 게임이 멈춘 상태로 시작하지 않게 복구
        Time.timeScale = 1f;


        // 메인 씬
        SceneManager.LoadScene(
            "LobbyScene"
        );
    }
}