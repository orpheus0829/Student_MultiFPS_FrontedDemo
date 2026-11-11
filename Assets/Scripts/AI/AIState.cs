public enum AIStateId
{
    Search = 0,
    Engage = 1,
    Cover = 2
}

public abstract class AIState
{
    protected readonly AIPlayerCtrl Owner;

    public abstract AIStateId Id { get; }

    protected AIState(AIPlayerCtrl owner)
    {
        Owner = owner;
    }

    public virtual void OnEnter()
    {
    }

    public abstract void OnUpdate();

    public virtual void OnExit()
    {
    }
}
