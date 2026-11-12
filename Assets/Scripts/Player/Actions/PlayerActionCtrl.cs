using System;
using UnityEngine;

[DefaultExecutionOrder(-100)]
public class PlayerActionCtrl : MonoBehaviour
{
    [Header("动作来源")]
    [SerializeField, InspectorName("玩家输入")] private PlayerInputCtrl _inputCtrl;

    [Header("动作执行")]
    [SerializeField, InspectorName("玩家移动")] private PlayerMoveCtrl _moveCtrl;
    [SerializeField, InspectorName("玩家瞄准")] private PlayerAimCtrl _aimCtrl;
    [SerializeField, InspectorName("玩家枪械栏")] private PlayerWeaponInventoryCtrl _weaponInventoryCtrl;
    [SerializeField, InspectorName("枪械动作")] private WeaponActionCtrl _weaponActionCtrl;
    [SerializeField, InspectorName("枪械战斗")] private WeaponCombatCtrl _weaponCombatCtrl;
    [SerializeField, InspectorName("近战战斗")] private PlayerMeleeCombatCtrl _meleeCombatCtrl;

    [Header("第一人称反馈")]
    [SerializeField, InspectorName("镜头运动反馈")] private FirstPersonCameraMotionCtrl _cameraMotionCtrl;

    private EventMgr.Subscription _inputSubscription;
    private FirstPersonCameraCtrl _cameraCtrl;
    private bool _isFiring;
    private bool _weaponActionBlockedAim;
    private bool _wasAirborne;
    private bool _isPunching;

    public event Action<PlayerInputCommand> CommandReceived;
    public event Action<bool> OperationLockChanged;
    public event Action FireShot;
    public event Action VaultStarted;
    public event Action VaultFinished;
    public event Action PunchStarted;
    public event Action PunchCompleted;
    public event Action PunchCanceled;

    public PlayerDataSO PlayerData
    {
        get
        {
            return _moveCtrl ? _moveCtrl.PlayerData : null;
        }
    }

    public Vector2 MoveInput
    {
        get
        {
            return _moveCtrl ? _moveCtrl.MoveInput : Vector2.zero;
        }
    }

    public bool IsSprinting
    {
        get
        {
            return _moveCtrl && _moveCtrl.IsSprinting;
        }
    }

    public bool IsOperationLocked
    {
        get
        {
            return _moveCtrl && _moveCtrl.IsOperationLocked;
        }
    }

    public bool IsFiring
    {
        get
        {
            return _isFiring;
        }
    }

    public bool IsAiming
    {
        get
        {
            return _aimCtrl && _aimCtrl.IsAiming;
        }
    }

    public bool IsPunching
    {
        get
        {
            return _isPunching;
        }
    }

    public float PunchProgress
    {
        get
        {
            return _meleeCombatCtrl ? _meleeCombatCtrl.NormalizedProgress : 0f;
        }
    }

    private void Awake()
    {
        if (!_moveCtrl)
        {
            TryGetComponent(out _moveCtrl);
        }
        if (!_aimCtrl)
        {
            TryGetComponent(out _aimCtrl);
        }
        if (!_weaponActionCtrl)
        {
            TryGetComponent(out _weaponActionCtrl);
        }
        if (!_weaponInventoryCtrl)
        {
            TryGetComponent(out _weaponInventoryCtrl);
        }
        if (!_weaponCombatCtrl)
        {
            TryGetComponent(out _weaponCombatCtrl);
        }
        if (!_meleeCombatCtrl)
        {
            TryGetComponent(out _meleeCombatCtrl);
        }
        if (!_meleeCombatCtrl)
        {
            _meleeCombatCtrl = gameObject.AddComponent<PlayerMeleeCombatCtrl>();
        }
        if (!_cameraMotionCtrl)
        {
            _cameraMotionCtrl = GetComponentInChildren<FirstPersonCameraMotionCtrl>(true);
        }
        _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);

        if (_moveCtrl && _aimCtrl && _weaponInventoryCtrl &&
            _weaponActionCtrl && _weaponCombatCtrl && _inputCtrl)
        {
            return;
        }

        Debug.LogError($"{name} 的玩家动作引用未配置完整。", this);
        enabled = false;
    }

    private void OnEnable()
    {
        if (_inputCtrl)
        {
            _inputSubscription = EventMgr.Bind(this, StartListening, StopListening);
        }

        if (_moveCtrl)
        {
            _moveCtrl.VaultStarted += HandleVaultStarted;
            _moveCtrl.VaultFinished += HandleVaultFinished;
            _moveCtrl.SlideStarted += HandleSlideStarted;
        }

        if (_weaponActionCtrl)
        {
            _weaponActionCtrl.WeaponChanged += HandleWeaponChanged;
            _weaponActionCtrl.ActionStarted += HandleWeaponActionStarted;
            _weaponActionCtrl.MarkerReached += HandleWeaponMarker;
            _weaponActionCtrl.ActionCompleted += HandleWeaponActionCompleted;
            _weaponActionCtrl.ActionCanceled += HandleWeaponActionCanceled;
        }
        if (_weaponCombatCtrl)
        {
            _weaponCombatCtrl.ShotFired += HandleShotFired;
        }
        if (_meleeCombatCtrl)
        {
            _meleeCombatCtrl.PunchStarted += HandlePunchStarted;
            _meleeCombatCtrl.PunchCompleted += HandlePunchCompleted;
            _meleeCombatCtrl.PunchCanceled += HandlePunchCanceled;
        }
    }

    private void Start()
    {
        if (_aimCtrl && _weaponActionCtrl)
        {
            _aimCtrl.SetWeaponData(_weaponActionCtrl.CurrentWeaponData);
        }
    }

    private void Update()
    {
        RefreshMovementSpeed();
    }

    private void LateUpdate()
    {
        RefreshCameraMovement();
        RefreshLandingFeedback();
    }

    private void OnDisable()
    {
        if (_inputSubscription != null)
        {
            _inputSubscription.Dispose();
            _inputSubscription = null;
        }

        if (_moveCtrl)
        {
            _moveCtrl.VaultStarted -= HandleVaultStarted;
            _moveCtrl.VaultFinished -= HandleVaultFinished;
            _moveCtrl.SlideStarted -= HandleSlideStarted;
            _moveCtrl.ResetInput();
            _moveCtrl.SetExternalSpeedMultiplier(1f);
        }

        if (_weaponActionCtrl)
        {
            _weaponActionCtrl.WeaponChanged -= HandleWeaponChanged;
            _weaponActionCtrl.ActionStarted -= HandleWeaponActionStarted;
            _weaponActionCtrl.MarkerReached -= HandleWeaponMarker;
            _weaponActionCtrl.ActionCompleted -= HandleWeaponActionCompleted;
            _weaponActionCtrl.ActionCanceled -= HandleWeaponActionCanceled;
        }
        if (_weaponCombatCtrl)
        {
            _weaponCombatCtrl.ShotFired -= HandleShotFired;
        }
        if (_meleeCombatCtrl)
        {
            _meleeCombatCtrl.PunchStarted -= HandlePunchStarted;
            _meleeCombatCtrl.PunchCompleted -= HandlePunchCompleted;
            _meleeCombatCtrl.PunchCanceled -= HandlePunchCanceled;
            _meleeCombatCtrl.CancelPunch();
        }

        if (_aimCtrl)
        {
            _aimCtrl.ReceiveAimInput(PlayerInputPhase.Canceled);
            _aimCtrl.SetActionLocked(false);
        }
        if (_cameraMotionCtrl)
        {
            _cameraMotionCtrl.ClearMotion();
        }
        if (_weaponCombatCtrl)
        {
            _weaponCombatCtrl.ReceiveFireInput(PlayerInputPhase.Canceled);
        }

        _isFiring = false;
        _isPunching = false;
        _weaponActionBlockedAim = false;
        _wasAirborne = false;
    }

    public void Receive(PlayerInputCommand command)
    {
        if (HandleMovementCommand(command))
        {
            return;
        }

        if (command.Action == GJInputAction.Aim)
        {
            if (_isPunching)
            {
                if (command.Phase == PlayerInputPhase.Canceled)
                {
                    _aimCtrl.ReceiveAimInput(PlayerInputPhase.Canceled);
                }
                return;
            }
            HandleAimCommand(command);
            return;
        }

        if (command.Action == GJInputAction.CycleWeapon)
        {
            if (_isPunching)
            {
                return;
            }
            HandleWeaponCycleCommand(command);
            return;
        }

        if (command.Action == GJInputAction.Fire &&
            command.Phase == PlayerInputPhase.Canceled)
        {
            _isFiring = false;
            _weaponCombatCtrl.ReceiveFireInput(PlayerInputPhase.Canceled);
            if (!IsOperationLocked)
            {
                CommandReceived?.Invoke(command);
            }
            return;
        }

        if (IsOperationLocked)
        {
            return;
        }

        if (command.Action == GJInputAction.Punch)
        {
            HandlePunchCommand(command);
            return;
        }

        if (_isPunching &&
            (command.Action == GJInputAction.Fire ||
            command.Action == GJInputAction.Reload ||
            command.Action == GJInputAction.Inspect))
        {
            return;
        }

        switch (command.Action)
        {
            case GJInputAction.Fire:
                HandleFireCommand(command);
                break;
            case GJInputAction.Reload:
                HandleReloadCommand(command);
                break;
            case GJInputAction.Inspect:
                HandleInspectCommand(command);
                break;
        }

        CommandReceived?.Invoke(command);
    }

    private bool HandleMovementCommand(PlayerInputCommand command)
    {
        switch (command.Action)
        {
            case GJInputAction.Move:
                _moveCtrl.SetMoveInput(command.Value);
                if (!IsOperationLocked)
                {
                    CommandReceived?.Invoke(command);
                }
                return true;
            case GJInputAction.Sprint:
                if (!IsOperationLocked || command.Phase == PlayerInputPhase.Canceled)
                {
                    _moveCtrl.SetSprintInput(command.Phase != PlayerInputPhase.Canceled);
                }
                if (!IsOperationLocked)
                {
                    CommandReceived?.Invoke(command);
                }
                return true;
            case GJInputAction.JumpVault:
                if (command.Phase == PlayerInputPhase.Started && !IsOperationLocked)
                {
                    _moveCtrl.TryJumpOrVault();
                    CommandReceived?.Invoke(command);
                }
                return true;
            case GJInputAction.Slide:
                if (command.Phase == PlayerInputPhase.Started && !IsOperationLocked &&
                    _moveCtrl.TryStartSlide())
                {
                    CommandReceived?.Invoke(command);
                }
                return true;
        }

        return false;
    }

    private void HandleAimCommand(PlayerInputCommand command)
    {
        if (command.Phase == PlayerInputPhase.Started)
        {
            CancelInspect();
        }

        _aimCtrl.ReceiveAimInput(command.Phase);

        if (!IsOperationLocked)
        {
            CommandReceived?.Invoke(command);
        }
    }

    private void HandleFireCommand(PlayerInputCommand command)
    {
        if (command.Phase != PlayerInputPhase.Started)
        {
            return;
        }

        CancelInspect();
        if (!CanRequestFire())
        {
            return;
        }

        _isFiring = true;
        if (_cameraCtrl && _cameraCtrl.IsThirdPerson)
        {
            _cameraCtrl.FaceAimDirection();
        }
        _weaponCombatCtrl.ReceiveFireInput(command.Phase);
    }

    private void HandleReloadCommand(PlayerInputCommand command)
    {
        if (command.Phase != PlayerInputPhase.Started)
        {
            return;
        }

        CancelInspect();
        _weaponCombatCtrl.TryReload();
    }

    private void HandleInspectCommand(PlayerInputCommand command)
    {
        if (command.Phase != PlayerInputPhase.Started || _aimCtrl.IsAimRequested ||
            _weaponActionCtrl.IsBusy)
        {
            return;
        }

        WeaponDefinitionSO weaponData = _weaponActionCtrl.CurrentWeaponData;
        if (!weaponData || !weaponData.Animation.CanInspect)
        {
            return;
        }

        if (_weaponActionCtrl.TryStartAction(WeaponActionType.Inspect))
        {
            _cameraMotionCtrl?.PlayInspectFeedback();
        }
    }

    private void HandlePunchCommand(PlayerInputCommand command)
    {
        if (command.Phase != PlayerInputPhase.Started || !_meleeCombatCtrl)
        {
            return;
        }

        _meleeCombatCtrl.TryPunch();
    }

    private void CancelInspect()
    {
        WeaponActionDefinition action = _weaponActionCtrl.CurrentAction;
        if (action != null && action.ActionType == WeaponActionType.Inspect)
        {
            if (_weaponActionCtrl.CancelCurrentAction(WeaponActionCancelReason.Manual, true))
            {
                _cameraMotionCtrl?.CancelInspectFeedback();
            }
        }
    }

    private void HandleWeaponCycleCommand(PlayerInputCommand command)
    {
        if (command.Phase != PlayerInputPhase.Performed ||
            IsOperationLocked ||
            Mathf.Approximately(command.Value.y, 0f))
        {
            return;
        }

        int direction = command.Value.y > 0f ? 1 : -1;
        if (!_weaponInventoryCtrl.TryCycle(direction))
        {
            return;
        }

        _isFiring = false;
        _weaponCombatCtrl.ReceiveFireInput(PlayerInputPhase.Canceled);
        _moveCtrl.SetSprintInput(false);
        _aimCtrl.StopAim();
        CommandReceived?.Invoke(command);
    }

    private void HandleVaultStarted()
    {
        _meleeCombatCtrl?.CancelPunch();
        _isFiring = false;
        _weaponCombatCtrl.ReceiveFireInput(PlayerInputPhase.Canceled);
        _aimCtrl.CancelForVault();
        _weaponActionCtrl.CancelForVault();
        if (_cameraMotionCtrl)
        {
            _cameraMotionCtrl.PlayVaultFeedback();
        }

        OperationLockChanged?.Invoke(true);
        VaultStarted?.Invoke();
    }

    private void HandleSlideStarted()
    {
        _cameraMotionCtrl?.PlaySlideFeedback();
    }

    private void HandleVaultFinished()
    {
        _aimCtrl.SetActionLocked(false);
        OperationLockChanged?.Invoke(false);
        VaultFinished?.Invoke();
    }

    private void HandleWeaponChanged(WeaponDefinitionSO weaponData)
    {
        _aimCtrl.SetWeaponData(weaponData);
        RefreshMovementSpeed();
    }

    private void HandleWeaponActionStarted(
        WeaponActionDefinition action,
        bool startedWhileAiming)
    {
        _weaponActionBlockedAim = !action.AllowsAim;
        if (_weaponActionBlockedAim)
        {
            _aimCtrl.BeginWeaponAction();
        }

        RefreshMovementSpeed();
    }

    private void HandleWeaponMarker(
        WeaponActionType actionType,
        WeaponActionMarkerType markerType)
    {
        if ((actionType == WeaponActionType.Reload ||
            actionType == WeaponActionType.EmptyReload) &&
            markerType == WeaponActionMarkerType.AmmoCommit)
        {
            _cameraMotionCtrl?.PlayMagazineSeatFeedback();
            return;
        }

        if (actionType == WeaponActionType.PerRoundInsert &&
            markerType == WeaponActionMarkerType.InsertRound)
        {
            _cameraMotionCtrl?.PlayRoundInsertFeedback();
        }
    }

    private void HandleShotFired(WeaponDefinitionSO weaponData)
    {
        FireShot?.Invoke();
        if (!_cameraMotionCtrl)
        {
            return;
        }

        if (weaponData)
        {
            _cameraMotionCtrl.PlayFireFeedback(
                weaponData.CameraFeedback,
                _aimCtrl.AimProgress);
        }
        else
        {
            _cameraMotionCtrl.PlayFireFeedback();
        }
    }

    private void HandlePunchStarted()
    {
        _isPunching = true;
        _isFiring = false;
        _weaponCombatCtrl.ReceiveFireInput(PlayerInputPhase.Canceled);

        WeaponActionDefinition currentAction = _weaponActionCtrl.CurrentAction;
        bool wasInspecting = currentAction != null &&
            currentAction.ActionType == WeaponActionType.Inspect;
        _weaponActionCtrl.CancelCurrentAction(
            WeaponActionCancelReason.Manual,
            true);
        if (wasInspecting)
        {
            _cameraMotionCtrl?.CancelInspectFeedback();
        }

        // 即时清除瞄准进度，确保逻辑、FOV 与 Animator 不会继续残留举枪姿势。
        _aimCtrl.BeginMeleeAction();
        PunchStarted?.Invoke();
    }

    private void HandlePunchCompleted()
    {
        FinishPunch(false);
    }

    private void HandlePunchCanceled()
    {
        FinishPunch(true);
    }

    private void FinishPunch(bool canceled)
    {
        if (!_isPunching)
        {
            return;
        }

        _isPunching = false;
        _aimCtrl.EndMeleeAction();
        if (canceled)
        {
            PunchCanceled?.Invoke();
        }
        else
        {
            PunchCompleted?.Invoke();
        }
    }

    private void HandleWeaponActionCompleted(WeaponActionType actionType)
    {
        FinishAimBlock();
        RefreshMovementSpeed();

    }

    private void HandleWeaponActionCanceled(
        WeaponActionType actionType,
        WeaponActionCancelReason reason)
    {
        FinishAimBlock();
        RefreshMovementSpeed();
    }

    private bool CanRequestFire()
    {
        WeaponDefinitionSO weaponData = _weaponActionCtrl.CurrentWeaponData;
        if (!weaponData ||
            (!_weaponActionCtrl.AllowsFire &&
                !_weaponActionCtrl.CanFireAfterReloadCommit &&
                !_weaponCombatCtrl.CanFireDuringPerRoundReload))
        {
            return false;
        }

        if (_moveCtrl.IsSprinting && !weaponData.Handling.CanFireWhileSprinting)
        {
            _moveCtrl.SetSprintInput(false);
        }
        if (!_moveCtrl.IsGrounded && !weaponData.Handling.CanFireWhileAirborne)
        {
            return false;
        }

        return true;
    }

    private void FinishAimBlock()
    {
        if (!_weaponActionBlockedAim)
        {
            return;
        }

        _weaponActionBlockedAim = false;
        _aimCtrl.EndWeaponAction();
    }

    private void RefreshMovementSpeed()
    {
        if (!_moveCtrl || !_aimCtrl || !_weaponActionCtrl)
        {
            return;
        }

        WeaponDefinitionSO weaponData = _weaponActionCtrl.CurrentWeaponData;
        float weaponMultiplier = weaponData
            ? weaponData.Handling.MovementSpeedMultiplier
            : 1f;
        float actionMultiplier = _weaponActionCtrl.AllowsMovement
            ? _weaponActionCtrl.MovementSpeedMultiplier
            : 0f;
        float speedMultiplier = weaponMultiplier *
            _aimCtrl.MovementSpeedMultiplier *
            actionMultiplier;
        _moveCtrl.SetExternalSpeedMultiplier(speedMultiplier);
    }

    private void RefreshCameraMovement()
    {
        if (!_cameraMotionCtrl || !_moveCtrl)
        {
            return;
        }

        Vector3 localVelocity = _moveCtrl.LocalVelocity;
        Vector2 horizontalVelocity = new Vector2(localVelocity.x, localVelocity.z);
        PlayerDataSO playerData = _moveCtrl.PlayerData;
        float referenceSpeed = _moveCtrl.IsSprinting
            ? playerData.SprintSpeed
            : playerData.MoveSpeed;
        Vector2 motionInput = _moveCtrl.IsVaulting || _moveCtrl.IsSliding
            ? Vector2.zero
            : horizontalVelocity / Mathf.Max(0.01f, referenceSpeed);
        _cameraMotionCtrl.SetMovementState(
            Vector2.ClampMagnitude(motionInput, 1f),
            _moveCtrl.IsGrounded,
            _moveCtrl.IsSprinting);
    }

    private void RefreshLandingFeedback()
    {
        if (!_moveCtrl.IsGrounded)
        {
            if (!_moveCtrl.IsVaulting)
            {
                _wasAirborne = true;
            }
            return;
        }

        if (!_wasAirborne)
        {
            return;
        }

        _wasAirborne = false;
        if (_cameraMotionCtrl)
        {
            _cameraMotionCtrl.PlayLandingFeedback();
        }
    }

    private void StartListening()
    {
        _inputCtrl.CommandIssued += Receive;
    }

    private void StopListening()
    {
        _inputCtrl.CommandIssued -= Receive;
    }
}
