using System;
using System.Threading.Tasks;
using KINEMATION.FPSAnimationPack.Scripts.Player;
using KINEMATION.FPSAnimationPack.Scripts.Weapon;
using KINEMATION.KAnimationCore.Runtime.Core;
using KINEMATION.ProceduralRecoilAnimationSystem.Runtime;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Experimental.Animations;
using UnityEngine.Playables;

[DisallowMultipleComponent]
public sealed class PlayerKinemationViewCtrl : MonoBehaviour
{
    private const float GaitSmoothing = 8f;

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
    private Animator _animator;
    private RecoilAnimation _recoilAnimation;
    private RuntimeAnimatorController _layerController;
    private FPSWeaponSettings _runtimeWeaponSettings;
    private FPSProceduralJob _job;
    private AnimationScriptPlayable _playable;
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
    private bool _hasWeaponConfigured;
    private bool _isReloading;
    private Vector3 _restLocalPosition;
    private Quaternion _restLocalRotation;
    private Vector3 _laggedCameraPosition;
    private Vector3 _laggedCameraRotation;
    private float _holsterWeight;
    private bool _equipImpactPlayed;
    private bool _showWeaponNextFrame;
    private int _showWeaponFrame;

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
    }

    private void Start()
    {
        InitializeKinemation();
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
        if (!_isReady || !_hasWeaponConfigured)
        {
            return;
        }

        RefreshAnimatorState();
        _job.Update();
        _job.adsWeight = _aimCtrl ? _aimCtrl.AimProgress : 0f;
        _job.ikWeight = _ikWeight;
        _playable.SetJobData(_job);
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
            ? _cameraMotionCtrl.CurrentPositionOffset
            : Vector3.zero;
        Vector3 motionRotation = _cameraMotionCtrl
            ? _cameraMotionCtrl.CurrentRotationOffset
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

        if (_showWeaponNextFrame && Time.frameCount >= _showWeaponFrame)
        {
            SetFirstPersonRenderersHidden(false);
            _showWeaponNextFrame = false;
        }
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

        _weaponLoadVersion++;
        ReleaseRecoilProfile();
        if (_animator)
        {
            _animator.transform.localPosition = _restLocalPosition;
            _animator.transform.localRotation = _restLocalRotation;
        }
        SetFirstPersonRenderersHidden(true);
        if (_runtimeWeaponSettings)
        {
            Destroy(_runtimeWeaponSettings);
            _runtimeWeaponSettings = null;
        }
    }

    private void CacheProjectComponents()
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
            Debug.LogError($"{name} 无法取得 Animator PlayableGraph。", this);
            enabled = false;
            return;
        }

        _recoilAnimation = _animator.GetComponent<RecoilAnimation>();
        if (!_recoilAnimation)
        {
            _recoilAnimation = _animator.gameObject.AddComponent<RecoilAnimation>();
        }

        _job.recoilAnimation = _recoilAnimation;
        _job.Setup();
        _playable = AnimationScriptPlayable.Create(_animator.playableGraph, _job);
        AnimationPlayableOutput output = AnimationPlayableOutput.Create(
            _animator.playableGraph,
            "GunDemo KINEMATION Output",
            _animator);
        output.SetAnimationStreamSource(AnimationStreamSource.PreviousInputs);
        output.SetSourcePlayable(_playable);

        RefreshLayerIndices();
        KTransform root = new KTransform(_animator.transform);
        _localCameraPoint = root.GetRelativeTransform(new KTransform(_cameraPoint), false);
        _isReady = true;
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
        float targetSprintWeight = _isReloading ? 0f : Mathf.Clamp01(_smoothGait - 2f);
        _sprintLayerWeight = _isReloading
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
        ClearWeaponPose();
        _holsterWeight = 1f;
        _isReloading = false;
    }

    private void ClearWeaponPose()
    {
        _weaponLoadVersion++;
        if (_recoilAnimation)
        {
            _recoilAnimation.Init(null, 1f, FireMode.Semi);
        }
        _hasWeaponConfigured = false;
        _showWeaponNextFrame = false;
        _laggedCameraPosition = _cameraMotionCtrl
            ? _cameraMotionCtrl.CurrentPositionOffset
            : Vector3.zero;
        _laggedCameraRotation = _cameraMotionCtrl
            ? _cameraMotionCtrl.CurrentRotationOffset
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
        _playable.SetJobData(_job);
    }

    private void ConfigureWeapon(WeaponDefinitionSO weaponData)
    {
        if (!_isReady || !weaponData || !_weaponViewCtrl.FirstPersonWeapon)
        {
            return;
        }

        int loadVersion = ++_weaponLoadVersion;
        ReleaseRecoilProfile();
        if (_runtimeWeaponSettings)
        {
            Destroy(_runtimeWeaponSettings);
        }
        _runtimeWeaponSettings = CreateRuntimeSettings(weaponData, null);
        ConfigureWeaponPose(weaponData, _weaponViewCtrl.FirstPersonWeapon.transform);
        _job.recoilAnimation = _recoilAnimation;
        _playable.SetJobData(_job);
        _hasWeaponConfigured = true;
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

        Transform aimPoint = FindOrCreateChild(weapon, "AimPoint");
        aimPoint.localPosition = weaponData.Ik.AimPointLocalPosition;
        aimPoint.localRotation = Quaternion.identity;

        KTransform weaponTransform = new KTransform(_weaponBone);
        _job.defaultRightHandPose = new KTransform(_rightArm.tip).GetRelativeTransform(
            weaponTransform,
            false);
        KTransform root = new KTransform(_animator.transform);
        KTransform localWeapon = root.GetRelativeTransform(weaponTransform, false);
        localWeapon.rotation *= _runtimeWeaponSettings.rotationOffset;
        _job.additiveAdsPose.position = _localCameraPoint.position - localWeapon.position;
        _job.additiveAdsPose.rotation = Quaternion.Inverse(localWeapon.rotation);
        _job.aimPointTransform = aimPoint;
        _job.gunSettings = _runtimeWeaponSettings;
        _playable.SetJobData(_job);
    }

    private void SampleWeaponIdlePose()
    {
        if (!_animator || !_animator.runtimeAnimatorController)
        {
            return;
        }

        AnimationClip[] clips = _animator.runtimeAnimatorController.animationClips;
        AnimationClip idlePose = null;
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (!clip)
            {
                continue;
            }
            if (!clip.name.Contains("Idle") && !clip.name.Contains("Pose"))
            {
                continue;
            }

            idlePose = clip;
            break;
        }

        if (idlePose)
        {
            idlePose.SampleAnimation(_animator.gameObject, 0f);
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
        FireMode fireMode = weaponData.Fire.DefaultMode == WeaponFireMode.Automatic
            ? FireMode.Auto
            : FireMode.Semi;
        _recoilAnimation.Init(recoilData, roundsPerMinute, fireMode);
    }

    private void HandleShotFired(WeaponDefinitionSO weaponData)
    {
        if (_recoilAnimation)
        {
            _recoilAnimation.isAiming = _aimCtrl && _aimCtrl.IsAiming;
            _recoilAnimation.Play();
        }
    }

    private void HandleActionStarted(
        WeaponActionDefinition action,
        bool startedWhileAiming)
    {
        _isReloading = action != null && IsReloadAction(action.ActionType);
        if (_isReloading)
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
        if (IsReloadAction(actionType))
        {
            _isReloading = false;
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
        if (IsReloadAction(actionType))
        {
            _isReloading = false;
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
