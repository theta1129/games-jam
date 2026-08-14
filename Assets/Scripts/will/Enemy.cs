using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public List<ColorType> pattern { get; private set; }

    public void Setup(EnemyData enemyData)
    {
        pattern = new(enemyData.pattern);
    }


    public void OnHit(ColorType colorType)
    {
        if (pattern[0] == colorType)
        {
            pattern.Remove(colorType);
            if (pattern.Count == 0)
            {
                Death();
            }
        }
        else
        {
            // Player KnockBack
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
}
