using UnityEngine;

public sealed class AISearchState : AIState
{
    private float _nextPatrolTime;

    public override AIStateId Id
    {
        get { return AIStateId.Search; }
    }

    public AISearchState(AIPlayerCtrl owner) : base(owner)
    {
    }

    public override void OnEnter()
    {
        Owner.SetAiming(false);
        Owner.SetFiring(false);
        _nextPatrolTime = 0f;
    }

    public override void OnUpdate()
    {
        if (Owner.TryAcquireTarget())
        {
            Owner.ChangeState(AIStateId.Engage);
            return;
        }

        if (Time.time >= _nextPatrolTime || Owner.HasArrived(1.1f))
        {
            Owner.ChoosePatrolDestination();
            _nextPatrolTime = Time.time + Random.Range(3f, 6f);
        }

        Owner.FollowPath(false);
    }
}
