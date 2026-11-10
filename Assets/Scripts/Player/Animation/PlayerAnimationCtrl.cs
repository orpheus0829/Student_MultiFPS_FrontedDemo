using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAnimationCtrl : MonoBehaviour
{
    [Header("唯一玩法状态")]
    [SerializeField, InspectorName("玩家移动")] private PlayerMoveCtrl _moveCtrl;
    [SerializeField, InspectorName("玩家瞄准")] private PlayerAimCtrl _aimCtrl;
    [SerializeField, InspectorName("枪械动作")] private WeaponActionCtrl _weaponActionCtrl;

    [Header("双视角表现")]
    [SerializeField, InspectorName("第一人称表现")] private PlayerAnimationView _firstPersonView;
    [SerializeField, InspectorName("第三人称表现")] private PlayerAnimationView _thirdPersonView;

    public PlayerAnimationView FirstPersonView
    {
        get
        {
            return _firstPersonView;
        }
    }

    public PlayerAnimationView ThirdPersonView
    {
        get
        {
            return _thirdPersonView;
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
    }

    private void OnEnable()
    {
        if (_moveCtrl)
        {
            _moveCtrl.VaultStarted += HandleVaultStarted;
            _moveCtrl.VaultFinished += HandleVaultFinished;
            _moveCtrl.SlideStarted += HandleSlideStarted;
            _moveCtrl.SlideFinished += HandleSlideFinished;
        }

        if (_weaponActionCtrl)
        {
            _weaponActionCtrl.ActionStarted += HandleWeaponActionStarted;
            _weaponActionCtrl.ActionCompleted += HandleWeaponActionCompleted;
            _weaponActionCtrl.ActionCanceled += HandleWeaponActionCanceled;
        }
    }

    private void LateUpdate()
    {
        PlayerAnimationFrame frame = CreateAnimationFrame();
        if (_thirdPersonView)
        {
            _thirdPersonView.ApplyFrame(frame);
        }
    }

    private void OnDisable()
    {
        if (_moveCtrl)
        {
            _moveCtrl.VaultStarted -= HandleVaultStarted;
            _moveCtrl.VaultFinished -= HandleVaultFinished;
            _moveCtrl.SlideStarted -= HandleSlideStarted;
            _moveCtrl.SlideFinished -= HandleSlideFinished;
        }

        if (_weaponActionCtrl)
        {
            _weaponActionCtrl.ActionStarted -= HandleWeaponActionStarted;
            _weaponActionCtrl.ActionCompleted -= HandleWeaponActionCompleted;
            _weaponActionCtrl.ActionCanceled -= HandleWeaponActionCanceled;
        }

        if (_firstPersonView)
        {
            _firstPersonView.ResetPresentation();
        }
        if (_thirdPersonView)
        {
            _thirdPersonView.ResetPresentation();
        }
    }

    private PlayerAnimationFrame CreateAnimationFrame()
    {
        Vector2 move = Vector2.zero;
        bool hasMovement = _moveCtrl && _moveCtrl.isActiveAndEnabled;
        Vector3 localVelocity = hasMovement ? _moveCtrl.LocalVelocity : Vector3.zero;
        bool isVaulting = hasMovement && _moveCtrl.IsVaulting;
        float speed = new Vector2(localVelocity.x, localVelocity.z).magnitude;

        if (hasMovement && _moveCtrl.MoveInput.sqrMagnitude > 0.01f && speed > 0.01f)
        {
            move = new Vector2(localVelocity.x, localVelocity.z).normalized;
        }

        if (isVaulting)
        {
            move = Vector2.zero;
            speed = 0f;
        }

        return new PlayerAnimationFrame(
            move,
            speed,
            !hasMovement || _moveCtrl.IsGrounded,
            hasMovement ? _moveCtrl.VerticalSpeed : 0f,
            hasMovement && _moveCtrl.IsSprinting,
            _aimCtrl ? _aimCtrl.AimProgress : 0f,
            isVaulting,
            hasMovement && _moveCtrl.IsSliding);
    }

    private void HandleVaultStarted()
    {
        if (_thirdPersonView)
        {
            _thirdPersonView.StartVault();
        }
    }

    private void HandleVaultFinished()
    {
        if (_thirdPersonView)
        {
            _thirdPersonView.FinishVault();
        }
    }

    private void HandleSlideStarted()
    {
        if (_thirdPersonView)
        {
            _thirdPersonView.StartSlide();
        }
    }

    private void HandleSlideFinished()
    {
        if (_thirdPersonView)
        {
            _thirdPersonView.FinishSlide();
        }
    }

    private void HandleWeaponActionStarted(
        WeaponActionDefinition action,
        bool startedWhileAiming)
    {
        if (_thirdPersonView)
        {
            _thirdPersonView.StartWeaponAction(action, startedWhileAiming);
        }
    }

    private void HandleWeaponActionCompleted(WeaponActionType actionType)
    {
        if (_weaponActionCtrl && _weaponActionCtrl.CurrentAction != null)
        {
            return;
        }

        if (_thirdPersonView)
        {
            _thirdPersonView.CompleteWeaponAction(actionType);
        }
    }

    private void HandleWeaponActionCanceled(
        WeaponActionType actionType,
        WeaponActionCancelReason cancelReason)
    {
        if (_thirdPersonView)
        {
            _thirdPersonView.CancelWeaponAction(actionType);
        }
    }
}
