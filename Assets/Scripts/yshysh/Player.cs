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
    [SerializeField] private InputActionAsset inputActions;

    private const string MoveActionName = "Player/Move";
    private const string AttackActionName = "Player/Attack";

    private Rigidbody2D rb;
    private InputAction moveAction;
    private InputAction attackAction;
    private Vector2 moveInput;
    private Vector2 facingDirection = Vector2.right;

    public ColorType CurrentAttackColor => attackColor;

    public ColorType GetCurrentAttackColor()
    {
        return attackColor;
    }

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

        ResolveInputActions();
    }

    private void OnEnable()
    {
        ResolveInputActions();

        if (moveAction != null)
        {
            moveAction.Enable();
        }

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

        if (moveAction != null)
        {
            moveAction.Disable();
        }
    }

    private void FixedUpdate()
    {
        if (moveAction != null)
        {
            moveInput = moveAction.ReadValue<Vector2>();
        }

        if (moveInput.sqrMagnitude > 0.001f)
        {
            facingDirection = moveInput.normalized;
        }

        rb.linearVelocity = moveInput * moveSpeed;
    }

    private void OnAttackPerformed(InputAction.CallbackContext context)
    {
        Attack();
    }

    private void Attack()
    {
        if (attackHitBox == null)
        {
            return;
        }

        attackHitBox.transform.localPosition = facingDirection;
        attackHitBox.Show(attackDuration, attackColor);
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
