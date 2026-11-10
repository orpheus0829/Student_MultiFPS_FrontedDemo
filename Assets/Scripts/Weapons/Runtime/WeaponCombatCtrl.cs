using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponCombatCtrl : MonoBehaviour
{
    [Header("动作与瞄准")]
    [SerializeField, InspectorName("枪械动作")] private WeaponActionCtrl _actionCtrl;
    [SerializeField, InspectorName("玩家瞄准")] private PlayerAimCtrl _aimCtrl;
    [SerializeField, InspectorName("玩家移动")] private PlayerMoveCtrl _moveCtrl;
    [SerializeField, InspectorName("枪械表现")] private PlayerWeaponViewCtrl _weaponViewCtrl;

    [Header("射击来源")]
    [SerializeField, InspectorName("第一人称相机")] private FirstPersonCameraCtrl _cameraCtrl;
    [SerializeField, InspectorName("攻击者信息")] private PlayerInfo _playerInfo;
    [SerializeField, InspectorName("可命中层")] private LayerMask _hitMask = ~0;

    [Header("场景命中")]
    [SerializeField, InspectorName("弹孔表现")] private BulletImpactCtrl _impactCtrl;

    private readonly Dictionary<WeaponDefinitionSO, WeaponAmmoState> _ammoStates =
        new Dictionary<WeaponDefinitionSO, WeaponAmmoState>();
    private readonly RaycastHit[] _aimHits = new RaycastHit[128];

    private ResourceMgr _resourceMgr;
    private ObjectPool _projectilePool;
    private GameObject _projectilePrefab;
    private string _projectileResourceId;
    private int _projectileLoadVersion;
    private WeaponDefinitionSO _weaponData;
    private WeaponAmmoState _ammoState;
    private WeaponFireMode _fireMode;
    private bool _isFireHeld;
    private int _burstShotsRemaining;
    private float _nextShotTime;
    private float _lastShotTime = float.NegativeInfinity;
    private float _spreadFromShots;
    private int _recoilShotIndex;
    private float _lastRecoilShotTime = float.NegativeInfinity;
    private Vector2 _lastPatternPoint;
    private Vector2 _recoilTarget;
    private Vector2 _recoilApplied;

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

    public bool CanFireDuringPerRoundReload
    {
        get
        {
            return _ammoState != null && _ammoState.MagazineAmmo > 0 &&
                _actionCtrl && _actionCtrl.CurrentAction != null &&
                IsPerRoundAction(_actionCtrl.CurrentAction.ActionType);
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
        if (!_weaponViewCtrl)
        {
            TryGetComponent(out _weaponViewCtrl);
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
        UpdateRecoil();
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
        _projectileLoadVersion++;
        ReleaseProjectilePrefab();
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
        if (!_weaponData || _ammoState == null || !_projectilePrefab ||
            Time.time < _nextShotTime)
        {
            return false;
        }
        if (!GetMuzzlePoint())
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

            if (isPerRoundReload && _ammoState.MagazineAmmo <= 0)
            {
                return false;
            }

            if (!_actionCtrl.CancelCurrentAction(
                WeaponActionCancelReason.Manual,
                true))
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
        if (_cameraCtrl)
        {
            _cameraCtrl.ApplyRecoilAngles(-_recoilApplied);
        }
        _weaponData = weaponData;
        _ = LoadProjectilePrefabAsync(weaponData, ++_projectileLoadVersion);
        _ammoState = GetOrCreateAmmoState(weaponData);
        _fireMode = weaponData ? GetValidDefaultFireMode(weaponData.Fire) : WeaponFireMode.SemiAutomatic;
        _isFireHeld = false;
        _burstShotsRemaining = 0;
        _nextShotTime = 0f;
        _spreadFromShots = 0f;
        _recoilShotIndex = 0;
        _lastPatternPoint = Vector2.zero;
        _recoilTarget = Vector2.zero;
        _recoilApplied = Vector2.zero;
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

        Transform muzzlePoint = GetMuzzlePoint();
        if (!muzzlePoint || !_projectilePrefab)
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
        float maximumRange = Mathf.Max(0f, _weaponData.Damage.MaximumHitRange);
        Ray aimRay = _cameraCtrl.GetCenterAimRay();
        Ray centerRay = CreateProjectileRay(muzzlePoint.position, aimRay, maximumRange);
        int pelletCount = Mathf.Max(1, _weaponData.Fire.PelletsPerShot);
        for (int i = 0; i < pelletCount; i++)
        {
            Ray shotRay = CreateSpreadRay(centerRay, spread, pelletCount > 1);
            SpawnProjectile(shotRay);
        }

        _spreadFromShots = Mathf.Min(
            _weaponData.Accuracy.MaximumSpread,
            _spreadFromShots + _weaponData.Accuracy.SpreadPerShot);
        _lastShotTime = Time.time;
        AddShotRecoil();
        NotifyAmmoChanged();
        ShotFired?.Invoke(_weaponData);
    }

    private Ray CreateProjectileRay(Vector3 muzzlePosition, Ray aimRay, float maximumRange)
    {
        // 准星只确定视差收敛点；实际命中和伤害仍由枪口发出的实体弹丸裁决。
        Vector3 aimPoint = aimRay.GetPoint(maximumRange);
        int hitCount = Physics.RaycastNonAlloc(
            aimRay, _aimHits, maximumRange, _hitMask, QueryTriggerInteraction.Collide);
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _aimHits[i];
            if (hit.distance < nearestDistance &&
                WeaponProjectileCtrl.CanHitCollider(hit.collider, transform))
            {
                nearestDistance = hit.distance;
                aimPoint = hit.point;
            }
        }

        Vector3 direction = aimPoint - muzzlePosition;
        if (direction.sqrMagnitude < 0.000001f)
        {
            direction = aimRay.direction;
        }
        return new Ray(muzzlePosition, direction.normalized);
    }

    private void SpawnProjectile(Ray shotRay)
    {
        if (!_projectilePool)
        {
            _projectilePool = ObjectPool.instance;
        }
        GameObject instance = _projectilePool.Spawn(
            _projectilePrefab,
            shotRay.origin,
            Quaternion.LookRotation(shotRay.direction, Vector3.up));
        if (!instance || !instance.TryGetComponent(out WeaponProjectileCtrl projectile))
        {
            if (instance)
            {
                _projectilePool.Despawn(instance);
            }
            Debug.LogError("子弹拖尾 Prefab 缺少 WeaponProjectileCtrl。", this);
            return;
        }

        projectile.Initialize(
            _weaponData,
            _playerInfo,
            transform,
            _impactCtrl,
            _hitMask,
            shotRay.direction);
    }

    private Transform GetMuzzlePoint()
    {
        if (!_weaponViewCtrl)
        {
            return null;
        }

        return _cameraCtrl.IsThirdPerson
            ? _weaponViewCtrl.ThirdPersonMuzzlePoint
            : _weaponViewCtrl.MuzzlePoint;
    }

    private async Task LoadProjectilePrefabAsync(
        WeaponDefinitionSO weaponData,
        int loadVersion)
    {
        string resourceId = weaponData ? weaponData.Effects.TracerResourceId : string.Empty;
        if (_projectilePrefab &&
            string.Equals(resourceId, _projectileResourceId, StringComparison.Ordinal))
        {
            return;
        }

        ReleaseProjectilePrefab();
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return;
        }

        _resourceMgr = ResourceMgr.instance;
        try
        {
            GameObject prefab = await _resourceMgr.LoadAsync<GameObject>(resourceId);
            if (loadVersion != _projectileLoadVersion || !this)
            {
                _resourceMgr.Release<GameObject>(resourceId);
                return;
            }

            if (!prefab || !prefab.GetComponent<WeaponProjectileCtrl>())
            {
                _resourceMgr.Release<GameObject>(resourceId);
                Debug.LogError($"子弹拖尾资源无效或缺少 WeaponProjectileCtrl：{resourceId}", this);
                return;
            }

            _projectilePrefab = prefab;
            _projectileResourceId = resourceId;
            _projectilePool = ObjectPool.instance;
            _projectilePool.Warmup(_projectilePrefab, 32);
        }
        catch (Exception exception)
        {
            Debug.LogError($"加载子弹拖尾资源失败：{resourceId}，{exception.Message}", this);
        }
    }

    private void ReleaseProjectilePrefab()
    {
        // 场景退出时池可能已先销毁，清理阶段不能通过 instance 再创建管理器。
        if (_projectilePrefab && _projectilePool)
        {
            _projectilePool.ClearPool(_projectilePrefab);
        }
        if (_resourceMgr != null && !string.IsNullOrWhiteSpace(_projectileResourceId))
        {
            _resourceMgr.Release<GameObject>(_projectileResourceId);
        }

        _projectilePrefab = null;
        _projectileResourceId = string.Empty;
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

    private void AddShotRecoil()
    {
        WeaponGameplayRecoilData recoil = _weaponData.GameplayRecoil;
        if (Time.time - _lastRecoilShotTime > recoil.PatternResetTime)
        {
            _recoilShotIndex = 0;
            _lastPatternPoint = Vector2.zero;
        }

        _recoilShotIndex++;
        _lastRecoilShotTime = Time.time;
        Vector2 step;
        if (recoil.Pattern.Count >= 2)
        {
            float progress = Mathf.Clamp01(
                (float)_recoilShotIndex / Mathf.Max(1, _weaponData.Ammo.MagazineCapacity));
            float position = progress * (recoil.Pattern.Count - 1);
            int index = Mathf.Min(Mathf.FloorToInt(position), recoil.Pattern.Count - 2);
            Vector2 point = Vector2.Lerp(
                recoil.Pattern[index], recoil.Pattern[index + 1], position - index);
            step = point - _lastPatternPoint;
            _lastPatternPoint = point;
            step += new Vector2(
                UnityEngine.Random.Range(recoil.HorizontalRecoil.x, recoil.HorizontalRecoil.y),
                UnityEngine.Random.Range(-recoil.VerticalRecoil.y, recoil.VerticalRecoil.y)) *
                recoil.Randomness;
        }
        else
        {
            step = new Vector2(
                UnityEngine.Random.Range(recoil.HorizontalRecoil.x, recoil.HorizontalRecoil.y),
                UnityEngine.Random.Range(recoil.VerticalRecoil.x, recoil.VerticalRecoil.y));
        }

        if (_recoilShotIndex == 1)
        {
            step *= recoil.FirstShotMultiplier;
        }
        if (_aimCtrl)
        {
            step *= Mathf.Lerp(1f, recoil.AimMultiplier, _aimCtrl.AimProgress);
        }
        _recoilTarget += step;
    }

    private void UpdateRecoil()
    {
        if (!_weaponData || !_cameraCtrl)
        {
            return;
        }

        WeaponGameplayRecoilData recoil = _weaponData.GameplayRecoil;
        if (Time.time - _lastRecoilShotTime >= recoil.RecoveryDelay &&
            (_fireMode == WeaponFireMode.SemiAutomatic || !_isFireHeld ||
             _ammoState == null || _ammoState.MagazineAmmo <= 0))
        {
            _recoilTarget = Vector2.MoveTowards(
                _recoilTarget, Vector2.zero, recoil.RecoverySpeed * Time.deltaTime);
        }

        Vector2 next = Vector2.MoveTowards(
            _recoilApplied, _recoilTarget, recoil.KickSpeed * Time.deltaTime);
        _cameraCtrl.ApplyRecoilAngles(next - _recoilApplied);
        _recoilApplied = next;
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
