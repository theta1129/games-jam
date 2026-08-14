using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Enemy : MonoBehaviour
{
    public List<ColorType> patterns { get; private set; }


    public void OnHit(ColorType colorType)
    {
        if (patterns[0] == colorType)
        {
            patterns.Remove(colorType);
            if (patterns.Count == 0)
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
