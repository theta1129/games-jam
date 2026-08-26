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

    [Header("Hit / Stun")]
    [SerializeField] private float postHitAttackLockDuration = 0.30f;
    [SerializeField] private float redStunDuration = 0.70f;
    [SerializeField] private float blueStunDuration = 0.45f;
    [SerializeField] private float yellowStunDuration = 1.00f;
    [SerializeField] private float yellowThrowBonusStunDuration = 0.70f;

    [Header("Default Knockback")]
    [Tooltip("Used when the attack itself passes 0 knockback.")]
    [SerializeField] private float defaultRedKnockback = 14f;
    [SerializeField] private float defaultBlueKnockback = 10f;
    [SerializeField] private float defaultYellowKnockback = 9f;

    [Header("Hit Shake")]
    [SerializeField] private float redShakeIntensity = 0.35f;
    [SerializeField] private float blueShakeIntensity = 0.16f;
    [SerializeField] private float yellowShakeIntensity = 0.20f;

    [Header("Hit Slow Motion")]
    [SerializeField, Range(0.1f, 1f)] private float hitSlowTimeScale = 0.55f;
    [SerializeField] private float hitSlowDuration = 0.09f;

    [Header("Animation")]
    [SerializeField] private Animator animator;
    [SerializeField] private float hitAnimationPauseDuration = 0.12f;

    private readonly List<SpriteRenderer> healthPoints = new();
    private EnemyMovement movement;
    private EnemyAttack attack;
    private Coroutine animationPauseRoutine;
    private float normalAnimatorSpeed = 1f;

    private void Awake()
    {
        movement = GetComponent<EnemyMovement>();
        if (movement == null) movement = gameObject.AddComponent<EnemyMovement>();

        attack = GetComponent<EnemyAttack>();
        if (attack == null) attack = gameObject.AddComponent<EnemyAttack>();

        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator != null) normalAnimatorSpeed = animator.speed;
    }

    private void OnEnable()
    {
        Setup(testenemydata);
    }

    public void Setup(EnemyData enemyData)
    {
        pattern = enemyData != null
            ? enemyData.GeneratePattern()
            : new List<ColorType> { ColorType.Red, ColorType.Blue, ColorType.Yellow };

        UpdateHealth();
    }

    private void Update()
    {
        Player player = Player.Instance;
        bool canAttack = !movement.IsStunned;

        attack.Tick(player, canAttack);

        // During the red warning/windup the enemy stays still so the warning area
        // does not chase the player.
        if (!attack.IsPreparingAttack)
        {
            movement.Tick(player);
        }
    }

    public bool OnHit(ColorType colorType, Vector2 sourcePosition, float knockbackForce)
    {
        if (pattern == null || pattern.Count == 0) return false;

        // The color check controls ONLY health loss.
        bool correctColor = pattern[0] == colorType;
        if (correctColor)
        {
            pattern.RemoveAt(0);
        }

        // Everything below is a contact reaction and therefore happens for both
        // correct-color and wrong-color hits.
        Stun(postHitAttackLockDuration);

        float finalKnockback = GetEffectiveKnockback(colorType, knockbackForce);
        if (finalKnockback > 0f)
        {
            movement.KnockBack(sourcePosition, finalKnockback);
        }

        PauseAnimationOnHit();
        Stop.Slow(hitSlowDuration, hitSlowTimeScale);

        HitBurstVfx.Spawn(transform.position, colorType);

        HitFlash flash = GetComponent<HitFlash>() ?? gameObject.AddComponent<HitFlash>();
        flash.Flash(Color.white, 0.08f);

        // Every enemy hit by an area attack is registered independently.
        CombatCameraController.RegisterHit(transform);

        PlayHitReaction(colorType);

        if (correctColor)
        {
            UpdateHealth();
        }

        if (correctColor && pattern.Count == 0)
        {
            Death();
        }

        // A wrong-color hit is still a real hit. Returning true lets
        // PlayerAttackHitBox mark this enemy as already hit for this attack,
        // preventing repeated knockback/effects every frame.
        return true;
    }

    private float GetEffectiveKnockback(ColorType colorType, float requestedKnockback)
    {
        if (requestedKnockback > 0f) return requestedKnockback;

        return colorType switch
        {
            ColorType.Blue => defaultBlueKnockback,
            ColorType.Yellow => defaultYellowKnockback,
            _ => defaultRedKnockback,
        };
    }

    public void Stun(float duration)
    {
        if (movement == null) return;
        movement.Stun(Mathf.Max(0f, duration));
    }

    public void ApplyYellowThrowStun(float baseStunDuration)
    {
        float totalStun = Mathf.Max(0f, baseStunDuration)
                        + Mathf.Max(0f, yellowThrowBonusStunDuration);
        Stun(totalStun);
    }

    private void PlayHitReaction(ColorType colorType)
    {
        switch (colorType)
        {
            case ColorType.Red:
                Stun(redStunDuration);
                ShakeCamera(redShakeIntensity);
                break;

            case ColorType.Blue:
                Stun(blueStunDuration);
                ShakeCamera(blueShakeIntensity);
                break;

            case ColorType.Yellow:
                Stun(yellowStunDuration);
                ShakeCamera(yellowShakeIntensity);
                break;
        }
    }

    private void PauseAnimationOnHit()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
        if (animator == null) return;

        if (animationPauseRoutine != null)
        {
            StopCoroutine(animationPauseRoutine);
        }

        animationPauseRoutine = StartCoroutine(PauseAnimationRoutine());
    }

    private IEnumerator PauseAnimationRoutine()
    {
        animator.speed = 0f;
        yield return new WaitForSecondsRealtime(hitAnimationPauseDuration);

        if (animator != null)
        {
            animator.speed = normalAnimatorSpeed;
        }

        animationPauseRoutine = null;
    }

    private static void ShakeCamera(float intensity)
    {
        Camera gameCamera = Camera.main;
        if (gameCamera == null) return;

        CameraShake shake = gameCamera.GetComponent<CameraShake>()
                         ?? gameCamera.gameObject.AddComponent<CameraShake>();
        shake.ShakeScreen(0.18f, intensity);
    }

    private void Death()
    {
        Destroy(gameObject);
    }

    private void UpdateHealth()
    {
        foreach (SpriteRenderer healthPoint in healthPoints)
        {
            if (healthPoint != null)
            {
                Destroy(healthPoint.gameObject);
            }
        }

        healthPoints.Clear();

        if (pattern == null || healthGroup == null || healthPointPrefab == null) return;

        foreach (ColorType color in pattern)
        {
            int spriteIndex = (int)color;
            if (healthSprites == null || spriteIndex < 0 || spriteIndex >= healthSprites.Count) continue;

            SpriteRenderer healthPoint = Instantiate(healthPointPrefab, healthGroup.transform);
            healthPoint.sprite = healthSprites[spriteIndex];
            healthPoints.Add(healthPoint);
        }
    }

    private void OnValidate()
    {
        postHitAttackLockDuration = Mathf.Max(0f, postHitAttackLockDuration);
        redStunDuration = Mathf.Max(postHitAttackLockDuration, redStunDuration);
        blueStunDuration = Mathf.Max(postHitAttackLockDuration, blueStunDuration);
        yellowStunDuration = Mathf.Max(postHitAttackLockDuration, yellowStunDuration);
        yellowThrowBonusStunDuration = Mathf.Max(0f, yellowThrowBonusStunDuration);

        defaultRedKnockback = Mathf.Max(0f, defaultRedKnockback);
        defaultBlueKnockback = Mathf.Max(0f, defaultBlueKnockback);
        defaultYellowKnockback = Mathf.Max(0f, defaultYellowKnockback);

        hitSlowTimeScale = Mathf.Clamp(hitSlowTimeScale, 0.1f, 1f);
        hitSlowDuration = Mathf.Max(0f, hitSlowDuration);
        hitAnimationPauseDuration = Mathf.Max(0f, hitAnimationPauseDuration);
    }
}
