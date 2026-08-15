using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Enemy))]
public class YshyshEnemySetup : MonoBehaviour
{
    [SerializeField] private List<ColorType> pattern = new() { ColorType.Red };

    private void Awake()
    {
        EnemyData data = ScriptableObject.CreateInstance<EnemyData>();
        data.pattern = new List<ColorType>(pattern);

        if (!TryGetComponent(out Enemy enemy))
        {
            return;
        }

        enemy.Setup(data);
    }
}
