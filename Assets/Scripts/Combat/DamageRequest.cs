using UnityEngine;

public sealed class DamageRequest
{
    private CharacterHealth _target;
    private PlayerInfo _instigator;
    private string _sourceId;
    private string _sourceName;
    private string _sourceIconId;
    private float _baseDamage;
    private float _hitDistance;
    private float _baseHitScore;
    private float _weaponScoreFactor;
    private float _distanceScoreFactor;
    private BodyPart _bodyPart;
    private float _bodyDamageFactor;
    private float _bodyScoreFactor;
    private float _killBonus;
    private Vector3 _hitPoint;
    private Vector3 _hitDirection;

    public CharacterHealth Target
    {
        get
        {
            return _target;
        }
        set
        {
            _target = value;
        }
    }

    public PlayerInfo Instigator
    {
        get
        {
            return _instigator;
        }
        set
        {
            _instigator = value;
        }
    }

    public string SourceId
    {
        get
        {
            return _sourceId;
        }
        set
        {
            _sourceId = value ?? string.Empty;
        }
    }

    public string SourceName
    {
        get
        {
            return _sourceName;
        }
        set
        {
            _sourceName = value ?? string.Empty;
        }
    }

    public string SourceIconId
    {
        get
        {
            return _sourceIconId;
        }
        set
        {
            _sourceIconId = value ?? string.Empty;
        }
    }

    public float BaseDamage
    {
        get
        {
            return _baseDamage;
        }
        set
        {
            _baseDamage = value;
        }
    }

    public float HitDistance
    {
        get
        {
            return _hitDistance;
        }
        set
        {
            _hitDistance = value;
        }
    }

    public float BaseHitScore
    {
        get
        {
            return _baseHitScore;
        }
        set
        {
            _baseHitScore = value;
        }
    }

    public float WeaponScoreFactor
    {
        get
        {
            return _weaponScoreFactor;
        }
        set
        {
            _weaponScoreFactor = value;
        }
    }

    public float DistanceScoreFactor
    {
        get
        {
            return _distanceScoreFactor;
        }
        set
        {
            _distanceScoreFactor = value;
        }
    }

    public BodyPart BodyPart
    {
        get
        {
            return _bodyPart;
        }
        set
        {
            _bodyPart = value;
        }
    }

    public float BodyDamageFactor
    {
        get
        {
            return _bodyDamageFactor;
        }
        set
        {
            _bodyDamageFactor = value;
        }
    }

    public float BodyScoreFactor
    {
        get
        {
            return _bodyScoreFactor;
        }
        set
        {
            _bodyScoreFactor = value;
        }
    }

    public float KillBonus
    {
        get
        {
            return _killBonus;
        }
        set
        {
            _killBonus = value;
        }
    }

    public Vector3 HitPoint
    {
        get
        {
            return _hitPoint;
        }
        set
        {
            _hitPoint = value;
        }
    }

    public Vector3 HitDirection
    {
        get
        {
            return _hitDirection;
        }
        set
        {
            _hitDirection = value;
        }
    }

    public DamageRequest(CharacterHealth target, float baseDamage)
    {
        _target = target;
        _instigator = null;
        _sourceId = string.Empty;
        _sourceName = string.Empty;
        _sourceIconId = string.Empty;
        _baseDamage = baseDamage;
        _hitDistance = 0f;
        _baseHitScore = 0f;
        _weaponScoreFactor = 1f;
        _distanceScoreFactor = 1f;
        _bodyPart = BodyPart.None;
        _bodyDamageFactor = 1f;
        _bodyScoreFactor = 1f;
        _killBonus = 0f;
        _hitPoint = Vector3.zero;
        _hitDirection = Vector3.zero;
    }

    public DamageRequest(DamageRequest source)
    {
        if (source == null)
        {
            _target = null;
            _instigator = null;
            _sourceId = string.Empty;
            _sourceName = string.Empty;
            _sourceIconId = string.Empty;
            _baseDamage = 0f;
            _hitDistance = 0f;
            _baseHitScore = 0f;
            _weaponScoreFactor = 1f;
            _distanceScoreFactor = 1f;
            _bodyPart = BodyPart.None;
            _bodyDamageFactor = 1f;
            _bodyScoreFactor = 1f;
            _killBonus = 0f;
            _hitPoint = Vector3.zero;
            _hitDirection = Vector3.zero;
            return;
        }

        _target = source.Target;
        _instigator = source.Instigator;
        _sourceId = source.SourceId;
        _sourceName = source.SourceName;
        _sourceIconId = source.SourceIconId;
        _baseDamage = source.BaseDamage;
        _hitDistance = source.HitDistance;
        _baseHitScore = source.BaseHitScore;
        _weaponScoreFactor = source.WeaponScoreFactor;
        _distanceScoreFactor = source.DistanceScoreFactor;
        _bodyPart = source.BodyPart;
        _bodyDamageFactor = source.BodyDamageFactor;
        _bodyScoreFactor = source.BodyScoreFactor;
        _killBonus = source.KillBonus;
        _hitPoint = source.HitPoint;
        _hitDirection = source.HitDirection;
    }
}
