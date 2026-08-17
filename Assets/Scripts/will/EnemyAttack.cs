using UnityEngine;

public sealed class EnemyAttack : MonoBehaviour
{
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float knockbackForce = 5f;
    private float nextAttackTime;

    public void Tick(Player player, bool canAttack)
    {
        if (!canAttack || player == null || Time.time < nextAttackTime) return;
        if (Vector2.Distance(transform.position, player.transform.position) > attackRange) return;

        // This overlap is the enemy's short-range circular attack hitbox.
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, attackRange);
        foreach (Collider2D hit in hits)
        {
            Player target = hit.GetComponentInParent<Player>();
            if (target == player)
            {
                target.KnockBack(transform.position, knockbackForce);
                nextAttackTime = Time.time + attackCooldown;
                break;
            }
        }
    }
}
