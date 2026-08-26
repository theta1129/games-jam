using System.Collections.Generic;
using UnityEngine;

public sealed class EnemyAttack : MonoBehaviour
{
    // =========================================================
    // Attack
    // =========================================================

    [Header("Melee Attack")]

    [Tooltip("이 거리 안으로 플레이어가 들어오면 공격 준비를 시작합니다.")]
    [SerializeField]
    private float detectionRange = 1.8f;

    [Tooltip("붉은 원의 크기 = 실제 공격 범위입니다.")]
    [SerializeField]
    private float attackRadius = 1.45f;

    [Tooltip("붉은 범위가 나타난 뒤 실제 공격까지 걸리는 시간")]
    [SerializeField]
    private float attackWindup = 0.42f;

    [Tooltip("공격 후 다음 공격까지 대기 시간")]
    [SerializeField]
    private float attackCooldown = 1.15f;

    [Tooltip("공격 준비 중 스턴을 당해 취소되었을 때 재공격까지 짧은 대기시간")]
    [SerializeField]
    private float interruptedRecovery = 0.25f;

    [Tooltip("플레이어가 맞았을 때 넉백")]
    [SerializeField]
    private float knockbackForce = 7f;


    // =========================================================
    // Warning Visual
    // =========================================================

    [Header("Red Warning Area")]

    [Tooltip("경고 원의 기본 투명도")]
    [SerializeField, Range(0f, 1f)]
    private float warningStartAlpha = 0.18f;

    [Tooltip("공격 직전 경고 원의 투명도")]
    [SerializeField, Range(0f, 1f)]
    private float warningEndAlpha = 0.52f;

    [Tooltip("경고 범위 Sprite의 Sorting Order")]
    [SerializeField]
    private int warningSortingOrder = -2;


    // =========================================================
    // Runtime
    // =========================================================

    private bool isPreparingAttack;

    private float attackExecuteTime;
    private float attackStartTime;
    private float nextAttackTime;

    private SpriteRenderer warningRenderer;

    private static Sprite warningCircleSprite;


    public bool IsPreparingAttack =>
        isPreparingAttack;


    // =========================================================
    // Tick
    // =========================================================

    public void Tick(
        Player player,
        bool canAttack
    )
    {
        // =========================================
        // 플레이어 없음
        // =========================================

        if (player == null)
        {
            CancelAttack(false);
            return;
        }


        // =========================================
        // 스턴 등으로 공격 불가능
        // =========================================

        if (!canAttack)
        {
            CancelAttack(true);
            return;
        }


        // =========================================
        // 현재 공격 준비 중
        // =========================================

        if (isPreparingAttack)
        {
            UpdateWarningVisual();


            if (
                Time.time >=
                attackExecuteTime
            )
            {
                ExecuteAttack();


                FinishAttack();
            }


            return;
        }


        // =========================================
        // 쿨타임
        // =========================================

        if (
            Time.time <
            nextAttackTime
        )
        {
            return;
        }


        // =========================================
        // 플레이어와 거리
        // =========================================

        float distance =
            Vector2.Distance(
                transform.position,
                player.transform.position
            );


        // 아직 감지 범위 밖
        if (
            distance >
            detectionRange
        )
        {
            return;
        }


        // =========================================
        // 공격 시작
        // =========================================

        StartAttack();
    }


    // =========================================================
    // Attack Start
    // =========================================================

    private void StartAttack()
    {
        if (isPreparingAttack)
        {
            return;
        }


        isPreparingAttack =
            true;


        attackStartTime =
            Time.time;


        attackExecuteTime =
            Time.time +
            attackWindup;


        ShowWarningArea();


        UpdateWarningVisual();
    }


    // =========================================================
    // 실제 근접 범위 공격
    // =========================================================

    private void ExecuteAttack()
    {
        Vector2 center =
            transform.position;


        // =========================================
        // 빨간 원과 정확히 같은 크기로 검사
        // =========================================

        Collider2D[] hits =
            Physics2D.OverlapCircleAll(
                center,
                attackRadius
            );


        // Player가 Collider를 여러 개 가지고 있어도
        // 한 번만 맞도록 처리
        HashSet<Player> hitPlayers =
            new HashSet<Player>();


        foreach (
            Collider2D hit
            in hits
        )
        {
            if (hit == null)
            {
                continue;
            }


            Player player =
                hit.GetComponentInParent<Player>();


            if (
                player == null ||
                hitPlayers.Contains(player)
            )
            {
                continue;
            }


            hitPlayers.Add(player);


            // =========================================
            // 대시 후 무적 확인
            // =========================================

            PlayerInvulnerability invulnerability =
                player.GetComponent<PlayerInvulnerability>();


            if (
                invulnerability != null &&
                invulnerability.IsInvulnerable
            )
            {
                continue;
            }


            // =========================================
            // 실제로 범위 안에 있을 때만 데미지
            // =========================================

            player.ReceiveHit(
                center,
                knockbackForce
            );
        }
    }


    // =========================================================
    // Warning
    // =========================================================

    private void ShowWarningArea()
    {
        EnsureWarningRenderer();


        if (warningRenderer == null)
        {
            return;
        }


        // Sprite 원본의 월드 크기가 1x1이므로
        // 지름 = attackRadius * 2
        float diameter =
            attackRadius * 2f;


        warningRenderer.transform.localScale =
            new Vector3(
                diameter,
                diameter,
                1f
            );


        warningRenderer.transform.localPosition =
            Vector3.zero;


        warningRenderer.enabled =
            true;
    }


    private void UpdateWarningVisual()
    {
        if (
            warningRenderer == null ||
            !warningRenderer.enabled
        )
        {
            return;
        }


        float progress;


        if (attackWindup <= 0.001f)
        {
            progress = 1f;
        }
        else
        {
            progress =
                Mathf.Clamp01(
                    (
                        Time.time -
                        attackStartTime
                    )
                    /
                    attackWindup
                );
        }


        // 공격에 가까워질수록
        // 붉은색이 진해짐
        float alpha =
            Mathf.Lerp(
                warningStartAlpha,
                warningEndAlpha,
                progress
            );


        // 살짝 깜빡이는 효과
        float pulse =
            Mathf.Sin(
                Time.time * 28f
            )
            *
            0.035f;


        alpha =
            Mathf.Clamp01(
                alpha +
                pulse
            );


        warningRenderer.color =
            new Color(
                1f,
                0.04f,
                0.02f,
                alpha
            );
    }


    private void HideWarningArea()
    {
        if (warningRenderer != null)
        {
            warningRenderer.enabled =
                false;
        }
    }


    // =========================================================
    // Attack Finish
    // =========================================================

    private void FinishAttack()
    {
        isPreparingAttack =
            false;


        HideWarningArea();


        nextAttackTime =
            Time.time +
            attackCooldown;
    }


    // =========================================================
    // Attack Cancel
    // =========================================================

    private void CancelAttack(
        bool applyRecovery
    )
    {
        if (!isPreparingAttack)
        {
            return;
        }


        isPreparingAttack =
            false;


        HideWarningArea();


        if (applyRecovery)
        {
            nextAttackTime =
                Mathf.Max(
                    nextAttackTime,
                    Time.time +
                    interruptedRecovery
                );
        }
    }


    // =========================================================
    // Warning Renderer
    // =========================================================

    private void EnsureWarningRenderer()
    {
        if (warningRenderer != null)
        {
            return;
        }


        GameObject warningObject =
            new GameObject(
                "Enemy Attack Warning"
            );


        warningObject.transform.SetParent(
            transform,
            false
        );


        warningObject.transform.localPosition =
            Vector3.zero;


        warningRenderer =
            warningObject.AddComponent<SpriteRenderer>();


        warningRenderer.sprite =
            GetWarningCircleSprite();


        warningRenderer.color =
            new Color(
                1f,
                0.04f,
                0.02f,
                warningStartAlpha
            );


        warningRenderer.sortingOrder =
            warningSortingOrder;


        warningRenderer.enabled =
            false;
    }


    // =========================================================
    // 빨간 원 Sprite 자동 생성
    // =========================================================

    private static Sprite GetWarningCircleSprite()
    {
        if (warningCircleSprite != null)
        {
            return warningCircleSprite;
        }


        const int textureSize =
            128;


        Texture2D texture =
            new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false
            );


        texture.name =
            "Runtime Enemy Attack Warning";


        texture.filterMode =
            FilterMode.Bilinear;


        texture.wrapMode =
            TextureWrapMode.Clamp;


        Color[] pixels =
            new Color[
                textureSize *
                textureSize
            ];


        Vector2 center =
            new Vector2(
                (textureSize - 1) * 0.5f,
                (textureSize - 1) * 0.5f
            );


        float radius =
            textureSize *
            0.48f;


        float borderStart =
            radius *
            0.87f;


        for (
            int y = 0;
            y < textureSize;
            y++
        )
        {
            for (
                int x = 0;
                x < textureSize;
                x++
            )
            {
                float distance =
                    Vector2.Distance(
                        new Vector2(x, y),
                        center
                    );


                float alpha =
                    0f;


                if (distance <= radius)
                {
                    // 내부는 옅게
                    alpha =
                        0.45f;


                    // 가장자리 링은 더 진하게
                    if (
                        distance >=
                        borderStart
                    )
                    {
                        float borderProgress =
                            Mathf.InverseLerp(
                                borderStart,
                                radius,
                                distance
                            );


                        alpha =
                            Mathf.Lerp(
                                0.7f,
                                1f,
                                borderProgress
                            );
                    }


                    // 가장자리 안티앨리어싱
                    if (
                        distance >
                        radius - 1.5f
                    )
                    {
                        alpha *=
                            Mathf.Clamp01(
                                radius -
                                distance
                            );
                    }
                }


                pixels[
                    y * textureSize + x
                ] =
                    new Color(
                        1f,
                        1f,
                        1f,
                        alpha
                    );
            }
        }


        texture.SetPixels(
            pixels
        );


        texture.Apply();


        warningCircleSprite =
            Sprite.Create(
                texture,
                new Rect(
                    0f,
                    0f,
                    textureSize,
                    textureSize
                ),
                new Vector2(
                    0.5f,
                    0.5f
                ),

                // Sprite 자체의 월드 크기 = 1
                textureSize
            );


        warningCircleSprite.name =
            "Enemy Attack Warning Circle";


        return warningCircleSprite;
    }


    // =========================================================
    // Disable
    // =========================================================

    private void OnDisable()
    {
        isPreparingAttack =
            false;


        HideWarningArea();
    }


    // =========================================================
    // Inspector
    // =========================================================

    private void OnValidate()
    {
        detectionRange =
            Mathf.Max(
                0.1f,
                detectionRange
            );


        attackRadius =
            Mathf.Max(
                0.1f,
                attackRadius
            );


        attackWindup =
            Mathf.Max(
                0f,
                attackWindup
            );


        attackCooldown =
            Mathf.Max(
                0.05f,
                attackCooldown
            );


        interruptedRecovery =
            Mathf.Max(
                0f,
                interruptedRecovery
            );


        knockbackForce =
            Mathf.Max(
                0f,
                knockbackForce
            );


        warningStartAlpha =
            Mathf.Clamp01(
                warningStartAlpha
            );


        warningEndAlpha =
            Mathf.Clamp01(
                warningEndAlpha
            );
    }


#if UNITY_EDITOR

    private void OnDrawGizmosSelected()
    {
        // 실제 공격 범위
        Gizmos.DrawWireSphere(
            transform.position,
            attackRadius
        );
    }

#endif
}