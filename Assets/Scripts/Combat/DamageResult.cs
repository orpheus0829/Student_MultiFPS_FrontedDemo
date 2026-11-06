using UnityEngine;

public sealed class DamageResult
{
    private readonly DamageOutcome _outcome;
    private readonly CharacterHealth _target;
    private readonly PlayerInfo _victim;
    private readonly PlayerInfo _instigator;
    private readonly string _sourceId;
    private readonly string _sourceName;
    private readonly string _sourceIconId;
    private readonly BodyPart _bodyPart;
    private readonly float _requestedDamage;
    private readonly float _appliedDamage;
    private readonly float _remainingHealth;
    private readonly float _hitDistance;
    private readonly Vector3 _hitPoint;
    private readonly Vector3 _hitDirection;
    private readonly int _hitScore;
    private readonly int _killScore;

    public DamageOutcome Outcome
    {
        get
        {
            return _outcome;
        }
    }

    public bool WasApplied
    {
        get
        {
            return _outcome != DamageOutcome.Ignored;
        }
    }

    public bool WasKill
    {
        get
        {
            return _outcome == DamageOutcome.Kill;
        }
    }

    public bool WasHeadshot
    {
        get
        {
            return _bodyPart == BodyPart.Head;
        }
    }

    public CharacterHealth Target
    {
        get
        {
            return _target;
        }
    }

    public PlayerInfo Victim
    {
        get
        {
            return _victim;
        }
    }

    public PlayerInfo Instigator
    {
        get
        {
            return _instigator;
        }
    }

    public string SourceId
    {
        get
        {
            return _sourceId;
        }
    }

    public string SourceName
    {
        get
        {
            return _sourceName;
        }
    }

    public string SourceIconId
    {
        get
        {
            return _sourceIconId;
        }
    }

    public BodyPart BodyPart
    {
        get
        {
            return _bodyPart;
        }
    }

    public float RequestedDamage
    {
        get
        {
            return _requestedDamage;
        }
    }

    public float AppliedDamage
    {
        get
        {
            return _appliedDamage;
        }
    }

    public float RemainingHealth
    {
        get
        {
            return _remainingHealth;
        }
    }

    public float HitDistance
    {
        get
        {
            return _hitDistance;
        }
    }

    public Vector3 HitPoint
    {
        get
        {
            return _hitPoint;
        }
    }

    public Vector3 HitDirection
    {
        get
        {
            return _hitDirection;
        }
    }

    public int HitScore
    {
        get
        {
            return _hitScore;
        }
    }

    public int KillScore
    {
        get
        {
            return _killScore;
        }
    }

    public int AwardedScore
    {
        get
        {
            return _hitScore + _killScore;
        }
    }

    internal DamageResult(
        DamageOutcome outcome,
        DamageRequest request,
        PlayerInfo victim,
        float requestedDamage,
        float appliedDamage,
        float remainingHealth,
        int hitScore,
        int killScore)
    {
        _outcome = outcome;
        _target = request != null ? request.Target : null;
        _victim = victim;
        _instigator = request != null ? request.Instigator : null;
        _sourceId = request != null ? request.SourceId : string.Empty;
        _sourceName = request != null ? request.SourceName : string.Empty;
        _sourceIconId = request != null ? request.SourceIconId : string.Empty;
        _bodyPart = request != null ? request.BodyPart : BodyPart.None;
        _requestedDamage = requestedDamage;
        _appliedDamage = appliedDamage;
        _remainingHealth = remainingHealth;
        _hitDistance = request != null ? request.HitDistance : 0f;
        _hitPoint = request != null ? request.HitPoint : Vector3.zero;
        _hitDirection = request != null ? request.HitDirection : Vector3.zero;
        _hitScore = hitScore;
        _killScore = killScore;
    }
}
