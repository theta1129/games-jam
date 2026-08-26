using UnityEngine;

public sealed class EnemyMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.5f;

    [Header("Visual / Flip")]
    [Tooltip("오른쪽을 보고 있는 스프라이트가 기본입니다.")]
    [SerializeField] private SpriteRenderer visualRenderer;

    private Vector2 knockbackVelocity;

    private float knockbackEndTime;
    private float stunEndTime;


    private void Awake()
    {
        ResolveVisualRenderer();
    }


    public void Tick(Player player)
    {
        if (player == null)
        {
            return;
        }


        // =========================================
        // 넉백을 스턴보다 먼저 처리
        //
        // 맞아서 스턴 상태가 되어도
        // 날아가는 효과는 정상적으로 보여야 함
        // =========================================

        if (Time.time < knockbackEndTime)
        {
            transform.position +=
                (Vector3)(
                    knockbackVelocity *
                    Time.deltaTime
                );

            return;
        }


        // 넉백은 끝남
        knockbackVelocity = Vector2.zero;


        // =========================================
        // 스턴
        // =========================================

        if (Time.time < stunEndTime)
        {
            return;
        }


        // =========================================
        // 플레이어 추적
        // =========================================

        Vector2 toPlayer =
            (Vector2)player.transform.position -
            (Vector2)transform.position;


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
                    direction *
                    moveSpeed *
                    Time.deltaTime
                );
        }
    }


    public void KnockBack(
        Vector2 sourcePosition,
        float force
    )
    {
        Vector2 direction =
            (Vector2)transform.position -
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


    public void Stun(float duration)
    {
        stunEndTime =
            Mathf.Max(
                stunEndTime,
                Time.time +
                Mathf.Max(0f, duration)
            );
    }


    public bool IsStunned =>
        Time.time < stunEndTime;


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


    private void UpdateSpriteFlip(
        float horizontalDirection
    )
    {
        if (
            Mathf.Abs(horizontalDirection) <=
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


        // 원본 Sprite = 오른쪽
        visualRenderer.flipX =
            horizontalDirection < 0f;
    }
}