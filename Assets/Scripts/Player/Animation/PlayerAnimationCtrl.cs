using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAnimationCtrl : MonoBehaviour
{
    private const string IdleStateName = "Idle";
    private const string ThirdPersonBodyLayerName = "ThirdPersonBody";

    [Header("唯一玩法状态")]
    [SerializeField, InspectorName("玩家移动")] private PlayerMoveCtrl _moveCtrl;
    [SerializeField, InspectorName("玩家瞄准")] private PlayerAimCtrl _aimCtrl;
    [SerializeField, InspectorName("枪械动作")] private WeaponActionCtrl _weaponActionCtrl;
    [SerializeField, InspectorName("玩家动作")] private PlayerActionCtrl _actionCtrl;
    [SerializeField, InspectorName("枪械表现")] private PlayerWeaponViewCtrl _weaponViewCtrl;
    [SerializeField, InspectorName("近战战斗")] private PlayerMeleeCombatCtrl _meleeCombatCtrl;

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

    private FirstPersonCameraCtrl _cameraCtrl;
    private Transform _firstPersonMeleeRoot;
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
    private Renderer[] _firstPersonArmRenderers;
    private bool[] _firstPersonArmRendererStates;

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
        if (!_meleeCombatCtrl)
        {
            TryGetComponent(out _meleeCombatCtrl);
        }
        TryGetComponent(out _health);
        _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);
        Camera viewCamera = _cameraCtrl
            ? _cameraCtrl.GetComponentInChildren<Camera>(true)
            : null;
        _firstPersonMeleeRoot = viewCamera ? viewCamera.transform :
            (_cameraCtrl ? _cameraCtrl.transform : null);
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
        if (_cameraCtrl)
        {
            _cameraCtrl.ViewChanged += HandleViewChanged;
        }
    }

    private void LateUpdate()
    {
        PlayerAnimationFrame frame = CreateAnimationFrame();
        if (_thirdPersonView)
        {
            _thirdPersonView.ApplyFrame(frame);
        }
        RefreshMeleePresentation();
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
        if (_cameraCtrl)
        {
            _cameraCtrl.ViewChanged -= HandleViewChanged;
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
        SetFirstPersonArmsVisible(false);
        if (_showMeleeBlade)
        {
            SpawnMeleeBlades();
        }

        WeaponMeleeData melee = GetMeleeData();
        if (_thirdPersonView)
        {
            float playbackSpeed = melee != null
                ? melee.ThirdPersonAnimationSpeed
                : 1f;
            _thirdPersonView.StartPunch(playbackSpeed);
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
        WeaponMeleeData melee = GetMeleeData();
        if (!_meleeBladePrefab || melee == null)
        {
            return;
        }

        Transform thirdHand = FindLeftHand(_thirdPersonView, true);
        _firstPersonMeleeBlade = SpawnBlade(
            _firstPersonMeleeRoot,
            melee.FirstPersonStartPosition,
            melee.FirstPersonStartRotation,
            melee.FirstPersonStartScale,
            "FP_BLACK_STICK");
        _thirdPersonMeleeBlade = SpawnBlade(
            thirdHand,
            melee.ThirdPersonPosition,
            melee.ThirdPersonRotation,
            melee.ThirdPersonScale,
            "TP_BLACK_STICK");
        if (_firstPersonMeleeBlade && _firstPersonView && _firstPersonView.WeaponRoot)
        {
            SetLayerRecursively(
                _firstPersonMeleeBlade,
                _firstPersonView.WeaponRoot.gameObject.layer);
        }
        if (_thirdPersonMeleeBlade)
        {
            SetScaleCompensatedLocalPosition(
                _thirdPersonMeleeBlade.transform,
                melee.ThirdPersonPosition);
            SetWorldScale(_thirdPersonMeleeBlade.transform, melee.ThirdPersonScale);
            int thirdPersonLayer = LayerMask.NameToLayer(ThirdPersonBodyLayerName);
            if (thirdPersonLayer >= 0)
            {
                SetLayerRecursively(_thirdPersonMeleeBlade, thirdPersonLayer);
            }
            SetThirdPersonMeleeVisible(_cameraCtrl && _cameraCtrl.IsThirdPerson);
        }
        if (_thirdPersonMeleeBlade && melee.ThirdPersonWeaponShowDelay > 0f)
        {
            _thirdPersonMeleeBlade.SetActive(false);
        }
    }

    private GameObject SpawnBlade(
        Transform parent,
        Vector3 localPosition,
        Quaternion localRotation,
        Vector3 localScale,
        string objectName)
    {
        if (!parent)
        {
            return null;
        }

        GameObject blade = Instantiate(_meleeBladePrefab, parent, false);
        blade.name = objectName;
        blade.transform.localPosition = localPosition;
        blade.transform.localRotation = localRotation;
        blade.transform.localScale = localScale;
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

    private void RefreshMeleePresentation()
    {
        if (!_isPunchPresentationActive)
        {
            return;
        }

        WeaponMeleeData melee = GetMeleeData();
        if (melee == null)
        {
            return;
        }

        float duration = Mathf.Max(0.01f, melee.ActionDuration);
        float elapsed = (_actionCtrl ? _actionCtrl.PunchProgress : 0f) * duration;
        if (_firstPersonMeleeBlade)
        {
            ApplyFirstPersonMeleePose(_firstPersonMeleeBlade.transform, melee, elapsed);
        }
        if (_thirdPersonMeleeBlade && !_thirdPersonMeleeBlade.activeSelf &&
            elapsed >= melee.ThirdPersonWeaponShowDelay)
        {
            _thirdPersonMeleeBlade.SetActive(true);
        }
    }

    private static void ApplyFirstPersonMeleePose(
        Transform blade,
        WeaponMeleeData melee,
        float elapsed)
    {
        float duration = Mathf.Max(0.01f, melee.ActionDuration);
        float impactTime = Mathf.Clamp(melee.ImpactDelay, 0.01f, duration * 0.8f);
        float windUpEnd = Mathf.Min(duration * 0.1f, impactTime * 0.55f);
        float overshootEnd = Mathf.Min(duration, impactTime + duration * 0.1f);
        Vector3 start = melee.FirstPersonStartPosition;
        Vector3 end = melee.FirstPersonEndPosition;
        Vector3 travel = end - start;
        float distance = Mathf.Max(0.01f, travel.magnitude);
        Vector3 direction = travel / distance;
        float strength = Mathf.Clamp(distance / 0.75f, 0.8f, 1.25f);
        Vector3 windUp = start - direction * distance * 0.045f +
            Vector3.down * distance * 0.025f;
        Quaternion windUpRotation = melee.FirstPersonStartRotation *
            Quaternion.AngleAxis(-7f * strength, Vector3.forward);
        Vector3 control = Vector3.Lerp(start, end, 0.5f) +
            Vector3.up * distance * melee.ArcHeightFactor +
            Vector3.forward * distance * melee.ArcForwardFactor;
        Vector3 overshoot = end + direction * distance * melee.OvershootPercent +
            Vector3.down * distance * melee.OvershootPercent * 0.45f;
        Quaternion overshootRotation = melee.FirstPersonEndRotation *
            Quaternion.AngleAxis(12f * strength, Vector3.forward);

        if (elapsed <= windUpEnd)
        {
            float progress = windUpEnd > 0f ? elapsed / windUpEnd : 1f;
            progress = SmoothProgress(progress);
            blade.localPosition = Vector3.LerpUnclamped(start, windUp, progress);
            blade.localRotation = Quaternion.SlerpUnclamped(
                melee.FirstPersonStartRotation,
                windUpRotation,
                progress);
            blade.localScale = melee.FirstPersonStartScale;
            return;
        }

        if (elapsed <= impactTime)
        {
            float progress = Mathf.InverseLerp(windUpEnd, impactTime, elapsed);
            progress = 1f - Mathf.Pow(1f - progress, 3f);
            blade.localPosition = EvaluateQuadratic(windUp, control, end, progress);
            Quaternion rotation = Quaternion.SlerpUnclamped(
                windUpRotation,
                melee.FirstPersonEndRotation,
                progress);
            blade.localRotation = rotation * Quaternion.AngleAxis(
                -10f * strength * Mathf.Sin(progress * Mathf.PI),
                Vector3.forward);
            blade.localScale = Vector3.LerpUnclamped(
                melee.FirstPersonStartScale,
                melee.FirstPersonEndScale,
                progress);
            return;
        }

        if (elapsed <= overshootEnd)
        {
            float progress = Mathf.InverseLerp(impactTime, overshootEnd, elapsed);
            progress = 1f - Mathf.Pow(1f - progress, 2f);
            blade.localPosition = Vector3.LerpUnclamped(end, overshoot, progress);
            blade.localRotation = Quaternion.SlerpUnclamped(
                melee.FirstPersonEndRotation,
                overshootRotation,
                progress);
            blade.localScale = melee.FirstPersonEndScale;
            return;
        }

        float settleProgress = Mathf.InverseLerp(overshootEnd, duration, elapsed);
        settleProgress = GetDampedSettleProgress(settleProgress);
        blade.localPosition = Vector3.LerpUnclamped(overshoot, end, settleProgress);
        blade.localRotation = Quaternion.SlerpUnclamped(
            overshootRotation,
            melee.FirstPersonEndRotation,
            settleProgress);
        blade.localScale = melee.FirstPersonEndScale;
    }

    private static Vector3 EvaluateQuadratic(
        Vector3 start,
        Vector3 control,
        Vector3 end,
        float progress)
    {
        float inverse = 1f - progress;
        return inverse * inverse * start +
            2f * inverse * progress * control +
            progress * progress * end;
    }

    private static float SmoothProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        return progress * progress * (3f - 2f * progress);
    }

    private static float GetDampedSettleProgress(float progress)
    {
        progress = Mathf.Clamp01(progress);
        if (progress >= 1f)
        {
            return 1f;
        }
        return 1f - Mathf.Exp(-6f * progress) *
            Mathf.Cos(progress * Mathf.PI * 2f);
    }

    private WeaponMeleeData GetMeleeData()
    {
        WeaponDefinitionSO weapon = _meleeCombatCtrl
            ? _meleeCombatCtrl.WeaponData
            : null;
        return weapon ? weapon.Melee : null;
    }

    private void SetFirstPersonArmsVisible(bool visible)
    {
        Animator animator = _firstPersonView ? _firstPersonView.CharacterAnimator : null;
        if (!visible)
        {
            if (!animator)
            {
                return;
            }

            _firstPersonArmRenderers = animator.GetComponentsInChildren<Renderer>(true);
            _firstPersonArmRendererStates = new bool[_firstPersonArmRenderers.Length];
            for (int i = 0; i < _firstPersonArmRenderers.Length; i++)
            {
                Renderer renderer = _firstPersonArmRenderers[i];
                _firstPersonArmRendererStates[i] = renderer && renderer.enabled;
                if (renderer)
                {
                    renderer.enabled = false;
                }
            }
            return;
        }

        if (_firstPersonArmRenderers == null || _firstPersonArmRendererStates == null)
        {
            return;
        }
        for (int i = 0; i < _firstPersonArmRenderers.Length; i++)
        {
            Renderer renderer = _firstPersonArmRenderers[i];
            if (renderer)
            {
                renderer.enabled = _firstPersonArmRendererStates[i];
            }
        }
        _firstPersonArmRenderers = null;
        _firstPersonArmRendererStates = null;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            children[i].gameObject.layer = layer;
        }
    }

    private static void SetWorldScale(Transform target, Vector3 worldScale)
    {
        Transform parent = target.parent;
        if (!parent)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            DivideScale(worldScale.x, parentScale.x),
            DivideScale(worldScale.y, parentScale.y),
            DivideScale(worldScale.z, parentScale.z));
    }

    private static void SetScaleCompensatedLocalPosition(Transform target, Vector3 position)
    {
        Transform parent = target.parent;
        if (!parent)
        {
            target.localPosition = position;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localPosition = new Vector3(
            DivideScale(position.x, parentScale.x),
            DivideScale(position.y, parentScale.y),
            DivideScale(position.z, parentScale.z));
    }

    private static float DivideScale(float value, float parentScale)
    {
        return Mathf.Abs(parentScale) > 0.0001f ? value / parentScale : value;
    }

    private void HandleViewChanged(bool isThirdPerson)
    {
        SetThirdPersonMeleeVisible(isThirdPerson);
    }

    private void SetThirdPersonMeleeVisible(bool visible)
    {
        if (!_thirdPersonMeleeBlade)
        {
            return;
        }

        Renderer[] renderers = _thirdPersonMeleeBlade.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].forceRenderingOff = !visible;
        }
    }

    private void FinishMeleePresentation(bool playEquip)
    {
        _thirdPersonView?.FinishPunch();
        if (_firstPersonMeleeBlade) Destroy(_firstPersonMeleeBlade);
        if (_thirdPersonMeleeBlade) Destroy(_thirdPersonMeleeBlade);
        _firstPersonMeleeBlade = null;
        _thirdPersonMeleeBlade = null;
        SetFirstPersonArmsVisible(true);
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
