using UnityEngine;

public sealed class EnemyMovement : MonoBehaviour
{
    [SerializeField]
    private float moveSpeed = 2.5f;


    [Header("Visual / Flip")]

    [Tooltip("오른쪽을 보고 있는 스프라이트가 기본입니다.")]
    [SerializeField]
    private SpriteRenderer visualRenderer;


    private Vector2 knockbackVelocity;

    private float knockbackEndTime;
    private float stunEndTime;

    private bool isMoving;


    // =========================================================
    // Properties
    // =========================================================

    public bool IsMoving =>
        isMoving;


    public bool IsStunned =>
        Time.time <
        stunEndTime;


    // =========================================================
    // Awake
    // =========================================================

    private void Awake()
    {
        ResolveVisualRenderer();
    }


    // =========================================================
    // Tick
    // =========================================================

    public void Tick(
        Player player
    )
    {
        // 매 프레임 기본은 이동하지 않는 상태
        isMoving = false;


        if (player == null)
        {
            return;
        }


        // =====================================================
        // Knockback
        // =====================================================

        if (
            Time.time <
            knockbackEndTime
        )
        {
            transform.position +=
                (Vector3)(
                    knockbackVelocity
                    *
                    Time.deltaTime
                );


            return;
        }


        knockbackVelocity =
            Vector2.zero;


        // =====================================================
        // Stun
        // =====================================================

        if (
            Time.time <
            stunEndTime
        )
        {
            return;
        }


        // =====================================================
        // Player 방향
        // =====================================================

        Vector2 toPlayer =
            (Vector2)player.transform.position
            -
            (Vector2)transform.position;


        // 플레이어와 충분히 멀 때만 추적
        if (
            toPlayer.sqrMagnitude >
            1.2f * 1.2f
        )
        {
            Vector2 direction =
                toPlayer.normalized;


            UpdateSpriteFlip(
                direction.x
            );


            transform.position +=
                (Vector3)(
                    direction
                    *
                    moveSpeed
                    *
                    Time.deltaTime
                );


            // ★ 실제로 움직임
            isMoving = true;
        }
    }


    // =========================================================
    // Face Player
    // =========================================================

    public void FacePlayer(
        Player player
    )
    {
        isMoving = false;


        if (player == null)
        {
            return;
        }


        float horizontalDirection =
            player.transform.position.x
            -
            transform.position.x;


        UpdateSpriteFlip(
            horizontalDirection
        );
    }


    // =========================================================
    // Knockback
    // =========================================================

    public void KnockBack(
        Vector2 sourcePosition,
        float force
    )
    {
        isMoving = false;


        Vector2 direction =
            (Vector2)transform.position
            -
            sourcePosition;


        if (
            direction.sqrMagnitude <=
            0.0001f
        )
        {
            direction =
                Vector2.right;
        }


        direction.Normalize();


        knockbackVelocity =
            direction *
            force;


        knockbackEndTime =
            Time.time +
            0.18f;
    }


    // =========================================================
    // Stun
    // =========================================================

    public void Stun(
        float duration
    )
    {
        isMoving = false;


        stunEndTime =
            Mathf.Max(
                stunEndTime,
                Time.time
                +
                Mathf.Max(
                    0f,
                    duration
                )
            );
    }


    // =========================================================
    // Stop
    // =========================================================

    public void StopImmediately()
    {
        isMoving = false;

        knockbackVelocity =
            Vector2.zero;


        knockbackEndTime =
            -1f;


        stunEndTime =
            -1f;
    }


    // =========================================================
    // Sprite Renderer
    // =========================================================

    private void ResolveVisualRenderer()
    {
        if (visualRenderer != null)
        {
            return;
        }


        visualRenderer =
            GetComponent<SpriteRenderer>();


        if (visualRenderer == null)
        {
            visualRenderer =
                GetComponentInChildren<SpriteRenderer>(
                    true
                );
        }
    }


    // =========================================================
    // Flip
    // =========================================================

    private void UpdateSpriteFlip(
        float horizontalDirection
    )
    {
        if (
            Mathf.Abs(
                horizontalDirection
            )
            <=
            0.001f
        )
        {
            return;
        }


        ResolveVisualRenderer();


        if (visualRenderer == null)
        {
            return;
        }


        // 원본 스프라이트가 오른쪽을 바라보는 기준
        visualRenderer.flipX =
            horizontalDirection < 0f;
    }
}