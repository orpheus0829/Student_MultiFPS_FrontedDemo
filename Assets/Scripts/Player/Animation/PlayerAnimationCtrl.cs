using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAnimationCtrl : MonoBehaviour
{
    private const string IdleStateName = "Idle";

    [Header("唯一玩法状态")]
    [SerializeField, InspectorName("玩家移动")] private PlayerMoveCtrl _moveCtrl;
    [SerializeField, InspectorName("玩家瞄准")] private PlayerAimCtrl _aimCtrl;
    [SerializeField, InspectorName("枪械动作")] private WeaponActionCtrl _weaponActionCtrl;

    [Header("双视角表现")]
    [SerializeField, InspectorName("第一人称表现")] private PlayerAnimationView _firstPersonView;
    [SerializeField, InspectorName("第三人称表现")] private PlayerAnimationView _thirdPersonView;

    [Header("第三人称待机俯仰")]
    [SerializeField, Range(0f, 60f), InspectorName("最大抬头角度")]
    private float _maximumLookUpAngle = 42f;
    [SerializeField, Range(0f, 60f), InspectorName("最大低头角度")]
    private float _maximumLookDownAngle = 32f;
    [SerializeField, Min(0f), InspectorName("俯仰跟随速度")]
    private float _upperBodyPitchSpeed = 10f;
    [SerializeField, Min(0f), InspectorName("待机融合速度")]
    private float _upperBodyPitchBlendSpeed = 8f;

    private FirstPersonCameraCtrl _cameraCtrl;
    private CharacterHealth _health;
    private Animator _pitchAnimator;
    private Transform _spine;
    private Transform _chest;
    private Transform _upperChest;
    private float _upperBodyPitch;
    private float _upperBodyPitchWeight;

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
        TryGetComponent(out _health);
        _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);
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
        RefreshUpperBodyPitch();
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

        _upperBodyPitch = 0f;
        _upperBodyPitchWeight = 0f;
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

    private void RefreshUpperBodyPitch()
    {
        Animator animator = _thirdPersonView ? _thirdPersonView.CharacterAnimator : null;
        if (animator != _pitchAnimator)
        {
            CachePitchBones(animator);
        }

        bool canApplyPitch = CanApplyUpperBodyPitch();
        float targetWeight = canApplyPitch ? 1f : 0f;
        _upperBodyPitchWeight = Mathf.MoveTowards(
            _upperBodyPitchWeight,
            targetWeight,
            _upperBodyPitchBlendSpeed * Time.deltaTime);

        if (!_pitchAnimator || _upperBodyPitchWeight <= 0f)
        {
            return;
        }

        Ray aimRay = _cameraCtrl.GetCenterAimRay();
        float targetPitch = Mathf.Asin(Mathf.Clamp(aimRay.direction.normalized.y, -1f, 1f)) *
            Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(
            targetPitch,
            -_maximumLookDownAngle,
            _maximumLookUpAngle);
        _upperBodyPitch = Mathf.MoveTowardsAngle(
            _upperBodyPitch,
            targetPitch,
            _upperBodyPitchSpeed * Time.deltaTime * 90f);

        float appliedPitch = -_upperBodyPitch * _upperBodyPitchWeight;
        Vector3 pitchAxis = _pitchAnimator.transform.right;
        float upperChestWeight = _upperChest ? 0.45f : 0f;
        float chestWeight = _chest ? 0.35f + (0.45f - upperChestWeight) : 0f;
        float spineWeight = 1f - chestWeight - upperChestWeight;
        ApplyBonePitch(_spine, pitchAxis, appliedPitch * spineWeight);
        ApplyBonePitch(_chest, pitchAxis, appliedPitch * chestWeight);
        ApplyBonePitch(_upperChest, pitchAxis, appliedPitch * upperChestWeight);
    }

    private bool CanApplyUpperBodyPitch()
    {
        if (!_cameraCtrl || !_pitchAnimator || !_pitchAnimator.enabled ||
            !_pitchAnimator.isHuman || (_health && _health.IsDead))
        {
            return false;
        }

        if (_moveCtrl && (!_moveCtrl.IsGrounded || _moveCtrl.IsMoving ||
            _moveCtrl.MoveInput.sqrMagnitude > 0.01f || _moveCtrl.IsSprinting ||
            _moveCtrl.IsVaulting || _moveCtrl.IsSliding))
        {
            return false;
        }

        WeaponActionDefinition currentAction = _weaponActionCtrl
            ? _weaponActionCtrl.CurrentAction
            : null;
        if (currentAction != null && !AllowsStationaryUpperBodyPitch(currentAction.ActionType))
        {
            return false;
        }

        if (_pitchAnimator.IsInTransition(0))
        {
            return false;
        }

        return _pitchAnimator.GetCurrentAnimatorStateInfo(0).IsName(IdleStateName);
    }

    private static bool AllowsStationaryUpperBodyPitch(WeaponActionType actionType)
    {
        return actionType == WeaponActionType.Fire ||
            actionType == WeaponActionType.Reload ||
            actionType == WeaponActionType.EmptyReload ||
            actionType == WeaponActionType.PerRoundStart ||
            actionType == WeaponActionType.PerRoundInsert ||
            actionType == WeaponActionType.PerRoundEnd;
    }

    private void CachePitchBones(Animator animator)
    {
        _pitchAnimator = animator;
        _spine = null;
        _chest = null;
        _upperChest = null;
        _upperBodyPitchWeight = 0f;
        if (!animator || !animator.isHuman)
        {
            return;
        }

        _spine = animator.GetBoneTransform(HumanBodyBones.Spine);
        _chest = animator.GetBoneTransform(HumanBodyBones.Chest);
        _upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
    }

    private static void ApplyBonePitch(
        Transform bone,
        Vector3 worldAxis,
        float angle)
    {
        if (!bone)
        {
            return;
        }

        bone.rotation = Quaternion.AngleAxis(angle, worldAxis) * bone.rotation;
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
