using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class EnemySpawner : MonoBehaviour
{
    private bool summon = true;
    private float summonCooldown = 0f;

    [SerializeField] private GameObject enemyPrefab;

    private List<GameObject> enemies = new();
    [SerializeField] private int maxEnemyCount = 5;
    [SerializeField] private Image black;
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
        if (summon)
        {
            summonCooldown -= Time.deltaTime;
            if (summonCooldown <= 0)
            {
                Summon();
                summonCooldown = UnityEngine.Random.Range(3, 6);
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
        var enemy = Instantiate(enemyPrefab);
        float angle = UnityEngine.Random.Range(0, 360);
        enemy.transform.position = Player.Instance.transform.position + (15 * new Vector3((float)Math.Cos(angle), (float)Math.Sin(angle), 0));
        enemies.Add(enemy);
    }
}
