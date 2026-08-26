using System;
using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    private bool summon = true;
    private float summonCooldown = 0f;

    [SerializeField] private GameObject enemyPrefab;
    void Start()
    {
        
    }

    void Update()
    {
        if (summon)
        {
            summonCooldown -= Time.deltaTime;
            if (summonCooldown <= 0)
            {
                Summon();
                summonCooldown = UnityEngine.Random.Range(5, 8);
            }
        }
    }

    private void Summon()
    {
        var enemy = Instantiate(enemyPrefab);
        float angle = UnityEngine.Random.Range(0, 360);
        enemy.transform.position = Player.Instance.transform.position + (15 * new Vector3((float)Math.Cos(angle), (float)Math.Sin(angle), 0));
    }
}
