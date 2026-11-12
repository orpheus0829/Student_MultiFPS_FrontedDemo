using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerMeleeCombatCtrl : MonoBehaviour
{
    private const string DefaultMeleeWeaponId = "weapon.melee-fist.data";
    private const int HitBufferSize = 48;

    [Header("近战武器数据")]
    [SerializeField, ResourceId(typeof(WeaponDefinitionSO)), InspectorName("拳击武器数据 ID")]
    private string _weaponResourceId = DefaultMeleeWeaponId;
    [SerializeField, InspectorName("近战武器数据兜底")] private WeaponDefinitionSO _weaponDataFallback;
    [SerializeField, InspectorName("命中爆裂特效")] private GameObject _hitEffectPrefab;
    [SerializeField, Range(0.001f, 1f), InspectorName("命中特效缩放")] private float _hitEffectScale = 0.08f;

    [Header("依赖（可自动查找）")]
    [SerializeField, InspectorName("玩家动作")] private PlayerActionCtrl _actionCtrl;
    [SerializeField, InspectorName("玩家身份")] private PlayerInfo _playerInfo;
    [SerializeField, InspectorName("生命组件")] private CharacterHealth _health;
    [SerializeField, InspectorName("瞄准相机")] private FirstPersonCameraCtrl _cameraCtrl;

    private readonly Collider[] _hitBuffer = new Collider[HitBufferSize];
    private readonly HashSet<CharacterHealth> _seenTargets = new HashSet<CharacterHealth>();
    private ResourceMgr _resources;
    private WeaponDefinitionSO _weaponData;
    private float _elapsed;
    private bool _isPunching;
    private bool _impactResolved;
    private int _loadVersion;

    public event Action PunchStarted;
    public event Action<DamageResult> ImpactResolved;
    public event Action PunchCompleted;
    public event Action PunchCanceled;

    public bool IsPunching { get { return _isPunching; } }
    public float NormalizedProgress
    {
        get
        {
            float duration = _weaponData ? _weaponData.Melee.ActionDuration : 0f;
            return duration > 0f ? Mathf.Clamp01(_elapsed / duration) : 0f;
        }
    }
    public WeaponDefinitionSO WeaponData { get { return _weaponData; } }

    private void Awake()
    {
        ResolveDependencies();
        if (_weaponDataFallback)
        {
            _weaponData = _weaponDataFallback;
        }
    }

    private void OnEnable()
    {
        ResolveDependencies();
    }

    private void Start()
    {
        _ = LoadWeaponDataAsync();
    }

    private void Update()
    {
        if (!_isPunching)
        {
            return;
        }

        _elapsed += Time.deltaTime;
        WeaponMeleeData melee = _weaponData.Melee;
        if (!_impactResolved && _elapsed >= melee.ImpactDelay)
        {
            ResolveImpactNow();
        }

        if (_elapsed >= melee.ActionDuration)
        {
            CompletePunch();
        }
    }

    private void OnDisable()
    {
        CancelPunch();
    }

    private void OnDestroy()
    {
        _loadVersion++;
        if (_resources && _weaponData && !string.IsNullOrWhiteSpace(_weaponResourceId))
        {
            _resources.Release<WeaponDefinitionSO>(_weaponResourceId);
        }
        _weaponData = null;
    }

    public bool TryPunch()
    {
        if (_isPunching || !_weaponData || !_weaponData.Melee.Enabled ||
            (_health && _health.IsDead))
        {
            return false;
        }

        _elapsed = 0f;
        _impactResolved = false;
        _isPunching = true;
        PunchStarted?.Invoke();
        return true;
    }

    public bool TryStartPunch()
    {
        return TryPunch();
    }

    public bool CancelPunch()
    {
        if (!_isPunching)
        {
            return false;
        }

        _isPunching = false;
        _elapsed = 0f;
        _impactResolved = false;
        PunchCanceled?.Invoke();
        return true;
    }

    public void SetWeaponDataForTests(WeaponDefinitionSO weaponData)
    {
        _weaponData = weaponData;
    }

    // 可由动画事件精确对齐接触帧；没有动画事件时仍由 ImpactDelay 自动触发。
    public bool ResolveImpactNow()
    {
        if (!_isPunching || _impactResolved)
        {
            return false;
        }

        _impactResolved = true;
        ResolveMeleeHit();
        return true;
    }

    private async Task LoadWeaponDataAsync()
    {
        int version = ++_loadVersion;
        if (string.IsNullOrWhiteSpace(_weaponResourceId))
        {
            return;
        }

        _resources = ResourceMgr.instance;
        try
        {
            WeaponDefinitionSO loaded = await _resources.LoadAsync<WeaponDefinitionSO>(
                _weaponResourceId);
            if (!this || version != _loadVersion)
            {
                if (loaded)
                {
                    _resources.Release<WeaponDefinitionSO>(_weaponResourceId);
                }
                return;
            }
            if (loaded)
            {
                _weaponData = loaded;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"拳击武器数据加载失败：{exception.Message}", this);
        }
    }

    private void ResolveMeleeHit()
    {
        if (!_weaponData)
        {
            return;
        }

        GetAttackRay(out Vector3 origin, out Vector3 direction);
        WeaponMeleeData melee = _weaponData.Melee;
        Vector3 start = origin + direction * Mathf.Min(0.2f, melee.Range * 0.25f);
        Vector3 end = origin + direction * melee.Range;
        int count = Physics.OverlapCapsuleNonAlloc(
            start, end, melee.Radius, _hitBuffer, ~0, QueryTriggerInteraction.Collide);

        _seenTargets.Clear();
        CharacterHealth bestTarget = null;
        Vector3 bestPoint = Vector3.zero;
        float bestScore = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider candidate = _hitBuffer[i];
            if (!candidate || candidate.transform.IsChildOf(transform))
            {
                continue;
            }

            CharacterHealth target = candidate.GetComponentInParent<CharacterHealth>();
            if (!target || target == _health || target.IsDead || !_seenTargets.Add(target))
            {
                continue;
            }

            Vector3 point = candidate.ClosestPoint(end);
            Vector3 offset = point - origin;
            float forwardDistance = Vector3.Dot(offset, direction);
            if (forwardDistance < -melee.Radius || forwardDistance > melee.Range + melee.Radius)
            {
                continue;
            }

            float lateralDistance = Vector3.Cross(direction, offset).magnitude;
            float score = Mathf.Max(0f, forwardDistance) + lateralDistance * 0.35f;
            if (score < bestScore && HasLineOfSight(origin, point, target))
            {
                bestScore = score;
                bestTarget = target;
                bestPoint = point;
            }
        }

        if (!bestTarget)
        {
            return;
        }

        WeaponDamageData damage = _weaponData.Damage;
        DamageRequest request = new DamageRequest(bestTarget, damage.CloseDamage);
        request.Instigator = _playerInfo;
        request.SourceId = _weaponData.Basic.WeaponId;
        request.SourceName = _weaponData.Basic.WeaponName;
        request.SourceIconId = _weaponData.Basic.SilhouetteResourceId;
        request.HitDistance = Vector3.Distance(origin, bestPoint);
        request.BaseHitScore = damage.BaseHitScore;
        request.DamageScorePerHealth = damage.DamageScorePerHealth;
        request.WeaponScoreFactor = damage.WeaponScoreFactor;
        request.DistanceScoreFactor = 1f;
        request.BodyPart = BodyPart.None;
        request.BodyDamageFactor = 1f;
        request.BodyScoreFactor = 1f;
        request.KillBonus = damage.KillBonus;
        request.HitPoint = bestPoint;
        request.HitDirection = direction;
        DamageResult result = CombatDamage.Apply(request);
        if (result.Outcome != DamageOutcome.Ignored && _hitEffectPrefab)
        {
            GameObject effect = Instantiate(
                _hitEffectPrefab,
                bestPoint,
                Quaternion.LookRotation(-direction, Vector3.up));
            effect.transform.localScale = Vector3.one * _hitEffectScale;
            Destroy(effect, 1.25f);
        }
        ImpactResolved?.Invoke(result);
    }

    private bool HasLineOfSight(Vector3 origin, Vector3 point, CharacterHealth target)
    {
        Vector3 delta = point - origin;
        float distance = delta.magnitude;
        if (distance <= 0.001f)
        {
            return true;
        }

        RaycastHit[] hits = Physics.RaycastAll(
            origin, delta / distance, distance, ~0, QueryTriggerInteraction.Collide);
        Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i].collider;
            if (!hit || hit.transform.IsChildOf(transform) ||
                (hit.isTrigger && !hit.GetComponentInParent<CharacterHealth>()))
            {
                continue;
            }
            return hit.GetComponentInParent<CharacterHealth>() == target;
        }
        return true;
    }

    private void GetAttackRay(out Vector3 origin, out Vector3 direction)
    {
        if (_cameraCtrl)
        {
            Ray ray = _cameraCtrl.GetCenterAimRay();
            origin = ray.origin;
            direction = ray.direction.normalized;
            return;
        }

        origin = transform.position + Vector3.up * 1.25f;
        direction = transform.forward;
    }

    private void CompletePunch()
    {
        _isPunching = false;
        _elapsed = 0f;
        _impactResolved = false;
        PunchCompleted?.Invoke();
    }

    private void ResolveDependencies()
    {
        if (!_actionCtrl) TryGetComponent(out _actionCtrl);
        if (!_playerInfo) TryGetComponent(out _playerInfo);
        if (!_health) TryGetComponent(out _health);
        if (!_cameraCtrl) _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);
    }
}
