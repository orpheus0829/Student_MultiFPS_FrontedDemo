using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponProjectileCtrl : MonoBehaviour
{
    private const int HitBufferSize = 32;
    private const float MinimumDirectionMagnitude = 0.0001f;
    private const float BodyHitboxPreferenceTolerance = 0.75f;

    [Header("弹丸曳光")]
    [SerializeField, Min(0.1f)] private float _tracerLength = 3f;
    [SerializeField, Min(0.001f)] private float _tracerWidth = 0.085f;
    [SerializeField, Min(0f)] private float _tracerFadeDuration = 0.06f;

    private readonly RaycastHit[] _hitBuffer = new RaycastHit[HitBufferSize];
    private readonly Collider[] _overlapBuffer = new Collider[HitBufferSize];

    private ParticleSystem[] _particleSystems;
    private LineRenderer _tracer;
    private float _tracerFadeRemaining;
    private Transform _ownerRoot;
    private PlayerInfo _instigator;
    private BulletImpactCtrl _impactCtrl;
    private LayerMask _hitMask;
    private Vector3 _direction;
    private float _speed;
    private float _radius;
    private float _maximumDistance;
    private float _travelledDistance;
    private float _closeDamage;
    private float _farDamage;
    private float _closeDamageRange;
    private float _farDamageRange;
    private float _baseHitScore;
    private float _damageScorePerHealth;
    private float _weaponScoreFactor;
    private float _closeDistanceScoreFactor;
    private float _farDistanceScoreFactor;
    private float _killBonus;
    private string _sourceId;
    private string _sourceName;
    private string _sourceIconId;
    private bool _isFlying;

    private void Awake()
    {
        _particleSystems = GetComponentsInChildren<ParticleSystem>(true);
        PrepareTracer();
    }

    private void OnEnable()
    {
        _tracerFadeRemaining = 0f;
        if (_tracer)
        {
            _tracer.enabled = false;
            _tracer.widthMultiplier = _tracerWidth;
        }
        RestartParticles();
    }

    private void Update()
    {
        if (!_isFlying)
        {
            UpdateTracerFade();
            return;
        }

        // SphereCast 不报告起点已在内部的碰撞体，贴脸和枪口穿入物体时先检测重叠。
        if (TryFindInitialOverlap(out Collider overlappingCollider))
        {
            ResolveCharacterHit(overlappingCollider, overlappingCollider.ClosestPoint(transform.position));
            FinishFlight();
            return;
        }

        float remainingDistance = _maximumDistance - _travelledDistance;
        float stepDistance = Mathf.Min(_speed * Time.deltaTime, remainingDistance);
        if (stepDistance <= 0f)
        {
            FinishFlight();
            return;
        }

        if (TryFindNearestHit(stepDistance, out RaycastHit hit))
        {
            _travelledDistance += hit.distance;
            transform.position += _direction * hit.distance;
            RefreshTracer();
            ResolveHit(hit);
            FinishFlight();
            return;
        }

        transform.position += _direction * stepDistance;
        _travelledDistance += stepDistance;
        RefreshTracer();
        if (_travelledDistance >= _maximumDistance)
        {
            FinishFlight();
        }
    }

    private void OnDisable()
    {
        _isFlying = false;
        _tracerFadeRemaining = 0f;
        if (_tracer)
        {
            _tracer.enabled = false;
        }
        StopParticles();
    }

    public void Initialize(
        WeaponDefinitionSO weaponData,
        PlayerInfo instigator,
        Transform ownerRoot,
        BulletImpactCtrl impactCtrl,
        LayerMask hitMask,
        Vector3 direction)
    {
        if (!weaponData || direction.sqrMagnitude <= MinimumDirectionMagnitude)
        {
            FinishFlight();
            return;
        }

        WeaponDamageData damage = weaponData.Damage;
        _ownerRoot = ownerRoot;
        _instigator = instigator;
        _impactCtrl = impactCtrl;
        _hitMask = hitMask;
        _direction = direction.normalized;
        _speed = Mathf.Max(0.01f, damage.ProjectileSpeed);
        _radius = Mathf.Max(0f, damage.ProjectileRadius);
        _maximumDistance = Mathf.Max(0f, damage.MaximumHitRange);
        _travelledDistance = 0f;
        _closeDamage = damage.CloseDamage;
        _farDamage = damage.FarDamage;
        _closeDamageRange = damage.CloseDamageRange;
        _farDamageRange = damage.FarDamageRange;
        _baseHitScore = damage.BaseHitScore;
        _damageScorePerHealth = damage.DamageScorePerHealth;
        _weaponScoreFactor = damage.WeaponScoreFactor;
        _closeDistanceScoreFactor = damage.CloseDistanceScoreFactor;
        _farDistanceScoreFactor = damage.FarDistanceScoreFactor;
        _killBonus = damage.KillBonus;
        _sourceId = weaponData.Basic.WeaponId;
        _sourceName = weaponData.Basic.WeaponName;
        _sourceIconId = weaponData.Basic.SilhouetteResourceId;
        transform.rotation = Quaternion.LookRotation(_direction, Vector3.up);
        _isFlying = _maximumDistance > 0f;

        if (!_isFlying)
        {
            FinishFlight();
        }
    }

    private bool TryFindNearestHit(float distance, out RaycastHit nearestHit)
    {
        int hitCount = _radius > 0f
            ? Physics.SphereCastNonAlloc(
                transform.position, _radius, _direction, _hitBuffer, distance,
                _hitMask, QueryTriggerInteraction.Collide)
            : Physics.RaycastNonAlloc(
                transform.position, _direction, _hitBuffer, distance,
                _hitMask, QueryTriggerInteraction.Collide);
        int bodyIndex = -1;
        int targetIndex = -1;
        int worldIndex = -1;
        float bodyDistance = float.PositiveInfinity;
        float targetDistance = float.PositiveInfinity;
        float worldDistance = float.PositiveInfinity;
        nearestHit = default;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _hitBuffer[i];
            if (!CanHitCollider(hit.collider, _ownerRoot))
            {
                continue;
            }

            if (hit.collider.TryGetComponent(out BodyHitbox _))
            {
                if (hit.distance < bodyDistance)
                {
                    bodyIndex = i;
                    bodyDistance = hit.distance;
                }
                continue;
            }

            if (hit.collider.GetComponentInParent<CharacterHealth>())
            {
                if (hit.distance < targetDistance)
                {
                    targetIndex = i;
                    targetDistance = hit.distance;
                }
                continue;
            }

            if (hit.distance < worldDistance)
            {
                worldIndex = i;
                worldDistance = hit.distance;
            }
        }

        if (bodyIndex >= 0 &&
            bodyDistance <= worldDistance &&
            bodyDistance <= targetDistance + BodyHitboxPreferenceTolerance)
        {
            nearestHit = _hitBuffer[bodyIndex];
        }
        else if (targetIndex >= 0 && targetDistance <= worldDistance)
        {
            nearestHit = _hitBuffer[targetIndex];
        }
        else if (worldIndex >= 0)
        {
            nearestHit = _hitBuffer[worldIndex];
        }

        return nearestHit.collider;
    }

    public static bool CanHitCollider(Collider hitCollider, Transform ownerRoot)
    {
        if (!hitCollider || (ownerRoot && hitCollider.transform.IsChildOf(ownerRoot)))
        {
            return false;
        }

        // 出生区等玩法 Trigger 不挡弹；BodyHitbox 和生命触发体仍可受到伤害。
        return !hitCollider.isTrigger || hitCollider.GetComponent<BodyHitbox>() ||
            hitCollider.GetComponentInParent<CharacterHealth>();
    }

    private bool TryFindInitialOverlap(out Collider overlappingCollider)
    {
        int count = Physics.OverlapSphereNonAlloc(
            transform.position, Mathf.Max(0.001f, _radius), _overlapBuffer,
            _hitMask, QueryTriggerInteraction.Collide);
        Collider body = null;
        Collider character = null;
        overlappingCollider = null;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = _overlapBuffer[i];
            if (!CanHitCollider(candidate, _ownerRoot))
            {
                continue;
            }
            if (candidate.TryGetComponent(out BodyHitbox _))
            {
                body = candidate;
            }
            else if (candidate.GetComponentInParent<CharacterHealth>())
            {
                character = candidate;
            }
            else
            {
                // 起点同时陷在墙和角色里时先由实体遮挡，避免隔墙扣血。
                overlappingCollider = candidate;
                return true;
            }
        }

        overlappingCollider = body ? body : character;
        return overlappingCollider;
    }

    private void ResolveHit(RaycastHit hit)
    {
        if (ResolveCharacterHit(hit.collider, hit.point))
        {
            return;
        }

        if (_impactCtrl)
        {
            _impactCtrl.ShowImpact(hit);
        }
    }

    private bool ResolveCharacterHit(Collider hitCollider, Vector3 hitPoint)
    {
        DamageRequest request = CreateDamageRequest(hitPoint);
        if (hitCollider.TryGetComponent(out BodyHitbox bodyHitbox))
        {
            bodyHitbox.ReceiveHit(request);
            return true;
        }

        CharacterHealth target = hitCollider.GetComponentInParent<CharacterHealth>();
        if (target)
        {
            request.Target = target;
            CombatDamage.Apply(request);
            return true;
        }

        return false;
    }

    private DamageRequest CreateDamageRequest(Vector3 hitPoint)
    {
        float damage = GetDistanceValue(
            _travelledDistance,
            _closeDamageRange,
            _farDamageRange,
            _closeDamage,
            _farDamage);
        DamageRequest request = new DamageRequest(null, damage);
        request.Instigator = _instigator;
        request.SourceId = _sourceId;
        request.SourceName = _sourceName;
        request.SourceIconId = _sourceIconId;
        request.HitDistance = _travelledDistance;
        request.BaseHitScore = _baseHitScore;
        request.DamageScorePerHealth = _damageScorePerHealth;
        request.WeaponScoreFactor = _weaponScoreFactor;
        request.DistanceScoreFactor = GetDistanceValue(
            _travelledDistance,
            _closeDamageRange,
            _farDamageRange,
            _closeDistanceScoreFactor,
            _farDistanceScoreFactor);
        request.KillBonus = _killBonus;
        request.HitPoint = hitPoint;
        request.HitDirection = _direction;
        return request;
    }

    private static float GetDistanceValue(
        float distance,
        float closeRange,
        float farRange,
        float closeValue,
        float farValue)
    {
        if (farRange <= closeRange)
        {
            return distance <= closeRange ? closeValue : farValue;
        }

        float progress = Mathf.InverseLerp(closeRange, farRange, distance);
        return Mathf.Lerp(closeValue, farValue, progress);
    }

    private void RestartParticles()
    {
        if (_particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            _particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _particleSystems[i].Play(true);
        }
    }

    private void StopParticles()
    {
        if (_particleSystems == null)
        {
            return;
        }

        for (int i = 0; i < _particleSystems.Length; i++)
        {
            _particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void FinishFlight()
    {
        _isFlying = false;
        // 伤害在命中帧结算，已飞过的短光段稍后淡出，近距离射击也能看见轨迹。
        if (_tracer && _tracer.enabled && _tracerFadeDuration > 0f)
        {
            _tracerFadeRemaining = _tracerFadeDuration;
            for (int i = 0; i < _particleSystems.Length; i++)
            {
                _particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }
            return;
        }

        gameObject.SetActive(false);
    }

    private void PrepareTracer()
    {
        // 复用正式 Hovl 材质，不为每颗弹丸创建材质实例。
        ParticleSystemRenderer particleRenderer = GetComponentInChildren<ParticleSystemRenderer>(true);
        if (!particleRenderer || !particleRenderer.sharedMaterial)
        {
            return;
        }

        _tracer = gameObject.AddComponent<LineRenderer>();
        _tracer.sharedMaterial = particleRenderer.sharedMaterial;
        _tracer.useWorldSpace = false;
        _tracer.positionCount = 3;
        _tracer.textureMode = LineTextureMode.Stretch;
        _tracer.widthMultiplier = _tracerWidth;
        _tracer.widthCurve = AnimationCurve.Linear(0f, 0.15f, 1f, 1f);
        _tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _tracer.receiveShadows = false;
        _tracer.allowOcclusionWhenDynamic = false;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.68f, 0.12f), 0f),
                new GradientColorKey(new Color(1f, 0.92f, 0.58f), 0.55f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.35f),
                new GradientAlphaKey(1f, 1f)
            });
        _tracer.colorGradient = gradient;
        _tracer.enabled = false;
    }

    private void RefreshTracer()
    {
        if (!_tracer)
        {
            return;
        }

        // 只显示已经实际飞行过的一小段，既不提前贯穿目标，也不画整条射程射线。
        float length = Mathf.Min(_tracerLength, _travelledDistance);
        _tracer.SetPosition(0, Vector3.back * length);
        _tracer.SetPosition(1, Vector3.back * length * 0.45f);
        _tracer.SetPosition(2, Vector3.zero);
        _tracer.enabled = length > 0.001f;
    }

    private void UpdateTracerFade()
    {
        if (_tracerFadeRemaining <= 0f)
        {
            return;
        }

        _tracerFadeRemaining = Mathf.Max(0f, _tracerFadeRemaining - Time.deltaTime);
        float alpha = _tracerFadeRemaining / _tracerFadeDuration;
        // 宽度回落保留原有黄白渐变，避免修改共享材质影响其他弹丸。
        _tracer.widthMultiplier = _tracerWidth * alpha;
        if (_tracerFadeRemaining <= 0f)
        {
            gameObject.SetActive(false);
        }
    }
}
