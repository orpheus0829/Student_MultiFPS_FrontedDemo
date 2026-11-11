using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerWeaponViewCtrl : MonoBehaviour
{
    private const string StagingRootName = "武器加载暂存";

    [Header("唯一枪械来源")]
    [SerializeField, InspectorName("枪械动作")] private WeaponActionCtrl _weaponActionCtrl;
    [SerializeField, InspectorName("玩家瞄准")] private PlayerAimCtrl _aimCtrl;

    [Header("双视角表现")]
    [SerializeField, InspectorName("第一人称表现")] private PlayerAnimationView _firstPersonView;
    [SerializeField, InspectorName("第三人称表现")] private PlayerAnimationView _thirdPersonView;
    [SerializeField, InspectorName("第三人称右手握枪位置")]
    private Vector3 _thirdPersonGripPosition = new Vector3(0f, 0f, 0f);
    [SerializeField, InspectorName("第三人称右手握枪旋转")]
    private Vector3 _thirdPersonGripRotation = Vector3.zero;

    private ResourceMgr _resourceMgr;
    private Transform _firstPersonPoseRoot;
    private Transform _firstPersonStagingRoot;
    private Transform _thirdPersonStagingRoot;
    private GameObject _firstPersonWeapon;
    private GameObject _thirdPersonWeapon;
    private RuntimeAnimatorController _firstPersonCharacterController;
    private RuntimeAnimatorController _defaultFirstPersonCharacterController;
    private string _firstPersonCharacterControllerResourceId;
    private Transform _muzzlePoint;
    private Transform _thirdPersonMuzzlePoint;
    private int _loadVersion;
    private int _prepareVersion;
    private string _preparedFirstPersonId;
    private string _preparedThirdPersonId;
    private string _preparedControllerId;
    private WeaponDefinitionSO _preparedWeaponData;
    private GameObject _preparedFirstPersonWeapon;
    private GameObject _preparedThirdPersonWeapon;
    private Quaternion _defaultHandFrame;
    private bool _hasDefaultHandFrame;
    private PlayerInputCtrl _localInputCtrl;
    private readonly List<Renderer> _firstPersonRenderers = new List<Renderer>();
    private bool _hideFirstPersonPresentation;

    public event Action<WeaponDefinitionSO, Transform> FirstPersonWeaponReady;
    public event Action<WeaponDefinitionSO, Transform> ThirdPersonWeaponReady;

    public Transform MuzzlePoint
    {
        get
        {
            return _muzzlePoint;
        }
    }

    public Transform ThirdPersonMuzzlePoint
    {
        get
        {
            return _thirdPersonMuzzlePoint;
        }
    }

    public GameObject FirstPersonWeapon
    {
        get
        {
            return _firstPersonWeapon;
        }
    }

    public GameObject ThirdPersonWeapon
    {
        get
        {
            return _thirdPersonWeapon;
        }
    }

    private void Awake()
    {
        if (!_weaponActionCtrl)
        {
            TryGetComponent(out _weaponActionCtrl);
        }
        if (!_aimCtrl)
        {
            TryGetComponent(out _aimCtrl);
        }
        RefreshOwnerVisibilityPolicy();

        if (_firstPersonView && _firstPersonView.CharacterAnimator)
        {
            _firstPersonPoseRoot = _firstPersonView.CharacterAnimator.transform;
            _defaultFirstPersonCharacterController =
                _firstPersonView.CharacterAnimator.runtimeAnimatorController;
            PrepareFirstPersonSkinnedRenderers();
        }
        if (_firstPersonView && _firstPersonView.WeaponRoot)
        {
            _firstPersonStagingRoot = GetOrCreateStagingRoot(_firstPersonView.WeaponRoot);
        }
        if (_thirdPersonView && _thirdPersonView.WeaponRoot)
        {
            AttachThirdPersonWeaponRootToHand();
            _thirdPersonStagingRoot = GetOrCreateStagingRoot(_thirdPersonView.WeaponRoot);
        }

        if (!_weaponActionCtrl || !_aimCtrl || !_firstPersonView || !_thirdPersonView ||
            !_firstPersonPoseRoot)
        {
            Debug.LogError($"{name} 的双视角枪械表现引用未配置完整。", this);
            enabled = false;
        }

        RefreshFirstPersonRendererCache();
    }

    private void OnEnable()
    {
        // Unity 在 Play Mode 脚本热重载后只重新调用 OnEnable，非序列化字段会丢失，
        // 但旧的暂存根仍在场景中。重新接管它，避免再创建一套根或让加载中的枪直接露出。
        RefreshOwnerVisibilityPolicy();
        if (!_firstPersonStagingRoot && _firstPersonView && _firstPersonView.WeaponRoot)
        {
            _firstPersonStagingRoot = GetOrCreateStagingRoot(_firstPersonView.WeaponRoot);
        }
        if (!_thirdPersonStagingRoot && _thirdPersonView && _thirdPersonView.WeaponRoot)
        {
            _thirdPersonStagingRoot = GetOrCreateStagingRoot(_thirdPersonView.WeaponRoot);
        }
        RefreshFirstPersonRendererCache();

        if (!_weaponActionCtrl)
        {
            return;
        }

        _weaponActionCtrl.WeaponChanged += ReceiveWeaponChanged;
        ReceiveWeaponChanged(_weaponActionCtrl.CurrentWeaponData);
    }

    private void OnDisable()
    {
        if (_weaponActionCtrl)
        {
            _weaponActionCtrl.WeaponChanged -= ReceiveWeaponChanged;
        }

        _loadVersion++;
        _prepareVersion++;
        ReleasePreparedViews();
        ReleaseWeaponViews();
    }

    private void LateUpdate()
    {
        // AI 与其他非本地角色仍需要第一人称 Animator 提供握枪采样，不能停掉骨架；
        // 但它们的手臂/枪绝不能被本地玩家的第一人称叠加相机（Layer 9）画出来。
        if (_hideFirstPersonPresentation)
        {
            // 枪口火花等 Renderer 会在 WeaponReady 之后继续异步生成；非分配式重扫可确保
            // 后生成的任何第一人称表现也不会短暂出现在本地玩家的叠加相机里。
            RefreshFirstPersonRendererCache();
        }
    }

    public void RefreshThirdPersonWeaponGrip()
    {
        if (_thirdPersonView && _thirdPersonView.WeaponRoot)
        {
            AttachThirdPersonWeaponRootToHand();
        }
    }

    public async Task<bool> PrepareWeaponViewsAsync(WeaponDefinitionSO weaponData)
    {
        if (!weaponData)
        {
            return false;
        }

        int prepareVersion = ++_prepareVersion;
        ReleasePreparedViews();
        _resourceMgr = ResourceMgr.instance;
        string firstPersonId = weaponData.Resources.FirstPersonPrefabResourceId;
        string thirdPersonId = weaponData.Resources.WorldModelPrefabResourceId;
        string controllerId = weaponData.Animation.CharacterAnimatorControllerResourceId;
        Task<GameObject> firstPersonTask = null;
        Task<GameObject> thirdPersonTask = null;
        Task<RuntimeAnimatorController> controllerTask = null;
        Task<GameObject> stagedFirstPersonTask = null;
        Task<GameObject> stagedThirdPersonTask = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(firstPersonId))
            {
                firstPersonTask = _resourceMgr.LoadAsync<GameObject>(firstPersonId);
            }
            if (!string.IsNullOrWhiteSpace(thirdPersonId))
            {
                thirdPersonTask = _resourceMgr.LoadAsync<GameObject>(thirdPersonId);
            }
            if (!string.IsNullOrWhiteSpace(controllerId))
            {
                controllerTask = _resourceMgr.LoadAsync<RuntimeAnimatorController>(controllerId);
            }

            await Task.WhenAll(
                firstPersonTask ?? Task.FromResult<GameObject>(null),
                thirdPersonTask ?? Task.FromResult<GameObject>(null),
                controllerTask ?? Task.FromResult<RuntimeAnimatorController>(null));
            if (!this || !isActiveAndEnabled || prepareVersion != _prepareVersion ||
                (firstPersonTask != null && !firstPersonTask.Result) ||
                (thirdPersonTask != null && !thirdPersonTask.Result) ||
                (controllerTask != null && !controllerTask.Result))
            {
                ReleaseCompletedPreparation(firstPersonId, firstPersonTask);
                ReleaseCompletedPreparation(thirdPersonId, thirdPersonTask);
                ReleaseCompletedPreparation(controllerId, controllerTask);
                return false;
            }

            _preparedFirstPersonId = firstPersonId;
            _preparedThirdPersonId = thirdPersonId;
            _preparedControllerId = controllerId;
            stagedFirstPersonTask = InstantiateWeaponAsync(
                firstPersonId, _firstPersonStagingRoot, weaponData.Ik, true);
            stagedThirdPersonTask = InstantiateWeaponAsync(
                thirdPersonId, _thirdPersonStagingRoot, weaponData.Ik, false);
            await Task.WhenAll(stagedFirstPersonTask, stagedThirdPersonTask);
            if (!this || !isActiveAndEnabled || prepareVersion != _prepareVersion ||
                !stagedFirstPersonTask.Result || !stagedThirdPersonTask.Result)
            {
                GameObject discardedFirst = stagedFirstPersonTask.Result;
                GameObject discardedThird = stagedThirdPersonTask.Result;
                ReleaseInstance(ref discardedFirst);
                ReleaseInstance(ref discardedThird);
                ReleasePreparedViews();
                return false;
            }

            _preparedWeaponData = weaponData;
            _preparedFirstPersonWeapon = stagedFirstPersonTask.Result;
            _preparedThirdPersonWeapon = stagedThirdPersonTask.Result;
            return true;
        }
        catch (Exception exception)
        {
            if (!string.IsNullOrWhiteSpace(_preparedFirstPersonId) ||
                !string.IsNullOrWhiteSpace(_preparedThirdPersonId) ||
                !string.IsNullOrWhiteSpace(_preparedControllerId))
            {
                ReleasePreparedViews();
            }
            else
            {
                ReleaseCompletedPreparation(firstPersonId, firstPersonTask);
                ReleaseCompletedPreparation(thirdPersonId, thirdPersonTask);
                ReleaseCompletedPreparation(controllerId, controllerTask);
            }
            if (stagedFirstPersonTask != null &&
                stagedFirstPersonTask.Status == TaskStatus.RanToCompletion)
            {
                GameObject discardedFirst = stagedFirstPersonTask.Result;
                ReleaseInstance(ref discardedFirst);
            }
            if (stagedThirdPersonTask != null &&
                stagedThirdPersonTask.Status == TaskStatus.RanToCompletion)
            {
                GameObject discardedThird = stagedThirdPersonTask.Result;
                ReleaseInstance(ref discardedThird);
            }
            if (this && isActiveAndEnabled && prepareVersion == _prepareVersion)
            {
                Debug.LogWarning($"{name} 预载双视角枪械表现失败：{exception.Message}", this);
            }
            return false;
        }
    }

    private void ReceiveWeaponChanged(WeaponDefinitionSO weaponData)
    {
        _ = RefreshWeaponViewsAsync(weaponData);
    }

    private async Task RefreshWeaponViewsAsync(WeaponDefinitionSO weaponData)
    {
        int loadVersion = ++_loadVersion;
        ReleaseWeaponViews(false);
        RemoveUnownedWeaponChildren(_firstPersonView.WeaponRoot, _firstPersonStagingRoot);
        RemoveUnownedWeaponChildren(_thirdPersonView.WeaponRoot, _thirdPersonStagingRoot);
        if (!weaponData)
        {
            return;
        }

        _resourceMgr = ResourceMgr.instance;
        GameObject firstPersonWeapon = null;
        GameObject thirdPersonWeapon = null;
        Task<GameObject> firstPersonTask = null;
        Task<GameObject> thirdPersonTask = null;
        bool adoptedInstances = false;
        try
        {
            string firstPersonId = weaponData.Resources.FirstPersonPrefabResourceId;
            string thirdPersonId = weaponData.Resources.WorldModelPrefabResourceId;
            Task controllerTask = LoadFirstPersonCharacterControllerAsync(weaponData, loadVersion);
            bool usePrepared = _preparedWeaponData == weaponData &&
                _preparedFirstPersonWeapon && _preparedThirdPersonWeapon;
            firstPersonTask = usePrepared
                ? Task.FromResult(_preparedFirstPersonWeapon)
                : InstantiateWeaponAsync(
                    firstPersonId, _firstPersonView.WeaponRoot, weaponData.Ik, true);
            thirdPersonTask = usePrepared
                ? Task.FromResult(_preparedThirdPersonWeapon)
                : InstantiateWeaponAsync(
                    thirdPersonId, _thirdPersonView.WeaponRoot, weaponData.Ik, false);
            await Task.WhenAll(controllerTask, firstPersonTask, thirdPersonTask);
            firstPersonWeapon = firstPersonTask.Result;
            thirdPersonWeapon = thirdPersonTask.Result;
            if (usePrepared)
            {
                _preparedFirstPersonWeapon = null;
                _preparedThirdPersonWeapon = null;
            }
            if (!IsLoadCurrent(loadVersion))
            {
                return;
            }

            // 实例在所有资源（含手臂 AnimatorController）就绪前始终留在禁用的暂存根下。
            // 这样异步加载被取消、脚本热重载或连续切枪时，不会先把一把失去所有权的 AK
            // 暴露到正式挂点，随后再与真正选中的枪重合。
            firstPersonWeapon.transform.SetParent(_firstPersonView.WeaponRoot, false);
            thirdPersonWeapon.transform.SetParent(_thirdPersonView.WeaponRoot, false);

            _firstPersonWeapon = firstPersonWeapon;
            _thirdPersonWeapon = thirdPersonWeapon;
            firstPersonWeapon = null;
            thirdPersonWeapon = null;
            adoptedInstances = true;
            Animator firstPersonAnimator = GetWeaponAnimator(_firstPersonWeapon);
            Animator thirdPersonAnimator = GetWeaponAnimator(_thirdPersonWeapon);
            InitializeWeaponIdle(firstPersonAnimator);
            InitializeWeaponIdle(thirdPersonAnimator);
            _firstPersonView.SetWeaponAnimator(firstPersonAnimator);
            _thirdPersonView.SetWeaponAnimator(thirdPersonAnimator);
            RefreshFirstPersonRendererCache();
            _muzzlePoint = ResolveWeaponPoint(_firstPersonWeapon, weaponData.Ik.MuzzlePointPath);
            _thirdPersonMuzzlePoint = ResolveWeaponPoint(
                _thirdPersonWeapon, weaponData.Ik.MuzzlePointPath);
            FirstPersonWeaponReady?.Invoke(weaponData, _muzzlePoint);
            ThirdPersonWeaponReady?.Invoke(weaponData, _thirdPersonMuzzlePoint);
            if (_hideFirstPersonPresentation)
            {
                SetFirstPersonRenderersHidden(true);
            }
            _weaponActionCtrl.TryStartAction(WeaponActionType.Equip);
        }
        catch (Exception exception)
        {
            if (IsLoadCurrent(loadVersion))
            {
                Debug.LogWarning($"{name} 加载双视角枪械表现失败：{exception.Message}", this);
                ReleaseWeaponViews();
            }
        }
        finally
        {
            if (!adoptedInstances)
            {
                if (!firstPersonWeapon && firstPersonTask != null &&
                    firstPersonTask.Status == TaskStatus.RanToCompletion)
                {
                    firstPersonWeapon = firstPersonTask.Result;
                }
                if (!thirdPersonWeapon && thirdPersonTask != null &&
                    thirdPersonTask.Status == TaskStatus.RanToCompletion)
                {
                    thirdPersonWeapon = thirdPersonTask.Result;
                }
            }
            ReleaseInstance(ref firstPersonWeapon);
            ReleaseInstance(ref thirdPersonWeapon);
            ReleasePreparedViews();
        }
    }

    private void ReleasePreparedViews()
    {
        ReleaseInstance(ref _preparedFirstPersonWeapon);
        ReleaseInstance(ref _preparedThirdPersonWeapon);
        if (_resourceMgr)
        {
            if (!string.IsNullOrWhiteSpace(_preparedFirstPersonId))
            {
                _resourceMgr.Release<GameObject>(_preparedFirstPersonId);
            }
            if (!string.IsNullOrWhiteSpace(_preparedThirdPersonId))
            {
                _resourceMgr.Release<GameObject>(_preparedThirdPersonId);
            }
            if (!string.IsNullOrWhiteSpace(_preparedControllerId))
            {
                _resourceMgr.Release<RuntimeAnimatorController>(_preparedControllerId);
            }
        }

        _preparedFirstPersonId = string.Empty;
        _preparedThirdPersonId = string.Empty;
        _preparedControllerId = string.Empty;
        _preparedWeaponData = null;
    }

    private void ReleaseCompletedPreparation<T>(string resourceId, Task<T> task)
        where T : UnityEngine.Object
    {
        if (_resourceMgr && task != null && task.Status == TaskStatus.RanToCompletion &&
            task.Result && !string.IsNullOrWhiteSpace(resourceId))
        {
            _resourceMgr.Release<T>(resourceId);
        }
    }

    private async Task LoadFirstPersonCharacterControllerAsync(
        WeaponDefinitionSO weaponData,
        int loadVersion)
    {
        string resourceId = weaponData.Animation.CharacterAnimatorControllerResourceId;
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            ReleaseFirstPersonCharacterController();
            return;
        }

        RuntimeAnimatorController controller =
            await _resourceMgr.LoadAsync<RuntimeAnimatorController>(resourceId);
        if (!IsLoadCurrent(loadVersion))
        {
            if (controller)
            {
                _resourceMgr.Release<RuntimeAnimatorController>(resourceId);
            }
            return;
        }
        if (!controller)
        {
            throw new InvalidOperationException(
                $"{weaponData.Basic.WeaponName} 的第一人称角色控制器不存在：{resourceId}");
        }

        RuntimeAnimatorController previousController = _firstPersonCharacterController;
        string previousResourceId = _firstPersonCharacterControllerResourceId;
        _firstPersonCharacterController = controller;
        _firstPersonCharacterControllerResourceId = resourceId;
        _firstPersonView.SetCharacterAnimatorController(controller);
        if (previousController && !string.IsNullOrWhiteSpace(previousResourceId))
        {
            _resourceMgr.Release<RuntimeAnimatorController>(previousResourceId);
        }
    }

    private async Task<GameObject> InstantiateWeaponAsync(
        string resourceId,
        Transform parent,
        WeaponIkData pose,
        bool usesProceduralPose)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || !parent)
        {
            return null;
        }

        Transform stagingRoot = usesProceduralPose
            ? _firstPersonStagingRoot
            : _thirdPersonStagingRoot;
        GameObject instance = await _resourceMgr.InstantiateAsync(
            resourceId,
            stagingRoot ? stagingRoot : parent);
        if (instance)
        {
            Transform instanceTransform = instance.transform;
            if (usesProceduralPose)
            {
                // Addressables 实例生成与 IK 配置不在同一帧，先隐藏默认姿态的枪网格。
                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].forceRenderingOff = true;
                    renderers[i].allowOcclusionWhenDynamic = false;
                    if (renderers[i] is SkinnedMeshRenderer skinned)
                    {
                        skinned.updateWhenOffscreen = true;
                    }
                }
            }
            instanceTransform.localPosition = usesProceduralPose
                ? Vector3.zero
                : pose.UseThirdPersonGrip ? pose.ThirdPersonGripPosition : pose.WeaponPositionOffset;
            instanceTransform.localRotation = usesProceduralPose
                ? Quaternion.identity
                : pose.UseThirdPersonGrip ? pose.ThirdPersonGripRotation : pose.WeaponRotationOffset;
            instanceTransform.localScale = !usesProceduralPose && pose.UseThirdPersonGrip
                ? pose.ThirdPersonGripScale
                : Vector3.one;
            SetLayerRecursively(instanceTransform, usesProceduralPose ? 9 : 8);
        }

        return instance;
    }

    private Animator GetWeaponAnimator(GameObject weaponInstance)
    {
        if (!weaponInstance)
        {
            return null;
        }
        if (weaponInstance.TryGetComponent(out WeaponView weaponView))
        {
            return weaponView.WeaponAnimator;
        }

        return weaponInstance.GetComponentInChildren<Animator>(true);
    }

    private static void InitializeWeaponIdle(Animator animator)
    {
        if (!animator || !animator.isActiveAndEnabled || !animator.runtimeAnimatorController)
        {
            return;
        }

        // 商店枪械控制器默认进入 Equip_Override。项目切枪由根节点升降，
        // 必须同时将枪网格自己的 Animator 初始化为 Idle，不能只摆正手臂/枪口挂点。
        animator.Rebind();
        int idleHash = Animator.StringToHash("Idle");
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            int fullPathHash = Animator.StringToHash(animator.GetLayerName(layer) + ".Idle");
            if (animator.HasState(layer, fullPathHash))
            {
                animator.Play(fullPathHash, layer, 0f);
                break;
            }
            if (animator.HasState(layer, idleHash))
            {
                animator.Play(idleHash, layer, 0f);
                break;
            }
        }
        animator.Update(0f);
    }

    private bool IsLoadCurrent(int loadVersion)
    {
        return this && isActiveAndEnabled && loadVersion == _loadVersion;
    }

    private void PrepareFirstPersonSkinnedRenderers()
    {
        SkinnedMeshRenderer[] renderers =
            _firstPersonPoseRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].updateWhenOffscreen = true;
        }
    }

    private void AttachThirdPersonWeaponRootToHand()
    {
        Animator characterAnimator = _thirdPersonView.CharacterAnimator;
        if (!characterAnimator || !characterAnimator.isHuman)
        {
            Debug.LogError($"{name} 的第三人称角色没有可用的 Humanoid 骨架。", this);
            return;
        }

        Transform rightHand = characterAnimator.GetBoneTransform(HumanBodyBones.RightHand);
        if (!rightHand)
        {
            Debug.LogError($"{name} 的第三人称角色缺少右手骨。", this);
            return;
        }

        Transform weaponRoot = _thirdPersonView.WeaponRoot;
        Quaternion handFrame = GetHandFrame(characterAnimator, rightHand);
        if (!_hasDefaultHandFrame)
        {
            _defaultHandFrame = handFrame;
            _hasDefaultHandFrame = true;
        }
        // Humanoid 只保证动作语义相同，不保证各模型腕骨的局部坐标轴相同。
        Quaternion gripCorrection = handFrame * Quaternion.Inverse(_defaultHandFrame);
        weaponRoot.SetParent(rightHand, false);
        // 不同 FBX 的骨骼可能保留厘米导入缩放，挂点按玩家的米制空间使用。
        Vector3 handScale = rightHand.lossyScale;
        Vector3 playerScale = transform.lossyScale;
        Vector3 scale = new Vector3(
            playerScale.x / Mathf.Max(0.0001f, Mathf.Abs(handScale.x)),
            playerScale.y / Mathf.Max(0.0001f, Mathf.Abs(handScale.y)),
            playerScale.z / Mathf.Max(0.0001f, Mathf.Abs(handScale.z)));
        weaponRoot.localPosition = Vector3.Scale(gripCorrection * _thirdPersonGripPosition, scale);
        weaponRoot.localRotation = gripCorrection * Quaternion.Euler(_thirdPersonGripRotation);
        weaponRoot.localScale = scale;
    }

    private static Quaternion GetHandFrame(Animator animator, Transform hand)
    {
        Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
        Transform index = animator.GetBoneTransform(HumanBodyBones.RightIndexProximal);
        Transform little = animator.GetBoneTransform(HumanBodyBones.RightLittleProximal);
        if (!middle || !index || !little)
        {
            return Quaternion.identity;
        }

        Vector3 forward = middle.position - hand.position;
        Vector3 up = Vector3.Cross(forward, index.position - little.position);
        if (forward.sqrMagnitude < 0.000001f || up.sqrMagnitude < 0.000000001f)
        {
            return Quaternion.identity;
        }
        return Quaternion.Inverse(hand.rotation) * Quaternion.LookRotation(forward, up);
    }

    private void RefreshOwnerVisibilityPolicy()
    {
        if (!_localInputCtrl)
        {
            TryGetComponent(out _localInputCtrl);
        }
        _hideFirstPersonPresentation = !_localInputCtrl || !_localInputCtrl.enabled;
    }

    private static Transform GetOrCreateStagingRoot(Transform parent)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == StagingRootName)
            {
                child.gameObject.SetActive(false);
                return child;
            }
        }

        GameObject stagingObject = new GameObject(StagingRootName);
        Transform stagingRoot = stagingObject.transform;
        stagingRoot.SetParent(parent, false);
        stagingObject.SetActive(false);
        return stagingRoot;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), layer);
        }
    }

    private void RefreshFirstPersonRendererCache()
    {
        _firstPersonRenderers.Clear();
        if (_firstPersonView)
        {
            _firstPersonView.GetComponentsInChildren(true, _firstPersonRenderers);
        }
        if (_hideFirstPersonPresentation)
        {
            SetFirstPersonRenderersHidden(true);
        }
    }

    private void SetFirstPersonRenderersHidden(bool hidden)
    {
        for (int i = 0; i < _firstPersonRenderers.Count; i++)
        {
            if (_firstPersonRenderers[i])
            {
                _firstPersonRenderers[i].forceRenderingOff = hidden;
            }
        }
    }

    private void RemoveUnownedWeaponChildren(Transform weaponRoot, Transform stagingRoot)
    {
        if (!weaponRoot)
        {
            return;
        }

        for (int i = weaponRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = weaponRoot.GetChild(i);
            if (child == stagingRoot)
            {
                continue;
            }

            GameObject orphan = child.gameObject;
            orphan.SetActive(false);
            if (!_resourceMgr || !_resourceMgr.ReleaseInstance(orphan))
            {
                Destroy(orphan);
            }
        }
    }

    private static Transform ResolveWeaponPoint(GameObject weapon, string pointPath)
    {
        if (!weapon || string.IsNullOrWhiteSpace(pointPath))
        {
            return null;
        }

        return weapon.transform.Find(pointPath);
    }

    private void ReleaseWeaponViews(bool releaseCharacterController = true)
    {
        if (_firstPersonView)
        {
            _firstPersonView.SetWeaponAnimator(null);
        }
        if (_thirdPersonView)
        {
            _thirdPersonView.SetWeaponAnimator(null);
        }

        ReleaseInstance(ref _firstPersonWeapon);
        ReleaseInstance(ref _thirdPersonWeapon);
        _muzzlePoint = null;
        _thirdPersonMuzzlePoint = null;
        if (releaseCharacterController)
        {
            ReleaseFirstPersonCharacterController();
        }
    }

    private void ReleaseFirstPersonCharacterController()
    {
        if (_firstPersonView)
        {
            _firstPersonView.SetCharacterAnimatorController(
                _defaultFirstPersonCharacterController);
        }
        if (_resourceMgr &&
            _firstPersonCharacterController &&
            !string.IsNullOrWhiteSpace(_firstPersonCharacterControllerResourceId))
        {
            _resourceMgr.Release<RuntimeAnimatorController>(
                _firstPersonCharacterControllerResourceId);
        }

        _firstPersonCharacterController = null;
        _firstPersonCharacterControllerResourceId = string.Empty;
    }

    private void ReleaseInstance(ref GameObject instance)
    {
        if (!instance)
        {
            instance = null;
            return;
        }

        // ReleaseInstance 只认识仍登记在 ResourceMgr 内的 Addressables 实例。
        // 脚本热重载或旧场景遗留对象可能已丢失登记，必须回退到普通销毁；否则引用虽清空，
        // 模型却会永久留在挂点下，并与下一把枪重合。
        instance.SetActive(false);
        bool released = _resourceMgr && _resourceMgr.ReleaseInstance(instance);
        if (!released)
        {
            Destroy(instance);
        }
        instance = null;
    }
}
