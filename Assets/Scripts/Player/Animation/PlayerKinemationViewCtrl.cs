using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KINEMATION.FPSAnimationPack.Scripts.Player;
using KINEMATION.FPSAnimationPack.Scripts.Weapon;
using KINEMATION.KAnimationCore.Runtime.Core;
using KINEMATION.ProceduralRecoilAnimationSystem.Runtime;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

[DefaultExecutionOrder(200)]
[DisallowMultipleComponent]
public sealed class PlayerKinemationViewCtrl : MonoBehaviour
{
    private const float GaitSmoothing = 8f;
    private const float RightHandUnlockBlendDuration = 0.12f;
    private const float ReloadIkDisableBlendDuration = 0.05f;
    private const float ReloadIkRestoreDuration = 0.14f;
    private const float AkReloadRightArmSmoothingTime = 0.12f;
    private const string DefaultCharacterModelId = "character.anbi";
    private static readonly string[] RightFingerBoneNames =
    {
        "thumb_01_r", "thumb_02_r", "thumb_03_r",
        "index_01_r", "index_02_r", "index_03_r",
        "middle_01_r", "middle_02_r", "middle_03_r",
        "ring_01_r", "ring_02_r", "ring_03_r",
        "pinky_01_r", "pinky_02_r", "pinky_03_r"
    };
    private static readonly float[] FingerSwingLimits = { 40f, 50f, 35f };
    private static readonly float[] FingerTwistLimits = { 8f, 5f, 5f };
    private static readonly float[] FingerMinimumCurl = { 12f, 22f, 18f };

    [Header("项目动作状态")]
    [SerializeField, InspectorName("玩家移动")] private PlayerMoveCtrl _moveCtrl;
    [SerializeField, InspectorName("玩家瞄准")] private PlayerAimCtrl _aimCtrl;
    [SerializeField, InspectorName("枪械动作")] private WeaponActionCtrl _weaponActionCtrl;
    [SerializeField, InspectorName("枪械战斗")] private WeaponCombatCtrl _weaponCombatCtrl;
    [SerializeField, InspectorName("枪械表现")] private PlayerWeaponViewCtrl _weaponViewCtrl;

    [Header("KINEMATION 表现")]
    [SerializeField, InspectorName("第一人称表现")] private PlayerAnimationView _firstPersonView;
    [SerializeField, InspectorName("第一人称相机点")] private Transform _cameraPoint;
    [SerializeField, InspectorName("第一人称镜头反馈")]
    private FirstPersonCameraMotionCtrl _cameraMotionCtrl;
    [SerializeField, Range(0f, 1f), InspectorName("手臂 IK 权重")] private float _ikWeight = 1f;

    [Header("切枪表现")]
    [SerializeField, InspectorName("右下方收枪位移")]
    private Vector3 _holsterOffset = new Vector3(0.35f, -0.55f, -0.25f);
    [SerializeField, Range(0.01f, 0.15f), InspectorName("手臂跟镜头的滞后时间")]
    private float _cameraMotionLag = 0.06f;
    [SerializeField, Range(0f, 1f), InspectorName("手臂滞后幅度")]
    private float _cameraMotionLagWeight = 0.7f;

    [Header("检视表现")]
    [SerializeField, InspectorName("检视时手枪整体位移")]
    private Vector3 _inspectPositionOffset = new Vector3(0.025f, -0.02f, 0.02f);
    [SerializeField, InspectorName("检视时手枪整体旋转")]
    private Vector3 _inspectRotationOffset = new Vector3(6f, -10f, -7f);

    private ResourceMgr _resourceMgr;
    private FirstPersonCameraCtrl _cameraCtrl;
    private PlayerCharacterModelCtrl _characterModelCtrl;
    private PlayerThirdPersonGripCtrl _thirdPersonGripCtrl;
    private Animator _animator;
    private RecoilAnimation _recoilAnimation;
    private RuntimeAnimatorController _layerController;
    private FPSWeaponSettings _runtimeWeaponSettings;
    private WeaponDefinitionSO _configuredWeaponData;
    private FPSProceduralJob _job;
    private AnimationScriptPlayable _playable;
    private AnimationPlayableOutput _proceduralOutput;
    private PlayableGraph _proceduralGraph;
    private Transform _weaponBone;
    private IKTransforms _rightArm;
    private KTransform _localCameraPoint;
    private string _recoilResourceId;
    private int _weaponLoadVersion;
    private int _tacSprintLayerIndex = -1;
    private int _triggerDisciplineLayerIndex = -1;
    private int _rightHandLayerIndex = -1;
    private float _smoothGait;
    private float _sprintLayerWeight;
    private bool _isReady;
    private bool _hasStarted;
    private bool _hasWeaponConfigured;
    private bool _sprintActionOverrides;
    private Vector3 _restLocalPosition;
    private Quaternion _restLocalRotation;
    private Vector3 _laggedCameraPosition;
    private Vector3 _laggedCameraRotation;
    private float _holsterWeight;
    private bool _equipImpactPlayed;
    private bool _showWeaponNextFrame;
    private int _showWeaponFrame;
    private bool _isRecoilActive;
    private bool _rightHandLockRequested;
    private float _rightHandLockWeight;
    private float _currentIkWeight;
    private Vector3[] _rightArmBindPositions;
    private Vector3[] _rightArmBindScales;
    private Transform[] _rightArmLengthBones;
    private Transform[] _akReloadRightArmBones;
    private bool _akReloadRightArmSmoothingActive;
    private Vector3[] _akReloadSmoothedRightArmPositions;
    private Quaternion[] _akReloadSmoothedRightArmRotations;
    private Transform[] _rightFingerBones;
    private Vector3[] _rightFingerBindPositions;
    private Quaternion[] _rightFingerBindRotations;
    private Vector3[] _rightFingerBindScales;

    private void Awake()
    {
        CacheProjectComponents();
        if (!TryPrepareSkeleton())
        {
            enabled = false;
        }
        else
        {
            _restLocalPosition = _animator.transform.localPosition;
            _restLocalRotation = _animator.transform.localRotation;
            _currentIkWeight = _ikWeight;
        }
    }

    private void OnEnable()
    {
        if (_weaponActionCtrl)
        {
            _weaponActionCtrl.WeaponChanged += HandleWeaponChanged;
            _weaponActionCtrl.ActionStarted += HandleActionStarted;
            _weaponActionCtrl.ActionCompleted += HandleActionCompleted;
            _weaponActionCtrl.ActionCanceled += HandleActionCanceled;
        }
        if (_weaponCombatCtrl)
        {
            _weaponCombatCtrl.ShotFired += HandleShotFired;
        }
        if (_weaponViewCtrl)
        {
            _weaponViewCtrl.FirstPersonWeaponReady += HandleWeaponReady;
        }
        if (_cameraCtrl)
        {
            _cameraCtrl.ViewChanged += HandleViewChanged;
        }
        if (_hasStarted)
        {
            InitializeKinemation();
            TryConfigureCurrentWeapon();
        }
    }

    private void Start()
    {
        _hasStarted = true;
        InitializeKinemation();
        TryConfigureCurrentWeapon();
    }

    private void TryConfigureCurrentWeapon()
    {
        WeaponDefinitionSO weaponData = _weaponActionCtrl
            ? _weaponActionCtrl.CurrentWeaponData
            : null;
        if (weaponData && _weaponViewCtrl && _weaponViewCtrl.FirstPersonWeapon)
        {
            HandleWeaponReady(weaponData, _weaponViewCtrl.MuzzlePoint);
        }
    }

    private void Update()
    {
        if (!_isReady)
        {
            InitializeKinemation();
            TryConfigureCurrentWeapon();
        }
        if (!_hasWeaponConfigured || !EnsureProceduralPlayable())
        {
            return;
        }

        RefreshAnimatorState();
        _job.Update();
        _job.adsWeight = _aimCtrl ? _aimCtrl.AimProgress : 0f;
        RefreshActionIkWeight();
        UpdateRightHandGripLock();
        _playable.SetJobData(_job);
        RefreshRecoilPlayback();
    }

    private void LateUpdate()
    {
        if (!_animator)
        {
            return;
        }

        WeaponActionDefinition action = _weaponActionCtrl
            ? _weaponActionCtrl.CurrentAction
            : null;
        if (action != null)
        {
            float progress = Mathf.Clamp01(_weaponActionCtrl.Progress);
            if (action.ActionType == WeaponActionType.Holster)
            {
                _holsterWeight = EvaluateHolsterWeight(progress);
            }
            else if (action.ActionType == WeaponActionType.Equip)
            {
                _holsterWeight = EvaluateEquipWeight(progress);
                if (!_equipImpactPlayed && progress >= 0.82f)
                {
                    PlayEquipImpact();
                }
            }
        }

        Vector3 motionPosition = _cameraMotionCtrl
            ? _cameraMotionCtrl.CurrentMovementPositionOffset
            : Vector3.zero;
        Vector3 motionRotation = _cameraMotionCtrl
            ? _cameraMotionCtrl.CurrentMovementRotationOffset
            : Vector3.zero;
        float follow = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, _cameraMotionLag));
        _laggedCameraPosition = Vector3.Lerp(_laggedCameraPosition, motionPosition, follow);
        _laggedCameraRotation = Vector3.Lerp(_laggedCameraRotation, motionRotation, follow);
        float inspectWeight = 0f;
        if (action != null && action.ActionType == WeaponActionType.Inspect)
        {
            float progress = Mathf.Clamp01(_weaponActionCtrl.Progress);
            inspectWeight = Mathf.Sin(progress * Mathf.PI);
        }

        _animator.transform.localPosition = _restLocalPosition +
            _holsterOffset * _holsterWeight +
            _inspectPositionOffset * inspectWeight +
            (_laggedCameraPosition - motionPosition) * _cameraMotionLagWeight;
        _animator.transform.localRotation = _restLocalRotation * Quaternion.Euler(
            (_laggedCameraRotation - motionRotation) * _cameraMotionLagWeight +
            _inspectRotationOffset * inspectWeight);

        bool equipIsSettling = action != null &&
            action.ActionType == WeaponActionType.Equip &&
            _weaponActionCtrl.Progress < 0.22f;
        if (_showWeaponNextFrame && Time.frameCount >= _showWeaponFrame &&
            !equipIsSettling)
        {
            SetFirstPersonRenderersHidden(false);
            _showWeaponNextFrame = false;
        }

        ApplyAkReloadRightArmBoneLengthSafety(action);
        ApplyAkReloadRightArmRotationSmoothing(action);
        ApplyFirstPersonFingerSafety();
    }

    private void OnDisable()
    {
        if (_weaponActionCtrl)
        {
            _weaponActionCtrl.WeaponChanged -= HandleWeaponChanged;
            _weaponActionCtrl.ActionStarted -= HandleActionStarted;
            _weaponActionCtrl.ActionCompleted -= HandleActionCompleted;
            _weaponActionCtrl.ActionCanceled -= HandleActionCanceled;
        }
        if (_weaponCombatCtrl)
        {
            _weaponCombatCtrl.ShotFired -= HandleShotFired;
        }
        if (_weaponViewCtrl)
        {
            _weaponViewCtrl.FirstPersonWeaponReady -= HandleWeaponReady;
        }
        if (_cameraCtrl)
        {
            _cameraCtrl.ViewChanged -= HandleViewChanged;
        }

        _weaponLoadVersion++;
        _akReloadRightArmSmoothingActive = false;
        ResetRightHandGripLock();
        StopActiveRecoil();
        ReleaseRecoilProfile();
        if (_animator)
        {
            _animator.transform.localPosition = _restLocalPosition;
            _animator.transform.localRotation = _restLocalRotation;
        }
        SetFirstPersonRenderersHidden(true);
        _hasWeaponConfigured = false;
        _configuredWeaponData = null;
        _showWeaponNextFrame = false;
        _isReady = false;
        _currentIkWeight = _ikWeight;
        if (_proceduralOutput.IsOutputValid())
        {
            _proceduralGraph.DestroyOutput(_proceduralOutput);
        }
        if (_playable.IsValid())
        {
            _playable.GetGraph().DestroyPlayable(_playable);
        }
        if (_runtimeWeaponSettings)
        {
            Destroy(_runtimeWeaponSettings);
            _runtimeWeaponSettings = null;
        }
    }

    private void CacheProjectComponents()
    {
        _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);
        TryGetComponent(out _thirdPersonGripCtrl);
        TryGetComponent(out _characterModelCtrl);
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
        if (!_weaponCombatCtrl)
        {
            TryGetComponent(out _weaponCombatCtrl);
        }
        if (!_weaponViewCtrl)
        {
            TryGetComponent(out _weaponViewCtrl);
        }
        if (!_cameraPoint)
        {
            Camera viewCamera = GetComponentInChildren<Camera>(true);
            if (viewCamera)
            {
                _cameraPoint = viewCamera.transform;
            }
        }
        if (!_cameraMotionCtrl)
        {
            _cameraMotionCtrl = GetComponentInChildren<FirstPersonCameraMotionCtrl>(true);
        }
    }

    private bool TryPrepareSkeleton()
    {
        _animator = _firstPersonView ? _firstPersonView.CharacterAnimator : null;
        if (!_animator)
        {
            Debug.LogError($"{name} 未配置 KINEMATION 第一人称 Animator。", this);
            return false;
        }

        Transform root = _animator.transform;
        Transform skeletonRoot = FindChild(root, "root");
        _weaponBone = FindChild(root, "ik_hand_gun");
        Transform additiveBone = FindOrCreateChild(skeletonRoot, "ik_hand_gun_additive");
        _rightArm = CreateArm(root, "hand_r", "lowerarm_r", "upperarm_r");
        IKTransforms leftArm = CreateArm(root, "hand_l", "lowerarm_l", "upperarm_l");
        CacheRightArmBindLengths();
        CacheRightFingerBindPose(root);

        if (!skeletonRoot || !_weaponBone || !_cameraPoint ||
            !_rightArm.tip || !_rightArm.mid || !_rightArm.root ||
            !leftArm.tip || !leftArm.mid || !leftArm.root)
        {
            Debug.LogError($"{name} 的 KINEMATION 手臂骨架不完整。", this);
            return false;
        }

        _job = new FPSProceduralJob
        {
            animator = _animator,
            skeletonRoot = skeletonRoot,
            rightArm = _rightArm,
            leftArm = leftArm,
            weaponBone = _weaponBone,
            weaponBoneAdditive = additiveBone,
            cameraSocket = _cameraPoint
        };
        return true;
    }

    private void InitializeKinemation()
    {
        if (!_animator || !_animator.playableGraph.IsValid())
        {
            return;
        }

        _recoilAnimation = _animator.GetComponent<RecoilAnimation>();
        if (!_recoilAnimation)
        {
            _recoilAnimation = _animator.gameObject.AddComponent<RecoilAnimation>();
        }

        _job.recoilAnimation = _recoilAnimation;
        KTransform root = new KTransform(_animator.transform);
        _localCameraPoint = root.GetRelativeTransform(new KTransform(_cameraPoint), false);
        _isReady = EnsureProceduralPlayable();
    }

    private bool EnsureProceduralPlayable()
    {
        if (!_animator || !_animator.isActiveAndEnabled ||
            !_animator.playableGraph.IsValid())
        {
            return false;
        }
        if (_playable.IsValid() && _proceduralOutput.IsOutputValid())
        {
            return true;
        }

        // 切换 AnimatorController 会重建图，旧 Job 与骨骼流句柄必须一起重新绑定。
        if (_playable.IsValid())
        {
            _playable.GetGraph().DestroyPlayable(_playable);
        }
        if (_proceduralOutput.IsOutputValid())
        {
            _proceduralGraph.DestroyOutput(_proceduralOutput);
        }
        _job.Setup();
        _proceduralGraph = _animator.playableGraph;
        _playable = AnimationScriptPlayable.Create(_proceduralGraph, _job);
        _proceduralOutput = AnimationPlayableOutput.Create(
            _proceduralGraph, "GunDemo KINEMATION Output", _animator);
        _proceduralOutput.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        _proceduralOutput.SetSourcePlayable(_playable);
        RefreshLayerIndices();
        return true;
    }

    private void RefreshAnimatorState()
    {
        if (_layerController != _animator.runtimeAnimatorController)
        {
            RefreshLayerIndices();
        }

        float desiredGait = 0f;
        if (_moveCtrl && !_moveCtrl.IsVaulting && !_moveCtrl.IsSliding)
        {
            desiredGait = _moveCtrl.IsSprinting
                ? 3f
                : Mathf.Clamp01(_moveCtrl.MoveInput.magnitude);
        }

        _smoothGait = Mathf.Lerp(
            _smoothGait,
            desiredGait,
            KMath.ExpDecayAlpha(GaitSmoothing, Time.deltaTime));
        _animator.SetFloat(PlayerAnimatorContract.Gait, _smoothGait);
        float targetSprintWeight = _sprintActionOverrides || !_moveCtrl || !_moveCtrl.IsSprinting
            ? 0f
            : 1f;
        _sprintLayerWeight = _sprintActionOverrides
            ? 0f
            : Mathf.MoveTowards(_sprintLayerWeight, targetSprintWeight, Time.deltaTime / 0.14f);
        SetLayerWeight(_tacSprintLayerIndex, _sprintLayerWeight);

        float triggerWeight = _runtimeWeaponSettings &&
            _runtimeWeaponSettings.useSprintTriggerDiscipline
            ? _animator.GetFloat(PlayerAnimatorContract.TacSprintWeight)
            : 0f;
        SetLayerWeight(_triggerDisciplineLayerIndex, triggerWeight);
        SetLayerWeight(
            _rightHandLayerIndex,
            _animator.GetFloat(PlayerAnimatorContract.RightHandWeight));
    }

    private void RefreshLayerIndices()
    {
        _layerController = _animator.runtimeAnimatorController;
        _tacSprintLayerIndex = _animator.GetLayerIndex(
            PlayerAnimatorContract.TacSprintLayerName);
        _triggerDisciplineLayerIndex = _animator.GetLayerIndex(
            PlayerAnimatorContract.TriggerDisciplineLayerName);
        _rightHandLayerIndex = _animator.GetLayerIndex(
            PlayerAnimatorContract.RightHandLayerName);
    }

    private void HandleWeaponReady(WeaponDefinitionSO weaponData, Transform muzzlePoint)
    {
        ClearWeaponPose();
        SetFirstPersonRenderersHidden(true);
        ConfigureWeapon(weaponData);
    }

    private void HandleWeaponChanged(WeaponDefinitionSO weaponData)
    {
        // 预加载可在 WeaponChanged 的前一个监听中同步完成，不能把刚配置好的新枪再清空。
        if (!_hasWeaponConfigured || _configuredWeaponData != weaponData)
        {
            ClearWeaponPose();
        }
        _holsterWeight = 1f;
        _sprintActionOverrides = false;
    }

    private void ClearWeaponPose()
    {
        _weaponLoadVersion++;
        _akReloadRightArmSmoothingActive = false;
        ResetRightHandGripLock();
        StopActiveRecoil();
        if (_firstPersonView)
        {
            _firstPersonView.SetProceduralAutomaticFirePose(false);
        }
        if (_recoilAnimation)
        {
            _recoilAnimation.Init(null, 1f, FireMode.Semi);
        }
        _hasWeaponConfigured = false;
        _configuredWeaponData = null;
        _showWeaponNextFrame = false;
        _laggedCameraPosition = _cameraMotionCtrl
            ? _cameraMotionCtrl.CurrentMovementPositionOffset
            : Vector3.zero;
        _laggedCameraRotation = _cameraMotionCtrl
            ? _cameraMotionCtrl.CurrentMovementRotationOffset
            : Vector3.zero;
        SetFirstPersonRenderersHidden(true);
        if (!_isReady)
        {
            return;
        }

        _job.gunSettings = null;
        _job.aimPointTransform = null;
        _job.recoilAnimation = null;
        // Job 的有效配置标记也要同步清空，否则切枪帧会用空配置执行手臂 IK。
        // 这时可能正处于没有 KINEMATION 参数的默认控制器上，暂时跳过参数采样。
        Animator sampledAnimator = _job.animator;
        _job.animator = null;
        _job.Update();
        _job.animator = sampledAnimator;
        if (_playable.IsValid())
        {
            _playable.SetJobData(_job);
        }
    }

    private void ConfigureWeapon(WeaponDefinitionSO weaponData)
    {
        if (!_isReady || !weaponData || !_weaponViewCtrl.FirstPersonWeapon)
        {
            return;
        }

        int loadVersion = ++_weaponLoadVersion;
        _firstPersonView.SetProceduralAutomaticFirePose(
            weaponData.Fire.DefaultMode == WeaponFireMode.Automatic);
        ReleaseRecoilProfile();
        if (_runtimeWeaponSettings)
        {
            Destroy(_runtimeWeaponSettings);
        }
        _runtimeWeaponSettings = CreateRuntimeSettings(weaponData, null);
        ConfigureWeaponPose(weaponData, _weaponViewCtrl.FirstPersonWeapon.transform);
        if (!EnsureProceduralPlayable())
        {
            _isReady = false;
            return;
        }
        _job.recoilAnimation = _recoilAnimation;
        // 配置有效标记也在显示前更新，首帧不执行资源包的无配置默认旋转。
        _job.Update();
        _job.adsWeight = _aimCtrl ? _aimCtrl.AimProgress : 0f;
        _currentIkWeight = _ikWeight;
        _job.ikWeight = _currentIkWeight;
        _playable.SetJobData(_job);
        _hasWeaponConfigured = true;
        _configuredWeaponData = weaponData;
        _showWeaponFrame = Time.frameCount + 2;
        _showWeaponNextFrame = true;
        _ = LoadWeaponRecoilAsync(weaponData, loadVersion);
    }

    private async Task LoadWeaponRecoilAsync(WeaponDefinitionSO weaponData, int loadVersion)
    {
        string resourceId = weaponData.Animation.KinemationRecoilResourceId;
        RecoilAnimData recoilData;
        try
        {
            recoilData = await LoadRecoilAsync(resourceId);
        }
        catch (Exception exception)
        {
            if (this && isActiveAndEnabled && loadVersion == _weaponLoadVersion)
            {
                Debug.LogWarning($"{name} 的枪械后坐力资源加载失败：{exception.Message}", this);
            }
            return;
        }
        if (!this || !isActiveAndEnabled || loadVersion != _weaponLoadVersion)
        {
            ReleaseLoadedRecoil(resourceId, recoilData);
            return;
        }

        _recoilResourceId = resourceId;
        _runtimeWeaponSettings.recoilAnimData = recoilData;
        InitializeRecoil(weaponData, recoilData);
    }

    private FPSWeaponSettings CreateRuntimeSettings(
        WeaponDefinitionSO weaponData,
        RecoilAnimData recoilData)
    {
        FPSWeaponSettings settings = ScriptableObject.CreateInstance<FPSWeaponSettings>();
        settings.hideFlags = HideFlags.DontSave;
        settings.rotationOffset = weaponData.Ik.WeaponRotationOffset;
        settings.ikOffset = weaponData.Ik.WeaponPositionOffset;
        settings.leftClavicleOffset = weaponData.Ik.LeftClavicleOffset;
        settings.rightClavicleOffset = weaponData.Ik.RightClavicleOffset;
        settings.aimPointOffset = weaponData.Ik.AimPointOffset;
        settings.rightHandSprintOffset = weaponData.Ik.SprintRightHandRotationOffset;
        settings.adsBlend = weaponData.Ik.AdsBlend;
        settings.recoilAnimData = recoilData;
        settings.useSprintTriggerDiscipline =
            weaponData.Animation.UsesSprintTriggerDiscipline;
        return settings;
    }

    private void ConfigureWeaponPose(WeaponDefinitionSO weaponData, Transform weapon)
    {
        SampleWeaponIdlePose();
        if (_thirdPersonGripCtrl)
        {
            _thirdPersonGripCtrl.CacheNeutralGrip();
        }

        Transform aimPoint = FindOrCreateChild(weapon, "AimPoint");
        aimPoint.localPosition = weaponData.Ik.AimPointLocalPosition;
        aimPoint.localRotation = Quaternion.identity;

        KTransform weaponTransform = new KTransform(_weaponBone);
        _job.defaultRightHandPose = new KTransform(_rightArm.tip).GetRelativeTransform(
            weaponTransform,
            false);
        KTransform offsetWeaponTransform = weaponTransform;
        offsetWeaponTransform.rotation = KAnimationMath.RotateInSpace(
            offsetWeaponTransform,
            offsetWeaponTransform,
            _runtimeWeaponSettings.rotationOffset,
            1f);
        _job.lockedWeaponRelativeRightHandPose =
            offsetWeaponTransform.GetRelativeTransform(
                new KTransform(_rightArm.tip),
                false);
        KTransform root = new KTransform(_animator.transform);
        KTransform localWeapon = root.GetRelativeTransform(weaponTransform, false);
        localWeapon.rotation *= _runtimeWeaponSettings.rotationOffset;
        _job.additiveAdsPose.position = _localCameraPoint.position - localWeapon.position;
        _job.additiveAdsPose.rotation = Quaternion.Inverse(localWeapon.rotation);
        _job.aimPointTransform = aimPoint;
        _job.gunSettings = _runtimeWeaponSettings;
    }

    private void SampleWeaponIdlePose()
    {
        if (!_animator || !_animator.runtimeAnimatorController)
        {
            return;
        }
        // 只采样原始待机，避免上一把枪的程序 IK 再次修饰骨骼后被当作绑定姿势。
        if (_proceduralOutput.IsOutputValid())
        {
            _proceduralOutput.SetSourcePlayable(Playable.Null);
        }
        try
        {
            // 同源 OverrideController 会保留上一把枪的动作层和过渡。
            // 新枪首次显示前重置一次，避免旧检视/右手/疾跑姿态被采成握枪基准。
            _animator.Rebind();
            int idleState = Animator.StringToHash("Base Layer.Idle");
            if (_animator.HasState(0, idleState))
            {
                _animator.Play(idleState, 0, 0f);
            }
            _animator.Update(0f);
            // Animator.Update 会保留切枪时的过渡/换弹层姿势，不能用来计算永久的瞄准基准。
            // 与资源自带 Demo 一致，直接采样当前枪械的原始待机片段。
            AnimationClip[] clips = _animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip && clip.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    clip.SampleAnimation(_animator.gameObject, 0f);
                    return;
                }
            }
        }
        finally
        {
            if (_proceduralOutput.IsOutputValid() && _playable.IsValid())
            {
                _proceduralOutput.SetSourcePlayable(_playable);
            }
        }
    }

    private async Task<RecoilAnimData> LoadRecoilAsync(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        _resourceMgr = ResourceMgr.instance;
        return await _resourceMgr.LoadAsync<RecoilAnimData>(resourceId);
    }

    private void InitializeRecoil(WeaponDefinitionSO weaponData, RecoilAnimData recoilData)
    {
        if (!_recoilAnimation || !recoilData)
        {
            return;
        }

        float roundsPerMinute = Mathf.Max(1f, weaponData.Fire.AutomaticRpm);
        // 只让每颗真实 ShotFired 驱动一次后坐。插件的 Auto 模式会在第二发后启动
        // 与战斗/弹药状态脱钩的内部循环，造成枪和右手在停火、卡动作或空仓后继续抖。
        // Semi 脉冲会从当前平滑输出衔接，因此高射速下仍连续，但不会生成虚假后坐帧。
        _recoilAnimation.Init(recoilData, roundsPerMinute, FireMode.Semi);
        _isRecoilActive = false;
    }

    private void HandleShotFired(WeaponDefinitionSO weaponData)
    {
        if (_recoilAnimation)
        {
            _recoilAnimation.isAiming = _aimCtrl && _aimCtrl.IsAiming;
            _recoilAnimation.Play();
            _isRecoilActive = _recoilAnimation.RecoilData;
        }
    }

    private void RefreshRecoilPlayback()
    {
        if (!_isRecoilActive || !_recoilAnimation)
        {
            return;
        }

        bool keepAutomaticRecoilRunning =
            _recoilAnimation.fireMode == FireMode.Auto &&
            _weaponCombatCtrl &&
            _weaponCombatCtrl.IsFireHeld &&
            _weaponCombatCtrl.MagazineAmmo > 0;
        if (!keepAutomaticRecoilRunning)
        {
            StopActiveRecoil();
        }
    }

    private void StopActiveRecoil()
    {
        if (_recoilAnimation && _recoilAnimation.RecoilData)
        {
            _recoilAnimation.Stop();
        }
        _isRecoilActive = false;
    }

    private void UpdateRightHandGripLock()
    {
        WeaponActionDefinition action = _weaponActionCtrl
            ? _weaponActionCtrl.CurrentAction
            : null;
        bool authoredReloadHands = ShouldUseAuthoredReloadHands(action);
        bool shouldLock = _weaponCombatCtrl &&
            _weaponCombatCtrl.IsFireHeld &&
            _weaponCombatCtrl.MagazineAmmo > 0 &&
            !authoredReloadHands &&
            _currentIkWeight >= _ikWeight - 0.0001f;
        if (shouldLock && !_rightHandLockRequested && _weaponBone && _rightArm.tip)
        {
            // Update 读取的是上一帧已经完成程序动画后的骨骼。直接捕获此刻枪骨到右手的
            // 相对姿势，可以在按火首帧无缝接管，并且与 Job 中应用 rotationOffset 后的
            // weaponTransform 处于同一个坐标系。
            _job.lockedWeaponRelativeRightHandPose =
                new KTransform(_weaponBone).GetRelativeTransform(
                    new KTransform(_rightArm.tip),
                    false);
        }

        _rightHandLockRequested = shouldLock;
        if (shouldLock)
        {
            _rightHandLockWeight = 1f;
        }
        else
        {
            _rightHandLockWeight = Mathf.MoveTowards(
                _rightHandLockWeight,
                0f,
                Time.deltaTime / RightHandUnlockBlendDuration);
        }
        _job.rightHandLockWeight = _rightHandLockWeight;
    }

    private void ResetRightHandGripLock()
    {
        _rightHandLockRequested = false;
        _rightHandLockWeight = 0f;
        _job.rightHandLockWeight = 0f;
    }

    private void RefreshActionIkWeight()
    {
        WeaponActionDefinition action = _weaponActionCtrl
            ? _weaponActionCtrl.CurrentAction
            : null;
        if (ShouldUseAuthoredReloadHands(action))
        {
            // 换弹片段已经完整动画了枪、手腕和手肘。继续在同一帧用双骨 IK
            // 把手追到程序枪骨，会在枪离开握把、手臂接近伸展极限时产生解算翻转。
            // 用户允许换弹时轻微穿模，优先保留作者手部动作，不让两套系统争抢右手。
            _currentIkWeight = Mathf.MoveTowards(
                _currentIkWeight,
                0f,
                Time.deltaTime / ReloadIkDisableBlendDuration);
        }
        else
        {
            _currentIkWeight = Mathf.MoveTowards(
                _currentIkWeight,
                _ikWeight,
                Time.deltaTime / ReloadIkRestoreDuration);
        }
        _job.ikWeight = _currentIkWeight;
    }

    private void CacheRightFingerBindPose(Transform root)
    {
        _rightFingerBones = new Transform[RightFingerBoneNames.Length];
        _rightFingerBindPositions = new Vector3[RightFingerBoneNames.Length];
        _rightFingerBindRotations = new Quaternion[RightFingerBoneNames.Length];
        _rightFingerBindScales = new Vector3[RightFingerBoneNames.Length];
        for (int i = 0; i < RightFingerBoneNames.Length; i++)
        {
            Transform bone = FindChild(root, RightFingerBoneNames[i]);
            _rightFingerBones[i] = bone;
            _rightFingerBindPositions[i] = bone ? bone.localPosition : Vector3.zero;
            _rightFingerBindRotations[i] = bone ? bone.localRotation : Quaternion.identity;
            _rightFingerBindScales[i] = bone ? bone.localScale : Vector3.one;
        }
    }

    private void CacheRightArmBindLengths()
    {
        List<Transform> chain = new List<Transform>();
        Transform current = _rightArm.tip;
        while (current && current != _animator.transform)
        {
            chain.Add(current);
            current = current.parent;
        }
        _akReloadRightArmBones = chain.ToArray();
        _rightArmLengthBones = new[]
        {
            _rightArm.root,
            _rightArm.mid,
            _rightArm.tip
        };
        _rightArmBindPositions = new Vector3[_rightArmLengthBones.Length];
        _rightArmBindScales = new Vector3[_rightArmLengthBones.Length];
        for (int i = 0; i < _rightArmLengthBones.Length; i++)
        {
            Transform bone = _rightArmLengthBones[i];
            _rightArmBindPositions[i] = bone ? bone.localPosition : Vector3.zero;
            _rightArmBindScales[i] = bone ? bone.localScale : Vector3.one;
        }
    }

    private void ApplyAkReloadRightArmBoneLengthSafety(WeaponActionDefinition action)
    {
        if ((!ShouldUseAuthoredReloadHands(action) &&
                !_akReloadRightArmSmoothingActive) ||
            _rightArmBindPositions == null ||
            _rightArmBindScales == null)
        {
            return;
        }

        // The source AK clips key localPosition/localScale on the arm chain. Keep the
        // authored rotations, but never let reload curves stretch or squash the upper
        // arm, forearm, or wrist. A slightly looser grip is preferable to a visible
        // one-frame bone-length jump.
        Transform[] bones = _rightArmLengthBones ?? Array.Empty<Transform>();
        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];
            if (!bone)
            {
                continue;
            }
            bone.localPosition = _rightArmBindPositions[i];
            bone.localScale = _rightArmBindScales[i];
        }
    }

    private void BeginAkReloadRightArmRotationSmoothing()
    {
        Transform[] bones = GetAkReloadRightArmBones();
        _akReloadSmoothedRightArmPositions = new Vector3[bones.Length];
        _akReloadSmoothedRightArmRotations = new Quaternion[bones.Length];
        for (int i = 0; i < bones.Length; i++)
        {
            _akReloadSmoothedRightArmPositions[i] = bones[i]
                ? bones[i].localPosition
                : Vector3.zero;
            _akReloadSmoothedRightArmRotations[i] = bones[i]
                ? bones[i].localRotation
                : Quaternion.identity;
        }
        _akReloadRightArmSmoothingActive = true;
    }

    private void ApplyAkReloadRightArmRotationSmoothing(WeaponActionDefinition action)
    {
        bool authoredReload = ShouldUseAuthoredReloadHands(action);
        if (!_akReloadRightArmSmoothingActive)
        {
            if (authoredReload)
            {
                BeginAkReloadRightArmRotationSmoothing();
            }
            else
            {
                return;
            }
        }

        Transform[] bones = GetAkReloadRightArmBones();
        if (_akReloadSmoothedRightArmRotations == null ||
            _akReloadSmoothedRightArmRotations.Length != bones.Length)
        {
            BeginAkReloadRightArmRotationSmoothing();
        }

        float follow = 1f - Mathf.Exp(
            -Time.deltaTime / AkReloadRightArmSmoothingTime);
        float maximumRemainingDistance = 0f;
        float maximumRemainingAngle = 0f;
        for (int i = 0; i < bones.Length; i++)
        {
            Transform bone = bones[i];
            if (!bone)
            {
                continue;
            }

            Vector3 authoredPosition = bone.localPosition;
            Vector3 smoothedPosition = Vector3.Lerp(
                _akReloadSmoothedRightArmPositions[i],
                authoredPosition,
                follow);
            _akReloadSmoothedRightArmPositions[i] = smoothedPosition;
            bone.localPosition = smoothedPosition;
            maximumRemainingDistance = Mathf.Max(
                maximumRemainingDistance,
                Vector3.Distance(smoothedPosition, authoredPosition));

            Quaternion authoredRotation = bone.localRotation;
            Quaternion smoothedRotation = Quaternion.Slerp(
                _akReloadSmoothedRightArmRotations[i],
                authoredRotation,
                follow);
            _akReloadSmoothedRightArmRotations[i] = smoothedRotation;
            bone.localRotation = smoothedRotation;
            maximumRemainingAngle = Mathf.Max(
                maximumRemainingAngle,
                Quaternion.Angle(smoothedRotation, authoredRotation));
        }

        // Animator returns to idle through a 0.14 second cross-fade. Keep filtering that
        // release until the visible chain has converged so the fix cannot create a new
        // snap on the first frame after CurrentAction becomes null.
        if (!authoredReload &&
            maximumRemainingDistance <= 0.00025f &&
            maximumRemainingAngle <= 0.25f)
        {
            _akReloadRightArmSmoothingActive = false;
        }
    }

    private Transform[] GetAkReloadRightArmBones()
    {
        return _akReloadRightArmBones ?? Array.Empty<Transform>();
    }

    private void ApplyFirstPersonFingerSafety()
    {
        if (!_hasWeaponConfigured || !_characterModelCtrl ||
            string.Equals(_characterModelCtrl.CurrentModelId, DefaultCharacterModelId,
                StringComparison.Ordinal) || _rightFingerBones == null)
        {
            return;
        }

        for (int i = 0; i < _rightFingerBones.Length; i++)
        {
            Transform bone = _rightFingerBones[i];
            if (!bone)
            {
                continue;
            }

            int jointIndex = i % 3;
            Quaternion delta = Quaternion.Inverse(_rightFingerBindRotations[i]) *
                bone.localRotation;
            // 不能统一缩小所有动画旋转：那会把原本卷在握把上的指节拉直，视觉上
            // 反而变成长手指。保留正常卷曲，只严格限制沿指骨 X 轴的扭转和极端 swing。
            Quaternion safeDelta = ClampSwingTwist(
                delta,
                Vector3.right,
                FingerSwingLimits[jointIndex],
                FingerTwistLimits[jointIndex]);
            if (i >= 3)
            {
                // 非安比手网格更长；食指到小指至少保持轻微卷曲。用户明确接受
                // 握松或少量穿模，优先避免换弹/拉栓时指节拉成细长直线。
                Vector3 euler = safeDelta.eulerAngles;
                float curl = Mathf.DeltaAngle(0f, euler.z);
                if (curl > -FingerMinimumCurl[jointIndex])
                {
                    euler.x = Mathf.DeltaAngle(0f, euler.x);
                    euler.y = Mathf.DeltaAngle(0f, euler.y);
                    euler.z = -FingerMinimumCurl[jointIndex];
                    safeDelta = ClampSwingTwist(
                        Quaternion.Euler(euler),
                        Vector3.right,
                        FingerSwingLimits[jointIndex],
                        FingerTwistLimits[jointIndex]);
                }
            }
            bone.localRotation = _rightFingerBindRotations[i] * safeDelta;
            bone.localPosition = _rightFingerBindPositions[i];
            bone.localScale = _rightFingerBindScales[i];
        }
    }

    private static Quaternion ClampSwingTwist(
        Quaternion rotation,
        Vector3 twistAxis,
        float swingLimit,
        float twistLimit)
    {
        Vector3 rotationVector = new Vector3(rotation.x, rotation.y, rotation.z);
        Vector3 projected = Vector3.Project(rotationVector, twistAxis);
        Quaternion twist = new Quaternion(projected.x, projected.y, projected.z, rotation.w);
        float twistMagnitude = Mathf.Sqrt(
            twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
        twist = twistMagnitude > 0.00001f
            ? new Quaternion(
                twist.x / twistMagnitude,
                twist.y / twistMagnitude,
                twist.z / twistMagnitude,
                twist.w / twistMagnitude)
            : Quaternion.identity;

        Quaternion swing = rotation * Quaternion.Inverse(twist);
        swing.ToAngleAxis(out float swingAngle, out Vector3 swingAxis);
        if (swingAngle > 180f)
        {
            swingAngle = 360f - swingAngle;
            swingAxis = -swingAxis;
        }
        Quaternion clampedSwing = swingAngle > swingLimit && swingAxis.sqrMagnitude > 0.00001f
            ? Quaternion.AngleAxis(swingLimit, swingAxis.normalized)
            : swing;

        float signedTwist = 2f * Mathf.Atan2(
            Vector3.Dot(new Vector3(twist.x, twist.y, twist.z), twistAxis),
            twist.w) * Mathf.Rad2Deg;
        signedTwist = Mathf.DeltaAngle(0f, signedTwist);
        Quaternion clampedTwist = Quaternion.AngleAxis(
            Mathf.Clamp(signedTwist, -twistLimit, twistLimit),
            twistAxis);
        return clampedSwing * clampedTwist;
    }

    private void HandleActionStarted(
        WeaponActionDefinition action,
        bool startedWhileAiming)
    {
        _sprintActionOverrides = action != null &&
            (IsReloadAction(action.ActionType) || action.ActionType == WeaponActionType.Inspect);
        if (ShouldUseAuthoredReloadHands(action))
        {
            // 换弹提交弹药时，IsFireHeld 可能仍为 true。先丢弃连射锁定，避免在手已
            // 离开握把的换弹姿势上重新捕获目标；IK 权重会在 Update 中与 0.05 秒
            // Reload CrossFade 同步退出，避免首帧硬切造成腕部跳变。
            ResetRightHandGripLock();
            BeginAkReloadRightArmRotationSmoothing();
        }
        if (_sprintActionOverrides)
        {
            _sprintLayerWeight = 0f;
            SetLayerWeight(_tacSprintLayerIndex, 0f);
        }
        if (action != null && action.ActionType == WeaponActionType.Equip)
        {
            _equipImpactPlayed = false;
            _holsterWeight = 1f;
        }
        if (_firstPersonView)
        {
            _firstPersonView.StartWeaponAction(action, startedWhileAiming);
        }
    }

    private void HandleActionCompleted(WeaponActionType actionType)
    {
        if (_weaponActionCtrl && _weaponActionCtrl.CurrentAction != null)
        {
            return;
        }

        if (actionType == WeaponActionType.Holster)
        {
            _holsterWeight = 1f;
            SetFirstPersonRenderersHidden(true);
        }
        else if (actionType == WeaponActionType.Equip)
        {
            PlayEquipImpact();
            _holsterWeight = 0f;
        }
        if (IsReloadAction(actionType) || actionType == WeaponActionType.Inspect)
        {
            _sprintActionOverrides = false;
        }
        if (_firstPersonView)
        {
            _firstPersonView.CompleteWeaponAction(actionType);
        }
    }

    private void HandleActionCanceled(
        WeaponActionType actionType,
        WeaponActionCancelReason cancelReason)
    {
        if (IsReloadAction(actionType) || actionType == WeaponActionType.Inspect)
        {
            _sprintActionOverrides = false;
        }
        if (actionType == WeaponActionType.Equip ||
            actionType == WeaponActionType.Holster)
        {
            _holsterWeight = 0f;
        }
        if (_firstPersonView)
        {
            _firstPersonView.CancelWeaponAction(actionType);
        }
    }

    private void SetLayerWeight(int layerIndex, float weight)
    {
        if (layerIndex >= 0)
        {
            _animator.SetLayerWeight(layerIndex, Mathf.Clamp01(weight));
        }
    }

    private static bool IsReloadAction(WeaponActionType actionType)
    {
        return actionType == WeaponActionType.Reload ||
            actionType == WeaponActionType.EmptyReload ||
            actionType == WeaponActionType.PerRoundStart ||
            actionType == WeaponActionType.PerRoundInsert ||
            actionType == WeaponActionType.PerRoundEnd;
    }

    private bool ShouldUseAuthoredReloadHands(WeaponActionDefinition action)
    {
        return action != null &&
            IsReloadAction(action.ActionType) &&
            _weaponActionCtrl &&
            _weaponActionCtrl.CurrentWeaponData &&
            string.Equals(
                _weaponActionCtrl.CurrentWeaponData.Basic.WeaponId,
                "ak",
                StringComparison.Ordinal);
    }

    private void PlayEquipImpact()
    {
        if (_equipImpactPlayed)
        {
            return;
        }

        _equipImpactPlayed = true;
        if (_cameraMotionCtrl)
        {
            _cameraMotionCtrl.AddImpulse(
                new Vector3(0f, -0.016f, -0.018f),
                new Vector3(0.8f, 0f, 0.3f),
                0.035f,
                0.14f);
        }
    }

    private float EvaluateEquipWeight(float progress)
    {
        if (progress < 0.72f)
        {
            float body = progress / 0.72f;
            float remaining = 1f - body;
            return 1f - 1.08f * (1f - remaining * remaining * remaining);
        }

        float settle = (progress - 0.72f) / 0.28f;
        return -0.08f * (1f - Mathf.SmoothStep(0f, 1f, settle));
    }

    private float EvaluateHolsterWeight(float progress)
    {
        if (progress < 0.8f)
        {
            float body = progress / 0.8f;
            float remaining = 1f - body;
            return 1.08f * (1f - remaining * remaining);
        }

        float settle = (progress - 0.8f) / 0.2f;
        return 1f + 0.08f * (1f - Mathf.SmoothStep(0f, 1f, settle));
    }

    private void SetFirstPersonRenderersHidden(bool hidden)
    {
        if (!_animator)
        {
            return;
        }

        hidden = hidden || (_cameraCtrl && _cameraCtrl.IsThirdPerson);

        Renderer[] renderers = _animator.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].forceRenderingOff = hidden;
        }

        GameObject weapon = _weaponViewCtrl ? _weaponViewCtrl.FirstPersonWeapon : null;
        if (!weapon)
        {
            return;
        }

        Renderer[] weaponRenderers = weapon.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < weaponRenderers.Length; i++)
        {
            weaponRenderers[i].forceRenderingOff = hidden;
        }
    }

    private void HandleViewChanged(bool isThirdPerson)
    {
        SetFirstPersonRenderersHidden(
            isThirdPerson || !_hasWeaponConfigured || _showWeaponNextFrame ||
            (_weaponActionCtrl && _weaponActionCtrl.CurrentAction != null &&
             _weaponActionCtrl.CurrentAction.ActionType == WeaponActionType.Holster));
    }

    private void ReleaseRecoilProfile()
    {
        if (_resourceMgr && !string.IsNullOrWhiteSpace(_recoilResourceId))
        {
            _resourceMgr.Release<RecoilAnimData>(_recoilResourceId);
        }
        _recoilResourceId = string.Empty;
    }

    private void ReleaseLoadedRecoil(string resourceId, RecoilAnimData recoilData)
    {
        if (_resourceMgr && recoilData && !string.IsNullOrWhiteSpace(resourceId))
        {
            _resourceMgr.Release<RecoilAnimData>(resourceId);
        }
    }

    private static IKTransforms CreateArm(
        Transform root,
        string tipName,
        string midName,
        string rootName)
    {
        return new IKTransforms
        {
            tip = FindChild(root, tipName),
            mid = FindChild(root, midName),
            root = FindChild(root, rootName)
        };
    }

    private static Transform FindOrCreateChild(Transform root, string childName)
    {
        Transform child = FindChild(root, childName);
        if (child || !root)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        child = childObject.transform;
        child.SetParent(root, false);
        return child;
    }

    private static Transform FindChild(Transform root, string childName)
    {
        if (!root)
        {
            return null;
        }
        if (string.Equals(root.name, childName, StringComparison.OrdinalIgnoreCase))
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindChild(root.GetChild(i), childName);
            if (found)
            {
                return found;
            }
        }

        return null;
    }
}
