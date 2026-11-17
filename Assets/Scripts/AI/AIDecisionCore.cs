using UnityEngine;

public sealed class AIDecisionCore
{
    private AIBehaviorId _current = AIBehaviorId.Patrol;
    private float _currentScore;
    private float _lastSwitchTime = float.NegativeInfinity;
    private float _nextDecisionTime;

    public AIBehaviorId Current { get { return _current; } }
    public float CurrentScore { get { return _currentScore; } }

    public bool TrySelect(
        AIWorldModel world,
        CharacterHealth health,
        AIDifficultyProfile profile,
        bool hasCover,
        bool needsReload,
        AIUtilityBias utilityBias,
        out AIBehaviorId selected)
    {
        selected = _current;
        if (Time.time < _nextDecisionTime)
        {
            return false;
        }
        _nextDecisionTime = Time.time + profile.DecisionInterval;
        AITrackedTarget target = world.CurrentTarget;
        float healthRatio = Mathf.Clamp01(
            health.CurrentHealth / Mathf.Max(1f, health.MaxHealth));
        if (_current == AIBehaviorId.Retreat && health.RecoveryEnabled &&
            (healthRatio < profile.ReturnHealthRatio || (target != null && target.Visible)))
        {
            _currentScore = 1f;
            return false;
        }
        AIUtilityWeights w = profile.Utility;
        float visible = target != null && target.Visible ? 1f : 0f;
        float confidence = target != null && !target.Visible ? target.Confidence : 0f;
        float rangeSuitability = target == null ? 0f : 1f - Mathf.Clamp01(
            Mathf.Abs(target.Distance - profile.PreferredRange) /
            Mathf.Max(profile.RangeBand, profile.PreferredRange));
        float distanceDeviation = target == null ? 0f : 1f - rangeSuitability;
        float danger = 1f - healthRatio;
        float damagePressure = world.RecentDamagePressure;
        float patrol = target == null && !world.HasRecentNoise
            ? w.PatrolNoTarget + w.PatrolQuiet
            : 0f;
        float investigate = visible > 0f ? 0f :
            confidence * w.InvestigateConfidence +
            world.NoiseConfidence * w.InvestigateNoise;
        float engage = visible * (w.EngageVisibility +
            rangeSuitability * w.EngageRange +
            healthRatio * w.EngageHealth +
            profile.Aggression * w.EngageAggression) *
            Mathf.Lerp(1f, 0.55f, danger);
        float takeCover = hasCover && target != null ?
            visible * w.CoverVisibility +
            danger * w.CoverDanger +
            damagePressure * 0.55f +
            w.CoverAvailable * Mathf.Max(danger, damagePressure) +
            profile.CoverPreference * w.CoverPreference *
                Mathf.Max(danger, needsReload ? 0.65f : 0.15f) +
            (needsReload ? w.CoverReload : 0f) : 0f;
        if (target == null || !hasCover) takeCover = 0f;
        float reposition = target == null || !target.Visible ? 0f :
            distanceDeviation * w.RepositionDistance +
            profile.StrafeScale * w.RepositionStrafe +
            profile.FlankChance * w.RepositionFlank +
            damagePressure * 0.24f;
        float retreatWindow = Mathf.Max(
            0.01f,
            profile.ReturnHealthRatio - profile.RetreatHealthRatio);
        float healthDanger = Mathf.Clamp01(
            (profile.ReturnHealthRatio - healthRatio) / retreatWindow);
        float retreat = target != null && hasCover &&
            (health.RecoveryEnabled || healthRatio <= profile.RetreatHealthRatio) ?
            healthDanger * w.RetreatDanger +
            damagePressure * 0.45f +
            w.RetreatCover * healthDanger +
            profile.RetreatPreference * w.RetreatPreference : 0f;

        patrol = ApplyBias(patrol, utilityBias.Patrol);
        investigate = ApplyBias(investigate, utilityBias.Investigate);
        engage = ApplyBias(engage, utilityBias.Engage);
        takeCover = ApplyBias(takeCover, utilityBias.TakeCover);
        reposition = ApplyBias(reposition, utilityBias.Reposition);
        retreat = ApplyBias(retreat, utilityBias.Retreat);

        AIBehaviorId best = _current;
        float currentScore = Score(
            _current, patrol, investigate, engage, takeCover, reposition, retreat);
        float bestScore = currentScore;
        for (int i = 0; i < 6; i++)
        {
            float candidate = Score((AIBehaviorId)i, patrol, investigate, engage,
                takeCover, reposition, retreat);
            if (candidate > bestScore)
            {
                bestScore = candidate;
                best = (AIBehaviorId)i;
            }
        }
        if (best != _current &&
            (Time.time - _lastSwitchTime < GetCommitTime(_current, profile) ||
             bestScore <= currentScore + profile.Hysteresis))
        {
            _currentScore = currentScore;
            return false;
        }
        _currentScore = bestScore;
        if (best == _current)
        {
            return false;
        }
        _current = best;
        _lastSwitchTime = Time.time;
        selected = best;
        return true;
    }

    private static float ApplyBias(float score, float bias)
    {
        return score <= 0f ? 0f : Mathf.Clamp(score + bias, 0f, 2f);
    }

    private static float GetCommitTime(
        AIBehaviorId behavior,
        AIDifficultyProfile profile)
    {
        switch (behavior)
        {
            case AIBehaviorId.TakeCover:
                return Mathf.Max(
                    profile.MinimumCommitTime,
                    profile.CoverHideDuration + profile.CoverPeekDuration);
            case AIBehaviorId.Reposition:
                return Mathf.Max(profile.MinimumCommitTime, profile.FlankMoveDuration);
            case AIBehaviorId.Engage:
                return Mathf.Max(profile.MinimumCommitTime, profile.CombatHoldMin * 0.7f);
            default:
                return profile.MinimumCommitTime;
        }
    }

    private static float Score(
        AIBehaviorId behavior,
        float patrol,
        float investigate,
        float engage,
        float takeCover,
        float reposition,
        float retreat)
    {
        switch (behavior)
        {
            case AIBehaviorId.Patrol: return patrol;
            case AIBehaviorId.Investigate: return investigate;
            case AIBehaviorId.Engage: return engage;
            case AIBehaviorId.TakeCover: return takeCover;
            case AIBehaviorId.Reposition: return reposition;
            case AIBehaviorId.Retreat: return retreat;
            default: return 0f;
        }
    }
}
