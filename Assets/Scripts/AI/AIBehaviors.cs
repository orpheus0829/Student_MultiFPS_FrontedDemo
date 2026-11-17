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

public enum AICombatTactic
{
    None = 0,
    HoldAngle = 1,
    Advance = 2,
    ShortStrafe = 3,
    Flank = 4,
    CoverHidden = 5,
    CoverPeek = 6,
    Suppress = 7,
    Withdraw = 8
}

public struct AIIntent
{
    public AIBehaviorId Behavior;
    public AICombatTactic CombatTactic;
    public Vector3 MoveDestination;
    public bool HasMoveDestination;
    public bool Sprint;
    public bool Aim;
    public bool Fire;
    public bool Reload;
    public bool SwitchWeapon;
    public bool Melee;
    public bool ProtectedByCover;
    public bool AllowMovingFire;
    public float CombatPressure;
    public AIMobilityIntent Mobility;
    public AITrackedTarget Target;
}

/// <summary>
/// 将效用层选出的宏观行为展开成有持续时间的战斗动作。
/// 阶段内锁定目的地，避免 AI 每帧换方向形成无意义左右晃动。
/// </summary>
public sealed class AIBehaviors
{
    private AIBehaviorId _behavior = AIBehaviorId.Patrol;
    private AICombatTactic _tactic;
    private AITrackedTarget _target;
    private Vector3 _lockedDestination;
    private float _tacticEndTime;
    private int _tacticSequence;
    private int _strafeSign = 1;

    public AIIntent Build(
        AIBehaviorId behavior,
        AIWorldModel world,
        Transform owner,
        AIDifficultyProfile profile,
        Vector3 patrolPoint,
        Vector3 coverPoint,
        Vector3 coverPeekPoint,
        bool hasCover,
        int targetSlot)
    {
        AITrackedTarget target = world.CurrentTarget;
        if (behavior != _behavior || target != _target)
        {
            EnterBehavior(behavior, target, owner, profile);
        }

        AIIntent intent = new AIIntent
        {
            Behavior = behavior,
            CombatTactic = _tactic,
            Target = target,
            CombatPressure = world.RecentDamagePressure
        };

        switch (behavior)
        {
            case AIBehaviorId.Patrol:
                intent.HasMoveDestination = true;
                intent.MoveDestination = patrolPoint;
                return intent;
            case AIBehaviorId.Investigate:
                return BuildInvestigate(intent, world, owner, profile);
            case AIBehaviorId.Engage:
                return BuildEngage(intent, owner, profile, targetSlot);
            case AIBehaviorId.TakeCover:
                return BuildTakeCover(
                    intent,
                    owner,
                    profile,
                    coverPoint,
                    coverPeekPoint,
                    hasCover);
            case AIBehaviorId.Reposition:
                return BuildReposition(intent, owner, profile, targetSlot);
            case AIBehaviorId.Retreat:
                return BuildRetreat(intent, owner, profile, coverPoint, hasCover);
            default:
                return intent;
        }
    }

    private AIIntent BuildInvestigate(
        AIIntent intent,
        AIWorldModel world,
        Transform owner,
        AIDifficultyProfile profile)
    {
        AITrackedTarget target = intent.Target;
        bool canSuppress = target != null && !target.Visible &&
            target.Confidence >= 0.72f &&
            Time.time - target.LastSeenTime <= profile.SuppressionMemory;
        if (canSuppress)
        {
            if (_tactic != AICombatTactic.Suppress)
            {
                EnterTactic(AICombatTactic.Suppress, owner, profile);
            }
            if (Time.time < _tacticEndTime)
            {
                intent.CombatTactic = _tactic;
                intent.Aim = profile.Difficulty != AIDifficulty.Easy;
                intent.Fire = profile.Difficulty == AIDifficulty.Hard ||
                    profile.Difficulty == AIDifficulty.Normal &&
                    target.Confidence >= 0.86f;
                return intent;
            }
        }

        if (_tactic != AICombatTactic.Advance)
        {
            EnterTactic(AICombatTactic.Advance, owner, profile);
        }
        Vector3 destination = target != null
            ? target.LastKnownPosition
            : world.SuspiciousPosition;
        if (target != null && profile.Difficulty == AIDifficulty.Hard &&
            !target.Visible && target.UncertaintyRadius > 0.5f)
        {
            destination = BuildSearchPoint(owner, target);
        }
        intent.CombatTactic = AICombatTactic.Advance;
        intent.HasMoveDestination = true;
        intent.MoveDestination = destination;
        float distance = Vector3.Distance(owner.position, destination);
        intent.Sprint = profile.Difficulty == AIDifficulty.Hard
            ? distance > 4f
            : profile.Difficulty == AIDifficulty.Normal && distance > 10f;
        return intent;
    }

    private AIIntent BuildEngage(
        AIIntent intent,
        Transform owner,
        AIDifficultyProfile profile,
        int targetSlot)
    {
        AITrackedTarget target = intent.Target;
        if (target == null)
        {
            return intent;
        }

        float rangeError = target.Distance - profile.PreferredRange;
        if (rangeError > profile.RangeBand)
        {
            EnterAdvanceIfNeeded(owner, target, profile);
            intent.CombatTactic = AICombatTactic.Advance;
            intent.HasMoveDestination = true;
            intent.MoveDestination = _lockedDestination;
            intent.Sprint = target.Distance > profile.PreferredRange + profile.RangeBand * 2f;
            ApplyMovingFire(ref intent, profile, target, !intent.Sprint);
            return intent;
        }

        if (_tactic == AICombatTactic.ShortStrafe && Time.time >= _tacticEndTime)
        {
            // 一次横移后必须停稳架枪，不能连续反向抽动。
            EnterTactic(AICombatTactic.HoldAngle, owner, profile);
        }
        else if (_tactic == AICombatTactic.Advance ||
            _tactic == AICombatTactic.None ||
            _tactic == AICombatTactic.HoldAngle && Time.time >= _tacticEndTime)
        {
            SelectEngageTactic(owner, target, profile, targetSlot, intent.CombatPressure);
        }
        intent.CombatTactic = _tactic;
        if (_tactic == AICombatTactic.ShortStrafe)
        {
            intent.HasMoveDestination = true;
            intent.MoveDestination = _lockedDestination;
            intent.Aim = profile.Difficulty != AIDifficulty.Easy;
            ApplyMovingFire(ref intent, profile, target, true);
            return intent;
        }

        intent.Aim = target.Visible && profile.Difficulty != AIDifficulty.Easy;
        intent.Fire = target.Visible &&
            Time.time - target.VisibleSince >= profile.ReactionTime;
        return intent;
    }

    private AIIntent BuildTakeCover(
        AIIntent intent,
        Transform owner,
        AIDifficultyProfile profile,
        Vector3 coverPoint,
        Vector3 coverPeekPoint,
        bool hasCover)
    {
        if (!hasCover)
        {
            return intent;
        }

        if (Vector3.Distance(owner.position, coverPoint) > 1.2f &&
            _tactic != AICombatTactic.CoverPeek)
        {
            intent.CombatTactic = AICombatTactic.Withdraw;
            intent.HasMoveDestination = true;
            intent.MoveDestination = coverPoint;
            intent.Sprint = true;
            float coverDistance = Vector3.Distance(owner.position, coverPoint);
            bool controlledWithdrawal = profile.Difficulty != AIDifficulty.Easy &&
                coverDistance <= 10f && intent.CombatPressure < 0.72f;
            if (controlledWithdrawal)
            {
                intent.Sprint = false;
                ApplyMovingFire(ref intent, profile, intent.Target, true);
            }
            return intent;
        }

        if (_tactic != AICombatTactic.CoverHidden &&
            _tactic != AICombatTactic.CoverPeek)
        {
            EnterTactic(AICombatTactic.CoverHidden, owner, profile);
        }
        else if (Time.time >= _tacticEndTime)
        {
            AICombatTactic next = _tactic == AICombatTactic.CoverHidden
                ? AICombatTactic.CoverPeek
                : AICombatTactic.CoverHidden;
            EnterTactic(next, owner, profile);
        }

        bool peeking = _tactic == AICombatTactic.CoverPeek;
        Vector3 stancePoint = peeking ? coverPeekPoint : coverPoint;
        intent.CombatTactic = _tactic;
        intent.ProtectedByCover = !peeking;
        if (Vector3.Distance(owner.position, stancePoint) > 0.32f)
        {
            intent.HasMoveDestination = true;
            intent.MoveDestination = stancePoint;
            return intent;
        }

        AITrackedTarget target = intent.Target;
        intent.Aim = peeking && target != null && target.Visible &&
            profile.Difficulty != AIDifficulty.Easy;
        intent.Fire = peeking && target != null && target.Visible &&
            Time.time - target.VisibleSince >= profile.ReactionTime;
        return intent;
    }

    private AIIntent BuildReposition(
        AIIntent intent,
        Transform owner,
        AIDifficultyProfile profile,
        int targetSlot)
    {
        AITrackedTarget target = intent.Target;
        if (target == null)
        {
            return intent;
        }

        if (_tactic != AICombatTactic.Flank &&
            _tactic != AICombatTactic.HoldAngle)
        {
            EnterFlank(owner, target, profile, targetSlot);
        }
        else if (Time.time >= _tacticEndTime)
        {
            if (_tactic == AICombatTactic.Flank)
            {
                EnterTactic(AICombatTactic.HoldAngle, owner, profile);
            }
            else
            {
                EnterFlank(owner, target, profile, targetSlot);
            }
        }

        intent.CombatTactic = _tactic;
        if (_tactic == AICombatTactic.Flank)
        {
            intent.HasMoveDestination = true;
            intent.MoveDestination = _lockedDestination;
            intent.Sprint = profile.Difficulty != AIDifficulty.Easy;
            bool pressuredFlank = intent.CombatPressure >= 0.35f &&
                profile.Difficulty != AIDifficulty.Easy;
            if (pressuredFlank)
            {
                intent.Sprint = false;
                ApplyMovingFire(ref intent, profile, target, true);
            }
            return intent;
        }

        intent.Aim = target.Visible && profile.Difficulty != AIDifficulty.Easy;
        intent.Fire = target.Visible &&
            Time.time - target.VisibleSince >= profile.ReactionTime;
        return intent;
    }

    private AIIntent BuildRetreat(
        AIIntent intent,
        Transform owner,
        AIDifficultyProfile profile,
        Vector3 coverPoint,
        bool hasCover)
    {
        intent.CombatTactic = AICombatTactic.Withdraw;
        if (hasCover && Vector3.Distance(owner.position, coverPoint) > 1.1f)
        {
            intent.HasMoveDestination = true;
            intent.MoveDestination = coverPoint;
            intent.Sprint = true;
            float coverDistance = Vector3.Distance(owner.position, coverPoint);
            bool canFightBack = profile.Difficulty != AIDifficulty.Easy &&
                intent.Target != null && intent.Target.Visible && coverDistance <= 12f &&
                intent.CombatPressure < 0.55f;
            if (canFightBack)
            {
                intent.Sprint = false;
                ApplyMovingFire(ref intent, profile, intent.Target, true);
            }
        }
        else
        {
            intent.ProtectedByCover = hasCover;
        }
        return intent;
    }

    private static void ApplyMovingFire(
        ref AIIntent intent,
        AIDifficultyProfile profile,
        AITrackedTarget target,
        bool allowed)
    {
        if (!allowed || target == null || !target.Visible ||
            Time.time - target.VisibleSince < profile.ReactionTime)
        {
            return;
        }

        intent.AllowMovingFire = true;
        intent.Aim = profile.Difficulty == AIDifficulty.Hard;
        intent.Fire = true;
    }

    private void EnterBehavior(
        AIBehaviorId behavior,
        AITrackedTarget target,
        Transform owner,
        AIDifficultyProfile profile)
    {
        _behavior = behavior;
        _target = target;
        _tacticSequence++;
        AICombatTactic tactic = behavior == AIBehaviorId.Retreat
            ? AICombatTactic.Withdraw
            : AICombatTactic.None;
        EnterTactic(tactic, owner, profile);
    }

    private void EnterAdvanceIfNeeded(
        Transform owner,
        AITrackedTarget target,
        AIDifficultyProfile profile)
    {
        if (_tactic == AICombatTactic.Advance && Time.time < _tacticEndTime)
        {
            return;
        }
        EnterTactic(AICombatTactic.Advance, owner, profile);
        _lockedDestination = target.LastKnownPosition;
    }

    private void SelectEngageTactic(
        Transform owner,
        AITrackedTarget target,
        AIDifficultyProfile profile,
        int targetSlot,
        float combatPressure)
    {
        float strafeChance = profile.Difficulty == AIDifficulty.Easy
            ? 0.08f
            : profile.Difficulty == AIDifficulty.Normal ? 0.16f : 0.24f;
        float targetMotion = Mathf.Clamp01(
            Vector3.ProjectOnPlane(target.LastKnownVelocity, Vector3.up).magnitude / 5f);
        strafeChance += target.ThreatScore *
            (profile.Difficulty == AIDifficulty.Hard ? 0.24f : 0.12f);
        strafeChance += combatPressure *
            (profile.Difficulty == AIDifficulty.Hard ? 0.3f : 0.18f);
        strafeChance += targetMotion *
            (profile.Difficulty == AIDifficulty.Hard ? 0.12f : 0.05f);
        strafeChance = Mathf.Clamp(strafeChance, 0.04f, 0.72f);
        float choice = Hash01(owner.GetInstanceID(), _tacticSequence + 47);
        _tacticSequence++;
        if (choice >= strafeChance)
        {
            EnterTactic(AICombatTactic.HoldAngle, owner, profile);
            return;
        }

        EnterTactic(AICombatTactic.ShortStrafe, owner, profile);
        BuildLateralDestination(owner, target, profile, targetSlot, 2.2f);
    }

    private void EnterFlank(
        Transform owner,
        AITrackedTarget target,
        AIDifficultyProfile profile,
        int targetSlot)
    {
        EnterTactic(AICombatTactic.Flank, owner, profile);
        float distance = profile.Difficulty == AIDifficulty.Hard
            ? 7f
            : profile.Difficulty == AIDifficulty.Normal ? 4.5f : 2.5f;
        BuildLateralDestination(owner, target, profile, targetSlot, distance);
    }

    private void BuildLateralDestination(
        Transform owner,
        AITrackedTarget target,
        AIDifficultyProfile profile,
        int targetSlot,
        float lateralDistance)
    {
        Vector3 away = owner.position - target.LastKnownPosition;
        away.y = 0f;
        away = away.sqrMagnitude > 0.01f ? away.normalized : -owner.forward;
        Vector3 side = Vector3.Cross(Vector3.up, away);
        _strafeSign = -_strafeSign;
        float slotOffset = (targetSlot % 3 - 1) * 1.4f;
        float rangeError = target.Distance - profile.PreferredRange;
        _lockedDestination = owner.position +
            side * (_strafeSign * lateralDistance * profile.StrafeScale + slotOffset) -
            away * Mathf.Clamp(rangeError, -4f, 4f);
        if (profile.Difficulty == AIDifficulty.Hard)
        {
            _lockedDestination += Vector3.ProjectOnPlane(
                target.LastKnownVelocity,
                Vector3.up) * 0.25f;
        }
    }

    private void EnterTactic(
        AICombatTactic tactic,
        Transform owner,
        AIDifficultyProfile profile)
    {
        _tactic = tactic;
        float duration;
        switch (tactic)
        {
            case AICombatTactic.HoldAngle:
                duration = StableRange(
                    owner,
                    _tacticSequence + 11,
                    profile.CombatHoldMin,
                    profile.CombatHoldMax);
                break;
            case AICombatTactic.ShortStrafe:
                duration = profile.ShortStrafeDuration;
                break;
            case AICombatTactic.Flank:
                duration = profile.FlankMoveDuration;
                break;
            case AICombatTactic.CoverHidden:
                duration = profile.CoverHideDuration;
                break;
            case AICombatTactic.CoverPeek:
                duration = profile.CoverPeekDuration;
                break;
            case AICombatTactic.Suppress:
                duration = profile.SuppressionMemory;
                break;
            case AICombatTactic.Advance:
                duration = 1.25f;
                break;
            default:
                duration = 0f;
                break;
        }
        _tacticEndTime = Time.time + duration;
    }

    private static Vector3 BuildSearchPoint(Transform owner, AITrackedTarget target)
    {
        float searchStep = Mathf.Floor(Time.time / 1.6f);
        float hash = Hash01(owner.GetInstanceID(), Mathf.RoundToInt(searchStep) + 73);
        float angle = Mathf.Lerp(-80f, 80f, hash);
        Vector3 approach = target.LastKnownPosition - owner.position;
        approach.y = 0f;
        if (approach.sqrMagnitude < 0.01f)
        {
            approach = owner.forward;
        }
        Vector3 direction = Quaternion.Euler(0f, angle, 0f) * approach.normalized;
        float radius = target.UncertaintyRadius * Mathf.Lerp(0.45f, 1f, Hash01(
            owner.GetInstanceID(),
            Mathf.RoundToInt(searchStep) + 137));
        return target.LastKnownPosition + direction * radius;
    }

    private static float StableRange(
        Transform owner,
        int salt,
        float minimum,
        float maximum)
    {
        return Mathf.Lerp(minimum, maximum, Hash01(owner.GetInstanceID(), salt));
    }

    private static float Hash01(int value, int salt)
    {
        unchecked
        {
            uint hash = (uint)(value * 397) ^ (uint)(salt * 7919);
            hash ^= hash >> 16;
            hash *= 2246822519u;
            hash ^= hash >> 13;
            return (hash & 0x00ffffffu) / 16777215f;
        }
    }
}
