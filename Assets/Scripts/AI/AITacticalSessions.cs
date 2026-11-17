using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public enum AITacticalRole
{
    Entry = 0,
    Support = 1,
    Anchor = 2,
    Scout = 3,
    Lurker = 4
}

public enum AIBombRoundPlan
{
    Default = 0,
    Eco = 1,
    RushA = 2,
    RushB = 3,
    FakeA = 4,
    FakeB = 5
}

public static class AITacticalCoordinator
{
    private sealed class TeamPlan
    {
        public int Round;
        public AIBombRoundPlan Plan;
    }

    private static readonly Dictionary<int, TeamPlan> BombPlans =
        new Dictionary<int, TeamPlan>();

    public static AITacticalRole GetRole(int slot)
    {
        return (AITacticalRole)Mathf.Abs(slot % 5);
    }

    public static AIBombRoundPlan EnsureBombPlan(
        int team,
        int round,
        float economyRatio,
        bool attacker)
    {
        if (BombPlans.TryGetValue(team, out TeamPlan existing) &&
            existing.Round == round)
        {
            return existing.Plan;
        }

        AIBombRoundPlan plan = ChooseBombPlan(team, round, economyRatio, attacker);
        BombPlans[team] = new TeamPlan { Round = round, Plan = plan };
        return plan;
    }

    public static AIBombRoundPlan GetBombPlan(int team)
    {
        return BombPlans.TryGetValue(team, out TeamPlan plan)
            ? plan.Plan
            : AIBombRoundPlan.Default;
    }

    private static AIBombRoundPlan ChooseBombPlan(
        int team,
        int round,
        float economyRatio,
        bool attacker)
    {
        if (economyRatio < 0.42f)
        {
            return AIBombRoundPlan.Eco;
        }
        if (!attacker)
        {
            return AIBombRoundPlan.Default;
        }

        uint hash = (uint)(team * 73856093 ^ round * 19349663 ^ 0x5f3759df);
        int roll = (int)(hash % 100u);
        if (roll < 16) return AIBombRoundPlan.RushA;
        if (roll < 32) return AIBombRoundPlan.RushB;
        if (roll < 44) return AIBombRoundPlan.FakeA;
        if (roll < 56) return AIBombRoundPlan.FakeB;
        return AIBombRoundPlan.Default;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        BombPlans.Clear();
    }
}

public abstract class AITacticalSessionBase : IAIModePatchSession
{
    protected readonly AIPlayerCtrl Ai;
    protected readonly AIModeContext Context;
    protected readonly AITacticalRole Role;

    private float _nextIntelShareTime;

    protected AITacticalSessionBase(AIPlayerCtrl ai, AIModeContext context)
    {
        Ai = ai;
        Context = context;
        Role = AITacticalCoordinator.GetRole(context != null ? context.Slot : 0);
    }

    public virtual void OnMatchStart() { }
    public virtual void OnRoundStart(int roundIndex) { }
    public virtual void OnRoundEnd(bool won) { }
    public virtual void OnTargetKilled(PlayerInfo victim) { }

    public virtual void Tick()
    {
        ShareTeamIntel();
    }

    public virtual void ModifyIntent(ref AIIntent intent)
    {
        ApplySquadSeparation(ref intent);
    }

    public abstract AIUtilityBias GetUtilityBias();
    public abstract bool TryGetObjective(out AIObjective objective);

    protected bool IsAlly(AIPlayerCtrl other)
    {
        return other && other != Ai && Context != null &&
            Context.AllyFilter != null && Context.AllyFilter(other);
    }

    protected AICombatStanding GetStanding()
    {
        return Context != null && Context.StandingProvider != null
            ? Context.StandingProvider(Ai)
            : default;
    }

    protected bool TryGetContextObjective(out AIObjective objective)
    {
        AIObjective? value = Context != null && Context.ObjectiveProvider != null
            ? Context.ObjectiveProvider(Ai)
            : null;
        objective = value.GetValueOrDefault();
        return value.HasValue;
    }

    protected static Vector3 SampleNavMesh(Vector3 position, float radius = 5f)
    {
        return NavMesh.SamplePosition(
            position,
            out NavMeshHit hit,
            radius,
            NavMesh.AllAreas)
            ? hit.position
            : position;
    }

    private void ShareTeamIntel()
    {
        if (Context == null || Context.AllyFilter == null ||
            Ai.Profile.IntelShareConfidence <= 0f ||
            Time.time < _nextIntelShareTime)
        {
            return;
        }

        _nextIntelShareTime = Time.time + Mathf.Max(
            0.2f,
            Ai.Profile.IntelShareInterval);
        IReadOnlyList<AIPlayerCtrl> active = AIPlayerCtrl.All;
        for (int i = 0; i < active.Count; i++)
        {
            AIPlayerCtrl ally = active[i];
            if (!IsAlly(ally))
            {
                continue;
            }

            float distance = Vector3.Distance(
                ally.transform.position,
                Ai.transform.position);
            float radioConfidence = Ai.Profile.IntelShareConfidence * Mathf.Lerp(
                1f,
                0.72f,
                Mathf.Clamp01(distance / Mathf.Max(20f, Ai.Profile.IntelShareRange)));
            if (Ai.Target != null)
            {
                ally.ReceiveSharedTarget(Ai.Target, radioConfidence);
            }
            if (ally.Target != null)
            {
                Ai.ReceiveSharedTarget(ally.Target, radioConfidence);
            }
        }
    }

    private void ApplySquadSeparation(ref AIIntent intent)
    {
        if (!intent.HasMoveDestination || Context == null ||
            Context.AllyFilter == null || intent.Target != null ||
            intent.CombatTactic == AICombatTactic.ShortStrafe ||
            intent.CombatTactic == AICombatTactic.Flank ||
            intent.CombatTactic == AICombatTactic.CoverHidden ||
            intent.CombatTactic == AICombatTactic.CoverPeek ||
            intent.CombatTactic == AICombatTactic.Withdraw)
        {
            return;
        }

        float radius = Mathf.Max(2.5f, Ai.Profile.SquadSeparationRadius);
        Vector3 separation = Vector3.zero;
        IReadOnlyList<AIPlayerCtrl> active = AIPlayerCtrl.All;
        for (int i = 0; i < active.Count; i++)
        {
            AIPlayerCtrl ally = active[i];
            if (!IsAlly(ally))
            {
                continue;
            }

            Vector3 away = Ai.transform.position - ally.transform.position;
            away.y = 0f;
            float distance = away.magnitude;
            if (distance <= 0.05f || distance >= radius)
            {
                continue;
            }
            separation += away.normalized * (1f - distance / radius);
        }

        if (separation.sqrMagnitude > 0.001f)
        {
            intent.MoveDestination += separation.normalized *
                Mathf.Max(1.5f, Ai.Profile.SquadSeparationWeight);
        }
    }
}

public sealed class SoloTacticalSession : AITacticalSessionBase
{
    public SoloTacticalSession(AIPlayerCtrl ai, AIModeContext context)
        : base(ai, context)
    {
    }

    public override AIUtilityBias GetUtilityBias()
    {
        AICombatStanding standing = GetStanding();
        float kd = standing.Kills / Mathf.Max(1f, standing.Deaths);
        int lead = standing.Kills - standing.LeaderKills;
        float healthRatio = Ai.Info && Ai.Info.TryGetComponent(out CharacterHealth health)
            ? health.CurrentHealth / Mathf.Max(1f, health.MaxHealth)
            : 1f;

        AIUtilityBias bias = new AIUtilityBias
        {
            TakeCover = 0.16f,
            Reposition = 0.12f,
            Retreat = healthRatio < 0.45f ? 0.42f : 0.08f
        };
        if (kd >= 1.8f && lead >= -1)
        {
            bias.Engage = -0.12f;
            bias.TakeCover += 0.2f;
            bias.Reposition += 0.18f;
        }
        else if (kd <= 0.65f || lead <= -5)
        {
            bias.Engage = 0.24f;
            bias.Investigate = 0.16f;
            bias.Reposition += 0.12f;
            bias.Retreat -= 0.08f;
        }
        return bias;
    }

    public override bool TryGetObjective(out AIObjective objective)
    {
        objective = default;
        return false;
    }
}

public sealed class TeamTacticalSession : AITacticalSessionBase
{
    public TeamTacticalSession(AIPlayerCtrl ai, AIModeContext context)
        : base(ai, context)
    {
    }

    public override AIUtilityBias GetUtilityBias()
    {
        switch (Role)
        {
            case AITacticalRole.Entry:
                return new AIUtilityBias { Engage = 0.22f, Reposition = 0.2f };
            case AITacticalRole.Anchor:
                return new AIUtilityBias { TakeCover = 0.28f, Engage = 0.06f };
            case AITacticalRole.Lurker:
                return new AIUtilityBias
                {
                    Investigate = 0.18f,
                    Reposition = 0.24f,
                    Engage = 0.08f
                };
            default:
                return new AIUtilityBias { Engage = 0.1f, Reposition = 0.16f };
        }
    }

    public override bool TryGetObjective(out AIObjective objective)
    {
        if (Ai.Target != null)
        {
            objective = default;
            return false;
        }

        int slot = Context != null ? Context.Slot : 0;
        float independentDuration = Ai.Profile.Difficulty == AIDifficulty.Easy
            ? 7f
            : Ai.Profile.Difficulty == AIDifficulty.Normal ? 4f : 2f;
        if (Role == AITacticalRole.Lurker)
        {
            independentDuration += 5f;
        }
        else if (Role == AITacticalRole.Entry)
        {
            independentDuration += 2f;
        }
        bool independentWindow = Mathf.Repeat(
            Time.time + slot * 2.73f,
            18f) < independentDuration;
        AIPlayerCtrl fightingAlly = independentWindow ? null : FindFightingAlly();
        if (fightingAlly)
        {
            Vector3 offset = GetRoleOffset(fightingAlly.transform.forward, 6f);
            objective = new AIObjective(
                SampleNavMesh(fightingAlly.transform.position + offset),
                AIObjectiveKind.Regroup,
                3.5f,
                true);
            return true;
        }

        AIObjective? search = Context != null && Context.SearchObjectiveProvider != null
            ? Context.SearchObjectiveProvider(Ai)
            : null;
        if (!search.HasValue)
        {
            objective = default;
            return false;
        }

        AIObjective value = search.Value;
        objective = new AIObjective(
            SampleNavMesh(value.Position + GetRoleOffset(Vector3.forward, 7f)),
            AIObjectiveKind.Search,
            2.5f,
            true);
        return true;
    }

    private AIPlayerCtrl FindFightingAlly()
    {
        AIPlayerCtrl nearest = null;
        float nearestDistance = float.PositiveInfinity;
        IReadOnlyList<AIPlayerCtrl> active = AIPlayerCtrl.All;
        for (int i = 0; i < active.Count; i++)
        {
            AIPlayerCtrl ally = active[i];
            if (!IsAlly(ally) || ally.Target == null)
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(
                ally.transform.position - Ai.transform.position);
            if (distance < nearestDistance)
            {
                nearest = ally;
                nearestDistance = distance;
            }
        }
        return nearest;
    }

    private Vector3 GetRoleOffset(Vector3 forward, float radius)
    {
        int slot = Context != null ? Context.Slot : 0;
        float angle = (int)Role * 72f + slot * 17f;
        return Quaternion.Euler(0f, angle, 0f) * forward.normalized * radius;
    }
}

public sealed class BombTacticalSession : AITacticalSessionBase
{
    private const int PostPlantGuardCandidateCount = 30;
    private const float BombEscortRadius = 4.5f;
    private const float DroppedBombGuardRadius = 6f;
    private const float DefuseSafetyMargin = 1.25f;
    private const float EstimatedSprintSpeed = 5.5f;
    private const float GuardMinimumRadius = 4.5f;
    private const float GuardMaximumRadius = 11f;
    private const float GuardDamageInterruptDuration = 2.75f;

    private readonly bool _attacker;
    private readonly RaycastHit[] _guardHits = new RaycastHit[12];
    private readonly NavMeshPath _guardPath;
    private int _roundIndex;
    private AIBombRoundPlan _plan;
    private Vector3 _postPlantGuardPosition;
    private Vector3 _guardBombPosition;
    private bool _hasPostPlantGuardPosition;

    public BombTacticalSession(
        AIPlayerCtrl ai,
        AIModeContext context,
        bool attacker)
        : base(ai, context)
    {
        _attacker = attacker;
        _guardPath = new NavMeshPath();
    }

    public override void OnMatchStart()
    {
        RefreshPlan();
    }

    public override void OnRoundStart(int roundIndex)
    {
        _roundIndex = roundIndex;
        _hasPostPlantGuardPosition = false;
        RefreshPlan();
    }

    public override void OnRoundEnd(bool won)
    {
        _hasPostPlantGuardPosition = false;
    }

    public override void Tick()
    {
        base.Tick();
        PrioritizeBombInteractor();
    }

    public override AIUtilityBias GetUtilityBias()
    {
        BombEntity bomb = GetBomb();
        bool planted = bomb && bomb.State == BombEntityState.Planted;
        if (_plan == AIBombRoundPlan.Eco)
        {
            return new AIUtilityBias
            {
                Engage = 0.16f,
                TakeCover = 0.12f,
                Reposition = 0.2f,
                Retreat = -0.08f
            };
        }
        if (_plan == AIBombRoundPlan.RushA || _plan == AIBombRoundPlan.RushB)
        {
            return new AIUtilityBias
            {
                Engage = 0.28f,
                Investigate = 0.2f,
                Reposition = 0.16f,
                TakeCover = -0.1f
            };
        }
        if (planted)
        {
            return _attacker
                ? new AIUtilityBias { TakeCover = 0.3f, Reposition = 0.22f }
                : new AIUtilityBias { Engage = 0.22f, Reposition = 0.28f };
        }
        return Role == AITacticalRole.Anchor
            ? new AIUtilityBias { TakeCover = 0.28f, Engage = 0.08f }
            : new AIUtilityBias { Engage = 0.12f, Reposition = 0.18f };
    }

    public override void ModifyIntent(ref AIIntent intent)
    {
        bool canFollowObjective = intent.Behavior != AIBehaviorId.Retreat &&
            (intent.Target == null || !intent.Target.Visible);
        if (TryGetObjective(out AIObjective objective) &&
            (canFollowObjective || objective.ForceCommit) &&
            (objective.AllowInteraction ||
             objective.Kind == AIObjectiveKind.RecoverBomb))
        {
            if (intent.HasMoveDestination)
            {
                intent.MoveDestination = objective.Position;
            }
            return;
        }

        base.ModifyIntent(ref intent);
        if ((_plan == AIBombRoundPlan.RushA ||
             _plan == AIBombRoundPlan.RushB) &&
            intent.HasMoveDestination && !intent.Fire)
        {
            intent.Sprint = true;
        }
    }

    public override bool TryGetObjective(out AIObjective objective)
    {
        BombEntity bomb = GetBomb();
        if (bomb && bomb.State == BombEntityState.OnGround)
        {
            return TryGetDroppedBombObjective(bomb, out objective);
        }
        if (bomb && bomb.State == BombEntityState.Planted)
        {
            return TryGetPostPlantObjective(bomb, out objective);
        }

        IReadOnlyList<BombSite> sites = BombSite.Sites;
        if (sites.Count == 0)
        {
            return TryGetContextObjective(out objective);
        }

        if (_attacker)
        {
            BombSite site = SelectAttackSite(sites);
            bool isCarrier = IsBombCarrier();
            Vector3 position = GetAttackObjectivePosition(site, isCarrier);
            objective = new AIObjective(
                SampleNavMesh(position),
                AIObjectiveKind.AttackSite,
                isCarrier ? Mathf.Max(0.8f, site.Radius * 0.35f) : 1.8f,
                _plan != AIBombRoundPlan.Default,
                isCarrier);
            return true;
        }

        int slot = Context != null ? Context.Slot : 0;
        if (sites.Count == 1 || slot % 5 < 2)
        {
            return BuildSiteDefense(sites[0], out objective);
        }
        if (slot % 5 < 4)
        {
            return BuildSiteDefense(sites[Mathf.Min(1, sites.Count - 1)], out objective);
        }

        Vector3 middle = (sites[0].Position + sites[Mathf.Min(1, sites.Count - 1)].Position) * 0.5f;
        objective = new AIObjective(
            SampleNavMesh(middle),
            AIObjectiveKind.Search,
            3f,
            true);
        return true;
    }

    private bool TryGetDroppedBombObjective(
        BombEntity bomb,
        out AIObjective objective)
    {
        Vector3 bombPosition = bomb.transform.position;
        if (_attacker && IsNearestBombRetriever(bombPosition))
        {
            float pickupSampleRadius = Mathf.Clamp(
                bomb.PickupRadius * 0.25f,
                0.1f,
                0.35f);
            objective = new AIObjective(
                SampleNavMesh(bombPosition, pickupSampleRadius),
                AIObjectiveKind.RecoverBomb,
                Mathf.Clamp(bomb.PickupRadius * 0.45f, 0.5f, 0.8f),
                true);
            return true;
        }

        float radius = _attacker ? BombEscortRadius : DroppedBombGuardRadius;
        Vector3 direction = GetFormationDirection(23f);
        Vector3 guardPosition = SampleNavMesh(
            bombPosition + direction * radius,
            4f);
        objective = new AIObjective(
            guardPosition,
            _attacker ? AIObjectiveKind.DefendSite : AIObjectiveKind.Search,
            1.8f,
            true);
        return true;
    }

    private bool TryGetPostPlantObjective(BombEntity bomb, out AIObjective objective)
    {
        if (_attacker && ShouldInterruptPostPlantGuard())
        {
            _hasPostPlantGuardPosition = false;
            objective = default;
            return false;
        }

        if (!_attacker && IsNearestDefuser(bomb.transform.position))
        {
            float travelTime = Vector3.Distance(
                Ai.transform.position,
                bomb.transform.position) / EstimatedSprintSpeed;
            float defuseDuration = Context != null &&
                Context.BombDefuseDurationProvider != null
                ? Context.BombDefuseDurationProvider()
                : 5f;
            bool forceCommit = bomb.FuseRemaining <=
                travelTime + defuseDuration + DefuseSafetyMargin;
            objective = new AIObjective(
                bomb.transform.position,
                AIObjectiveKind.Bomb,
                1.35f,
                true,
                true,
                forceCommit);
            return true;
        }

        if (_attacker && TryGetPostPlantGuardPosition(bomb, out Vector3 guardPosition))
        {
            objective = new AIObjective(
                guardPosition,
                AIObjectiveKind.DefendSite,
                1.35f,
                true);
            return true;
        }

        float radius = _attacker ? 7f : 5f;
        int slot = Context != null ? Context.Slot : 0;
        int searchStep = _attacker ? 0 : Mathf.FloorToInt(Time.time / 7f);
        float angle = slot * 137.5f + (_attacker ? 35f : 0f) +
            searchStep * 41f;
        Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
        Vector3 position = SampleNavMesh(bomb.transform.position + direction * radius);
        objective = new AIObjective(
            position,
            _attacker ? AIObjectiveKind.DefendSite : AIObjectiveKind.Search,
            2f,
            true);
        return true;
    }

    private bool ShouldInterruptPostPlantGuard()
    {
        if (Ai.TimeSinceDamaged <= GuardDamageInterruptDuration ||
            Ai.RecentDamagePressure >= 0.16f)
        {
            return true;
        }

        AITrackedTarget target = Ai.Target;
        if (target == null)
        {
            return false;
        }

        float confidenceThreshold = Ai.Profile.Difficulty == AIDifficulty.Hard
            ? 0.42f
            : Ai.Profile.Difficulty == AIDifficulty.Normal ? 0.58f : 0.76f;
        return target.Visible || target.Confidence >= confidenceThreshold;
    }

    private bool TryGetPostPlantGuardPosition(
        BombEntity bomb,
        out Vector3 guardPosition)
    {
        Vector3 bombPosition = bomb.transform.position;
        if (_hasPostPlantGuardPosition &&
            Vector3.SqrMagnitude(_guardBombPosition - bombPosition) <= 1f)
        {
            guardPosition = _postPlantGuardPosition;
            return true;
        }

        int slot = Context != null ? Context.Slot : 0;
        float preferredAngle = slot * 137.5f + (int)Role * 23f +
            _roundIndex * 11f;
        Vector3 preferredDirection = Quaternion.Euler(
            0f,
            preferredAngle,
            0f) * Vector3.forward;
        float bestScore = float.NegativeInfinity;
        Vector3 bestPosition = bombPosition;
        for (int i = 0; i < PostPlantGuardCandidateCount; i++)
        {
            int ring = i % 3;
            int spoke = i / 3;
            float radius = Mathf.Lerp(
                GuardMinimumRadius,
                GuardMaximumRadius,
                ring / 2f);
            float angle = preferredAngle + spoke * 36f + ring * 13f;
            Vector3 direction = Quaternion.Euler(0f, angle, 0f) *
                Vector3.forward;
            Vector3 rawPosition = bombPosition + direction * radius;
            if (!NavMesh.SamplePosition(
                    rawPosition,
                    out NavMeshHit sample,
                    1.8f,
                    NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(
                    Ai.transform.position,
                    sample.position,
                    NavMesh.AllAreas,
                    _guardPath) ||
                _guardPath.status != NavMeshPathStatus.PathComplete)
            {
                continue;
            }

            float score = ScorePostPlantGuardPosition(
                sample.position,
                bomb,
                preferredDirection,
                radius,
                _guardPath);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            bestPosition = sample.position;
        }

        if (float.IsNegativeInfinity(bestScore))
        {
            guardPosition = bombPosition;
            return false;
        }

        _postPlantGuardPosition = bestPosition;
        _guardBombPosition = bombPosition;
        _hasPostPlantGuardPosition = true;
        guardPosition = bestPosition;
        return true;
    }

    private float ScorePostPlantGuardPosition(
        Vector3 position,
        BombEntity bomb,
        Vector3 preferredDirection,
        float radius,
        NavMeshPath path)
    {
        Vector3 bombPosition = bomb.transform.position;
        Vector3 fromBomb = position - bombPosition;
        fromBomb.y = 0f;
        Vector3 outward = fromBomb.sqrMagnitude > 0.01f
            ? fromBomb.normalized
            : preferredDirection;
        int coverDirections = CountGuardCoverDirections(position, bomb);
        float enclosure = coverDirections / 8f;
        float outwardCover = HasGuardBlocker(
            position + Vector3.up * 1.15f,
            outward,
            5f,
            bomb)
            ? 1f
            : 0f;
        Vector3 toBomb = bombPosition + Vector3.up * 1.1f -
            (position + Vector3.up * 1.15f);
        float bombVisibility = HasGuardBlocker(
            position + Vector3.up * 1.15f,
            toBomb.normalized,
            toBomb.magnitude,
            bomb)
            ? 0.12f
            : 0.35f;
        float threatCover = 0f;
        AITrackedTarget target = Ai.Target;
        if (target != null)
        {
            Vector3 toThreat = target.LastKnownPosition + Vector3.up * 1.15f -
                (position + Vector3.up * 1.15f);
            if (HasGuardBlocker(
                    position + Vector3.up * 1.15f,
                    toThreat.normalized,
                    toThreat.magnitude,
                    bomb))
            {
                threatCover = 0.65f;
            }
        }

        float directionScore = Mathf.Clamp01(
            (Vector3.Dot(outward, preferredDirection.normalized) + 1f) * 0.5f);
        float preferredRadius = Role == AITacticalRole.Anchor ? 5.5f :
            Role == AITacticalRole.Lurker ? 9.5f : 7.5f;
        float radiusScore = 1f - Mathf.Clamp01(
            Mathf.Abs(radius - preferredRadius) /
            (GuardMaximumRadius - GuardMinimumRadius));
        float pathLength = CalculateGuardPathLength(path);
        float travelScore = 1f - Mathf.Clamp01(pathLength / 24f);
        float separationPenalty = GetGuardSeparationPenalty(position);
        float exposedPenalty = coverDirections == 0 ? 0.85f : 0f;
        return enclosure * 1.7f + outwardCover * 0.65f + bombVisibility +
            threatCover + directionScore * 0.55f + radiusScore * 0.35f +
            travelScore * 0.3f - separationPenalty - exposedPenalty;
    }

    private int CountGuardCoverDirections(Vector3 position, BombEntity bomb)
    {
        int blockedDirections = 0;
        Vector3 origin = position + Vector3.up * 1.15f;
        for (int i = 0; i < 8; i++)
        {
            Vector3 direction = Quaternion.Euler(0f, i * 45f, 0f) *
                Vector3.forward;
            if (HasGuardBlocker(origin, direction, 2.8f, bomb))
            {
                blockedDirections++;
            }
        }
        return blockedDirections;
    }

    private bool HasGuardBlocker(
        Vector3 origin,
        Vector3 direction,
        float distance,
        BombEntity bomb)
    {
        if (direction.sqrMagnitude <= 0.0001f || distance <= 0.01f)
        {
            return false;
        }

        int count = Physics.RaycastNonAlloc(
            origin,
            direction.normalized,
            _guardHits,
            distance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Collider collider = _guardHits[i].collider;
            if (!collider || collider.transform.IsChildOf(Ai.transform) ||
                collider.GetComponentInParent<CharacterHealth>() ||
                bomb && collider.transform.IsChildOf(bomb.transform))
            {
                continue;
            }
            return true;
        }
        return false;
    }

    private float GetGuardSeparationPenalty(Vector3 position)
    {
        float penalty = 0f;
        IReadOnlyList<AIPlayerCtrl> active = AIPlayerCtrl.All;
        for (int i = 0; i < active.Count; i++)
        {
            AIPlayerCtrl ally = active[i];
            if (!IsLivingAlly(ally))
            {
                continue;
            }

            float distance = Vector3.Distance(position, ally.transform.position);
            if (distance < 4.5f)
            {
                penalty += (1f - distance / 4.5f) * 0.9f;
            }
        }
        return penalty;
    }

    private static float CalculateGuardPathLength(NavMeshPath path)
    {
        float length = 0f;
        Vector3[] corners = path.corners;
        for (int i = 1; i < corners.Length; i++)
        {
            length += Vector3.Distance(corners[i - 1], corners[i]);
        }
        return length;
    }

    private BombSite SelectAttackSite(IReadOnlyList<BombSite> sites)
    {
        int realIndex = GetRealAttackSiteIndex(sites.Count);
        int fakeIndex = realIndex == 0 ? 1 : 0;
        bool fakePlan = _plan == AIBombRoundPlan.FakeA ||
            _plan == AIBombRoundPlan.FakeB;
        bool isCarrier = IsBombCarrier();
        int slot = Context != null ? Context.Slot : 0;
        bool isDecoy = fakePlan && !isCarrier && slot % 5 < 2;
        bool defaultLurk = _plan == AIBombRoundPlan.Default && !isCarrier &&
            (Role == AITacticalRole.Lurker || Role == AITacticalRole.Scout && slot % 2 == 0);
        int selected = isDecoy || defaultLurk ? fakeIndex : realIndex;
        return sites[Mathf.Clamp(selected, 0, sites.Count - 1)];
    }

    private int GetRealAttackSiteIndex(int siteCount)
    {
        int safeCount = Mathf.Max(1, siteCount);
        int defaultIndex = Mathf.Abs((Context != null ? Context.Team : 0) * 17 +
            _roundIndex * 31) % safeCount;
        int realIndex = _plan == AIBombRoundPlan.RushA ||
            _plan == AIBombRoundPlan.FakeB ? 0 :
            _plan == AIBombRoundPlan.RushB ||
            _plan == AIBombRoundPlan.FakeA ? 1 : defaultIndex;
        return Mathf.Clamp(realIndex, 0, safeCount - 1);
    }

    private bool BuildSiteDefense(BombSite site, out AIObjective objective)
    {
        Vector3 offset = GetFormationOffset(site.transform.forward, 5.5f);
        objective = new AIObjective(
            SampleNavMesh(site.Position + offset),
            AIObjectiveKind.DefendSite,
            2f,
            true);
        return true;
    }

    private Vector3 GetFormationOffset(Vector3 forward, float radius)
    {
        int slot = Context != null ? Context.Slot : 0;
        float angle = slot * 111f + (int)Role * 29f;
        return Quaternion.Euler(0f, angle, 0f) * forward.normalized * radius;
    }

    private Vector3 GetAttackObjectivePosition(BombSite site, bool isCarrier)
    {
        if (isCarrier)
        {
            return site.Position;
        }

        PlayerInfo carrier = GetBombCarrier();
        IReadOnlyList<BombSite> sites = BombSite.Sites;
        BombSite realSite = sites.Count > 0
            ? sites[GetRealAttackSiteIndex(sites.Count)]
            : null;
        if (!carrier || realSite != site ||
            Vector3.Distance(carrier.transform.position, site.Position) <= 8f)
        {
            return site.Position + GetFormationOffset(
                site.transform.forward,
                BombEscortRadius);
        }

        return carrier.transform.position +
            GetFormationDirection(71f) * BombEscortRadius;
    }

    private Vector3 GetFormationDirection(float salt)
    {
        int slot = Context != null ? Context.Slot : 0;
        float angle = slot * 137.5f + (int)Role * 31f + salt;
        return Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
    }

    private bool IsNearestBombRetriever(Vector3 bombPosition)
    {
        if (!CanRecoverBomb(Ai))
        {
            return false;
        }

        float ownDistance = Vector3.SqrMagnitude(
            Ai.transform.position - bombPosition);
        IReadOnlyList<AIPlayerCtrl> active = AIPlayerCtrl.All;
        for (int i = 0; i < active.Count; i++)
        {
            AIPlayerCtrl ally = active[i];
            if (!IsLivingAlly(ally) || !CanRecoverBomb(ally))
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(
                ally.transform.position - bombPosition);
            if (distance < ownDistance - 0.01f ||
                Mathf.Abs(distance - ownDistance) <= 0.01f &&
                ally.GetInstanceID() < Ai.GetInstanceID())
            {
                return false;
            }
        }
        return true;
    }

    private static bool CanRecoverBomb(AIPlayerCtrl ai)
    {
        return ai && ai.CurrentBehavior != AIBehaviorId.Retreat &&
            (ai.Target == null || !ai.Target.Visible);
    }

    private bool IsNearestDefuser(Vector3 bombPosition)
    {
        float ownDistance = Vector3.SqrMagnitude(Ai.transform.position - bombPosition);
        IReadOnlyList<AIPlayerCtrl> active = AIPlayerCtrl.All;
        for (int i = 0; i < active.Count; i++)
        {
            AIPlayerCtrl ally = active[i];
            if (!IsLivingAlly(ally))
            {
                continue;
            }
            if (Vector3.SqrMagnitude(ally.transform.position - bombPosition) < ownDistance)
            {
                return false;
            }
        }
        return true;
    }

    private bool IsLivingAlly(AIPlayerCtrl ally)
    {
        return IsAlly(ally) && ally.Info &&
            ally.Info.TryGetComponent(out CharacterHealth health) &&
            !health.IsDead;
    }

    private void PrioritizeBombInteractor()
    {
        IReadOnlyList<BombInteractCtrl> interactions = BombInteractCtrl.Active;
        CharacterHealth nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < interactions.Count; i++)
        {
            BombInteractCtrl interaction = interactions[i];
            if (!interaction || !interaction.IsInteracting || !interaction.Player ||
                !interaction.Player.TryGetComponent(out CharacterHealth health) ||
                health.IsDead || Context == null || Context.TargetFilter == null ||
                !Context.TargetFilter(health))
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(
                health.transform.position - Ai.transform.position);
            if (distance < nearestDistance)
            {
                nearest = health;
                nearestDistance = distance;
            }
        }

        if (!nearest)
        {
            return;
        }

        float awarenessRange = Ai.Profile.Difficulty == AIDifficulty.Hard
            ? 65f
            : Ai.Profile.Difficulty == AIDifficulty.Normal ? 48f : 30f;
        Ai.PrioritizeModeTarget(nearest, awarenessRange);
    }

    private BombEntity GetBomb()
    {
        return Context != null && Context.BombProvider != null
            ? Context.BombProvider()
            : null;
    }

    private bool IsBombCarrier()
    {
        PlayerInfo carrier = GetBombCarrier();
        return carrier && carrier == Ai.Info;
    }

    private PlayerInfo GetBombCarrier()
    {
        return Context != null && Context.BombCarrierProvider != null
            ? Context.BombCarrierProvider()
            : null;
    }

    private void RefreshPlan()
    {
        float economy = Context != null && Context.TeamEconomyProvider != null
            ? Context.TeamEconomyProvider(Context.Team)
            : 1f;
        _plan = AITacticalCoordinator.EnsureBombPlan(
            Context != null ? Context.Team : 0,
            _roundIndex,
            economy,
            _attacker);
    }
}
