public sealed class MoveState : State
{
    public MoveState(Player player) : base(player)
    {
    }

    public override void UpdateState()
    {
        if (!player.HasMoveInput)
        {
            player.ChangeState(PlayerStates.Idle);
            return;
        }

        player.Move();
    }
}
