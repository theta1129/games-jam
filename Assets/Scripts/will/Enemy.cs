using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Enemy : MonoBehaviour
{
    [SerializeField] private HorizontalLayoutGroup healthGroup;
    [SerializeField] private List<Sprite> healthSprites;
    public List<ColorType> pattern { get; private set; }

    [SerializeField] private EnemyData testenemydata;
    [SerializeField] private SpriteRenderer healthPointPrefab;
    [SerializeField] private float playerKnockbackForce = 8f;

    private List<SpriteRenderer> healthPoints = new();

    void OnEnable()
    {
        Setup(testenemydata);
    }

    public void Setup(EnemyData enemyData)
    {
        pattern = new(enemyData.pattern);
        UpdateHealth();
    }


    public void OnHit(ColorType colorType)
    {
        if (pattern[0] == colorType)
        {
            pattern.Remove(colorType);
            UpdateHealth();
            if (pattern.Count == 0)
            {
                Death();
            }
        }
        else
        {
            Player.Instance?.KnockBack(transform.position, playerKnockbackForce);
        }
    }

    private IEnumerator Attack()
    {
        yield break;
    }

    private void Death()
    {
        Destroy(gameObject);
    }

    private void UpdateHealth()
    {
        foreach (var h in healthPoints) Destroy(h.gameObject);
        healthPoints.Clear();
        foreach (var color in pattern)
        {
            SpriteRenderer hP = Instantiate(healthPointPrefab, healthGroup.transform);
            hP.sprite = healthSprites[(int)color];
            healthPoints.Add(hP);
        }
    }
}
