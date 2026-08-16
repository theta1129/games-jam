public sealed class AttackState : State
{
    public AttackState(Player player) : base(player)
    {
    }

    public override void EnterState()
    {
        player.StopMovement();
        player.PerformAttack();
    }

    public override void UpdateState()
    {
        if (player.IsAttackComplete)
        {
            player.ChangeState(player.HasMoveInput ? PlayerStates.Move : PlayerStates.Idle);
        }
    }
}
