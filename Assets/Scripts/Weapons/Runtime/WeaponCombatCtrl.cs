using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponCombatCtrl : MonoBehaviour
{
    private const int HitBufferSize = 32;
    private const float BodyHitboxPreferenceTolerance = 0.75f;

    [Header("动作与瞄准")]
    [SerializeField, InspectorName("枪械动作")] private WeaponActionCtrl _actionCtrl;
    [SerializeField, InspectorName("玩家瞄准")] private PlayerAimCtrl _aimCtrl;
    [SerializeField, InspectorName("玩家移动")] private PlayerMoveCtrl _moveCtrl;

    [Header("射击来源")]
    [SerializeField, InspectorName("第一人称相机")] private FirstPersonCameraCtrl _cameraCtrl;
    [SerializeField, InspectorName("攻击者信息")] private PlayerInfo _playerInfo;
    [SerializeField, InspectorName("可命中层")] private LayerMask _hitMask = ~0;

    [Header("场景命中")]
    [SerializeField, InspectorName("弹孔表现")] private BulletImpactCtrl _impactCtrl;

    private readonly Dictionary<WeaponDefinitionSO, WeaponAmmoState> _ammoStates =
        new Dictionary<WeaponDefinitionSO, WeaponAmmoState>();
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[HitBufferSize];

    private WeaponDefinitionSO _weaponData;
    private WeaponAmmoState _ammoState;
    private WeaponFireMode _fireMode;
    private bool _isFireHeld;
    private int _burstShotsRemaining;
    private float _nextShotTime;
    private float _lastShotTime = float.NegativeInfinity;
    private float _spreadFromShots;

    public event Action<int, int> AmmoChanged;
    public event Action<WeaponDefinitionSO> ShotFired;

    public int MagazineAmmo
    {
        get
        {
            return _ammoState != null ? _ammoState.MagazineAmmo : 0;
        }
    }

    public int ReserveAmmo
    {
        get
        {
            return _ammoState != null ? _ammoState.ReserveAmmo : 0;
        }
    }

    public WeaponFireMode FireMode
    {
        get
        {
            return _fireMode;
        }
    }

    public float CurrentSpreadDegrees
    {
        get
        {
            if (!_weaponData)
            {
                return 0f;
            }

            float spread = CalculateCurrentSpread();
            if (_weaponData.Fire.PelletsPerShot > 1)
            {
                spread = Mathf.Max(spread, _weaponData.Accuracy.PelletSpread);
            }
            return spread;
        }
    }

    private void Awake()
    {
        if (!_actionCtrl)
        {
            TryGetComponent(out _actionCtrl);
        }
        if (!_aimCtrl)
        {
            TryGetComponent(out _aimCtrl);
        }
        if (!_moveCtrl)
        {
            TryGetComponent(out _moveCtrl);
        }
        if (!_playerInfo)
        {
            TryGetComponent(out _playerInfo);
        }
        if (!_cameraCtrl)
        {
            _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);
        }
        if (!_impactCtrl)
        {
            TryGetComponent(out _impactCtrl);
        }

        if (!_actionCtrl || !_cameraCtrl)
        {
            Debug.LogError($"{name} 的枪械实战引用未配置完整。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (!_actionCtrl)
        {
            return;
        }

        _actionCtrl.WeaponChanged += ReceiveWeaponChanged;
        _actionCtrl.MarkerReached += ReceiveActionMarker;
        _actionCtrl.ActionCompleted += ReceiveActionCompleted;
        ReceiveWeaponChanged(_actionCtrl.CurrentWeaponData);
    }

    private void Update()
    {
        RecoverSpread();
        ContinueFireInput();
    }

    private void OnDisable()
    {
        if (_actionCtrl)
        {
            _actionCtrl.WeaponChanged -= ReceiveWeaponChanged;
            _actionCtrl.MarkerReached -= ReceiveActionMarker;
            _actionCtrl.ActionCompleted -= ReceiveActionCompleted;
        }

        _isFireHeld = false;
        _burstShotsRemaining = 0;
    }

    public void ReceiveFireInput(PlayerInputPhase phase)
    {
        if (phase == PlayerInputPhase.Canceled)
        {
            _isFireHeld = false;
            return;
        }
        if (phase != PlayerInputPhase.Started)
        {
            return;
        }

        _isFireHeld = true;
        if (_fireMode == WeaponFireMode.Burst)
        {
            if (_burstShotsRemaining <= 0)
            {
                _burstShotsRemaining = Mathf.Max(1, _weaponData.Fire.BurstCount);
            }
        }

        TryFire();
    }

    public bool TryFire()
    {
        if (!_weaponData || _ammoState == null || Time.time < _nextShotTime)
        {
            return false;
        }
        if (!CanFireFromMovement())
        {
            return false;
        }

        if (_actionCtrl.IsBusy)
        {
            WeaponActionDefinition currentAction = _actionCtrl.CurrentAction;
            bool isPerRoundReload = IsPerRoundAction(currentAction.ActionType);
            if (!isPerRoundReload && !_actionCtrl.CanFireAfterReloadCommit)
            {
                return false;
            }

            if (!_actionCtrl.CancelCurrentAction(
                WeaponActionCancelReason.Manual,
                !isPerRoundReload))
            {
                return false;
            }
        }

        if (_ammoState.MagazineAmmo <= 0)
        {
            if (_weaponData.Ammo.AutoReloadWhenEmpty)
            {
                TryReload();
            }
            return false;
        }

        bool startedWhileAiming = _aimCtrl && _aimCtrl.IsAiming;
        return _actionCtrl.TryStartAction(WeaponActionType.Fire, startedWhileAiming);
    }

    public bool TryReload()
    {
        if (!_weaponData || _ammoState == null || _actionCtrl.IsBusy ||
            _ammoState.ReserveAmmo <= 0 ||
            _ammoState.MagazineAmmo >= _weaponData.Ammo.MagazineCapacity)
        {
            return false;
        }

        bool startedWhileAiming = _aimCtrl && _aimCtrl.IsAiming;
        if (_weaponData.Ammo.ReloadStyle == WeaponReloadStyle.PerRound ||
            _weaponData.Ammo.ReloadStyle == WeaponReloadStyle.Revolver)
        {
            return _actionCtrl.TryStartPerRoundReload(startedWhileAiming);
        }

        WeaponActionType actionType = _ammoState.MagazineAmmo <= 0 &&
            _actionCtrl.CurrentActionSet &&
            _actionCtrl.CurrentActionSet.TryGetAction(
                WeaponActionType.EmptyReload,
                out WeaponActionDefinition _)
            ? WeaponActionType.EmptyReload
            : WeaponActionType.Reload;
        return _actionCtrl.TryStartAction(actionType, startedWhileAiming);
    }

    public bool TrySetFireMode(WeaponFireMode fireMode)
    {
        if (!_weaponData || !_weaponData.Fire.CanSwitchMode ||
            !IsFireModeAllowed(_weaponData.Fire.AllowedModes, fireMode))
        {
            return false;
        }

        _fireMode = fireMode;
        _burstShotsRemaining = 0;
        return true;
    }

    private void ContinueFireInput()
    {
        if (!_weaponData ||
            (_actionCtrl.IsBusy && !_actionCtrl.CanFireAfterReloadCommit) ||
            Time.time < _nextShotTime)
        {
            return;
        }

        if (_fireMode == WeaponFireMode.Automatic && _isFireHeld)
        {
            TryFire();
        }
        else if (_fireMode == WeaponFireMode.Burst && _burstShotsRemaining > 0)
        {
            TryFire();
        }
    }

    private void ReceiveWeaponChanged(WeaponDefinitionSO weaponData)
    {
        _weaponData = weaponData;
        _ammoState = GetOrCreateAmmoState(weaponData);
        _fireMode = weaponData ? GetValidDefaultFireMode(weaponData.Fire) : WeaponFireMode.SemiAutomatic;
        _isFireHeld = false;
        _burstShotsRemaining = 0;
        _nextShotTime = 0f;
        _spreadFromShots = 0f;
        NotifyAmmoChanged();
    }

    private void ReceiveActionMarker(
        WeaponActionType actionType,
        WeaponActionMarkerType markerType)
    {
        switch (markerType)
        {
            case WeaponActionMarkerType.FireShot:
                ExecuteShot();
                break;
            case WeaponActionMarkerType.AmmoCommit:
                CommitMagazineReload();
                break;
            case WeaponActionMarkerType.InsertRound:
                InsertOneRound();
                break;
        }
    }

    private void ReceiveActionCompleted(WeaponActionType actionType)
    {
        if (actionType == WeaponActionType.PerRoundStart)
        {
            ContinuePerRoundReload();
        }
        else if (actionType == WeaponActionType.PerRoundInsert)
        {
            if (CanInsertRound())
            {
                _actionCtrl.ContinuePerRoundReload();
            }
            else
            {
                _actionCtrl.FinishPerRoundReload();
            }
        }
    }

    private void ExecuteShot()
    {
        if (!_weaponData || _ammoState == null || _ammoState.MagazineAmmo <= 0)
        {
            return;
        }

        _ammoState.MagazineAmmo--;
        if (_fireMode == WeaponFireMode.Burst && _burstShotsRemaining > 0)
        {
            _burstShotsRemaining--;
        }

        float shotInterval = 60f / Mathf.Max(1f, GetRoundsPerMinute());
        _nextShotTime = Time.time + shotInterval;
        if (_fireMode == WeaponFireMode.Burst && _burstShotsRemaining <= 0)
        {
            _nextShotTime += Mathf.Max(0f, _weaponData.Fire.BurstCooldown);
        }

        float spread = CalculateCurrentSpread();
        Ray centerRay = _cameraCtrl.GetCenterAimRay();
        int pelletCount = Mathf.Max(1, _weaponData.Fire.PelletsPerShot);
        for (int i = 0; i < pelletCount; i++)
        {
            Ray shotRay = CreateSpreadRay(centerRay, spread, pelletCount > 1);
            ResolveShot(shotRay);
        }

        _spreadFromShots = Mathf.Min(
            _weaponData.Accuracy.MaximumSpread,
            _spreadFromShots + _weaponData.Accuracy.SpreadPerShot);
        _lastShotTime = Time.time;
        NotifyAmmoChanged();
        ShotFired?.Invoke(_weaponData);
    }

    private void ResolveShot(Ray shotRay)
    {
        float maximumRange = Mathf.Max(0f, _weaponData.Damage.MaximumHitRange);
        int hitCount = Physics.RaycastNonAlloc(
            shotRay,
            _hitBuffer,
            maximumRange,
            _hitMask,
            QueryTriggerInteraction.Collide);

        if (!TryFindDamageHit(
                hitCount,
                out RaycastHit nearestHit,
                out BodyHitbox bodyHitbox,
                out CharacterHealth target))
        {
            if (_impactCtrl && TryFindWorldHit(hitCount, out RaycastHit worldHit))
            {
                _impactCtrl.ShowImpact(worldHit);
            }
            return;
        }

        DamageRequest request = CreateDamageRequest(nearestHit, shotRay.direction);
        if (bodyHitbox)
        {
            bodyHitbox.ReceiveHit(request);
            return;
        }

        request.Target = target;
        CombatDamage.Apply(request);
    }

    private bool TryFindWorldHit(int hitCount, out RaycastHit worldHit)
    {
        worldHit = default;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _hitBuffer[i];
            if (!hit.collider || hit.collider.transform.IsChildOf(transform) ||
                hit.distance >= nearestDistance)
            {
                continue;
            }

            if (hit.collider.GetComponentInParent<CharacterHealth>())
            {
                continue;
            }

            nearestDistance = hit.distance;
            worldHit = hit;
        }

        return worldHit.collider;
    }

    private bool TryFindDamageHit(
        int hitCount,
        out RaycastHit damageHit,
        out BodyHitbox bodyHitbox,
        out CharacterHealth target)
    {
        int bodyIndex = -1;
        int targetIndex = -1;
        float bodyDistance = float.PositiveInfinity;
        float targetDistance = float.PositiveInfinity;
        float blockingDistance = float.PositiveInfinity;
        BodyHitbox nearestBody = null;
        CharacterHealth nearestTarget = null;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _hitBuffer[i];
            if (!hit.collider || hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }
            if (hit.collider.TryGetComponent(out BodyHitbox hitBody))
            {
                if (hit.distance < bodyDistance)
                {
                    bodyIndex = i;
                    bodyDistance = hit.distance;
                    nearestBody = hitBody;
                }
                continue;
            }

            CharacterHealth hitTarget = hit.collider.GetComponentInParent<CharacterHealth>();
            if (hitTarget)
            {
                if (hit.distance < targetDistance)
                {
                    targetIndex = i;
                    targetDistance = hit.distance;
                    nearestTarget = hitTarget;
                }
                continue;
            }

            blockingDistance = Mathf.Min(blockingDistance, hit.distance);
        }

        if (bodyIndex >= 0 &&
            bodyDistance <= blockingDistance &&
            bodyDistance <= targetDistance + BodyHitboxPreferenceTolerance)
        {
            damageHit = _hitBuffer[bodyIndex];
            bodyHitbox = nearestBody;
            target = nearestBody.CharacterHealth;
            return true;
        }
        if (targetIndex >= 0 && targetDistance <= blockingDistance)
        {
            damageHit = _hitBuffer[targetIndex];
            bodyHitbox = null;
            target = nearestTarget;
            return true;
        }

        damageHit = default;
        bodyHitbox = null;
        target = null;
        return false;
    }

    private DamageRequest CreateDamageRequest(RaycastHit hit, Vector3 direction)
    {
        WeaponDamageData damageData = _weaponData.Damage;
        DamageRequest request = new DamageRequest(null, damageData.GetDamageAtDistance(hit.distance));
        request.Instigator = _playerInfo;
        request.SourceId = _weaponData.Basic.WeaponId;
        request.SourceName = _weaponData.Basic.WeaponName;
        request.SourceIconId = _weaponData.Basic.SilhouetteResourceId;
        request.HitDistance = hit.distance;
        request.BaseHitScore = damageData.BaseHitScore;
        request.WeaponScoreFactor = damageData.WeaponScoreFactor;
        request.DistanceScoreFactor = damageData.GetDistanceScoreFactor(hit.distance);
        request.KillBonus = damageData.KillBonus;
        request.HitPoint = hit.point;
        request.HitDirection = direction;
        return request;
    }

    private void CommitMagazineReload()
    {
        if (!_weaponData || _ammoState == null)
        {
            return;
        }

        int neededAmmo = Mathf.Max(
            0,
            _weaponData.Ammo.MagazineCapacity - _ammoState.MagazineAmmo);
        int loadedAmmo = Mathf.Min(neededAmmo, _ammoState.ReserveAmmo);
        _ammoState.MagazineAmmo += loadedAmmo;
        _ammoState.ReserveAmmo -= loadedAmmo;
        NotifyAmmoChanged();
    }

    private void InsertOneRound()
    {
        if (!CanInsertRound())
        {
            return;
        }

        _ammoState.MagazineAmmo++;
        _ammoState.ReserveAmmo--;
        NotifyAmmoChanged();
    }

    private void ContinuePerRoundReload()
    {
        if (CanInsertRound())
        {
            _actionCtrl.ContinuePerRoundReload();
        }
        else
        {
            _actionCtrl.FinishPerRoundReload();
        }
    }

    private bool CanInsertRound()
    {
        return _weaponData &&
            _ammoState != null &&
            _ammoState.ReserveAmmo > 0 &&
            _ammoState.MagazineAmmo < _weaponData.Ammo.MagazineCapacity;
    }

    private bool CanFireFromMovement()
    {
        if (!_moveCtrl || !_weaponData)
        {
            return true;
        }
        if (_moveCtrl.IsSprinting && !_weaponData.Handling.CanFireWhileSprinting)
        {
            return false;
        }

        return _moveCtrl.IsGrounded || _weaponData.Handling.CanFireWhileAirborne;
    }

    private float CalculateCurrentSpread()
    {
        WeaponAccuracyData accuracy = _weaponData.Accuracy;
        float aimProgress = _aimCtrl ? _aimCtrl.AimProgress : 0f;
        float spread = Mathf.Lerp(accuracy.HipSpread, accuracy.AimSpread, aimProgress);
        spread += _spreadFromShots;

        if (_moveCtrl)
        {
            if (_moveCtrl.IsSprinting)
            {
                spread *= accuracy.SprintingSpreadMultiplier;
            }
            else if (_moveCtrl.IsMoving)
            {
                spread *= accuracy.MovingSpreadMultiplier;
            }
            if (!_moveCtrl.IsGrounded)
            {
                spread *= accuracy.AirborneSpreadMultiplier;
            }
        }

        if (accuracy.FirstShotAccurate &&
            _spreadFromShots <= 0f &&
            Time.time - _lastShotTime >= accuracy.SpreadRecoveryDelay)
        {
            spread = Mathf.Lerp(spread, 0f, aimProgress);
        }

        return Mathf.Clamp(spread, 0f, accuracy.MaximumSpread);
    }

    private Ray CreateSpreadRay(Ray centerRay, float spread, bool isPellet)
    {
        float usedSpread = isPellet
            ? Mathf.Max(spread, _weaponData.Accuracy.PelletSpread)
            : spread;
        Vector2 offset = UnityEngine.Random.insideUnitCircle * usedSpread;
        Quaternion lookRotation = Quaternion.LookRotation(centerRay.direction, Vector3.up);
        Vector3 direction = lookRotation * Quaternion.Euler(-offset.y, offset.x, 0f) * Vector3.forward;
        return new Ray(centerRay.origin, direction.normalized);
    }

    private void RecoverSpread()
    {
        if (!_weaponData || _spreadFromShots <= 0f ||
            Time.time - _lastShotTime < _weaponData.Accuracy.SpreadRecoveryDelay)
        {
            return;
        }

        _spreadFromShots = Mathf.MoveTowards(
            _spreadFromShots,
            0f,
            _weaponData.Accuracy.SpreadRecoverySpeed * Time.deltaTime);
    }

    private float GetRoundsPerMinute()
    {
        switch (_fireMode)
        {
            case WeaponFireMode.Automatic:
                return _weaponData.Fire.AutomaticRpm;
            case WeaponFireMode.Burst:
                return _weaponData.Fire.BurstRpm;
            default:
                return _weaponData.Fire.SemiAutomaticRpm;
        }
    }

    private WeaponAmmoState GetOrCreateAmmoState(WeaponDefinitionSO weaponData)
    {
        if (!weaponData)
        {
            return null;
        }
        if (_ammoStates.TryGetValue(weaponData, out WeaponAmmoState state))
        {
            return state;
        }

        state = new WeaponAmmoState(
            weaponData.Ammo.MagazineCapacity,
            weaponData.Ammo.StartingReserveAmmo);
        _ammoStates.Add(weaponData, state);
        return state;
    }

    private WeaponFireMode GetValidDefaultFireMode(WeaponFireData fireData)
    {
        if (IsFireModeAllowed(fireData.AllowedModes, fireData.DefaultMode))
        {
            return fireData.DefaultMode;
        }
        if (IsFireModeAllowed(fireData.AllowedModes, WeaponFireMode.SemiAutomatic))
        {
            return WeaponFireMode.SemiAutomatic;
        }
        if (IsFireModeAllowed(fireData.AllowedModes, WeaponFireMode.Automatic))
        {
            return WeaponFireMode.Automatic;
        }

        return WeaponFireMode.Burst;
    }

    private bool IsFireModeAllowed(WeaponFireModeMask mask, WeaponFireMode fireMode)
    {
        WeaponFireModeMask modeMask = (WeaponFireModeMask)(1 << (int)fireMode);
        return (mask & modeMask) != 0;
    }

    private bool IsPerRoundAction(WeaponActionType actionType)
    {
        return actionType == WeaponActionType.PerRoundStart ||
            actionType == WeaponActionType.PerRoundInsert ||
            actionType == WeaponActionType.PerRoundEnd;
    }

    private void NotifyAmmoChanged()
    {
        AmmoChanged?.Invoke(MagazineAmmo, ReserveAmmo);
    }

    private sealed class WeaponAmmoState
    {
        public int MagazineAmmo;
        public int ReserveAmmo;

        public WeaponAmmoState(int magazineAmmo, int reserveAmmo)
        {
            MagazineAmmo = Mathf.Max(0, magazineAmmo);
            ReserveAmmo = Mathf.Max(0, reserveAmmo);
        }
    }
}
