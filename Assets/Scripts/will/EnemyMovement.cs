using UnityEngine;

public sealed class EnemyMovement : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 2.5f;
    private Vector2 knockbackVelocity;
    private float knockbackEndTime;
    private float stunEndTime;

    public void Tick(Player player)
    {
        if (player == null || Time.time < stunEndTime) return;

        if (Time.time < knockbackEndTime)
        {
            transform.position += (Vector3)(knockbackVelocity * Time.deltaTime);
            return;
        }

        Vector2 toPlayer = (Vector2)player.transform.position - (Vector2)transform.position;
        if (toPlayer.sqrMagnitude > 1.2f * 1.2f)
        {
            transform.position += (Vector3)(toPlayer.normalized * moveSpeed * Time.deltaTime);
        }
    }

    public void KnockBack(Vector2 sourcePosition, float force)
    {
        Vector2 direction = ((Vector2)transform.position - sourcePosition).normalized;
        knockbackVelocity = direction * force;
        knockbackEndTime = Time.time + 0.18f;
    }

    public void Stun(float duration) => stunEndTime = Mathf.Max(stunEndTime, Time.time + duration);
    public bool IsStunned => Time.time < stunEndTime;
}
