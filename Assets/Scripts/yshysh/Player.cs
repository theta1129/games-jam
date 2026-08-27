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

    // =========================================================
    // Animation
    // =========================================================

    [Header("Animation")]
    [SerializeField] private Animator animator;

    [Header("Red Animation Clips")]
    [SerializeField] private AnimationClip idleRed;
    [SerializeField] private AnimationClip walkRed;
    [SerializeField] private AnimationClip attackRed;

    [Header("Blue Animation Clips")]
    [SerializeField] private AnimationClip idleBlue;
    [SerializeField] private AnimationClip walkBlue;
    [SerializeField] private AnimationClip attackBlue;

    [Header("Yellow Animation Clips")]
    [SerializeField] private AnimationClip idleYellow;
    [SerializeField] private AnimationClip walkYellow;
    [SerializeField] private AnimationClip attackYellow;

    // =========================================================
    // Visual / Flip
    // =========================================================

    [Header("Visual / Flip")]
    [Tooltip("플레이어 본체 SpriteRenderer. 비워두면 자동으로 찾습니다.")]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

    // =========================================================
    // Constants
    // =========================================================

    private const string MoveActionName = "Player/Move";
    private const string AttackActionName = "Player/Attack";
    private const float InputThreshold = 0.001f;

    private static readonly ColorType[] AttackColorCycle =
    {
        ColorType.Yellow,
        ColorType.Blue,
        ColorType.Red
    };

    private static readonly Dictionary<ColorType, Texture2D>
        colorWheelTextures = new();

    // Animator에는 State를 딱 3개만 둔다.
    //
    // idle
    // walk
    // attack
    //
    // Animator.Play는 전체 경로를 사용한다.
    private static readonly int IdleAnimatorStateHash =
        Animator.StringToHash("Base Layer.idle");

    private static readonly int WalkAnimatorStateHash =
        Animator.StringToHash("Base Layer.walk");

    private static readonly int AttackAnimatorStateHash =
        Animator.StringToHash("Base Layer.attack");

    private static Texture2D colorWheelOutlineTexture;

    // =========================================================
    // Runtime
    // =========================================================

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

    private bool pendingAttackHitBoxColorSync;
    private float invulnerableUntilRealtime = -1f;

    private bool isIgnoringDashEnemyCollision;
    private bool isThrowingYellowArm;

    // 코드에서 자동으로 만드는 Override Controller.
    // Project 창에 Override Controller 에셋을 만들 필요는 없다.
    private AnimatorOverrideController runtimeOverrideController;

    // 현재 idle / walk / attack 중 무엇을 재생 중인지 저장.
    private int currentAnimatorStateHash;

    // State 이름 오류가 있을 때 Console 도배 방지.
    private bool animatorStateErrorLogged;

    // =========================================================
    // Properties
    // =========================================================

    public PlayerStates CurrentState { get; private set; }

    public ColorType CurrentAttackColor => attackColor;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    public bool IsInvulnerable =>
        Time.unscaledTime < invulnerableUntilRealtime;

    public bool IsBusyForColorTriggeredAction =>
        CurrentState == PlayerStates.Attack
        || isThrowingYellowArm
        || (attackHitBox != null && attackHitBox.IsBusy);

    internal Vector2 MoveInput => moveInput;

    internal bool HasMoveInput =>
        moveInput.sqrMagnitude > InputThreshold;

    internal bool IsAttackComplete =>
        Time.time >= attackEndTime;

    public ColorType GetCurrentAttackColor()
    {
        return attackColor;
    }

    // =========================================================
    // Awake
    // =========================================================

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        rb = GetComponent<Rigidbody2D>();

        animator ??=
            GetComponentInChildren<Animator>(true);

        rb.gravityScale = 0f;
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;

        if (attackHitBox == null)
        {
            attackHitBox =
                GetComponentInChildren<PlayerAttackHitBox>(true);
        }

        ResolvePlayerSpriteRenderer();

        // 런타임 Animation Clip 교체 기능 준비.
        SetupRuntimeAnimatorOverride();

        attackHitBox?.SetColor(attackColor);

        CreateStates();
        ChangeState(PlayerStates.Idle);

        ResolveInputActions();
    }

    // =========================================================
    // Start
    // =========================================================

    private void Start()
    {
        CombatCameraController.Ensure(
            Camera.main,
            transform
        );
    }

    // =========================================================
    // Enable
    // =========================================================

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

    // =========================================================
    // Disable
    // =========================================================

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

    // =========================================================
    // Update
    // =========================================================

    private void Update()
    {
        // 색깔 입력은 즉시 처리.
        HandleColorSelection();

        TrySyncAttackHitBoxColor();

        Vector2 mouseDirection =
            GetMouseAttackDirection();

        if (mouseDirection.sqrMagnitude > InputThreshold)
        {
            facingDirection =
                mouseDirection.normalized;

            attackHitBox?.Aim(facingDirection);
        }

        // 노랑 우클릭 투척
        if (
            attackColor == ColorType.Yellow
            &&
            Mouse.current?.rightButton.wasPressedThisFrame == true
        )
        {
            TryStartAttack(true);
        }

        // 콤보 시간 종료 시 무기 복귀
        if (
            comboStep != 0
            &&
            Time.time > lastAttackTime + comboResetTime
        )
        {
            comboStep = 0;

            attackHitBox?.RecoverToRestPose(
                facingDirection,
                weaponRecoverDuration
            );
        }

        TrySyncAttackHitBoxColor();

        UpdatePlayerAnimation();
    }

    // =========================================================
    // Fixed Update
    // =========================================================

    private void FixedUpdate()
    {
        moveInput = ReadMoveInput();

        // 넉백
        if (Time.time < knockbackEndTime)
        {
            rb.linearVelocity = knockbackVelocity;
            return;
        }

        // 공격 대시
        if (Time.time < attackDashEndTime)
        {
            rb.linearVelocity = attackDashVelocity;
            return;
        }

        SetDashEnemyCollisionIgnored(false);

        activeState?.UpdateState();
    }

    // =========================================================
    // Attack Input
    // =========================================================

    private void OnAttackPerformed(
        InputAction.CallbackContext context
    )
    {
        // Space는 오른쪽 색 변경키이므로
        // 같은 입력으로 공격까지 발동하지 않게 한다.
        if (
            Keyboard.current != null
            &&
            context.control == Keyboard.current.spaceKey
        )
        {
            return;
        }

        TryStartAttack(false);
    }

    private void TryStartAttack(bool rightClick)
    {
        if (
            CurrentState == PlayerStates.Attack
            ||
            isThrowingYellowArm
            ||
            attackHitBox == null
        )
        {
            return;
        }

        if (
            rightClick
            &&
            (
                attackColor != ColorType.Yellow
                ||
                Time.time < nextYellowThrowTime
            )
        )
        {
            return;
        }

        if (rightClick)
        {
            StartCoroutine(ThrowYellowArmRoutine());
            return;
        }

        ChangeState(PlayerStates.Attack);
    }

    // =========================================================
    // Player Logic States
    // =========================================================

    private void CreateStates()
    {
        states[PlayerStates.Idle] =
            new IdleState(this);

        states[PlayerStates.Move] =
            new MoveState(this);

        states[PlayerStates.Attack] =
            new AttackState(this);
    }

    public void ChangeState(PlayerStates nextState)
    {
        if (!states.TryGetValue(nextState, out State next))
        {
            Debug.LogError(
                $"No player state is registered for {nextState}.",
                this
            );

            return;
        }

        activeState?.ExitState();

        activeState = next;

        CurrentState = nextState;

        activeState.EnterState();

        UpdatePlayerAnimation();
    }

    // =========================================================
    // Move
    // =========================================================

    internal void Move()
    {
        rb.linearVelocity =
            Vector2.ClampMagnitude(moveInput, 1f)
            * moveSpeed;

        // 일반 이동 중에도 방향에 맞춰 Flip.
        UpdatePlayerFlip(moveInput.x);
    }

    internal void StopMovement()
    {
        rb.linearVelocity = Vector2.zero;
    }

    // =========================================================
    // Knockback
    // =========================================================

    public void KnockBack(
        Vector2 sourcePosition,
        float force
    )
    {
        Vector2 direction =
            (
                (Vector2)transform.position
                -
                sourcePosition
            ).normalized;

        if (direction.sqrMagnitude <= InputThreshold)
        {
            direction = -facingDirection;
        }

        knockbackVelocity =
            direction * force;

        knockbackEndTime =
            Time.time + knockbackDuration;
    }

    // =========================================================
    // Receive Hit
    // =========================================================

    public void ReceiveHit(
        Vector2 sourcePosition,
        float force
    )
    {
        if (IsInvulnerable)
        {
            return;
        }

        Damage(1);

        KnockBack(
            sourcePosition,
            force
        );

        Stop.Pause(receivedHitStop);

        HitBurstVfx.Spawn(
            transform.position,
            attackColor
        );

        HitFlash flash =
            GetComponent<HitFlash>()
            ??
            gameObject.AddComponent<HitFlash>();

        flash.Flash(
            Color.white,
            0.09f
        );

        Camera gameCamera =
            Camera.main;

        if (gameCamera != null)
        {
            CameraShake shake =
                gameCamera.GetComponent<CameraShake>()
                ??
                gameCamera.gameObject.AddComponent<CameraShake>();

            shake.ShakeScreen(
                0.14f,
                receivedHitShakeIntensity
            );
        }
    }

    // =========================================================
    // Invulnerability
    // =========================================================

    public void GrantInvulnerability(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        invulnerableUntilRealtime =
            Mathf.Max(
                invulnerableUntilRealtime,
                Time.unscaledTime + duration
            );
    }

    // =========================================================
    // Attack
    // =========================================================

    internal void PerformAttack()
    {
        Vector2 attackDirection =
            GetMouseAttackDirection();

        if (attackDirection.sqrMagnitude > InputThreshold)
        {
            facingDirection =
                attackDirection.normalized;

            // 공격할 때는 마우스 방향으로 플레이어 Flip.
            UpdatePlayerFlip(facingDirection.x);
        }

        if (Time.time > lastAttackTime + comboResetTime)
        {
            comboStep = 0;
        }

        int steps =
            attackColor switch
            {
                ColorType.Blue => 3,
                ColorType.Red => 2,
                _ => 1,
            };

        int step =
            comboStep % steps;

        comboStep++;

        lastAttackTime =
            Time.time;

        switch (attackColor)
        {
            // =================================================
            // BLUE 3타
            // =================================================

            case ColorType.Blue when step == 2:

                ShakeCamera(
                    0.08f,
                    0.08f
                );

                StartAttackDash(
                    facingDirection,
                    blueDashSpeed,
                    swordDashThrustDuration
                );

                attackHitBox.Thrust(
                    0f,
                    swordDashThrustDuration,
                    attackColor,
                    facingDirection,
                    0f,
                    true,
                    blueDashReach,
                    0f
                );

                attackEndTime =
                    Time.time + swordDashThrustDuration;

                break;

            // =================================================
            // BLUE 1 / 2타
            // =================================================

            case ColorType.Blue:

                ShakeCamera(
                    0.055f,
                    0.045f
                );

                StartAttackDash(
                    facingDirection,
                    blueStepSpeed,
                    attackStepDuration
                );

                attackHitBox.Swing(
                    0f,
                    swordSwingDuration,
                    attackColor,
                    facingDirection,
                    blueSwingArc,
                    0f,
                    false,
                    step % 2 == 1
                );

                attackEndTime =
                    Time.time + swordSwingDuration;

                break;

            // =================================================
            // YELLOW
            // =================================================

            case ColorType.Yellow:

                ShakeCamera(
                    0.065f,
                    0.06f
                );

                StartAttackDash(
                    facingDirection,
                    yellowStepSpeed,
                    attackStepDuration
                );

                attackHitBox.Thrust(
                    spearPullbackDuration,
                    spearThrustDuration,
                    attackColor,
                    facingDirection,
                    0f,
                    false,
                    spearThrustReach,
                    spearPullbackDistance
                );

                attackEndTime =
                    Time.time
                    + spearPullbackDuration
                    + spearThrustDuration;

                break;

            // =================================================
            // RED
            // =================================================

            default:

                ShakeCamera(
                    0.09f,
                    0.1f
                );

                StartAttackDash(
                    facingDirection,
                    redStepSpeed,
                    attackStepDuration
                );

                float knockback =
                    step == 0
                        ? redFirstKnockbackForce
                        : redSecondKnockbackForce;

                bool isSecondRedSwing =
                    step % 2 == 1;

                attackHitBox.Swing(
                    0f,
                    batSwingDuration,
                    attackColor,
                    facingDirection,
                    redSwingArc,
                    knockback,
                    false,
                    isSecondRedSwing,
                    isSecondRedSwing,
                    weaponRecoverDuration
                );

                attackEndTime =
                    Time.time
                    + batSwingDuration
                    + (
                        isSecondRedSwing
                            ? weaponRecoverDuration
                            : 0f
                    );

                break;
        }
    }

    // =========================================================
    // Attack Dash
    // =========================================================

    private void StartAttackDash(
        Vector2 direction,
        float speed,
        float duration
    )
    {
        Vector2 dashDirection =
            direction.sqrMagnitude > InputThreshold
                ? direction.normalized
                : facingDirection;

        attackDashVelocity =
            dashDirection * speed;

        attackDashEndTime =
            Time.time + duration;

        SetDashEnemyCollisionIgnored(true);
    }

    private void SetDashEnemyCollisionIgnored(
        bool ignored
    )
    {
        if (isIgnoringDashEnemyCollision == ignored)
        {
            return;
        }

        int playerLayer =
            LayerMask.NameToLayer("Player");

        int enemyLayer =
            LayerMask.NameToLayer("Enemy");

        if (
            playerLayer >= 0
            &&
            enemyLayer >= 0
        )
        {
            Physics2D.IgnoreLayerCollision(
                playerLayer,
                enemyLayer,
                ignored
            );
        }

        isIgnoringDashEnemyCollision =
            ignored;
    }

    // =========================================================
    // Yellow Throw
    // =========================================================

    private System.Collections.IEnumerator ThrowYellowArmRoutine()
    {
        Vector2 direction =
            GetMouseAttackDirection().normalized;

        if (direction.sqrMagnitude <= InputThreshold)
        {
            direction = facingDirection;
        }

        // 투척 방향으로 Flip.
        UpdatePlayerFlip(direction.x);

        isThrowingYellowArm = true;

        // 일반 AttackState는 아니지만 attack 애니메이션을 사용.
        UpdatePlayerAnimation();

        attackHitBox.ReleaseComboPose(direction);

        ShakeCamera(
            0.075f,
            0.07f
        );

        StartAttackDash(
            direction,
            yellowStepSpeed,
            attackStepDuration
        );

        attackHitBox.ThrowMotion(
            direction,
            yellowThrowWindup
        );

        yield return new WaitForSeconds(
            yellowThrowWindup
        );

        ThrownArmProjectile.Create(
            transform.position
            + (Vector3)(direction * 0.9f),
            direction,
            yellowThrowSpeed,
            yellowThrowStunDuration,
            attackHitBox.WeaponSprite
        );

        nextYellowThrowTime =
            Time.time + yellowThrowCooldown;

        isThrowingYellowArm = false;

        UpdatePlayerAnimation();
    }

    // =========================================================
    // Mouse Attack Direction
    // =========================================================

    private Vector2 GetMouseAttackDirection()
    {
        if (
            Mouse.current == null
            ||
            Camera.main == null
        )
        {
            return facingDirection;
        }

        Vector3 mousePosition =
            Mouse.current.position.ReadValue();

        mousePosition.z =
            Mathf.Abs(
                Camera.main.transform.position.z
                -
                transform.position.z
            );

        Vector3 mouseWorldPosition =
            Camera.main.ScreenToWorldPoint(
                mousePosition
            );

        return
            (Vector2)(
                mouseWorldPosition
                -
                transform.position
            );
    }

    // =========================================================
    // Move Input
    // =========================================================

    private Vector2 ReadMoveInput()
    {
        if (moveAction == null)
        {
            return Vector2.zero;
        }

        Vector2 input =
            moveAction.ReadValue<Vector2>();

        return
            input.sqrMagnitude
            <
            moveDeadZone * moveDeadZone
                ? Vector2.zero
                : Vector2.ClampMagnitude(input, 1f);
    }

    // =========================================================
    // Color Input
    // =========================================================

    private void HandleColorSelection()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        // 왼쪽 색 변경
        bool shiftPressed =
            Keyboard.current.leftShiftKey.wasPressedThisFrame
            ||
            Keyboard.current.rightShiftKey.wasPressedThisFrame;

        if (shiftPressed)
        {
            RequestColorSwitch(-1);
        }

        // 오른쪽 색 변경
        if (
            Keyboard.current.spaceKey.wasPressedThisFrame
        )
        {
            RequestColorSwitch(1);
        }
    }

    private void RequestColorSwitch(int direction)
    {
        int normalizedDirection =
            NormalizeColorSwitchSteps(direction);

        if (normalizedDirection == 0)
        {
            return;
        }

        CycleAttackColor(normalizedDirection);
    }

    // =========================================================
    // Attack HitBox Color Sync
    // =========================================================

    private void TrySyncAttackHitBoxColor()
    {
        if (
            !pendingAttackHitBoxColorSync
            ||
            IsBusyForColorTriggeredAction
        )
        {
            return;
        }

        pendingAttackHitBoxColorSync = false;

        SyncAttackHitBoxColor();
    }

    private void SyncAttackHitBoxColor()
    {
        if (attackHitBox == null)
        {
            return;
        }

        attackHitBox.SetColor(attackColor);

        attackHitBox.RecoverToRestPose(
            facingDirection,
            weaponRecoverDuration * 0.5f
        );
    }

    // =========================================================
    // Runtime Animator Override
    // =========================================================

    private void SetupRuntimeAnimatorOverride()
    {
        if (
            animator == null
            ||
            animator.runtimeAnimatorController == null
        )
        {
            Debug.LogError(
                "Player Animator 또는 Runtime Animator Controller가 없습니다.",
                this
            );

            return;
        }

        RuntimeAnimatorController baseController =
            animator.runtimeAnimatorController;

        // 혹시 Inspector에 Override Controller가 들어가 있다면
        // 그 안의 원본 Controller를 사용.
        if (
            baseController is AnimatorOverrideController existingOverride
            &&
            existingOverride.runtimeAnimatorController != null
        )
        {
            baseController =
                existingOverride.runtimeAnimatorController;
        }

        // Project에 별도 Override Controller 파일을 만들 필요 없이
        // 실행 중에 코드가 하나 만든다.
        runtimeOverrideController =
            new AnimatorOverrideController(
                baseController
            );

        animator.runtimeAnimatorController =
            runtimeOverrideController;

        // 현재 시작 색의 클립 적용.
        ApplyCurrentColorAnimationClips(false);
    }

    // =========================================================
    // Color Animation Clip Change
    // =========================================================

    private void ApplyCurrentColorAnimationClips(
        bool replayCurrentState = true
    )
    {
        if (runtimeOverrideController == null)
        {
            return;
        }

        // 기본값 = Red
        AnimationClip selectedIdle =
            idleRed;

        AnimationClip selectedWalk =
            walkRed;

        AnimationClip selectedAttack =
            attackRed;

        // Blue
        if (attackColor == ColorType.Blue)
        {
            selectedIdle =
                idleBlue;

            selectedWalk =
                walkBlue;

            selectedAttack =
                attackBlue;
        }

        // Yellow
        else if (attackColor == ColorType.Yellow)
        {
            selectedIdle =
                idleYellow;

            selectedWalk =
                walkYellow;

            selectedAttack =
                attackYellow;
        }

        // Base Controller의 Motion으로 사용하는
        // Red 클립 3개는 반드시 연결되어 있어야 한다.
        if (
            idleRed == null
            ||
            walkRed == null
            ||
            attackRed == null
        )
        {
            Debug.LogError(
                "Player Inspector에 idleRed / walkRed / attackRed를 모두 연결해주세요.",
                this
            );

            return;
        }

        // 현재 색깔의 클립들도 모두 있어야 함.
        if (
            selectedIdle == null
            ||
            selectedWalk == null
            ||
            selectedAttack == null
        )
        {
            Debug.LogError(
                $"{attackColor} 애니메이션 클립이 Player Inspector에 전부 연결되지 않았습니다.",
                this
            );

            return;
        }

        float normalizedTime = 0f;

        bool canReplay =
            replayCurrentState
            &&
            animator != null
            &&
            currentAnimatorStateHash != 0;

        // 색이 바뀌기 전 현재 애니메이션 진행 위치 기억.
        if (canReplay)
        {
            AnimatorStateInfo stateInfo =
                animator.GetCurrentAnimatorStateInfo(0);

            normalizedTime =
                stateInfo.normalizedTime;
        }

        // 핵심:
        //
        // Animator State:
        // idle   → 기본 Motion idleRed
        // walk   → 기본 Motion walkRed
        // attack → 기본 Motion attackRed
        //
        // 위 3개의 Motion만 현재 색으로 교체한다.
        runtimeOverrideController[idleRed] =
            selectedIdle;

        runtimeOverrideController[walkRed] =
            selectedWalk;

        runtimeOverrideController[attackRed] =
            selectedAttack;

        // 같은 State를 재생 중이어도
        // 색깔 변경이 즉시 화면에 보이도록 다시 적용.
        //
        // normalizedTime을 그대로 넣어서
        // 애니메이션을 처음부터 다시 시작하지 않게 한다.
        if (
            canReplay
            &&
            animator.HasState(
                0,
                currentAnimatorStateHash
            )
        )
        {
            animator.Play(
                currentAnimatorStateHash,
                0,
                normalizedTime
            );

            animator.Update(0f);
        }
    }

    // =========================================================
    // Animation State Play
    // =========================================================

    private void UpdatePlayerAnimation()
    {
        if (animator == null)
        {
            animator =
                GetComponentInChildren<Animator>(true);

            if (animator == null)
            {
                return;
            }
        }

        int nextStateHash;

        // 공격
        if (
            CurrentState == PlayerStates.Attack
            ||
            isThrowingYellowArm
        )
        {
            nextStateHash =
                AttackAnimatorStateHash;
        }

        // 걷기
        else if (
            CurrentState == PlayerStates.Move
        )
        {
            nextStateHash =
                WalkAnimatorStateHash;
        }

        // Idle
        else
        {
            nextStateHash =
                IdleAnimatorStateHash;
        }

        // 같은 State를 매 프레임 Play하면
        // 첫 프레임으로 계속 돌아가므로 재생하지 않는다.
        if (
            currentAnimatorStateHash ==
            nextStateHash
        )
        {
            return;
        }

        // State 이름을 잘못 만들었으면
        // Unity의 Animator.GotoState 경고 대신
        // 알아보기 쉬운 오류를 출력.
        if (
            !animator.HasState(
                0,
                nextStateHash
            )
        )
        {
            if (!animatorStateErrorLogged)
            {
                Debug.LogError(
                    "Animator의 Base Layer에 " +
                    "idle / walk / attack State가 있는지 확인해주세요. " +
                    "State 이름은 소문자로 정확히 idle, walk, attack 이어야 합니다.",
                    this
                );

                animatorStateErrorLogged = true;
            }

            return;
        }

        animatorStateErrorLogged = false;

        animator.Play(
            nextStateHash,
            0,
            0f
        );

        currentAnimatorStateHash =
            nextStateHash;
    }

    // =========================================================
    // Player Sprite Renderer
    // =========================================================

    private void ResolvePlayerSpriteRenderer()
    {
        if (playerSpriteRenderer != null)
        {
            return;
        }

        // Player 오브젝트 자체 먼저 확인.
        playerSpriteRenderer =
            GetComponent<SpriteRenderer>();

        if (playerSpriteRenderer != null)
        {
            return;
        }

        // 없으면 자식에서 검색.
        SpriteRenderer[] renderers =
            GetComponentsInChildren<SpriteRenderer>(true);

        foreach (SpriteRenderer renderer in renderers)
        {
            if (renderer == null)
            {
                continue;
            }

            // 공격 무기 Renderer는 제외.
            if (
                attackHitBox != null
                &&
                renderer.transform.IsChildOf(
                    attackHitBox.transform
                )
            )
            {
                continue;
            }

            playerSpriteRenderer = renderer;

            return;
        }
    }

    // =========================================================
    // Flip
    // =========================================================

    private void UpdatePlayerFlip(
        float horizontalDirection
    )
    {
        // X 방향이 없으면 마지막 방향 유지.
        if (
            Mathf.Abs(horizontalDirection)
            <= 0.001f
        )
        {
            return;
        }

        ResolvePlayerSpriteRenderer();

        if (playerSpriteRenderer == null)
        {
            return;
        }

        // 원본 Sprite가 오른쪽을 보는 그림 기준.
        //
        // 오른쪽 = false
        // 왼쪽   = true
        playerSpriteRenderer.flipX =
            horizontalDirection < 0f;
    }

    // =========================================================
    // Damage
    // =========================================================

    private void Damage(int amount)
    {
        if (
            amount <= 0
            ||
            currentHealth <= 0
        )
        {
            return;
        }

        currentHealth =
            Mathf.Max(
                0,
                currentHealth - amount
            );
    }

    // =========================================================
    // Color Change
    // =========================================================

    private void CycleAttackColor(int direction)
    {
        direction =
            NormalizeColorSwitchSteps(direction);

        if (direction == 0)
        {
            return;
        }

        int currentIndex =
            Array.IndexOf(
                AttackColorCycle,
                attackColor
            );

        if (currentIndex < 0)
        {
            currentIndex = 0;
        }

        ColorType nextColor =
            AttackColorCycle[
                (
                    currentIndex
                    +
                    direction
                    +
                    AttackColorCycle.Length
                )
                %
                AttackColorCycle.Length
            ];

        if (nextColor == attackColor)
        {
            return;
        }

        // =====================================================
        // 선택 색 즉시 변경
        // =====================================================

        attackColor =
            nextColor;

        colorWheelAnimationDirection =
            Math.Sign(direction);

        colorWheelAnimationStartTime =
            Time.unscaledTime;

        comboStep = 0;

        // =====================================================
        // ★ 캐릭터 외형 애니메이션도 즉시 변경
        // =====================================================

        ApplyCurrentColorAnimationClips();

        // =====================================================
        // 공격 중 HitBox 색은 공격이 끝날 때까지 기존 색 유지
        // =====================================================

        if (IsBusyForColorTriggeredAction)
        {
            pendingAttackHitBoxColorSync = true;
            return;
        }

        pendingAttackHitBoxColorSync = false;

        SyncAttackHitBoxColor();
    }

    private static int NormalizeColorSwitchSteps(
        int steps
    )
    {
        int colorCount =
            AttackColorCycle.Length;

        steps %= colorCount;

        if (steps > colorCount / 2)
        {
            steps -= colorCount;
        }

        if (steps < -colorCount / 2)
        {
            steps += colorCount;
        }

        return steps;
    }

    // =========================================================
    // Camera Shake
    // =========================================================

    private static void ShakeCamera(
        float duration,
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
            duration,
            intensity
        );
    }

    // =========================================================
    // GUI
    // =========================================================

    private void OnGUI()
    {
        float scale =
            Mathf.Clamp(
                Screen.height / 720f,
                0.75f,
                1.25f
            );

        DrawHealthHud(scale);

        if (!showColorWheel)
        {
            return;
        }

        float orbSize =
            52f * scale;

        float activeOrbSize =
            58f * scale;

        Vector2 wheelCenter =
            new Vector2(
                100f * scale,
                Screen.height - 82f * scale
            );

        float wheelRadius =
            62f * scale;

        int activeIndex =
            Array.IndexOf(
                AttackColorCycle,
                attackColor
            );

        if (activeIndex < 0)
        {
            activeIndex = 0;
        }

        float animationProgress =
            colorWheelRotateDuration <= 0f
                ? 1f
                : Mathf.Clamp01(
                    (
                        Time.unscaledTime
                        -
                        colorWheelAnimationStartTime
                    )
                    /
                    colorWheelRotateDuration
                );

        float angleOffset =
            -(
                1f - animationProgress
            )
            *
            colorWheelAnimationDirection
            *
            120f;

        foreach (
            ColorType colorType
            in AttackColorCycle
        )
        {
            bool active =
                colorType == attackColor;

            float angle =
                GetWheelSlotAngle(
                    colorType,
                    activeIndex
                )
                +
                angleOffset;

            Vector2 position =
                wheelCenter
                +
                AngleToScreenDirection(angle)
                *
                wheelRadius;

            DrawColorOrb(
                position,
                colorType,
                active,
                active
                    ? activeOrbSize
                    : orbSize,
                scale
            );
        }

        GUIStyle keyStyle =
            new GUIStyle(GUI.skin.label)
            {
                alignment =
                    TextAnchor.MiddleCenter,

                fontSize =
                    Mathf.RoundToInt(
                        22f * scale
                    ),

                fontStyle =
                    FontStyle.Bold,
            };

        keyStyle.normal.textColor =
            Color.white;

        keyStyle.fontSize =
            Mathf.RoundToInt(
                16f * scale
            );

        GUI.Label(
            new Rect(
                wheelCenter.x - 82f * scale,
                wheelCenter.y + 58f * scale,
                70f * scale,
                30f * scale
            ),
            "SHIFT",
            keyStyle
        );

        GUI.Label(
            new Rect(
                wheelCenter.x + 12f * scale,
                wheelCenter.y + 58f * scale,
                70f * scale,
                30f * scale
            ),
            "SPACE",
            keyStyle
        );
    }

    // =========================================================
    // Health HUD
    // =========================================================

    private void DrawHealthHud(float scale)
    {
        Sprite heartSprite =
            GetHeartSprite(attackColor);

        if (heartSprite == null)
        {
            return;
        }

        float height =
            heartHudSize * scale;

        float spacing =
            heartHudSpacing * scale;

        float width =
            height
            *
            heartSprite.rect.width
            /
            heartSprite.rect.height;

        Vector2 start =
            new Vector2(
                28f * scale,
                24f * scale
            );

        for (int i = 0; i < maxHealth; i++)
        {
            Rect rect =
                new Rect(
                    start.x
                    +
                    i * (width + spacing),

                    start.y,

                    width,

                    height
                );

            DrawSpriteInGui(
                heartSprite,
                rect,
                i < currentHealth
                    ? 1f
                    : 0.18f
            );
        }
    }

    private Sprite GetHeartSprite(
        ColorType colorType
    )
    {
        return
            colorType switch
            {
                ColorType.Blue =>
                    blueHeartSprite,

                ColorType.Yellow =>
                    yellowHeartSprite,

                _ =>
                    redHeartSprite,
            };
    }

    // =========================================================
    // Color Wheel
    // =========================================================

    private static float GetWheelSlotAngle(
        ColorType colorType,
        int activeIndex
    )
    {
        int colorIndex =
            Array.IndexOf(
                AttackColorCycle,
                colorType
            );

        int relativeIndex =
            (
                colorIndex
                -
                activeIndex
                +
                AttackColorCycle.Length
            )
            %
            AttackColorCycle.Length;

        return
            relativeIndex switch
            {
                0 => 90f,
                1 => -30f,
                _ => 210f,
            };
    }

    private static Vector2 AngleToScreenDirection(
        float angle
    )
    {
        float radians =
            angle * Mathf.Deg2Rad;

        return
            new Vector2(
                Mathf.Cos(radians),
                -Mathf.Sin(radians)
            );
    }

    private static void DrawColorOrb(
        Vector2 center,
        ColorType colorType,
        bool active,
        float size,
        float scale
    )
    {
        if (active)
        {
            float outlineSize =
                size + 10f * scale;

            Rect outlineRect =
                new Rect(
                    center.x
                    -
                    outlineSize * 0.5f,

                    center.y
                    -
                    outlineSize * 0.5f,

                    outlineSize,

                    outlineSize
                );

            GUI.DrawTexture(
                outlineRect,
                GetCircleTexture(
                    new Color(
                        1f,
                        1f,
                        1f,
                        0.85f
                    ),
                    ref colorWheelOutlineTexture
                )
            );
        }

        Rect rect =
            new Rect(
                center.x - size * 0.5f,
                center.y - size * 0.5f,
                size,
                size
            );

        GUI.DrawTexture(
            rect,
            GetColorWheelTexture(
                colorType
            )
        );

        GUIStyle labelStyle =
            new GUIStyle(GUI.skin.label)
            {
                alignment =
                    TextAnchor.MiddleCenter,

                fontSize =
                    Mathf.RoundToInt(
                        (
                            active
                                ? 18f
                                : 16f
                        )
                        *
                        scale
                    ),

                fontStyle =
                    FontStyle.Bold,
            };

        labelStyle.normal.textColor =
            Color.black;

        GUI.Label(
            rect,
            GetColorLetter(colorType),
            labelStyle
        );
    }

    // =========================================================
    // Sprite GUI
    // =========================================================

    private static void DrawSpriteInGui(
        Sprite sprite,
        Rect rect,
        float alpha
    )
    {
        if (
            sprite == null
            ||
            sprite.texture == null
        )
        {
            return;
        }

        Rect textureRect =
            sprite.textureRect;

        Texture2D texture =
            sprite.texture;

        Rect textureCoords =
            new Rect(
                textureRect.x / texture.width,
                textureRect.y / texture.height,
                textureRect.width / texture.width,
                textureRect.height / texture.height
            );

        Color previousColor =
            GUI.color;

        GUI.color =
            new Color(
                1f,
                1f,
                1f,
                alpha
            );

        GUI.DrawTextureWithTexCoords(
            rect,
            texture,
            textureCoords,
            true
        );

        GUI.color =
            previousColor;
    }

    // =========================================================
    // Color Wheel Texture
    // =========================================================

    private static Texture2D GetColorWheelTexture(
        ColorType colorType
    )
    {
        if (
            !colorWheelTextures.TryGetValue(
                colorType,
                out Texture2D texture
            )
        )
        {
            texture =
                CreateCircleTexture(
                    GetHudColor(colorType)
                );

            colorWheelTextures[colorType] =
                texture;
        }

        return texture;
    }

    private static Texture2D GetCircleTexture(
        Color color,
        ref Texture2D texture
    )
    {
        texture ??=
            CreateCircleTexture(color);

        return texture;
    }

    private static Texture2D CreateCircleTexture(
        Color color
    )
    {
        const int textureSize = 64;

        Texture2D texture =
            new Texture2D(
                textureSize,
                textureSize,
                TextureFormat.RGBA32,
                false
            )
            {
                filterMode =
                    FilterMode.Bilinear,

                wrapMode =
                    TextureWrapMode.Clamp,
            };

        Vector2 center =
            new Vector2(
                (textureSize - 1) * 0.5f,
                (textureSize - 1) * 0.5f
            );

        float radius =
            textureSize * 0.47f;

        Color[] pixels =
            new Color[
                textureSize
                *
                textureSize
            ];

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float distance =
                    Vector2.Distance(
                        new Vector2(x, y),
                        center
                    );

                float alpha =
                    Mathf.Clamp01(
                        radius
                        +
                        1f
                        -
                        distance
                    )
                    *
                    color.a;

                pixels[
                    y * textureSize + x
                ] =
                    new Color(
                        color.r,
                        color.g,
                        color.b,
                        alpha
                    );
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        return texture;
    }

    private static Color GetHudColor(
        ColorType colorType
    )
    {
        return
            colorType switch
            {
                ColorType.Blue =>
                    new Color(
                        0.45f,
                        0.9f,
                        1f,
                        0.96f
                    ),

                ColorType.Yellow =>
                    new Color(
                        1f,
                        0.87f,
                        0.28f,
                        0.96f
                    ),

                _ =>
                    new Color(
                        1f,
                        0.32f,
                        0.26f,
                        0.96f
                    ),
            };
    }

    private static string GetColorLetter(
        ColorType colorType
    )
    {
        return
            colorType switch
            {
                ColorType.Blue => "B",
                ColorType.Yellow => "Y",
                _ => "R",
            };
    }

    // =========================================================
    // Input
    // =========================================================

    private void ResolveInputActions()
    {
        InputActionAsset actions =
            inputActions != null
                ? inputActions
                : InputSystem.actions;

        if (actions == null)
        {
            return;
        }

        moveAction ??=
            actions.FindAction(
                MoveActionName,
                false
            );

        attackAction ??=
            actions.FindAction(
                AttackActionName,
                false
            );
    }
}