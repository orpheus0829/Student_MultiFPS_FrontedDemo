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
    private readonly PlayerMoveCtrl _move;
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
        PlayerMoveCtrl move,
        AIDifficultyProfile profile)
    {
        _send = send;
        _combat = combat;
        _actions = actions;
        _inventory = inventory;
        _move = move;
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
        bool movingFire = intent.AllowMovingFire || _move && _move.IsSliding;
        float angularGate = movingFire
            ? _profile.FireSpreadGate * 1.8f
            : _profile.FireSpreadGate;
        bool accurate = angularError <= angularGate &&
            (movingFire || _combat.CurrentSpreadDegrees <= _profile.FireSpreadGate);
        BuildFireRhythm(weapon, intent, out float fireDuration, out float firePause);
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

    private void BuildFireRhythm(
        WeaponDefinitionSO weapon,
        AIIntent intent,
        out float fireDuration,
        out float firePause)
    {
        float rpm = GetRoundsPerMinute(weapon);
        float shotInterval = 60f / Mathf.Max(1f, rpm);
        AITrackedTarget target = intent.Target;
        float distance = target != null ? target.Distance : _profile.PreferredRange;
        float rangeRatio = Mathf.InverseLerp(
            Mathf.Max(2f, weapon.Damage.CloseDamageRange),
            Mathf.Max(3f, weapon.Damage.FarDamageRange),
            distance);
        int closeShots = 4;
        int farShots = 2;
        float pauseScale = 1f;

        switch (weapon.Basic.Category)
        {
            case WeaponCategory.SubmachineGun:
                closeShots = 8;
                farShots = 3;
                pauseScale = 0.75f;
                break;
            case WeaponCategory.LightMachineGun:
                closeShots = 11;
                farShots = 5;
                pauseScale = 0.7f;
                break;
            case WeaponCategory.Shotgun:
                closeShots = 2;
                farShots = 1;
                pauseScale = 1.3f;
                break;
            case WeaponCategory.MarksmanRifle:
            case WeaponCategory.SniperRifle:
                closeShots = 2;
                farShots = 1;
                pauseScale = 1.65f;
                break;
            case WeaponCategory.Pistol:
            case WeaponCategory.Revolver:
                closeShots = 4;
                farShots = 2;
                pauseScale = 1.15f;
                break;
        }

        float difficultyShotScale = _profile.Difficulty == AIDifficulty.Hard
            ? 1.15f
            : _profile.Difficulty == AIDifficulty.Normal ? 1f : 0.72f;
        int shotCount = Mathf.Max(1, Mathf.RoundToInt(
            Mathf.Lerp(closeShots, farShots, rangeRatio) * difficultyShotScale));
        bool hardCommit = _profile.Difficulty == AIDifficulty.Hard &&
            target != null && target.Visible &&
            distance <= _profile.PreferredRange + _profile.RangeBand * 1.5f;
        int lethalShots = 0;
        if (hardCommit && target.Actor)
        {
            float estimatedDamage = Mathf.Lerp(
                weapon.Damage.CloseDamage,
                weapon.Damage.FarDamage,
                rangeRatio);
            lethalShots = Mathf.CeilToInt(
                target.Actor.CurrentHealth /
                Mathf.Max(1f, estimatedDamage * 0.72f)) + 1;
            pauseScale *= target.ThreatScore >= 0.9f ? 0.45f : 0.68f;
        }
        if (intent.CombatTactic == AICombatTactic.HoldAngle ||
            intent.CombatTactic == AICombatTactic.CoverPeek)
        {
            shotCount = Mathf.Max(1, Mathf.RoundToInt(shotCount * 0.78f));
            pauseScale *= 1.12f;
        }
        else if (intent.CombatTactic == AICombatTactic.Suppress)
        {
            shotCount = Mathf.Max(3, Mathf.RoundToInt(shotCount * 1.35f));
            pauseScale *= 0.82f;
        }
        if (lethalShots > 0)
        {
            shotCount = Mathf.Max(
                shotCount,
                Mathf.Min(_combat.MagazineAmmo, Mathf.Clamp(lethalShots, 4, 18)));
        }
        fireDuration = Mathf.Max(0.06f, shotInterval * (shotCount - 0.25f));
        float maximumDuration = hardCommit
            ? Mathf.Max(_profile.FireDuration * 2.2f, shotInterval * 17.75f)
            : Mathf.Max(0.12f, _profile.FireDuration * 1.45f);
        fireDuration = Mathf.Min(fireDuration, maximumDuration);
        firePause = _profile.FirePause * pauseScale * Mathf.Lerp(0.78f, 1.25f, rangeRatio);
    }
}
