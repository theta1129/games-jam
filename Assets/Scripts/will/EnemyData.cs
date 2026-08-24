using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Enemy")]
public class EnemyData : ScriptableObject
{
    [SerializeField] private bool generatePattern = true;
    [SerializeField, Min(1)] private int minGeneratedHealth = 3;
    [SerializeField, Min(1)] private int maxGeneratedHealth = 7;
    [SerializeField] private ColorWeights firstColorWeights = new(1f, 1f, 1f);
    [SerializeField] private ColorWeights redNextColorWeights = new(0.2f, 0.55f, 0.25f);
    [SerializeField] private ColorWeights blueNextColorWeights = new(0.25f, 0.2f, 0.55f);
    [SerializeField] private ColorWeights yellowNextColorWeights = new(0.55f, 0.25f, 0.2f);
    [SerializeField] public List<ColorType> pattern = new();

    public List<ColorType> GeneratePattern()
    {
        if (!generatePattern && pattern != null && pattern.Count > 0)
        {
            return new List<ColorType>(pattern);
        }

        int minHealth = Mathf.Max(1, minGeneratedHealth);
        int maxHealth = Mathf.Max(minHealth, maxGeneratedHealth);
        int health = Random.Range(minHealth, maxHealth + 1);
        List<ColorType> generatedPattern = new(health);
        ColorType currentColor = SafeWeights(firstColorWeights).Pick();

        for (int i = 0; i < health; i++)
        {
            generatedPattern.Add(currentColor);
            currentColor = GetNextWeights(currentColor).Pick();
        }

        return generatedPattern;
    }

    private ColorWeights GetNextWeights(ColorType previousColor) => previousColor switch
    {
        ColorType.Blue => SafeWeights(blueNextColorWeights),
        ColorType.Yellow => SafeWeights(yellowNextColorWeights),
        _ => SafeWeights(redNextColorWeights),
    };

    private static ColorWeights SafeWeights(ColorWeights weights) => weights ?? new ColorWeights(1f, 1f, 1f);

    private void OnValidate()
    {
        minGeneratedHealth = Mathf.Max(1, minGeneratedHealth);
        maxGeneratedHealth = Mathf.Clamp(maxGeneratedHealth, minGeneratedHealth, 99);
        firstColorWeights ??= new ColorWeights(1f, 1f, 1f);
        redNextColorWeights ??= new ColorWeights(0.2f, 0.55f, 0.25f);
        blueNextColorWeights ??= new ColorWeights(0.25f, 0.2f, 0.55f);
        yellowNextColorWeights ??= new ColorWeights(0.55f, 0.25f, 0.2f);
        pattern ??= new List<ColorType>();
    }

    [System.Serializable]
    private sealed class ColorWeights
    {
        [Min(0f)] public float red;
        [Min(0f)] public float blue;
        [Min(0f)] public float yellow;

        public ColorWeights(float red, float blue, float yellow)
        {
            this.red = red;
            this.blue = blue;
            this.yellow = yellow;
        }

        public ColorType Pick()
        {
            float redWeight = Mathf.Max(0f, red);
            float blueWeight = Mathf.Max(0f, blue);
            float yellowWeight = Mathf.Max(0f, yellow);
            float totalWeight = redWeight + blueWeight + yellowWeight;

            if (totalWeight <= 0.0001f)
            {
                return (ColorType)Random.Range(0, 3);
            }

            float roll = Random.value * totalWeight;
            if (roll < redWeight) return ColorType.Red;
            roll -= redWeight;
            return roll < blueWeight ? ColorType.Blue : ColorType.Yellow;
        }
    }
}
