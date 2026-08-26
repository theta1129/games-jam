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
            return;
        }

        // 넉백이 끝난 뒤 Idle 상태로 돌아왔을 때
        // 남아 있는 Rigidbody 속도를 계속 제거한다.
        player.StopMovement();
    }
}