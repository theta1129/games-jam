using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class Player : MonoBehaviour
{
    public static Player Instance { get; private set; }

    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private ColorType attackColor = ColorType.Red;
    [SerializeField] private PlayerAttackHitBox attackHitBox;
    [SerializeField] private float attackDuration = 0.2f;
    [SerializeField] private float knockbackDuration = 0.15f;
    [SerializeField] private InputActionAsset inputActions;

    private const string MoveActionName = "Player/Move";
    private const string AttackActionName = "Player/Attack";
    private const float InputThreshold = 0.001f;

    private readonly Dictionary<PlayerStates, State> states = new();
    private Rigidbody2D rb;
    private InputAction moveAction;
    private InputAction attackAction;
    private State activeState;
    private Vector2 moveInput;
    private Vector2 facingDirection = Vector2.right;
    private Vector2 knockbackVelocity;
    private float attackEndTime;
    private float knockbackEndTime;

    public PlayerStates CurrentState { get; private set; }
    public ColorType CurrentAttackColor => attackColor;

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

        if (attackHitBox == null)
        {
            attackHitBox = GetComponentInChildren<PlayerAttackHitBox>(true);
        }

        CreateStates();
        ChangeState(PlayerStates.Idle);
        ResolveInputActions();
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
    }

    private void Update()
    {
        HandleColorSelection();
    }

    private void FixedUpdate()
    {
        moveInput = moveAction != null ? moveAction.ReadValue<Vector2>() : Vector2.zero;

        if (Time.time < knockbackEndTime)
        {
            rb.linearVelocity = knockbackVelocity;
            return;
        }

        activeState?.UpdateState();
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        if (CurrentState != PlayerStates.Attack && attackHitBox != null)
        {
            ChangeState(PlayerStates.Attack);
        }
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
        facingDirection = moveInput.normalized;
        rb.linearVelocity = facingDirection * moveSpeed;
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

    internal void PerformAttack()
    {
        Vector2 attackDirection = GetMouseAttackDirection();
        if (attackDirection.sqrMagnitude > InputThreshold)
        {
            facingDirection = attackDirection.normalized;
        }

        attackHitBox.transform.localPosition = facingDirection;
        attackHitBox.Show(attackDuration, attackColor);
        attackEndTime = Time.time + attackDuration;
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

    private void HandleColorSelection()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            CycleAttackColor(1);
        }

        if (Keyboard.current.leftShiftKey.wasPressedThisFrame || Keyboard.current.rightShiftKey.wasPressedThisFrame)
        {
            CycleAttackColor(-1);
        }
    }

    private void CycleAttackColor(int direction)
    {
        int colorCount = Enum.GetValues(typeof(ColorType)).Length;
        attackColor = (ColorType)(((int)attackColor + direction + colorCount) % colorCount);
    }

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
