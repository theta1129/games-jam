using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float moveDeadZone = 0.12f;
    [SerializeField] private ColorType attackColor = ColorType.Red;
    [SerializeField] private PlayerAttackHitBox attackHitBox;
    [SerializeField] private float batSwingDuration = 0.2f;
    [SerializeField] private float swordSwingDuration = 1f / 60f;
    [SerializeField] private float swordDashThrustDuration = 0.16f;
    [SerializeField] private float spearPullbackDuration = 0.08f;
    [SerializeField] private float spearThrustDuration = 0.1f;
    [Header("Attack tuning")]
    [SerializeField] private float comboResetTime = 0.45f;
    [SerializeField] private float weaponRecoverDuration = 0.14f;
    [SerializeField] private float redSwingArc = 180f;
    [SerializeField] private float blueSwingArc = 130f;
    [SerializeField] private float redFirstKnockbackForce = 24f;
    [SerializeField] private float redSecondKnockbackForce = 30f;
    [SerializeField] private float redStepSpeed = 3.4f;
    [SerializeField] private float blueStepSpeed = 4.4f;
    [SerializeField] private float yellowStepSpeed = 3.8f;
    [SerializeField] private float attackStepDuration = 0.08f;
    [SerializeField] private float blueDashSpeed = 18f;
    [SerializeField] private float blueDashReach = 2.35f;
    [SerializeField] private float spearThrustReach = 1.9f;
    [SerializeField] private float spearPullbackDistance = 0.35f;
    [SerializeField] private float yellowThrowCooldown = 2f;
    [SerializeField] private float yellowThrowSpeed = 12f;
    [SerializeField] private float yellowThrowStunDuration = 1.5f;
    [SerializeField] private float yellowThrowWindup = 0.22f;
    [SerializeField] private float knockbackDuration = 0.15f;
    [Header("Health")]
    [SerializeField] private int maxHealth = 4;
    [SerializeField] private Sprite redHeartSprite;
    [SerializeField] private Sprite blueHeartSprite;
    [SerializeField] private Sprite yellowHeartSprite;
    [SerializeField] private float heartHudSize = 38f;
    [SerializeField] private float heartHudSpacing = 8f;
    [Header("Player hit feedback")]
    [SerializeField] private float receivedHitStop = 0.06f;
    [SerializeField] private float receivedHitShakeIntensity = 0.25f;
    [Header("HUD")]
    [SerializeField] private bool showColorWheel = true;
    [SerializeField] private float colorWheelRotateDuration = 0.18f;
    [SerializeField] private InputActionAsset inputActions;

    private const string MoveActionName = "Player/Move";
    private const string AttackActionName = "Player/Attack";
    private const float InputThreshold = 0.001f;
    private static readonly ColorType[] AttackColorCycle = { ColorType.Yellow, ColorType.Blue, ColorType.Red };
    private static readonly Dictionary<ColorType, Texture2D> colorWheelTextures = new();
    private static Texture2D colorWheelOutlineTexture;

    private readonly Dictionary<PlayerStates, State> states = new();
    private Rigidbody2D rb;
    private InputAction moveAction;
    private InputAction attackAction;
    private State activeState;
    private Vector2 moveInput;
    private Vector2 facingDirection = Vector2.right;
    private Vector2 knockbackVelocity;
    private Vector2 attackDashVelocity;
    private float attackEndTime;
    private float knockbackEndTime;
    private float attackDashEndTime;
    private float lastAttackTime = -999f;
    private float nextYellowThrowTime;
    private float colorWheelAnimationStartTime = -999f;
    private int colorWheelAnimationDirection;
    private int currentHealth;
    private int comboStep;
    private int queuedColorSwitchSteps;
    private bool isIgnoringDashEnemyCollision;
    private bool isThrowingYellowArm;

    public PlayerStates CurrentState { get; private set; }
    public ColorType CurrentAttackColor => attackColor;
    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    internal Vector2 MoveInput => moveInput;
    internal bool HasMoveInput => moveInput.sqrMagnitude > InputThreshold;
    internal bool IsAttackComplete => Time.time >= attackEndTime;

    public ColorType GetCurrentAttackColor() => attackColor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;

        if (attackHitBox == null)
        {
            attackHitBox = GetComponentInChildren<PlayerAttackHitBox>(true);
        }

        attackHitBox?.SetColor(attackColor);

        CreateStates();
        ChangeState(PlayerStates.Idle);
        ResolveInputActions();
    }

    private void Start()
    {
        CombatCameraController.Ensure(Camera.main, transform);
    }

    private void OnEnable()
    {
        ResolveInputActions();

        moveAction?.Enable();

        if (attackAction != null)
        {
            attackAction.performed += OnAttackPerformed;
            attackAction.Enable();
        }
    }

    private void OnDisable()
    {
        if (attackAction != null)
        {
            attackAction.performed -= OnAttackPerformed;
            attackAction.Disable();
        }

        moveAction?.Disable();
        SetDashEnemyCollisionIgnored(false);
    }

    private void Update()
    {
        HandleColorSelection();
        TryApplyQueuedColorSwitch();

        Vector2 mouseDirection = GetMouseAttackDirection();
        if (mouseDirection.sqrMagnitude > InputThreshold)
        {
            facingDirection = mouseDirection.normalized;
            attackHitBox?.Aim(facingDirection);
        }

        if (attackColor == ColorType.Yellow && Mouse.current?.rightButton.wasPressedThisFrame == true)
        {
            TryStartAttack(true);
        }

        // A completed swing/thrust holds briefly for combo input, then eases back without an attack arc.
        if (comboStep != 0 && Time.time > lastAttackTime + comboResetTime)
        {
            comboStep = 0;
            attackHitBox?.RecoverToRestPose(facingDirection, weaponRecoverDuration);
        }

        TryApplyQueuedColorSwitch();
    }

    private void FixedUpdate()
    {
        moveInput = ReadMoveInput();

        if (Time.time < knockbackEndTime)
        {
            rb.linearVelocity = knockbackVelocity;
            return;
        }

        if (Time.time < attackDashEndTime)
        {
            rb.linearVelocity = attackDashVelocity;
            return;
        }

        SetDashEnemyCollisionIgnored(false);
        activeState?.UpdateState();
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        TryStartAttack(false);
    }

    private void TryStartAttack(bool rightClick)
    {
        if (CurrentState == PlayerStates.Attack || isThrowingYellowArm || attackHitBox == null) return;
        if (rightClick && (attackColor != ColorType.Yellow || Time.time < nextYellowThrowTime)) return;

        if (rightClick)
        {
            StartCoroutine(ThrowYellowArmRoutine());
            return;
        }

        ChangeState(PlayerStates.Attack);
    }

    private void CreateStates()
    {
        states[PlayerStates.Idle] = new IdleState(this);
        states[PlayerStates.Move] = new MoveState(this);
        states[PlayerStates.Attack] = new AttackState(this);
    }

    public void ChangeState(PlayerStates nextState)
    {
        if (!states.TryGetValue(nextState, out State next))
        {
            Debug.LogError($"No player state is registered for {nextState}.", this);
            return;
        }

        activeState?.ExitState();
        activeState = next;
        CurrentState = nextState;
        activeState.EnterState();
    }

    internal void Move()
    {
        rb.linearVelocity = Vector2.ClampMagnitude(moveInput, 1f) * moveSpeed;
    }

    internal void StopMovement()
    {
        rb.linearVelocity = Vector2.zero;
    }

    public void KnockBack(Vector2 sourcePosition, float force)
    {
        Vector2 direction = ((Vector2)transform.position - sourcePosition).normalized;
        if (direction.sqrMagnitude <= InputThreshold)
        {
            direction = -facingDirection;
        }

        knockbackVelocity = direction * force;
        knockbackEndTime = Time.time + knockbackDuration;
    }

    public void ReceiveHit(Vector2 sourcePosition, float force)
    {
        Damage(1);
        KnockBack(sourcePosition, force);
        Stop.Pause(receivedHitStop);
        HitBurstVfx.Spawn(transform.position, attackColor);
        HitFlash flash = GetComponent<HitFlash>() ?? gameObject.AddComponent<HitFlash>();
        flash.Flash(Color.white, 0.09f);

        Camera gameCamera = Camera.main;
        if (gameCamera != null)
        {
            CameraShake shake = gameCamera.GetComponent<CameraShake>() ?? gameCamera.gameObject.AddComponent<CameraShake>();
            shake.ShakeScreen(0.14f, receivedHitShakeIntensity);
        }
    }

    internal void PerformAttack()
    {
        Vector2 attackDirection = GetMouseAttackDirection();
        if (attackDirection.sqrMagnitude > InputThreshold)
        {
            facingDirection = attackDirection.normalized;
        }

        if (Time.time > lastAttackTime + comboResetTime) comboStep = 0;

        int steps = attackColor switch
        {
            ColorType.Blue => 3,
            ColorType.Red => 2,
            _ => 1,
        };
        int step = comboStep % steps;
        comboStep++;
        lastAttackTime = Time.time;

        switch (attackColor)
        {
            case ColorType.Blue when step == 2:
                ShakeCamera(0.08f, 0.08f);
                StartAttackDash(facingDirection, blueDashSpeed, swordDashThrustDuration);
                attackHitBox.Thrust(0f, swordDashThrustDuration, attackColor, facingDirection, 0f, true, blueDashReach, 0f);
                attackEndTime = Time.time + swordDashThrustDuration;
                break;
            case ColorType.Blue:
                ShakeCamera(0.055f, 0.045f);
                StartAttackDash(facingDirection, blueStepSpeed, attackStepDuration);
                attackHitBox.Swing(0f, swordSwingDuration, attackColor, facingDirection, blueSwingArc, 0f, false, step % 2 == 1);
                attackEndTime = Time.time + swordSwingDuration;
                break;
            case ColorType.Yellow:
                ShakeCamera(0.065f, 0.06f);
                StartAttackDash(facingDirection, yellowStepSpeed, attackStepDuration);
                attackHitBox.Thrust(spearPullbackDuration, spearThrustDuration, attackColor, facingDirection, 0f, false, spearThrustReach, spearPullbackDistance);
                attackEndTime = Time.time + spearPullbackDuration + spearThrustDuration;
                break;
            default:
                ShakeCamera(0.09f, 0.1f);
                StartAttackDash(facingDirection, redStepSpeed, attackStepDuration);
                float knockback = step == 0 ? redFirstKnockbackForce : redSecondKnockbackForce;
                bool isSecondRedSwing = step % 2 == 1;
                attackHitBox.Swing(0f, batSwingDuration, attackColor, facingDirection, redSwingArc, knockback, false, isSecondRedSwing, isSecondRedSwing, weaponRecoverDuration);
                attackEndTime = Time.time + batSwingDuration + (isSecondRedSwing ? weaponRecoverDuration : 0f);
                break;
        }
    }

    private void StartAttackDash(Vector2 direction, float speed, float duration)
    {
        Vector2 dashDirection = direction.sqrMagnitude > InputThreshold ? direction.normalized : facingDirection;
        attackDashVelocity = dashDirection * speed;
        attackDashEndTime = Time.time + duration;
        SetDashEnemyCollisionIgnored(true);
    }

    private void SetDashEnemyCollisionIgnored(bool ignored)
    {
        if (isIgnoringDashEnemyCollision == ignored)
        {
            return;
        }

        int playerLayer = LayerMask.NameToLayer("Player");
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (playerLayer >= 0 && enemyLayer >= 0)
        {
            Physics2D.IgnoreLayerCollision(playerLayer, enemyLayer, ignored);
        }

        isIgnoringDashEnemyCollision = ignored;
    }

    private System.Collections.IEnumerator ThrowYellowArmRoutine()
    {
        Vector2 direction = GetMouseAttackDirection().normalized;
        if (direction.sqrMagnitude <= InputThreshold) direction = facingDirection;
        isThrowingYellowArm = true;
        attackHitBox.ReleaseComboPose(direction);
        ShakeCamera(0.075f, 0.07f);
        StartAttackDash(direction, yellowStepSpeed, attackStepDuration);
        attackHitBox.ThrowMotion(direction, yellowThrowWindup);
        yield return new WaitForSeconds(yellowThrowWindup);
        ThrownArmProjectile.Create(transform.position + (Vector3)(direction * 0.9f), direction, yellowThrowSpeed, yellowThrowStunDuration, attackHitBox.WeaponSprite);
        nextYellowThrowTime = Time.time + yellowThrowCooldown;
        isThrowingYellowArm = false;
    }

    private Vector2 GetMouseAttackDirection()
    {
        if (Mouse.current == null || Camera.main == null)
        {
            return facingDirection;
        }

        Vector3 mousePosition = Mouse.current.position.ReadValue();
        mousePosition.z = Mathf.Abs(Camera.main.transform.position.z - transform.position.z);
        Vector3 mouseWorldPosition = Camera.main.ScreenToWorldPoint(mousePosition);
        return (Vector2)(mouseWorldPosition - transform.position);
    }

    private Vector2 ReadMoveInput()
    {
        if (moveAction == null)
        {
            return Vector2.zero;
        }

        Vector2 input = moveAction.ReadValue<Vector2>();
        return input.sqrMagnitude < moveDeadZone * moveDeadZone
            ? Vector2.zero
            : Vector2.ClampMagnitude(input, 1f);
    }

    private void HandleColorSelection()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            RequestColorSwitch(1);
        }

        if (Keyboard.current.qKey.wasPressedThisFrame)
        {
            RequestColorSwitch(-1);
        }
    }

    private void RequestColorSwitch(int direction)
    {
        int normalizedDirection = NormalizeColorSwitchSteps(direction);
        if (normalizedDirection == 0)
        {
            return;
        }

        if (IsColorSwitchLocked())
        {
            queuedColorSwitchSteps = NormalizeColorSwitchSteps(queuedColorSwitchSteps + normalizedDirection);
            return;
        }

        CycleAttackColor(normalizedDirection);
    }

    private void TryApplyQueuedColorSwitch()
    {
        if (queuedColorSwitchSteps == 0 || IsColorSwitchLocked())
        {
            return;
        }

        int direction = queuedColorSwitchSteps;
        queuedColorSwitchSteps = 0;
        CycleAttackColor(direction);
    }

    private bool IsColorSwitchLocked()
    {
        return CurrentState == PlayerStates.Attack
            || isThrowingYellowArm
            || (attackHitBox != null && attackHitBox.IsBusy);
    }

    private void Damage(int amount)
    {
        if (amount <= 0 || currentHealth <= 0)
        {
            return;
        }

        currentHealth = Mathf.Max(0, currentHealth - amount);
    }

    private void CycleAttackColor(int direction)
    {
        direction = NormalizeColorSwitchSteps(direction);
        if (direction == 0) return;

        int currentIndex = Array.IndexOf(AttackColorCycle, attackColor);
        if (currentIndex < 0) currentIndex = 0;
        attackColor = AttackColorCycle[(currentIndex + direction + AttackColorCycle.Length) % AttackColorCycle.Length];
        colorWheelAnimationDirection = Math.Sign(direction);
        colorWheelAnimationStartTime = Time.unscaledTime;
        comboStep = 0;
        attackHitBox?.SetColor(attackColor);
        attackHitBox?.RecoverToRestPose(facingDirection, weaponRecoverDuration * 0.5f);
    }

    private static int NormalizeColorSwitchSteps(int steps)
    {
        int colorCount = AttackColorCycle.Length;
        steps %= colorCount;
        if (steps > colorCount / 2) steps -= colorCount;
        if (steps < -colorCount / 2) steps += colorCount;
        return steps;
    }

    private static void ShakeCamera(float duration, float intensity)
    {
        Camera gameCamera = Camera.main;
        if (gameCamera == null) return;

        CameraShake shake = gameCamera.GetComponent<CameraShake>() ?? gameCamera.gameObject.AddComponent<CameraShake>();
        shake.ShakeScreen(duration, intensity);
    }

    private void OnGUI()
    {
        float scale = Mathf.Clamp(Screen.height / 720f, 0.75f, 1.25f);
        DrawHealthHud(scale);

        if (!showColorWheel) return;

        float orbSize = 52f * scale;
        float activeOrbSize = 58f * scale;
        Vector2 wheelCenter = new(100f * scale, Screen.height - 82f * scale);
        float wheelRadius = 62f * scale;

        int activeIndex = Array.IndexOf(AttackColorCycle, attackColor);
        if (activeIndex < 0) activeIndex = 0;

        float animationProgress = colorWheelRotateDuration <= 0f
            ? 1f
            : Mathf.Clamp01((Time.unscaledTime - colorWheelAnimationStartTime) / colorWheelRotateDuration);
        float angleOffset = -(1f - animationProgress) * colorWheelAnimationDirection * 120f;

        foreach (ColorType colorType in AttackColorCycle)
        {
            bool active = colorType == attackColor;
            float angle = GetWheelSlotAngle(colorType, activeIndex) + angleOffset;
            Vector2 position = wheelCenter + AngleToScreenDirection(angle) * wheelRadius;
            DrawColorOrb(position, colorType, active, active ? activeOrbSize : orbSize, scale);
        }

        GUIStyle keyStyle = new(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(22f * scale),
            fontStyle = FontStyle.Bold,
        };
        keyStyle.normal.textColor = Color.white;
        GUI.Label(new Rect(wheelCenter.x - 38f * scale, wheelCenter.y + 58f * scale, 34f * scale, 30f * scale), "Q", keyStyle);
        GUI.Label(new Rect(wheelCenter.x + 4f * scale, wheelCenter.y + 58f * scale, 34f * scale, 30f * scale), "E", keyStyle);
    }

    private void DrawHealthHud(float scale)
    {
        Sprite heartSprite = GetHeartSprite(attackColor);
        if (heartSprite == null)
        {
            return;
        }

        float height = heartHudSize * scale;
        float spacing = heartHudSpacing * scale;
        float width = height * heartSprite.rect.width / heartSprite.rect.height;
        Vector2 start = new(28f * scale, 24f * scale);

        for (int i = 0; i < maxHealth; i++)
        {
            Rect rect = new(start.x + i * (width + spacing), start.y, width, height);
            DrawSpriteInGui(heartSprite, rect, i < currentHealth ? 1f : 0.18f);
        }
    }

    private Sprite GetHeartSprite(ColorType colorType) => colorType switch
    {
        ColorType.Blue => blueHeartSprite,
        ColorType.Yellow => yellowHeartSprite,
        _ => redHeartSprite,
    };

    private static float GetWheelSlotAngle(ColorType colorType, int activeIndex)
    {
        int colorIndex = Array.IndexOf(AttackColorCycle, colorType);
        int relativeIndex = (colorIndex - activeIndex + AttackColorCycle.Length) % AttackColorCycle.Length;
        return relativeIndex switch
        {
            0 => 90f,
            1 => -30f,
            _ => 210f,
        };
    }

    private static Vector2 AngleToScreenDirection(float angle)
    {
        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), -Mathf.Sin(radians));
    }

    private static void DrawColorOrb(Vector2 center, ColorType colorType, bool active, float size, float scale)
    {
        if (active)
        {
            float outlineSize = size + 10f * scale;
            Rect outlineRect = new(center.x - outlineSize * 0.5f, center.y - outlineSize * 0.5f, outlineSize, outlineSize);
            GUI.DrawTexture(outlineRect, GetCircleTexture(new Color(1f, 1f, 1f, 0.85f), ref colorWheelOutlineTexture));
        }

        Rect rect = new(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
        GUI.DrawTexture(rect, GetColorWheelTexture(colorType));

        GUIStyle labelStyle = new(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt((active ? 18f : 16f) * scale),
            fontStyle = FontStyle.Bold,
        };
        labelStyle.normal.textColor = Color.black;
        GUI.Label(rect, GetColorLetter(colorType), labelStyle);
    }

    private static void DrawSpriteInGui(Sprite sprite, Rect rect, float alpha)
    {
        if (sprite == null || sprite.texture == null) return;

        Rect textureRect = sprite.textureRect;
        Texture2D texture = sprite.texture;
        Rect textureCoords = new(
            textureRect.x / texture.width,
            textureRect.y / texture.height,
            textureRect.width / texture.width,
            textureRect.height / texture.height);

        Color previousColor = GUI.color;
        GUI.color = new Color(1f, 1f, 1f, alpha);
        GUI.DrawTextureWithTexCoords(rect, texture, textureCoords, true);
        GUI.color = previousColor;
    }

    private static Texture2D GetColorWheelTexture(ColorType colorType)
    {
        if (!colorWheelTextures.TryGetValue(colorType, out Texture2D texture))
        {
            texture = CreateCircleTexture(GetHudColor(colorType));
            colorWheelTextures[colorType] = texture;
        }

        return texture;
    }

    private static Texture2D GetCircleTexture(Color color, ref Texture2D texture)
    {
        texture ??= CreateCircleTexture(color);
        return texture;
    }

    private static Texture2D CreateCircleTexture(Color color)
    {
        const int textureSize = 64;
        Texture2D texture = new(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        Vector2 center = new((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
        float radius = textureSize * 0.47f;
        Color[] pixels = new Color[textureSize * textureSize];
        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(radius + 1f - distance) * color.a;
                pixels[y * textureSize + x] = new Color(color.r, color.g, color.b, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Color GetHudColor(ColorType colorType) => colorType switch
    {
        ColorType.Blue => new Color(0.45f, 0.9f, 1f, 0.96f),
        ColorType.Yellow => new Color(1f, 0.87f, 0.28f, 0.96f),
        _ => new Color(1f, 0.32f, 0.26f, 0.96f),
    };

    private static string GetColorLetter(ColorType colorType) => colorType switch
    {
        ColorType.Blue => "B",
        ColorType.Yellow => "Y",
        _ => "R",
    };

    private void ResolveInputActions()
    {
        InputActionAsset actions = inputActions != null ? inputActions : InputSystem.actions;

        if (actions == null)
        {
            return;
        }

        moveAction ??= actions.FindAction(MoveActionName, false);
        attackAction ??= actions.FindAction(AttackActionName, false);
    }
}
