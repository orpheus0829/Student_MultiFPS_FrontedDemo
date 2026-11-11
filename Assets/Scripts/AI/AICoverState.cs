using UnityEngine;

public sealed class AICoverState : AIState
{
    private float _nextCoverSearch;
    private float _nextPeekMove;

    public override AIStateId Id
    {
        get { return AIStateId.Cover; }
    }

    public AICoverState(AIPlayerCtrl owner) : base(owner)
    {
    }

    public override void OnEnter()
    {
        _nextCoverSearch = 0f;
        _nextPeekMove = 0f;
        Owner.SetFiring(false);
    }

    public override void OnUpdate()
    {
        if (!Owner.HasLivingTarget && !Owner.TryAcquireTarget())
        {
            Owner.ChangeState(AIStateId.Search);
            return;
        }

        if (Time.time >= _nextCoverSearch && !Owner.HasCoverDestination)
        {
            Owner.FindCoverDestination();
            _nextCoverSearch = Time.time + 1.2f;
        }

        Owner.AimAtTarget();
        if (!Owner.HasArrived(1f))
        {
            Owner.SetAiming(false);
            Owner.SetFiring(false);
            Owner.FollowPath(true);
            if (Owner.Difficulty == AIDifficulty.Hard)
            {
                Owner.TryCombatMobility();
            }
            return;
        }

        Owner.RefreshTargetVisibility();
        Owner.SetAiming(Owner.Difficulty != AIDifficulty.Easy);
        Owner.SetFiring(Owner.HasVisibleTarget);
        if (Time.time >= _nextPeekMove)
        {
            Owner.ChooseCoverPeekDestination();
            _nextPeekMove = Time.time + Random.Range(0.8f, 1.7f);
        }
        Owner.FollowPath(false);
    }

    public override void OnExit()
    {
        Owner.ClearCoverDestination();
        Owner.SetFiring(false);
        Owner.SetAiming(false);
    }
}
