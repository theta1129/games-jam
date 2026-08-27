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


    // =========================================================
    // Hit / Stun
    // =========================================================

    [Header("Hit / Stun")]

    [SerializeField]
    private float postHitAttackLockDuration = 0.30f;

    [SerializeField]
    private float redStunDuration = 0.70f;

    [SerializeField]
    private float blueStunDuration = 0.45f;

    [SerializeField]
    private float yellowStunDuration = 1.00f;

    [SerializeField]
    private float yellowThrowBonusStunDuration = 0.70f;


    // =========================================================
    // Knockback
    // =========================================================

    [Header("Default Knockback")]

    [SerializeField]
    private float defaultRedKnockback = 14f;

    [SerializeField]
    private float defaultBlueKnockback = 10f;

    [SerializeField]
    private float defaultYellowKnockback = 9f;


    // =========================================================
    // Hit Shake
    // =========================================================

    [Header("Hit Shake")]

    [SerializeField]
    private float redShakeIntensity = 0.35f;

    [SerializeField]
    private float blueShakeIntensity = 0.16f;

    [SerializeField]
    private float yellowShakeIntensity = 0.20f;


    // =========================================================
    // Hit Slow Motion
    // =========================================================

    [Header("Hit Slow Motion")]

    [SerializeField, Range(0.1f, 1f)]
    private float hitSlowTimeScale = 0.55f;

    [SerializeField]
    private float hitSlowDuration = 0.09f;


    // =========================================================
    // Animation
    // =========================================================

    [Header("Animation")]

    [SerializeField]
    private Animator animator;

    [SerializeField]
    private float hitAnimationPauseDuration = 0.12f;


    // Animator State 이름
    private static readonly int EnemyIdleHash =
        Animator.StringToHash("Base Layer.enemyidle");

    private static readonly int EnemyWalkHash =
        Animator.StringToHash("Base Layer.enemywalk");

    private static readonly int EnemyAttackHash =
        Animator.StringToHash("Base Layer.enemyattack");

    private static readonly int EnemyDeathHash =
        Animator.StringToHash("Base Layer.enemydeath");


    // =========================================================
    // Runtime
    // =========================================================

    private readonly List<SpriteRenderer> healthPoints = new();

    private EnemyMovement movement;
    private EnemyAttack attack;

    private Coroutine animationPauseRoutine;
    private Coroutine deathRoutine;

    private float normalAnimatorSpeed = 1f;

    private int currentAnimationHash;

    private bool isDying;


    public bool IsDying => isDying;


    // =========================================================
    // Awake
    // =========================================================

    private void Awake()
    {
        movement =
            GetComponent<EnemyMovement>();


        if (movement == null)
        {
            movement =
                gameObject.AddComponent<EnemyMovement>();
        }


        attack =
            GetComponent<EnemyAttack>();


        if (attack == null)
        {
            attack =
                gameObject.AddComponent<EnemyAttack>();
        }


        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>(true);
        }


        if (animator != null)
        {
            normalAnimatorSpeed =
                animator.speed;
        }
    }


    // =========================================================
    // Enable
    // =========================================================

    private void OnEnable()
    {
        isDying = false;

        Setup(testenemydata);

        PlayIdleAnimation();
    }


    // =========================================================
    // Setup
    // =========================================================

    public void Setup(
        EnemyData enemyData
    )
    {
        pattern =
            enemyData != null

                ? enemyData.GeneratePattern()

                : new List<ColorType>
                {
                    ColorType.Red,
                    ColorType.Blue,
                    ColorType.Yellow
                };


        UpdateHealth();
    }


    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        if (isDying)
        {
            return;
        }


        Player player =
            Player.Instance;


        bool canAttack =
            movement != null
            &&
            !movement.IsStunned;


        // 공격 로직
        attack?.Tick(
            player,
            canAttack
        );


        // =====================================================
        // 공격 중
        // =====================================================

        if (
            attack != null
            &&
            attack.IsPreparingAttack
        )
        {
            // 공격하는 동안 플레이어 쪽 바라보기
            movement?.FacePlayer(player);

            PlayAttackAnimation();

            return;
        }


        // =====================================================
        // 평상시 이동
        // =====================================================

        movement?.Tick(player);


        if (
            movement != null
            &&
            movement.IsMoving
        )
        {
            PlayWalkAnimation();
        }
        else
        {
            PlayIdleAnimation();
        }
    }


    // =========================================================
    // Hit
    // =========================================================

    public bool OnHit(
        ColorType colorType,
        Vector2 sourcePosition,
        float knockbackForce
    )
    {
        if (isDying)
        {
            return false;
        }


        if (
            pattern == null
            ||
            pattern.Count == 0
        )
        {
            return false;
        }


        // =====================================================
        // 색깔 정답 여부
        // =====================================================

        bool correctColor =
            pattern[0] ==
            colorType;


        if (correctColor)
        {
            pattern.RemoveAt(0);
        }


        bool willDie =
            correctColor
            &&
            pattern.Count == 0;


        // =====================================================
        // 피격 반응
        // =====================================================

        Stun(
            postHitAttackLockDuration
        );


        float finalKnockback =
            GetEffectiveKnockback(
                colorType,
                knockbackForce
            );


        if (
            finalKnockback > 0f
            &&
            !willDie
        )
        {
            movement?.KnockBack(
                sourcePosition,
                finalKnockback
            );
        }


        // 죽는 공격이 아니라면 피격 프리즈
        if (!willDie)
        {
            PauseAnimationOnHit();
        }


        Stop.Slow(
            hitSlowDuration,
            hitSlowTimeScale
        );


        HitBurstVfx.Spawn(
            transform.position,
            colorType
        );


        HitFlash flash =
            GetComponent<HitFlash>()
            ??
            gameObject.AddComponent<HitFlash>();


        flash.Flash(
            Color.white,
            0.08f
        );


        CombatCameraController.RegisterHit(
            transform
        );


        PlayHitReaction(
            colorType
        );


        // =====================================================
        // 체력 UI 갱신
        // =====================================================

        if (correctColor)
        {
            UpdateHealth();
        }


        // =====================================================
        // 사망
        // =====================================================

        if (willDie)
        {
            BeginDeath();
        }


        // 틀린 색이어도 실제 공격 접촉은 성공 처리
        return true;
    }


    // =========================================================
    // Knockback
    // =========================================================

    private float GetEffectiveKnockback(
        ColorType colorType,
        float requestedKnockback
    )
    {
        if (requestedKnockback > 0f)
        {
            return requestedKnockback;
        }


        return colorType switch
        {
            ColorType.Blue =>
                defaultBlueKnockback,

            ColorType.Yellow =>
                defaultYellowKnockback,

            _ =>
                defaultRedKnockback,
        };
    }


    // =========================================================
    // Stun
    // =========================================================

    public void Stun(
        float duration
    )
    {
        if (
            movement == null
            ||
            isDying
        )
        {
            return;
        }


        movement.Stun(
            Mathf.Max(
                0f,
                duration
            )
        );
    }


    public void ApplyYellowThrowStun(
        float baseStunDuration
    )
    {
        if (isDying)
        {
            return;
        }


        float totalStun =
            Mathf.Max(
                0f,
                baseStunDuration
            )
            +
            Mathf.Max(
                0f,
                yellowThrowBonusStunDuration
            );


        Stun(totalStun);
    }


    // =========================================================
    // Hit Reaction
    // =========================================================

    private void PlayHitReaction(
        ColorType colorType
    )
    {
        if (isDying)
        {
            return;
        }


        switch (colorType)
        {
            case ColorType.Red:

                Stun(
                    redStunDuration
                );

                ShakeCamera(
                    redShakeIntensity
                );

                break;


            case ColorType.Blue:

                Stun(
                    blueStunDuration
                );

                ShakeCamera(
                    blueShakeIntensity
                );

                break;


            case ColorType.Yellow:

                Stun(
                    yellowStunDuration
                );

                ShakeCamera(
                    yellowShakeIntensity
                );

                break;
        }
    }


    // =========================================================
    // Hit Animation Pause
    // =========================================================

    private void PauseAnimationOnHit()
    {
        if (isDying)
        {
            return;
        }


        ResolveAnimator();


        if (animator == null)
        {
            return;
        }


        if (animationPauseRoutine != null)
        {
            StopCoroutine(
                animationPauseRoutine
            );
        }


        animationPauseRoutine =
            StartCoroutine(
                PauseAnimationRoutine()
            );
    }


    private IEnumerator PauseAnimationRoutine()
    {
        animator.speed = 0f;


        yield return
            new WaitForSecondsRealtime(
                hitAnimationPauseDuration
            );


        if (
            animator != null
            &&
            !isDying
        )
        {
            animator.speed =
                normalAnimatorSpeed;
        }


        animationPauseRoutine =
            null;
    }


    // =========================================================
    // Animation
    // =========================================================

    private void PlayIdleAnimation()
    {
        PlayAnimation(
            EnemyIdleHash,
            "enemyidle"
        );
    }


    private void PlayWalkAnimation()
    {
        PlayAnimation(
            EnemyWalkHash,
            "enemywalk"
        );
    }


    private void PlayAttackAnimation()
    {
        PlayAnimation(
            EnemyAttackHash,
            "enemyattack"
        );
    }


    private void PlayAnimation(
        int stateHash,
        string stateName
    )
    {
        if (isDying)
        {
            return;
        }


        ResolveAnimator();


        if (animator == null)
        {
            return;
        }


        // 같은 애니메이션을 매 프레임 Play하면
        // 첫 프레임으로 계속 초기화되므로 막음.
        if (
            currentAnimationHash ==
            stateHash
        )
        {
            return;
        }


        if (
            !animator.HasState(
                0,
                stateHash
            )
        )
        {
            // enemyidle은 아직 없을 수도 있으므로
            // idle만 오류 출력하지 않음.
            if (
                stateHash !=
                EnemyIdleHash
            )
            {
                Debug.LogWarning(
                    $"Enemy Animator에 '{stateName}' State가 없습니다.",
                    this
                );
            }

            return;
        }


        animator.Play(
            stateHash,
            0,
            0f
        );


        currentAnimationHash =
            stateHash;
    }


    private void ResolveAnimator()
    {
        if (animator != null)
        {
            return;
        }


        animator =
            GetComponentInChildren<Animator>(
                true
            );


        if (animator != null)
        {
            normalAnimatorSpeed =
                animator.speed;
        }
    }


    // =========================================================
    // Death
    // =========================================================

    private void BeginDeath()
    {
        if (isDying)
        {
            return;
        }


        isDying = true;


        // 공격 즉시 중단
        attack?.CancelAll();


        // 이동 / 넉백 즉시 중단
        movement?.StopImmediately();


        // 피격 때문에 Animator가 멈춰있었다면 해제
        if (animationPauseRoutine != null)
        {
            StopCoroutine(
                animationPauseRoutine
            );

            animationPauseRoutine =
                null;
        }


        ResolveAnimator();


        if (animator != null)
        {
            animator.speed =
                normalAnimatorSpeed;
        }


        // 더 이상 맞거나 충돌하지 않도록 Collider 끄기
        Collider2D[] colliders =
            GetComponentsInChildren<Collider2D>(
                true
            );


        foreach (
            Collider2D collider
            in colliders
        )
        {
            if (collider != null)
            {
                collider.enabled =
                    false;
            }
        }


        if (deathRoutine != null)
        {
            StopCoroutine(
                deathRoutine
            );
        }


        deathRoutine =
            StartCoroutine(
                DeathRoutine()
            );
    }


    private IEnumerator DeathRoutine()
    {
        ResolveAnimator();


        // Animator가 없거나
        // enemydeath State가 없는 경우
        if (
            animator == null
            ||
            !animator.HasState(
                0,
                EnemyDeathHash
            )
        )
        {
            Debug.LogWarning(
                "Enemy Animator에 'enemydeath' State가 없습니다.",
                this
            );


            Destroy(gameObject);

            yield break;
        }


        // =====================================================
        // Death 애니메이션 시작
        // =====================================================

        currentAnimationHash =
            EnemyDeathHash;


        animator.Play(
            EnemyDeathHash,
            0,
            0f
        );


        // 즉시 State 반영
        animator.Update(0f);


        // 한 프레임 기다려 State 정보 확정
        yield return null;


        AnimatorStateInfo stateInfo =
            animator.GetCurrentAnimatorStateInfo(
                0
            );


        float deathAnimationLength =
            Mathf.Max(
                0.05f,
                stateInfo.length
            );


        float speed =
            Mathf.Max(
                0.01f,
                Mathf.Abs(
                    animator.speed
                )
            );


        deathAnimationLength /=
            speed;


        // =====================================================
        // Death 애니메이션 끝날 때까지 기다림
        // =====================================================

        yield return
            new WaitForSeconds(
                deathAnimationLength
            );


        Destroy(gameObject);
    }


    // =========================================================
    // Camera Shake
    // =========================================================

    private static void ShakeCamera(
        float intensity
    )
    {
        Camera gameCamera =
            Camera.main;


        if (gameCamera == null)
        {
            return;
        }


        CameraShake shake =
            gameCamera.GetComponent<CameraShake>()
            ??
            gameCamera.gameObject.AddComponent<CameraShake>();


        shake.ShakeScreen(
            0.18f,
            intensity
        );
    }


    // =========================================================
    // Health UI
    // =========================================================

    private void UpdateHealth()
    {
        foreach (
            SpriteRenderer healthPoint
            in healthPoints
        )
        {
            if (healthPoint != null)
            {
                Destroy(
                    healthPoint.gameObject
                );
            }
        }


        healthPoints.Clear();


        if (
            pattern == null
            ||
            healthGroup == null
            ||
            healthPointPrefab == null
        )
        {
            return;
        }


        foreach (
            ColorType color
            in pattern
        )
        {
            int spriteIndex =
                (int)color;


            if (
                healthSprites == null
                ||
                spriteIndex < 0
                ||
                spriteIndex >=
                healthSprites.Count
            )
            {
                continue;
            }


            SpriteRenderer healthPoint =
                Instantiate(
                    healthPointPrefab,
                    healthGroup.transform
                );


            healthPoint.sprite =
                healthSprites[
                    spriteIndex
                ];


            healthPoints.Add(
                healthPoint
            );
        }
    }


    // =========================================================
    // Inspector
    // =========================================================

    private void OnValidate()
    {
        postHitAttackLockDuration =
            Mathf.Max(
                0f,
                postHitAttackLockDuration
            );


        redStunDuration =
            Mathf.Max(
                postHitAttackLockDuration,
                redStunDuration
            );


        blueStunDuration =
            Mathf.Max(
                postHitAttackLockDuration,
                blueStunDuration
            );


        yellowStunDuration =
            Mathf.Max(
                postHitAttackLockDuration,
                yellowStunDuration
            );


        yellowThrowBonusStunDuration =
            Mathf.Max(
                0f,
                yellowThrowBonusStunDuration
            );


        defaultRedKnockback =
            Mathf.Max(
                0f,
                defaultRedKnockback
            );


        defaultBlueKnockback =
            Mathf.Max(
                0f,
                defaultBlueKnockback
            );


        defaultYellowKnockback =
            Mathf.Max(
                0f,
                defaultYellowKnockback
            );


        hitSlowTimeScale =
            Mathf.Clamp(
                hitSlowTimeScale,
                0.1f,
                1f
            );


        hitSlowDuration =
            Mathf.Max(
                0f,
                hitSlowDuration
            );


        hitAnimationPauseDuration =
            Mathf.Max(
                0f,
                hitAnimationPauseDuration
            );
    }
}