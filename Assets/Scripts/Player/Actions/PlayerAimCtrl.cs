using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAimCtrl : MonoBehaviour
{
    [Header("默认瞄准")]
    [SerializeField, Min(1f)] private float _defaultAimFov = 60f;
    [SerializeField, Min(0f)] private float _defaultAimInDuration = 0.22f;
    [SerializeField, Min(0f)] private float _defaultAimOutDuration = 0.18f;

    private WeaponDefinitionSO _weaponData;
    private bool _isAimInputHeld;
    private bool _isAimRequested;
    private bool _isActionLocked;
    private bool _isWeaponActionBlocking;
    private bool _isMeleeBlocking;
    private bool _requiresAimRelease;
    private float _aimProgress;

    public bool IsAimRequested
    {
        get
        {
            return _isAimRequested;
        }
    }

    public bool IsAiming
    {
        get
        {
            return _isAimRequested && _aimProgress > 0f;
        }
    }

    public float AimProgress
    {
        get
        {
            return _aimProgress;
        }
    }

    public float AimFov
    {
        get
        {
            return _weaponData ? _weaponData.Handling.AimFov : _defaultAimFov;
        }
    }

    public float LookSensitivityMultiplier
    {
        get
        {
            if (!_weaponData)
            {
                return 1f;
            }

            return Mathf.Lerp(1f, _weaponData.Handling.AimSensitivityMultiplier, _aimProgress);
        }
    }

    public float MovementSpeedMultiplier
    {
        get
        {
            if (!_weaponData)
            {
                return 1f;
            }

            return Mathf.Lerp(1f, _weaponData.Handling.AimMovementSpeedMultiplier, _aimProgress);
        }
    }

    public event Action<float> AimProgressChanged;
    public event Action<bool> AimStateChanged;

    private void Update()
    {
        RefreshAimProgress();
    }

    private void OnDisable()
    {
        _isAimInputHeld = false;
        _isAimRequested = false;
        _isActionLocked = false;
        _isWeaponActionBlocking = false;
        _isMeleeBlocking = false;
        _requiresAimRelease = false;
        SetAimProgress(0f);
    }

    public void SetWeaponData(WeaponDefinitionSO weaponData)
    {
        _weaponData = weaponData;
        if (!CanAim())
        {
            StopAim();
        }
    }

    public void ReceiveAimInput(PlayerInputPhase phase)
    {
        if (phase == PlayerInputPhase.Canceled)
        {
            _isAimInputHeld = false;
            _requiresAimRelease = false;
            StopAim();
            return;
        }

        if (phase != PlayerInputPhase.Started)
        {
            return;
        }

        _isAimInputHeld = true;
        TryStartAim();
    }

    public bool TryStartAim()
    {
        if (!CanAim() ||
            _isActionLocked ||
            _isWeaponActionBlocking ||
            _isMeleeBlocking ||
            _requiresAimRelease)
        {
            return false;
        }

        ChangeAimRequest(true);
        return true;
    }

    public void StopAim()
    {
        ChangeAimRequest(false);
    }

    public void BeginWeaponAction()
    {
        _isWeaponActionBlocking = true;
        StopAim();
    }

    public void EndWeaponAction()
    {
        _isWeaponActionBlocking = false;
        if (_isAimInputHeld)
        {
            TryStartAim();
        }
    }

    /// <summary>
    /// 拳击开始时立刻撤销瞄准逻辑和瞄准姿势。这里独立于枪械动作锁，
    /// 避免拳击取消换弹后，换弹结束回调错误地把仍在按住的瞄准重新举起。
    /// </summary>
    public void BeginMeleeAction()
    {
        _isMeleeBlocking = true;
        StopAim();
        SetAimProgress(0f);
    }

    public void EndMeleeAction()
    {
        _isMeleeBlocking = false;
        if (_isAimInputHeld)
        {
            TryStartAim();
        }
    }

    public void CancelForVault()
    {
        _isActionLocked = true;
        _isWeaponActionBlocking = false;
        _requiresAimRelease = _isAimInputHeld;
        StopAim();
    }

    public void SetActionLocked(bool isLocked)
    {
        _isActionLocked = isLocked;
        if (isLocked)
        {
            CancelForVault();
        }
    }

    private bool CanAim()
    {
        return _weaponData && _weaponData.Handling.CanAim;
    }

    private void ChangeAimRequest(bool isRequested)
    {
        if (_isAimRequested == isRequested)
        {
            return;
        }

        _isAimRequested = isRequested;
        AimStateChanged?.Invoke(_isAimRequested);
    }

    private void RefreshAimProgress()
    {
        float targetProgress = _isAimRequested ? 1f : 0f;
        float duration = _isAimRequested ? GetAimInDuration() : GetAimOutDuration();
        float nextProgress;
        if (duration <= 0f)
        {
            nextProgress = targetProgress;
        }
        else
        {
            nextProgress = Mathf.MoveTowards(
                _aimProgress,
                targetProgress,
                Time.deltaTime / duration);
        }

        SetAimProgress(nextProgress);
    }

    private float GetAimInDuration()
    {
        return _weaponData
            ? _weaponData.Handling.AimInDuration
            : _defaultAimInDuration;
    }

    private float GetAimOutDuration()
    {
        return _weaponData
            ? _weaponData.Handling.AimOutDuration
            : _defaultAimOutDuration;
    }

    private void SetAimProgress(float progress)
    {
        float clampedProgress = Mathf.Clamp01(progress);
        if (Mathf.Approximately(_aimProgress, clampedProgress))
        {
            return;
        }

        _aimProgress = clampedProgress;
        AimProgressChanged?.Invoke(_aimProgress);
    }

    private void OnValidate()
    {
        _defaultAimFov = Mathf.Max(1f, _defaultAimFov);
        _defaultAimInDuration = Mathf.Max(0f, _defaultAimInDuration);
        _defaultAimOutDuration = Mathf.Max(0f, _defaultAimOutDuration);
    }
}
