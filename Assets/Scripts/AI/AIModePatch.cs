using System;
using UnityEngine;

public enum AIObjectiveKind
{
    None = 0,
    Regroup = 1,
    AttackSite = 2,
    DefendSite = 3,
    Bomb = 4
}

public readonly struct AIObjective
{
    public Vector3 Position { get; }
    public AIObjectiveKind Kind { get; }

    public AIObjective(Vector3 position, AIObjectiveKind kind)
    {
        Position = position;
        Kind = kind;
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
    AIUtilityBias GetUtilityBias();
    bool TryGetObjective(out AIObjective objective);
}

public abstract class AIModePatchBase : IAIModePatch
{
    public abstract string PatchId { get; }
    protected abstract AIUtilityBias Bias { get; }

    public IAIModePatchSession CreateSession(AIPlayerCtrl ai, AIModeContext context)
    {
        return new Session(ai, context, Bias);
    }

    private sealed class Session : IAIModePatchSession
    {
        private readonly AIPlayerCtrl _ai;
        private readonly AIModeContext _context;
        private readonly AIUtilityBias _bias;

        public Session(AIPlayerCtrl ai, AIModeContext context, AIUtilityBias bias)
        {
            _ai = ai;
            _context = context;
            _bias = bias;
        }

        public void OnMatchStart() { }
        public void OnRoundStart(int roundIndex) { }
        public void OnRoundEnd(bool won) { }
        public void OnTargetKilled(PlayerInfo victim) { }
        public AIUtilityBias GetUtilityBias() { return _bias; }

        public bool TryGetObjective(out AIObjective objective)
        {
            AIObjective? value = _context != null && _context.ObjectiveProvider != null
                ? _context.ObjectiveProvider(_ai)
                : null;
            objective = value.GetValueOrDefault();
            return value.HasValue;
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
    protected override AIUtilityBias Bias
    {
        get
        {
            return new AIUtilityBias { Engage = 0.12f, Reposition = 0.2f };
        }
    }
}

public sealed class BombAttackPatch : AIModePatchBase
{
    public override string PatchId { get { return "bomb.attack"; } }
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
    protected override AIUtilityBias Bias
    {
        get
        {
            return new AIUtilityBias { Engage = 0.12f, TakeCover = 0.22f };
        }
    }
}
