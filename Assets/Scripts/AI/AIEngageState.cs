using UnityEngine;

public sealed class AIEngageState : AIState
{
    private float _nextTacticalMove;

    public override AIStateId Id
    {
        get { return AIStateId.Engage; }
    }

    public AIEngageState(AIPlayerCtrl owner) : base(owner)
    {
    }

    public override void OnEnter()
    {
        _nextTacticalMove = 0f;
    }

    public override void OnUpdate()
    {
        if (!Owner.HasLivingTarget && !Owner.TryAcquireTarget())
        {
            Owner.ChangeState(AIStateId.Search);
            return;
        }
        if (Owner.ShouldRetreatToCover)
        {
            Owner.ChangeState(AIStateId.Cover);
            return;
        }

        Owner.RefreshTargetVisibility();
        float distance = Owner.TargetDistance;
        bool visible = Owner.HasVisibleTarget;
        bool easy = Owner.Difficulty == AIDifficulty.Easy;
        bool shouldAim = !easy && visible && Owner.ShouldAim;
        Owner.AimAtTarget();
        Owner.SetAiming(shouldAim);

        if (!visible || distance > Owner.Profile.PreferredRange * 1.2f)
        {
            Owner.SetDestination(Owner.TargetPosition);
            Owner.FollowPath(true);
            Owner.SetFiring(false);
            return;
        }

        if (easy)
        {
            Owner.StopMoving();
            Owner.SetFiring(true);
            return;
        }

        if (Time.time >= _nextTacticalMove)
        {
            if (visible && Random.value <= Owner.Profile.CoverPreference)
            {
                Owner.ChangeState(AIStateId.Cover);
                return;
            }
            Owner.ChooseCombatDestination();
            Owner.TryCombatMobility();
            _nextTacticalMove = Time.time + Owner.Profile.DecisionInterval *
                Random.Range(3f, 6f);
        }

        Owner.FollowPath(Owner.Difficulty == AIDifficulty.Hard);
        Owner.SetFiring(visible);
    }

    public override void OnExit()
    {
        Owner.SetFiring(false);
        Owner.SetAiming(false);
    }
}
