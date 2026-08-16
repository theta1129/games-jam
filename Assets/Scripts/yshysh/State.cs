public abstract class State
{
    protected readonly Player player;

    protected State(Player player)
    {
        this.player = player;
    }

    public virtual void EnterState()
    {
    }

    public virtual void UpdateState()
    {
    }

    public virtual void ExitState()
    {
    }
}
