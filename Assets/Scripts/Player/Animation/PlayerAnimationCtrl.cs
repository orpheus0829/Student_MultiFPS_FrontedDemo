using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAnimationCtrl : MonoBehaviour
{
    private const string IdleStateName = "Idle";

    [Header("唯一玩法状态")]
    [SerializeField, InspectorName("玩家移动")] private PlayerMoveCtrl _moveCtrl;
    [SerializeField, InspectorName("玩家瞄准")] private PlayerAimCtrl _aimCtrl;
    [SerializeField, InspectorName("枪械动作")] private WeaponActionCtrl _weaponActionCtrl;
    [SerializeField, InspectorName("玩家动作")] private PlayerActionCtrl _actionCtrl;
    [SerializeField, InspectorName("枪械表现")] private PlayerWeaponViewCtrl _weaponViewCtrl;

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

    [Header("近战刀劈表现")]
    [SerializeField, InspectorName("显示近战刀")] private bool _showMeleeBlade;
    [SerializeField, InspectorName("安比近战刀预制体")] private GameObject _meleeBladePrefab;
    [SerializeField, Min(0.001f), InspectorName("第一人称刀缩放倍率")] private float _firstPersonBladeScale = 0.5f;
    [SerializeField, Min(0.001f), InspectorName("第三人称刀缩放倍率")] private float _thirdPersonBladeScale = 0.5f;

    // RPG ATK2 在 0.1 秒激活的安比武器_L_2，挂在左手骨下的原始变换。
    private static readonly Vector3 RpgBladePosition = new Vector3(-0.0803f, -0.158f, 0.09f);
    private static readonly Quaternion RpgBladeRotation =
        new Quaternion(0.009287184f, 0.70543915f, -0.31225657f, 0.63621175f);
    private static readonly Vector3 RpgBladeScale = new Vector3(1f, 0.6f, 0.1f);

    private FirstPersonCameraCtrl _cameraCtrl;
    private CharacterHealth _health;
    private Animator _pitchAnimator;
    private Transform _spine;
    private Transform _chest;
    private Transform _upperChest;
    private float _upperBodyPitch;
    private float _upperBodyPitchWeight;
    private bool _isPunchPresentationActive;
    private GameObject _firstPersonMeleeBlade;
    private GameObject _thirdPersonMeleeBlade;

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
        if (!_actionCtrl)
        {
            TryGetComponent(out _actionCtrl);
        }
        if (!_weaponViewCtrl)
        {
            TryGetComponent(out _weaponViewCtrl);
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
        if (_actionCtrl)
        {
            _actionCtrl.PunchStarted += HandlePunchStarted;
            _actionCtrl.PunchCompleted += HandlePunchCompleted;
            _actionCtrl.PunchCanceled += HandlePunchCanceled;
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
        if (_actionCtrl)
        {
            _actionCtrl.PunchStarted -= HandlePunchStarted;
            _actionCtrl.PunchCompleted -= HandlePunchCompleted;
            _actionCtrl.PunchCanceled -= HandlePunchCanceled;
        }

        FinishMeleePresentation(false);

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

    private void HandlePunchStarted()
    {
        if (_health && _health.IsDead)
        {
            return;
        }

        _isPunchPresentationActive = true;
        _weaponViewCtrl?.SetWeaponModelsVisible(false);
        if (_showMeleeBlade)
        {
            SpawnMeleeBlades();
        }

        if (_firstPersonView)
        {
            _firstPersonView.StartPunch();
        }
        if (_thirdPersonView)
        {
            _thirdPersonView.StartPunch();
        }
    }

    private void HandlePunchCompleted()
    {
        FinishMeleePresentation(true);
    }

    private void HandlePunchCanceled()
    {
        FinishMeleePresentation(true);
    }

    private void SpawnMeleeBlades()
    {
        if (!_meleeBladePrefab)
        {
            return;
        }
        Transform firstHand = FindLeftHand(_firstPersonView, false);
        Transform thirdHand = FindLeftHand(_thirdPersonView, true);
        _firstPersonMeleeBlade = SpawnBlade(firstHand, _firstPersonBladeScale, "FP_BLACK_BLADE");
        _thirdPersonMeleeBlade = SpawnBlade(thirdHand, _thirdPersonBladeScale, "TP_BLACK_BLADE");
    }

    private GameObject SpawnBlade(Transform hand, float bladeScale, string pivotName)
    {
        if (!hand)
        {
            return null;
        }
        GameObject blade = Instantiate(_meleeBladePrefab, hand, false);
        blade.name = pivotName;
        Vector3 prefabScale = blade.transform.localScale;
        blade.transform.localPosition = RpgBladePosition;
        blade.transform.localRotation = RpgBladeRotation;
        blade.transform.localScale = Vector3.Scale(
            prefabScale,
            RpgBladeScale * Mathf.Max(0.01f, bladeScale));
        return blade;
    }

    private static Transform FindLeftHand(PlayerAnimationView view, bool humanoid)
    {
        Animator animator = view ? view.CharacterAnimator : null;
        if (!animator)
        {
            return null;
        }
        if (humanoid && animator.isHuman)
        {
            return animator.GetBoneTransform(HumanBodyBones.LeftHand);
        }
        foreach (Transform child in animator.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "hand_l") return child;
        }
        return null;
    }

    private void FinishMeleePresentation(bool playEquip)
    {
        _firstPersonView?.FinishPunch();
        _thirdPersonView?.FinishPunch();
        if (_firstPersonMeleeBlade) Destroy(_firstPersonMeleeBlade);
        if (_thirdPersonMeleeBlade) Destroy(_thirdPersonMeleeBlade);
        _firstPersonMeleeBlade = null;
        _thirdPersonMeleeBlade = null;
        _weaponViewCtrl?.SetWeaponModelsVisible(true);
        _isPunchPresentationActive = false;
        if (playEquip && _weaponActionCtrl && !_weaponActionCtrl.IsBusy)
        {
            _weaponActionCtrl.TryStartAction(WeaponActionType.Equip);
        }
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
