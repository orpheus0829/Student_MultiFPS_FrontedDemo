using UnityEngine;

public enum AIBehaviorId
{
    Patrol = 0,
    Investigate = 1,
    Engage = 2,
    TakeCover = 3,
    Reposition = 4,
    Retreat = 5
}

public enum AIMobilityIntent
{
    None = 0,
    JumpVault = 1,
    Slide = 2
}

public struct AIIntent
{
    public AIBehaviorId Behavior;
    public Vector3 MoveDestination;
    public bool HasMoveDestination;
    public bool Sprint;
    public bool Aim;
    public bool Fire;
    public bool Reload;
    public bool SwitchWeapon;
    public bool Melee;
    public bool ProtectedByCover;
    public AIMobilityIntent Mobility;
    public AITrackedTarget Target;
}

public static class AIBehaviors
{
    public static AIIntent Build(
        AIBehaviorId behavior,
        AIWorldModel world,
        Transform owner,
        AIDifficultyProfile profile,
        Vector3 patrolPoint,
        Vector3 coverPoint,
        bool hasCover)
    {
        AIIntent intent = new AIIntent { Behavior = behavior, Target = world.CurrentTarget };
        AITrackedTarget target = world.CurrentTarget;
        if (behavior == AIBehaviorId.Patrol)
        {
            intent.HasMoveDestination = true;
            intent.MoveDestination = patrolPoint;
            return intent;
        }
        if (behavior == AIBehaviorId.Investigate)
        {
            intent.HasMoveDestination = true;
            intent.MoveDestination = target != null
                ? target.LastKnownPosition
                : world.SuspiciousPosition;
            intent.Sprint = true;
            return intent;
        }
        if (behavior == AIBehaviorId.Retreat)
        {
            intent.Aim = false;
            intent.Fire = false;
            if (hasCover && Vector3.Distance(owner.position, coverPoint) > 1.1f)
            {
                intent.HasMoveDestination = true;
                intent.MoveDestination = coverPoint;
                intent.Sprint = true;
            }
            else
            {
                intent.ProtectedByCover = hasCover;
            }
            return intent;
        }
        if (target == null)
        {
            return intent;
        }
        intent.Aim = target.Visible;
        intent.Fire = target.Visible && behavior == AIBehaviorId.Engage &&
            Time.time - target.VisibleSince >= profile.ReactionTime;
        if (behavior == AIBehaviorId.TakeCover && hasCover)
        {
            if (Vector3.Distance(owner.position, coverPoint) <= 1.2f)
            {
                intent.HasMoveDestination = false;
                intent.ProtectedByCover = true;
                float coverCycle = Mathf.Repeat(
                    Time.time + Mathf.Abs(owner.GetInstanceID() % 17) * 0.13f, 2.4f);
                bool peek = coverCycle >= 1.55f;
                intent.Aim = peek && target.Visible;
                intent.Fire = peek && target.Visible &&
                    Time.time - target.VisibleSince >= profile.ReactionTime;
                return intent;
            }
            intent.HasMoveDestination = true;
            intent.MoveDestination = coverPoint;
            intent.Sprint = true;
            return intent;
        }
        Vector3 away = (owner.position - target.LastKnownPosition);
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : -owner.forward;
        Vector3 side = Vector3.Cross(Vector3.up, away);
        float sign = owner.GetInstanceID() % 2 == 0 ? 1f : -1f;
        AIPlayerCtrl controller = owner.GetComponent<AIPlayerCtrl>();
        int targetSlot = controller && target.Actor
            ? controller.GetTargetSlot(target.Actor) : 0;
        float slotOffset = (targetSlot % 3 - 1) * 2.2f;
        float rangeError = target.Distance - profile.PreferredRange;
        if (behavior == AIBehaviorId.Engage && rangeError > profile.RangeBand)
        {
            intent.Aim = false;
            intent.Fire = false;
            intent.HasMoveDestination = true;
            intent.MoveDestination = target.LastKnownPosition;
            intent.Sprint = true;
            return intent;
        }
        if (behavior == AIBehaviorId.Reposition)
        {
            float maneuverCycle = Mathf.Repeat(
                Time.time + Mathf.Abs(owner.GetInstanceID() % 23) * 0.11f, 2.2f);
            bool maneuvering = maneuverCycle < 0.9f;
            intent.Aim = !maneuvering && target.Visible;
            intent.Fire = !maneuvering && target.Visible &&
                Time.time - target.VisibleSince >= profile.ReactionTime;
            intent.Sprint = maneuvering;
        }
        intent.HasMoveDestination = behavior == AIBehaviorId.Reposition ||
            Mathf.Abs(rangeError) > profile.RangeBand;
        intent.MoveDestination = owner.position +
            side * (sign * 5f * profile.StrafeScale + slotOffset) -
            away * Mathf.Clamp(rangeError, -5f, 5f);
        return intent;
    }
}
