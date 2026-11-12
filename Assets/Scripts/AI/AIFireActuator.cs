using System;
using UnityEngine;

public sealed class AIFireActuator
{
    private enum FirePhase
    {
        Aiming,
        Firing,
        Pausing
    }

    private readonly Action<GJInputAction, PlayerInputPhase, Vector2> _send;
    private readonly WeaponCombatCtrl _combat;
    private readonly WeaponActionCtrl _actions;
    private readonly PlayerWeaponInventoryCtrl _inventory;
    private AIDifficultyProfile _profile;
    private FirePhase _phase;
    private float _phaseEndTime;
    private float _nextSemiShotTime;
    private bool _isFiring;
    private bool _isAiming;
    private bool _isSwitching;
    private bool _isReloading;
    private bool _aimPreference;
    private float _switchTimeout;

    public string PhaseName { get { return _phase.ToString(); } }

    public AIFireActuator(
        Action<GJInputAction, PlayerInputPhase, Vector2> send,
        WeaponCombatCtrl combat,
        WeaponActionCtrl actions,
        PlayerWeaponInventoryCtrl inventory,
        AIDifficultyProfile profile)
    {
        _send = send;
        _combat = combat;
        _actions = actions;
        _inventory = inventory;
        _profile = profile;
        RefreshAimPreference();
    }

    public void SetProfile(AIDifficultyProfile profile)
    {
        _profile = profile;
        RefreshAimPreference();
    }

    public void Enable()
    {
        _actions.ActionStarted += HandleActionStarted;
        _actions.ActionCompleted += HandleActionCompleted;
        _actions.ActionCanceled += HandleActionCanceled;
    }

    public void Disable()
    {
        _actions.ActionStarted -= HandleActionStarted;
        _actions.ActionCompleted -= HandleActionCompleted;
        _actions.ActionCanceled -= HandleActionCanceled;
        _isReloading = false;
    }

    public void Tick(AIIntent intent, float angularError, bool protectedByCover)
    {
        if (_isSwitching)
        {
            SetFiring(false);
            SetAiming(false);
            if (!_inventory.IsSwitching || Time.time >= _switchTimeout)
            {
                _isSwitching = false;
            }
            return;
        }
        bool wantsAim = intent.Aim && _aimPreference;
        SetAiming(wantsAim);
        WeaponDefinitionSO weapon = _actions.CurrentWeaponData;
        if (!weapon)
        {
            SetFiring(false);
            return;
        }
        float ammoRatio = _combat.MagazineAmmo /
            Mathf.Max(1f, weapon.Ammo.MagazineCapacity);
        bool empty = _combat.MagazineAmmo <= 0;
        bool tactical = _profile.TacticalReloadRatio > 0f &&
            ammoRatio < _profile.TacticalReloadRatio && protectedByCover;
        if (_isReloading || IsReloadAction(_actions.CurrentAction))
        {
            SetFiring(false);
            return;
        }
        if ((empty || tactical) && _combat.ReserveAmmo > 0)
        {
            SetFiring(false);
            if (!_actions.IsBusy)
            {
                _isReloading = true;
                _send(GJInputAction.Reload, PlayerInputPhase.Started, Vector2.zero);
                if (!_actions.IsBusy)
                {
                    _isReloading = false;
                }
            }
            return;
        }
        if (empty && _combat.ReserveAmmo <= 0 && _profile.CanSwitchWeapon)
        {
            TrySwitchWeapon();
            return;
        }
        if (intent.Melee && _profile.CanMelee)
        {
            SetFiring(false);
            _send(GJInputAction.Punch, PlayerInputPhase.Started, Vector2.zero);
            return;
        }
        bool accurate = angularError <= _profile.FireSpreadGate &&
            _combat.CurrentSpreadDegrees <= _profile.FireSpreadGate;
        float fireDuration = _profile.FireDuration;
        float firePause = _profile.FirePause;
        if (_profile.Difficulty == AIDifficulty.Hard && intent.Target != null &&
            intent.Target.Distance > _profile.PreferredRange + _profile.RangeBand)
        {
            WeaponDefinitionSO rangedWeapon = _actions.CurrentWeaponData;
            float rpm = rangedWeapon ? GetRoundsPerMinute(rangedWeapon) : 600f;
            fireDuration = Mathf.Clamp(60f / Mathf.Max(1f, rpm) * 2.5f, 0.18f, 0.42f);
            firePause = Mathf.Max(firePause, 0.32f);
        }
        UpdateFirePhase(intent.Fire && accurate, fireDuration, firePause);
        RefreshSemiAutomatic();
    }

    public void Stop()
    {
        SetFiring(false);
        SetAiming(false);
    }

    private void UpdateFirePhase(bool wantsFire, float fireDuration, float firePause)
    {
        if (!wantsFire)
        {
            SetFiring(false);
            _phase = FirePhase.Aiming;
            return;
        }
        if (_phase == FirePhase.Aiming)
        {
            _phase = FirePhase.Firing;
            _phaseEndTime = Time.time + fireDuration;
            SetFiring(true);
        }
        else if (_phase == FirePhase.Firing && Time.time >= _phaseEndTime)
        {
            SetFiring(false);
            _phase = FirePhase.Pausing;
            _phaseEndTime = Time.time + firePause;
        }
        else if (_phase == FirePhase.Pausing && Time.time >= _phaseEndTime)
        {
            _phase = FirePhase.Firing;
            _phaseEndTime = Time.time + fireDuration;
            SetFiring(true);
        }
    }

    private void RefreshSemiAutomatic()
    {
        if (!_isFiring || _combat.FireMode != WeaponFireMode.SemiAutomatic ||
            Time.time < _nextSemiShotTime)
        {
            return;
        }
        WeaponDefinitionSO weapon = _actions.CurrentWeaponData;
        float rpm = weapon ? weapon.Fire.SemiAutomaticRpm : 600f;
        _nextSemiShotTime = Time.time + 60f / Mathf.Max(1f, rpm);
        _send(GJInputAction.Fire, PlayerInputPhase.Canceled, Vector2.zero);
        _send(GJInputAction.Fire, PlayerInputPhase.Started, Vector2.zero);
    }

    private void TrySwitchWeapon()
    {
        int count = _inventory.WeaponResourceIds.Count;
        for (int offset = 1; offset < count; offset++)
        {
            int index = (_inventory.CurrentWeaponIndex + offset) % count;
            SetFiring(false);
            SetAiming(false);
            if (_inventory.TryEquipIndex(index))
            {
                _isSwitching = true;
                _switchTimeout = Time.time + 1.5f;
                return;
            }
        }
    }

    private void SetFiring(bool active)
    {
        if (_isFiring == active) return;
        _isFiring = active;
        _send(GJInputAction.Fire,
            active ? PlayerInputPhase.Started : PlayerInputPhase.Canceled, Vector2.zero);
        _nextSemiShotTime = Time.time;
    }

    private void SetAiming(bool active)
    {
        if (_isAiming == active) return;
        _isAiming = active;
        _send(GJInputAction.Aim,
            active ? PlayerInputPhase.Started : PlayerInputPhase.Canceled, Vector2.zero);
    }

    private void RefreshAimPreference()
    {
        uint hash = (uint)_inventory.GetInstanceID() * 2654435761u;
        float value = (hash & 0x00ffffffu) / 16777215f;
        _aimPreference = value <= _profile.AimChance;
    }

    private void HandleActionStarted(WeaponActionDefinition action, bool startedWhileAiming)
    {
        if (action != null && IsReloadAction(action.ActionType))
        {
            _isReloading = true;
            SetFiring(false);
        }
    }

    private void HandleActionCompleted(WeaponActionType actionType)
    {
        if (IsReloadAction(actionType) && !IsReloadAction(_actions.CurrentAction))
        {
            _isReloading = false;
        }
    }

    private void HandleActionCanceled(
        WeaponActionType actionType,
        WeaponActionCancelReason reason)
    {
        if (IsReloadAction(actionType))
        {
            _isReloading = false;
        }
    }

    private static bool IsReloadAction(WeaponActionDefinition action)
    {
        return action != null && IsReloadAction(action.ActionType);
    }

    private static bool IsReloadAction(WeaponActionType actionType)
    {
        return actionType == WeaponActionType.Reload ||
            actionType == WeaponActionType.EmptyReload ||
            actionType == WeaponActionType.PerRoundStart ||
            actionType == WeaponActionType.PerRoundInsert ||
            actionType == WeaponActionType.PerRoundEnd;
    }

    private static float GetRoundsPerMinute(WeaponDefinitionSO weapon)
    {
        if (!weapon) return 600f;
        switch (weapon.Fire.DefaultMode)
        {
            case WeaponFireMode.Automatic: return weapon.Fire.AutomaticRpm;
            case WeaponFireMode.Burst: return weapon.Fire.BurstRpm;
            default: return weapon.Fire.SemiAutomaticRpm;
        }
    }
}
