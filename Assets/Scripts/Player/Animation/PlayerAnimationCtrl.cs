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
    [SerializeField, InspectorName("安比近战刀预制体")] private GameObject _meleeBladePrefab;
    [SerializeField, Min(0.001f), InspectorName("第一人称刀缩放")] private float _firstPersonBladeScale = 0.1f;
    [SerializeField, Min(0.001f), InspectorName("第三人称刀缩放")] private float _thirdPersonBladeScale = 0.1f;

    // 原样取自 RPG/Action_RPG_Demo 的安比 Prefab：
    // hand_r -> Grab_Weapon -> 刀。不要把两级变换近似合并，否则在 FP 骨架上会偏位。
    private static readonly Vector3 RpgGrabWeaponPosition =
        new Vector3(-0.07887875f, 0.061014097f, 0.0068266448f);
    private static readonly Vector3 RpgBladePosition = new Vector3(-0.095f, -0.177f, -0.146f);
    private static readonly Quaternion RpgBladeRotation =
        new Quaternion(-0.11309409f, -0.678862f, -0.034314904f, -0.72469217f);

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
        SpawnMeleeBlades();

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
        Transform firstHand = FindRightHand(_firstPersonView, false);
        Transform thirdHand = FindRightHand(_thirdPersonView, true);
        _firstPersonMeleeBlade = SpawnBlade(firstHand, _firstPersonBladeScale, "FP_Grab_Weapon");
        _thirdPersonMeleeBlade = SpawnBlade(thirdHand, _thirdPersonBladeScale, "TP_Grab_Weapon");
    }

    private GameObject SpawnBlade(Transform hand, float bladeScale, string pivotName)
    {
        if (!hand)
        {
            return null;
        }
        GameObject pivot = new GameObject(pivotName);
        pivot.transform.SetParent(hand, false);
        pivot.transform.localPosition = RpgGrabWeaponPosition;
        pivot.transform.localRotation = Quaternion.identity;
        pivot.transform.localScale = Vector3.one;

        GameObject blade = Instantiate(_meleeBladePrefab, pivot.transform, false);
        blade.name = _meleeBladePrefab.name;
        blade.transform.localPosition = RpgBladePosition;
        blade.transform.localRotation = RpgBladeRotation;
        blade.transform.localScale = Vector3.one * Mathf.Max(0.01f, bladeScale);
        KeepOnlyBlackWeaponGeometry(blade);
        return pivot;
    }

    private static void KeepOnlyBlackWeaponGeometry(GameObject blade)
    {
        // 该 FBX 把绿色刀鞘和黑色棍状刀体合并在同一个 SkinnedMesh 中。
        // 只缩放 joint_katana 会把刀鞘压成一团，仍会残留；这里按蒙皮权重
        // 真正删除刀鞘三角形，只留下 joint_Anbi_Weapon_* 控制的黑色刀体。
        foreach (SkinnedMeshRenderer renderer in blade.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh source = renderer.sharedMesh;
            if (!source || !source.isReadable)
            {
                continue;
            }

            Transform[] bones = renderer.bones;
            bool[] weaponBones = new bool[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                weaponBones[i] = bones[i] && bones[i].name.IndexOf(
                    "joint_Anbi_Weapon", System.StringComparison.OrdinalIgnoreCase) >= 0;
            }

            BoneWeight[] weights = source.boneWeights;
            Mesh filtered = Instantiate(source);
            filtered.name = source.name + "_BlackWeaponOnly";
            for (int subMesh = 0; subMesh < source.subMeshCount; subMesh++)
            {
                int[] triangles = source.GetTriangles(subMesh);
                var kept = new System.Collections.Generic.List<int>(triangles.Length);
                for (int i = 0; i + 2 < triangles.Length; i += 3)
                {
                    if (UsesWeaponBone(weights[triangles[i]], weaponBones) +
                        UsesWeaponBone(weights[triangles[i + 1]], weaponBones) +
                        UsesWeaponBone(weights[triangles[i + 2]], weaponBones) >= 1.5f)
                    {
                        kept.Add(triangles[i]);
                        kept.Add(triangles[i + 1]);
                        kept.Add(triangles[i + 2]);
                    }
                }
                filtered.SetTriangles(kept, subMesh, false);
            }
            filtered.RecalculateBounds();
            renderer.sharedMesh = filtered;
        }
    }

    private static float UsesWeaponBone(BoneWeight weight, bool[] weaponBones)
    {
        float result = 0f;
        if (weight.boneIndex0 < weaponBones.Length && weaponBones[weight.boneIndex0]) result += weight.weight0;
        if (weight.boneIndex1 < weaponBones.Length && weaponBones[weight.boneIndex1]) result += weight.weight1;
        if (weight.boneIndex2 < weaponBones.Length && weaponBones[weight.boneIndex2]) result += weight.weight2;
        if (weight.boneIndex3 < weaponBones.Length && weaponBones[weight.boneIndex3]) result += weight.weight3;
        return result;
    }

    private static Transform FindRightHand(PlayerAnimationView view, bool humanoid)
    {
        Animator animator = view ? view.CharacterAnimator : null;
        if (!animator)
        {
            return null;
        }
        if (humanoid && animator.isHuman)
        {
            return animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        foreach (Transform child in animator.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == "hand_r") return child;
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
