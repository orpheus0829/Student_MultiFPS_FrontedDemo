using System;
using UnityEngine;

public enum AIObjectiveKind
{
    None = 0,
    Regroup = 1,
    AttackSite = 2,
    DefendSite = 3,
    Bomb = 4,
    Search = 5
}

public readonly struct AIObjective
{
    public Vector3 Position { get; }
    public AIObjectiveKind Kind { get; }
    public float ArrivalRadius { get; }
    public bool Sprint { get; }

    public AIObjective(Vector3 position, AIObjectiveKind kind)
        : this(position, kind, 2f, true)
    {
    }

    public AIObjective(
        Vector3 position,
        AIObjectiveKind kind,
        float arrivalRadius,
        bool sprint)
    {
        Position = position;
        Kind = kind;
        ArrivalRadius = Mathf.Max(0.5f, arrivalRadius);
        Sprint = sprint;
    }
}

public struct AIUtilityBias
{
    public float Patrol;
    public float Investigate;
    public float Engage;
    public float TakeCover;
    public float Reposition;
    public float Retreat;

    public float Get(AIBehaviorId behavior)
    {
        switch (behavior)
        {
            case AIBehaviorId.Patrol: return Patrol;
            case AIBehaviorId.Investigate: return Investigate;
            case AIBehaviorId.Engage: return Engage;
            case AIBehaviorId.TakeCover: return TakeCover;
            case AIBehaviorId.Reposition: return Reposition;
            case AIBehaviorId.Retreat: return Retreat;
            default: return 0f;
        }
    }
}

public sealed class AIModeContext
{
    public Func<CharacterHealth, bool> TargetFilter { get; set; }
    public Func<AIPlayerCtrl, AIObjective?> ObjectiveProvider { get; set; }
    public Func<AIPlayerCtrl, AIObjective?> SearchObjectiveProvider { get; set; }
    public Func<AIPlayerCtrl, CharacterHealth> NearestAllyProvider { get; set; }
    public Func<AIPlayerCtrl, bool> AllyFilter { get; set; }
}

public interface IAIModePatch
{
    string PatchId { get; }
    IAIModePatchSession CreateSession(AIPlayerCtrl ai, AIModeContext context);
}

public interface IAIModePatchSession
{
    void OnMatchStart();
    void OnRoundStart(int roundIndex);
    void OnRoundEnd(bool won);
    void OnTargetKilled(PlayerInfo victim);
    void Tick();
    void ModifyIntent(ref AIIntent intent);
    AIUtilityBias GetUtilityBias();
    bool TryGetObjective(out AIObjective objective);
}

public abstract class AIModePatchBase : IAIModePatch
{
    public abstract string PatchId { get; }
    protected abstract AIUtilityBias Bias { get; }
    protected virtual bool UsesTeamTactics { get { return false; } }

    public IAIModePatchSession CreateSession(AIPlayerCtrl ai, AIModeContext context)
    {
        return new Session(
            ai,
            context,
            Bias,
            UsesTeamTactics,
            ResolveObjective);
    }

    protected virtual AIObjective? ResolveObjective(
        AIPlayerCtrl ai,
        AIModeContext context)
    {
        return context != null && context.ObjectiveProvider != null
            ? context.ObjectiveProvider(ai)
            : null;
    }

    private sealed class Session : IAIModePatchSession
    {
        private readonly AIPlayerCtrl _ai;
        private readonly AIModeContext _context;
        private readonly AIUtilityBias _bias;
        private readonly bool _usesTeamTactics;
        private readonly Func<AIPlayerCtrl, AIModeContext, AIObjective?>
            _objectiveResolver;
        private float _nextIntelShareTime;

        public Session(
            AIPlayerCtrl ai,
            AIModeContext context,
            AIUtilityBias bias,
            bool usesTeamTactics,
            Func<AIPlayerCtrl, AIModeContext, AIObjective?> objectiveResolver)
        {
            _ai = ai;
            _context = context;
            _bias = bias;
            _usesTeamTactics = usesTeamTactics;
            _objectiveResolver = objectiveResolver;
        }

        public void OnMatchStart() { }
        public void OnRoundStart(int roundIndex) { }
        public void OnRoundEnd(bool won) { }
        public void OnTargetKilled(PlayerInfo victim) { }
        public AIUtilityBias GetUtilityBias() { return _bias; }

        public void Tick()
        {
            if (!_usesTeamTactics || _ai.Profile.IntelShareRange <= 0f ||
                Time.time < _nextIntelShareTime)
            {
                return;
            }

            _nextIntelShareTime = Time.time + _ai.Profile.IntelShareInterval;
            float rangeSquared = _ai.Profile.IntelShareRange *
                _ai.Profile.IntelShareRange;
            System.Collections.Generic.IReadOnlyList<AIPlayerCtrl> active =
                AIPlayerCtrl.All;
            for (int i = 0; i < active.Count; i++)
            {
                AIPlayerCtrl other = active[i];
                if (!IsAlly(other) || other.Target == null ||
                    Vector3.SqrMagnitude(
                        other.transform.position - _ai.transform.position) > rangeSquared)
                {
                    continue;
                }

                _ai.ReceiveSharedTarget(
                    other.Target,
                    _ai.Profile.IntelShareConfidence);
            }
        }

        public void ModifyIntent(ref AIIntent intent)
        {
            if (!_usesTeamTactics || !intent.HasMoveDestination ||
                _ai.Profile.SquadSeparationRadius <= 0f)
            {
                return;
            }

            Vector3 separation = Vector3.zero;
            System.Collections.Generic.IReadOnlyList<AIPlayerCtrl> active =
                AIPlayerCtrl.All;
            for (int i = 0; i < active.Count; i++)
            {
                AIPlayerCtrl other = active[i];
                if (!IsAlly(other))
                {
                    continue;
                }

                Vector3 away = _ai.transform.position - other.transform.position;
                away.y = 0f;
                float distance = away.magnitude;
                if (distance <= 0.01f ||
                    distance >= _ai.Profile.SquadSeparationRadius)
                {
                    continue;
                }

                float strength = 1f -
                    distance / _ai.Profile.SquadSeparationRadius;
                separation += away.normalized * strength;
            }

            if (separation.sqrMagnitude > 0.001f)
            {
                intent.MoveDestination += separation.normalized *
                    _ai.Profile.SquadSeparationWeight;
            }
        }

        public bool TryGetObjective(out AIObjective objective)
        {
            AIObjective? value = _objectiveResolver != null
                ? _objectiveResolver(_ai, _context)
                : null;
            objective = value.GetValueOrDefault();
            return value.HasValue;
        }

        private bool IsAlly(AIPlayerCtrl other)
        {
            return other && other != _ai && _context != null &&
                _context.AllyFilter != null && _context.AllyFilter(other);
        }
    }
}

public sealed class SoloSelfPreservationPatch : AIModePatchBase
{
    public override string PatchId { get { return "solo.self_preservation"; } }
    protected override AIUtilityBias Bias
    {
        get
        {
            return new AIUtilityBias
            {
                Engage = -0.12f,
                TakeCover = 0.18f,
                Retreat = 0.22f
            };
        }
    }
}

public sealed class TeamworkPatch : AIModePatchBase
{
    public override string PatchId { get { return "tdm.teamwork"; } }
    protected override bool UsesTeamTactics { get { return true; } }
    protected override AIUtilityBias Bias
    {
        get
        {
            return new AIUtilityBias { Engage = 0.12f, Reposition = 0.2f };
        }
    }

    protected override AIObjective? ResolveObjective(
        AIPlayerCtrl ai,
        AIModeContext context)
    {
        if (ai.Target != null)
        {
            return null;
        }

        AIPlayerCtrl fightingAlly = FindNearestFightingAlly(ai, context);
        if (fightingAlly)
        {
            Vector3 allyOffset = ai.transform.position -
                fightingAlly.transform.position;
            allyOffset.y = 0f;
            float allyDistance = allyOffset.magnitude;
            if (allyDistance > ai.Profile.TeamRegroupDistance)
            {
                Vector3 direction = allyDistance > 0.01f
                    ? allyOffset / allyDistance
                    : Quaternion.Euler(0f, ai.GetInstanceID() * 0.173f, 0f) *
                        Vector3.forward;
                Vector3 supportPoint = fightingAlly.transform.position +
                    direction * ai.Profile.TeamArrivalRadius;
                return new AIObjective(
                    supportPoint,
                    AIObjectiveKind.Regroup,
                    ai.Profile.TeamArrivalRadius * 0.55f,
                    true);
            }
        }

        return context != null && context.SearchObjectiveProvider != null
            ? context.SearchObjectiveProvider(ai)
            : null;
    }

    private static AIPlayerCtrl FindNearestFightingAlly(
        AIPlayerCtrl ai,
        AIModeContext context)
    {
        if (context == null || context.AllyFilter == null)
        {
            return null;
        }

        AIPlayerCtrl nearest = null;
        float nearestDistance = float.PositiveInfinity;
        System.Collections.Generic.IReadOnlyList<AIPlayerCtrl> active =
            AIPlayerCtrl.All;
        for (int i = 0; i < active.Count; i++)
        {
            AIPlayerCtrl other = active[i];
            if (!other || other == ai || !context.AllyFilter(other) ||
                other.Target == null)
            {
                continue;
            }

            float distance = Vector3.SqrMagnitude(
                other.transform.position - ai.transform.position);
            if (distance < nearestDistance)
            {
                nearest = other;
                nearestDistance = distance;
            }
        }

        return nearest;
    }
}

public sealed class BombAttackPatch : AIModePatchBase
{
    public override string PatchId { get { return "bomb.attack"; } }
    protected override bool UsesTeamTactics { get { return true; } }
    protected override AIUtilityBias Bias
    {
        get
        {
            return new AIUtilityBias { Engage = 0.1f, Reposition = 0.24f };
        }
    }
}

public sealed class BombDefensePatch : AIModePatchBase
{
    public override string PatchId { get { return "bomb.defend"; } }
    protected override bool UsesTeamTactics { get { return true; } }
    protected override AIUtilityBias Bias
    {
        get
        {
            return new AIUtilityBias { Engage = 0.12f, TakeCover = 0.22f };
        }
    }
}
