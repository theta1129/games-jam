using UnityEngine;

[DefaultExecutionOrder(100)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Player))]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class BlueSwitchDash : MonoBehaviour
{
    [Header("Dash")]
    [SerializeField] private float dashSpeed = 24f;
    [SerializeField] private float targetSearchRadius = 12f;
    [SerializeField] private float dashDuration = 0.22f;
    [SerializeField] private float stopDistance = 0.85f;

    [Header("Invulnerability")]
    [Tooltip("파란 전환 대시가 끝난 뒤 무적 시간")]
    [SerializeField] private float postDashInvulnerabilityDuration = 0.10f;

    [Header("Blue Trail")]
    [SerializeField] private float trailTime = 0.22f;
    [SerializeField] private float trailStartWidth = 0.55f;
    [SerializeField] private float trailEndWidth = 0.03f;

    [Header("Blue Particles")]
    [SerializeField] private float particleRate = 100f;
    [SerializeField] private float particleMinSize = 0.05f;
    [SerializeField] private float particleMaxSize = 0.14f;

    [Header("Visual")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    private Player player;
    private Rigidbody2D rb;

    private ColorType previousColor;
    private bool pendingBlueDash;
    private bool isDashing;

    private Transform dashTarget;
    private float dashEndTime;

    private TrailRenderer blueTrail;
    private ParticleSystem blueParticles;

    private Material trailMaterial;
    private Material particleMaterial;

    public bool IsDashing => isDashing;

    private void Awake()
    {
        player = GetComponent<Player>();
        rb = GetComponent<Rigidbody2D>();

        previousColor = player.CurrentAttackColor;

        FindPlayerSpriteRenderer();
        CreateBlueTrail();
        CreateBlueParticles();
    }

    private void Update()
    {
        ColorType currentColor = player.CurrentAttackColor;

        // 색 선택 자체는 Player에서 즉시 바뀐다.
        if (currentColor != previousColor)
        {
            if (currentColor == ColorType.Blue)
            {
                // 공격 중에 파랑을 골랐다면 색/UI는 즉시 파랑이지만
                // 대시는 현재 공격/무기 동작이 완전히 끝난 뒤 실행한다.
                pendingBlueDash = true;
            }
            else
            {
                // 공격 종료 전에 다른 색으로 다시 바꿨다면
                // 예약됐던 파란 대시는 취소한다.
                pendingBlueDash = false;

                if (isDashing)
                {
                    EndBlueDash();
                }
            }

            previousColor = currentColor;
        }

        TryStartPendingBlueDash();
    }

    private void FixedUpdate()
    {
        if (!isDashing)
        {
            return;
        }

        if (dashTarget == null)
        {
            EndBlueDash();
            return;
        }

        if (Time.time >= dashEndTime)
        {
            EndBlueDash();
            return;
        }

        Vector2 playerPosition = rb.position;
        Vector2 targetPosition = dashTarget.position;
        Vector2 toTarget = targetPosition - playerPosition;

        float distance = toTarget.magnitude;

        if (distance <= stopDistance)
        {
            EndBlueDash();
            return;
        }

        Vector2 direction = toTarget.normalized;

        UpdateFlip(direction.x);

        // Player.FixedUpdate 이후 실행되므로 전환 대시 속도가 최종 적용된다.
        rb.linearVelocity = direction * dashSpeed;
    }

    private void TryStartPendingBlueDash()
    {
        if (!pendingBlueDash || isDashing)
        {
            return;
        }

        // 파랑을 골랐다가 공격 종료 전에 다른 색으로 바뀐 경우.
        if (player.CurrentAttackColor != ColorType.Blue)
        {
            pendingBlueDash = false;
            return;
        }

        // 핵심:
        // 공격/노란 투척/무기 복귀가 끝날 때까지만 '행동'을 기다린다.
        // 색 선택과 HUD는 이미 즉시 Blue 상태다.
        if (player.IsBusyForColorTriggeredAction)
        {
            return;
        }

        pendingBlueDash = false;
        StartBlueDash();
    }

    private void StartBlueDash()
    {
        Enemy closestEnemy = FindClosestEnemy();

        if (closestEnemy == null)
        {
            return;
        }

        Vector2 direction =
            (Vector2)closestEnemy.transform.position - rb.position;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        dashTarget = closestEnemy.transform;
        dashEndTime = Time.time + dashDuration;
        isDashing = true;

        UpdateFlip(direction.x);
        StartDashEffects();
    }

    private Enemy FindClosestEnemy()
    {
        Enemy[] enemies = FindObjectsByType<Enemy>(FindObjectsSortMode.None);

        Enemy closestEnemy = null;

        float searchRadius = Mathf.Max(0.1f, targetSearchRadius);
        float closestDistanceSqr = searchRadius * searchRadius;
        Vector2 playerPosition = rb.position;

        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled)
            {
                continue;
            }

            if (enemy.pattern == null || enemy.pattern.Count == 0)
            {
                continue;
            }

            Vector2 enemyPosition = enemy.transform.position;
            float distanceSqr = (enemyPosition - playerPosition).sqrMagnitude;

            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestEnemy = enemy;
            }
        }

        return closestEnemy;
    }

    private void EndBlueDash()
    {
        if (!isDashing)
        {
            StopDashEffects();
            return;
        }

        isDashing = false;
        dashTarget = null;

        rb.linearVelocity = Vector2.zero;

        StopDashEffects();

        // Player.ReceiveHit 자체가 이 무적을 확인하므로
        // 적 공격 방식이 바뀌어도 대시 후 무적이 유지된다.
        player.GrantInvulnerability(postDashInvulnerabilityDuration);
    }

    private void CreateBlueTrail()
    {
        GameObject trailObject = new GameObject("Blue Switch Trail");
        trailObject.transform.SetParent(transform, false);

        blueTrail = trailObject.AddComponent<TrailRenderer>();

        blueTrail.time = trailTime;
        blueTrail.startWidth = trailStartWidth;
        blueTrail.endWidth = trailEndWidth;
        blueTrail.minVertexDistance = 0.025f;
        blueTrail.numCapVertices = 5;
        blueTrail.numCornerVertices = 5;
        blueTrail.emitting = false;
        blueTrail.sortingOrder = 20;
        blueTrail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        blueTrail.receiveShadows = false;

        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.8f, 0.97f, 1f), 0f),
                new GradientColorKey(new Color(0.15f, 0.65f, 1f), 0.45f),
                new GradientColorKey(new Color(0.05f, 0.3f, 1f), 1f),
            },
            new[]
            {
                new GradientAlphaKey(0.95f, 0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0f, 1f),
            });

        blueTrail.colorGradient = gradient;

        Shader shader = Shader.Find("Sprites/Default") ?? Shader.Find("Default");

        if (shader != null)
        {
            trailMaterial = new Material(shader)
            {
                name = "Blue Dash Trail Material",
            };

            blueTrail.material = trailMaterial;
        }
    }

    private void CreateBlueParticles()
    {
        GameObject particleObject = new GameObject("Blue Switch Particles");
        particleObject.transform.SetParent(transform, false);

        blueParticles = particleObject.AddComponent<ParticleSystem>();
        blueParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = blueParticles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(particleMinSize, particleMaxSize);
        main.startColor = new ParticleSystem.MinMaxGradient(
            new Color(0.15f, 0.65f, 1f, 1f),
            new Color(0.7f, 0.95f, 1f, 0.85f));
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = blueParticles.emission;
        emission.rateOverTime = particleRate;

        ParticleSystem.ShapeModule shape = blueParticles.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.22f;

        ParticleSystemRenderer renderer = blueParticles.GetComponent<ParticleSystemRenderer>();
        renderer.sortingOrder = 21;

        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
            ?? Shader.Find("Particles/Standard Unlit")
            ?? Shader.Find("Sprites/Default");

        if (shader != null)
        {
            particleMaterial = new Material(shader)
            {
                name = "Blue Dash Particle Material",
            };

            renderer.material = particleMaterial;
        }
    }

    private void StartDashEffects()
    {
        if (blueTrail != null)
        {
            blueTrail.time = trailTime;
            blueTrail.startWidth = trailStartWidth;
            blueTrail.endWidth = trailEndWidth;
            blueTrail.Clear();
            blueTrail.emitting = true;
        }

        if (blueParticles != null)
        {
            ParticleSystem.EmissionModule emission = blueParticles.emission;
            emission.rateOverTime = particleRate;

            blueParticles.Emit(14);
            blueParticles.Play();
        }
    }

    private void StopDashEffects()
    {
        if (blueTrail != null)
        {
            blueTrail.emitting = false;
        }

        if (blueParticles != null)
        {
            blueParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void FindPlayerSpriteRenderer()
    {
        if (playerSpriteRenderer != null)
        {
            return;
        }

        playerSpriteRenderer = GetComponent<SpriteRenderer>();

        if (playerSpriteRenderer != null)
        {
            return;
        }

        PlayerAttackHitBox attackHitBox = GetComponentInChildren<PlayerAttackHitBox>(true);
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer spriteRenderer in renderers)
        {
            if (spriteRenderer == null)
            {
                continue;
            }

            if (
                attackHitBox != null
                && spriteRenderer.transform.IsChildOf(attackHitBox.transform))
            {
                continue;
            }

            playerSpriteRenderer = spriteRenderer;
            break;
        }
    }

    private void UpdateFlip(float horizontalDirection)
    {
        if (Mathf.Abs(horizontalDirection) <= 0.001f)
        {
            return;
        }

        FindPlayerSpriteRenderer();

        if (playerSpriteRenderer == null)
        {
            return;
        }

        playerSpriteRenderer.flipX = horizontalDirection < 0f;
    }

    private void OnDisable()
    {
        pendingBlueDash = false;
        isDashing = false;
        dashTarget = null;

        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
        }

        StopDashEffects();
    }

    private void OnDestroy()
    {
        if (trailMaterial != null)
        {
            Destroy(trailMaterial);
        }

        if (particleMaterial != null)
        {
            Destroy(particleMaterial);
        }
    }

    private void OnValidate()
    {
        dashSpeed = Mathf.Max(0f, dashSpeed);
        targetSearchRadius = Mathf.Max(0.1f, targetSearchRadius);
        dashDuration = Mathf.Max(0.01f, dashDuration);
        stopDistance = Mathf.Max(0.05f, stopDistance);
        postDashInvulnerabilityDuration = Mathf.Max(0f, postDashInvulnerabilityDuration);

        trailTime = Mathf.Max(0.02f, trailTime);
        trailStartWidth = Mathf.Max(0f, trailStartWidth);
        trailEndWidth = Mathf.Max(0f, trailEndWidth);

        particleRate = Mathf.Max(0f, particleRate);
        particleMinSize = Mathf.Max(0.001f, particleMinSize);
        particleMaxSize = Mathf.Max(particleMinSize, particleMaxSize);
    }
}
