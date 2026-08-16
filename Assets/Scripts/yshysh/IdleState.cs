public sealed class IdleState : State
{
    public IdleState(Player player) : base(player)
    {
    }

    public override void EnterState()
    {
        player.StopMovement();
    }

    public override void UpdateState()
    {
        if (player.HasMoveInput)
        {
            player.ChangeState(PlayerStates.Move);
        }
    }
}
