using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Enemy")]
public class EnemyData : ScriptableObject
{
    [SerializeField] public int patternAmount { get; private set; }
    [SerializeField] public List<ColorType> pattern;
}
