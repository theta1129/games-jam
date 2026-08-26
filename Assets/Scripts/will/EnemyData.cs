using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Data/Enemy")]
public class EnemyData : ScriptableObject
{
    // =========================================================
    // 체력
    // =========================================================

    [Header("Health")]
    [Tooltip("적의 최소 체력")]
    [SerializeField, Range(3, 7)]
    private int minHealth = 3;

    [Tooltip("적의 최대 체력")]
    [SerializeField, Range(3, 7)]
    private int maxHealth = 7;


    // =========================================================
    // 첫 번째 체력 색깔
    // =========================================================

    [Header("First Color Probability")]

    [Tooltip("첫 번째 색이 빨강일 확률 가중치")]
    [SerializeField, Range(0f, 100f)]
    private float firstRed = 33.3f;

    [Tooltip("첫 번째 색이 파랑일 확률 가중치")]
    [SerializeField, Range(0f, 100f)]
    private float firstBlue = 33.3f;

    [Tooltip("첫 번째 색이 노랑일 확률 가중치")]
    [SerializeField, Range(0f, 100f)]
    private float firstYellow = 33.3f;


    // =========================================================
    // 빨강 다음 색
    // =========================================================

    [Header("After RED")]

    [Tooltip("빨강 다음에 빨강이 나올 확률")]
    [SerializeField, Range(0f, 100f)]
    private float redToRed = 20f;

    [Tooltip("빨강 다음에 파랑이 나올 확률")]
    [SerializeField, Range(0f, 100f)]
    private float redToBlue = 55f;

    [Tooltip("빨강 다음에 노랑이 나올 확률")]
    [SerializeField, Range(0f, 100f)]
    private float redToYellow = 25f;


    // =========================================================
    // 파랑 다음 색
    // =========================================================

    [Header("After BLUE")]

    [Tooltip("파랑 다음에 빨강이 나올 확률")]
    [SerializeField, Range(0f, 100f)]
    private float blueToRed = 25f;

    [Tooltip("파랑 다음에 파랑이 나올 확률")]
    [SerializeField, Range(0f, 100f)]
    private float blueToBlue = 20f;

    [Tooltip("파랑 다음에 노랑이 나올 확률")]
    [SerializeField, Range(0f, 100f)]
    private float blueToYellow = 55f;


    // =========================================================
    // 노랑 다음 색
    // =========================================================

    [Header("After YELLOW")]

    [Tooltip("노랑 다음에 빨강이 나올 확률")]
    [SerializeField, Range(0f, 100f)]
    private float yellowToRed = 55f;

    [Tooltip("노랑 다음에 파랑이 나올 확률")]
    [SerializeField, Range(0f, 100f)]
    private float yellowToBlue = 25f;

    [Tooltip("노랑 다음에 노랑이 나올 확률")]
    [SerializeField, Range(0f, 100f)]
    private float yellowToYellow = 20f;


    // =========================================================
    // 패턴 생성
    // =========================================================

    public List<ColorType> GeneratePattern()
    {
        // 무조건 3 이상 7 이하가 되도록 제한
        int safeMinHealth =
            Mathf.Clamp(minHealth, 3, 7);

        int safeMaxHealth =
            Mathf.Clamp(maxHealth, safeMinHealth, 7);

        // Random.Range int는 최대값이 포함되지 않으므로 +1
        int health =
            Random.Range(
                safeMinHealth,
                safeMaxHealth + 1
            );

        List<ColorType> generatedPattern =
            new List<ColorType>(health);


        // =========================
        // 첫 번째 색 결정
        // =========================

        ColorType currentColor =
            PickColor(
                firstRed,
                firstBlue,
                firstYellow
            );

        generatedPattern.Add(currentColor);


        // =========================
        // 두 번째 색부터
        // 이전 색을 보고 결정
        // =========================

        for (int i = 1; i < health; i++)
        {
            currentColor =
                PickNextColor(currentColor);

            generatedPattern.Add(currentColor);
        }


        return generatedPattern;
    }


    // =========================================================
    // 이전 색에 따른 다음 색 결정
    // =========================================================

    private ColorType PickNextColor(
        ColorType previousColor
    )
    {
        switch (previousColor)
        {
            // -----------------------------------------
            // 이전 색이 RED
            // -----------------------------------------

            case ColorType.Red:

                return PickColor(
                    redToRed,
                    redToBlue,
                    redToYellow
                );


            // -----------------------------------------
            // 이전 색이 BLUE
            // -----------------------------------------

            case ColorType.Blue:

                return PickColor(
                    blueToRed,
                    blueToBlue,
                    blueToYellow
                );


            // -----------------------------------------
            // 이전 색이 YELLOW
            // -----------------------------------------

            case ColorType.Yellow:

                return PickColor(
                    yellowToRed,
                    yellowToBlue,
                    yellowToYellow
                );
        }


        // 혹시 잘못된 값이 들어온 경우
        // 완전 랜덤
        return PickColor(
            1f,
            1f,
            1f
        );
    }


    // =========================================================
    // 세 색 중 확률에 따라 하나 선택
    // =========================================================

    private ColorType PickColor(
        float redChance,
        float blueChance,
        float yellowChance
    )
    {
        redChance =
            Mathf.Max(
                0f,
                redChance
            );

        blueChance =
            Mathf.Max(
                0f,
                blueChance
            );

        yellowChance =
            Mathf.Max(
                0f,
                yellowChance
            );


        float total =
            redChance +
            blueChance +
            yellowChance;


        // 셋 다 0이면
        // 1/3 확률로 랜덤
        if (total <= 0.0001f)
        {
            int random =
                Random.Range(0, 3);

            return (ColorType)random;
        }


        // 0 ~ 전체 가중치 사이 난수
        float roll =
            Random.Range(
                0f,
                total
            );


        // RED
        if (roll < redChance)
        {
            return ColorType.Red;
        }


        roll -= redChance;


        // BLUE
        if (roll < blueChance)
        {
            return ColorType.Blue;
        }


        // YELLOW
        return ColorType.Yellow;
    }


    // =========================================================
    // Inspector 값 보호
    // =========================================================

    private void OnValidate()
    {
        minHealth =
            Mathf.Clamp(
                minHealth,
                3,
                7
            );

        maxHealth =
            Mathf.Clamp(
                maxHealth,
                minHealth,
                7
            );
    }
}