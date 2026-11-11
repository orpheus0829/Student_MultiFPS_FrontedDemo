using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public sealed class PlayerCharacterModelOption
{
    [SerializeField, InspectorName("模型 ID")] private string _modelId;
    [SerializeField, InspectorName("显示名字")] private string _displayName;
    [SerializeField, ResourceId(typeof(GameObject)), InspectorName("第三人称模型资源 ID")]
    private string _bodyResourceId;
    [SerializeField, ResourceId(typeof(GameObject)), InspectorName("第一人称手臂资源 ID")]
    private string _armsResourceId;
    [SerializeField, ResourceId(typeof(CombatVoiceSO)), InspectorName("战斗语音盒资源 ID")]
    private string _voiceResourceId;

    public string ModelId { get { return _modelId; } }
    public string DisplayName { get { return _displayName; } }
    public string BodyResourceId { get { return _bodyResourceId; } }
    public string ArmsResourceId { get { return _armsResourceId; } }
    public string VoiceResourceId { get { return _voiceResourceId; } }
}

[DisallowMultipleComponent]
public sealed class PlayerCharacterModelCtrl : MonoBehaviour
{
    private const string DefaultModelId = "character.anbi";

    [Header("角色模型")]
    [SerializeField, InspectorName("初始模型 ID")] private string _initialModelId = DefaultModelId;
    [SerializeField, InspectorName("可选模型")] private PlayerCharacterModelOption[] _models =
        Array.Empty<PlayerCharacterModelOption>();

    [Header("玩家表现")]
    [SerializeField, InspectorName("第一人称表现")] private PlayerAnimationView _firstPersonView;
    [SerializeField, InspectorName("第三人称表现")] private PlayerAnimationView _thirdPersonView;
    [SerializeField, InspectorName("第一人称手臂网格")] private SkinnedMeshRenderer _armsRenderer;
    [SerializeField, InspectorName("枪械表现")] private PlayerWeaponViewCtrl _weaponViewCtrl;
    [SerializeField, InspectorName("第三人称镜头")] private FirstPersonCameraCtrl _cameraCtrl;
    [SerializeField, InspectorName("布娃娃")] private PlayerRagdollCtrl _ragdoll;
    [SerializeField, InspectorName("战斗语音")] private CombatVoiceCtrl _voiceCtrl;

    private readonly Dictionary<string, Transform> _armBones =
        new Dictionary<string, Transform>(StringComparer.Ordinal);
    private Animator _defaultBodyAnimator;
    private Renderer[] _defaultBodyRenderers;
    private bool[] _defaultRendererStates;
    private BodyHitbox[] _hitboxes;
    private Transform[] _defaultHitboxParents;
    private Vector3[] _defaultHitboxPositions;
    private Quaternion[] _defaultHitboxRotations;
    private Vector3[] _defaultHitboxScales;
    private Vector3[] _defaultHitboxParentScales;
    private Mesh _defaultArmsMesh;
    private Material[] _defaultArmsMaterials;
    private Transform[] _defaultArmsBones;
    private Transform _defaultArmsRootBone;
    private Transform _stagingRoot;
    private GameObject _currentBodyInstance;
    private string _currentArmsResourceId;
    private ResourceMgr _resources;
    private string _currentModelId;
    private bool _isSwitching;

    public string CurrentModelId { get { return _currentModelId; } }
    public bool IsSwitching { get { return _isSwitching; } }
    public int ModelCount { get { return _models.Length; } }

    private void Awake()
    {
        _defaultBodyAnimator = _thirdPersonView ? _thirdPersonView.CharacterAnimator : null;
        if (!_defaultBodyAnimator || !_armsRenderer || !_firstPersonView ||
            !_thirdPersonView || !_weaponViewCtrl || !_ragdoll || !_voiceCtrl)
        {
            Debug.LogError($"{name} 的角色模型切换引用不完整。", this);
            enabled = false;
            return;
        }

        // 默认安比不会经过 SwitchModelAsync 的完整切换分支，也必须与切换后的
        // 角色使用同一套 Animator 约束。否则第一人称隐藏第三人称渲染器时，
        // Animator 可能被裁剪而留下未求值的手臂骨骼，握持 IK 会基于随机旧姿势运行。
        _defaultBodyAnimator.applyRootMotion = false;
        _defaultBodyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        _currentModelId = DefaultModelId;
        GameObject stagingObject = new GameObject("角色模型加载暂存");
        _stagingRoot = stagingObject.transform;
        _stagingRoot.SetParent(transform, false);
        stagingObject.SetActive(false);
        _defaultArmsMesh = _armsRenderer.sharedMesh;
        _defaultArmsMaterials = _armsRenderer.sharedMaterials;
        _defaultArmsBones = _armsRenderer.bones;
        _defaultArmsRootBone = _armsRenderer.rootBone;
        Transform[] armTransforms = _firstPersonView.CharacterAnimator
            .GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < armTransforms.Length; i++)
        {
            if (!_armBones.ContainsKey(armTransforms[i].name))
            {
                _armBones.Add(armTransforms[i].name, armTransforms[i]);
            }
        }

        Renderer[] allRenderers = _defaultBodyAnimator.GetComponentsInChildren<Renderer>(true);
        List<Renderer> bodyRenderers = new List<Renderer>();
        for (int i = 0; i < allRenderers.Length; i++)
        {
            Transform current = allRenderers[i].transform;
            if (current.IsChildOf(_firstPersonView.transform) ||
                current.IsChildOf(_thirdPersonView.WeaponRoot))
            {
                continue;
            }
            bodyRenderers.Add(allRenderers[i]);
        }
        _defaultBodyRenderers = bodyRenderers.ToArray();
        _defaultRendererStates = new bool[_defaultBodyRenderers.Length];
        for (int i = 0; i < _defaultBodyRenderers.Length; i++)
        {
            _defaultRendererStates[i] = _defaultBodyRenderers[i].enabled;
        }

        _hitboxes = GetComponentsInChildren<BodyHitbox>(true);
        _defaultHitboxParents = new Transform[_hitboxes.Length];
        _defaultHitboxPositions = new Vector3[_hitboxes.Length];
        _defaultHitboxRotations = new Quaternion[_hitboxes.Length];
        _defaultHitboxScales = new Vector3[_hitboxes.Length];
        _defaultHitboxParentScales = new Vector3[_hitboxes.Length];
        for (int i = 0; i < _hitboxes.Length; i++)
        {
            Transform hitbox = _hitboxes[i].transform;
            _defaultHitboxParents[i] = hitbox.parent;
            _defaultHitboxPositions[i] = hitbox.localPosition;
            _defaultHitboxRotations[i] = hitbox.localRotation;
            _defaultHitboxScales[i] = hitbox.localScale;
            _defaultHitboxParentScales[i] = hitbox.parent.lossyScale;
        }
    }

    private void Start()
    {
        if (!string.Equals(_initialModelId, DefaultModelId, StringComparison.Ordinal))
        {
            _ = SwitchModelAsync(_initialModelId);
        }
    }

    private void OnDestroy()
    {
        if (_currentBodyInstance && _resources)
        {
            _resources.ReleaseInstance(_currentBodyInstance);
        }
        if (!string.IsNullOrWhiteSpace(_currentArmsResourceId) && _resources)
        {
            _resources.Release<GameObject>(_currentArmsResourceId);
        }
    }

    public string GetModelId(int index)
    {
        return index >= 0 && index < _models.Length && _models[index] != null
            ? _models[index].ModelId : string.Empty;
    }

    public string GetModelName(int index)
    {
        return index >= 0 && index < _models.Length && _models[index] != null
            ? _models[index].DisplayName : string.Empty;
    }

    public async Task<bool> SwitchModelAsync(string modelId)
    {
        if (!isActiveAndEnabled || _isSwitching)
        {
            return false;
        }
        if (string.Equals(modelId, _currentModelId, StringComparison.Ordinal))
        {
            return true;
        }
        CharacterHealth health = GetComponent<CharacterHealth>();
        if (health && health.IsDead)
        {
            return false;
        }

        PlayerCharacterModelOption option = FindOption(modelId);
        if (option == null)
        {
            return false;
        }

        _isSwitching = true;
        _resources = ResourceMgr.instance;
        string ownerName = name;
        bool isDefault = string.Equals(modelId, DefaultModelId, StringComparison.Ordinal);
        GameObject newArmsAsset = null;
        GameObject newBody = null;
        string armsId = isDefault ? string.Empty : option.ArmsResourceId;
        try
        {
            if (!isDefault)
            {
                if (string.IsNullOrWhiteSpace(option.BodyResourceId) ||
                    string.IsNullOrWhiteSpace(armsId))
                {
                    Debug.LogWarning($"{name} 的 {option.DisplayName} 缺少模型资源 ID。", this);
                    return false;
                }
                newArmsAsset = await _resources.LoadAsync<GameObject>(armsId);
                if (!this || !newArmsAsset)
                {
                    return false;
                }
                newBody = await _resources.InstantiateAsync(option.BodyResourceId, _stagingRoot);
                if (!this || !newBody)
                {
                    return false;
                }
            }

            Animator newAnimator = isDefault
                ? _defaultBodyAnimator
                : newBody.GetComponentInChildren<Animator>(true);
            if (!newAnimator || !newAnimator.isHuman ||
                !newAnimator.GetBoneTransform(HumanBodyBones.Hips) ||
                !newAnimator.GetBoneTransform(HumanBodyBones.Head) ||
                !newAnimator.GetBoneTransform(HumanBodyBones.RightHand))
            {
                Debug.LogWarning($"{name} 的 {option.DisplayName} 没有可用的人形骨架。", this);
                return false;
            }
            if (!TryGetArmSkin(newArmsAsset, isDefault,
                out Mesh mesh, out Material[] materials,
                out Transform[] bones, out Transform rootBone))
            {
                Debug.LogWarning($"{name} 的 {option.DisplayName} 手臂网格无法绑定。", this);
                return false;
            }

            if (newBody)
            {
                RestoreAccessoryBindings(newAnimator, modelId);
                newBody.transform.SetParent(_thirdPersonView.transform, false);
                newBody.transform.localPosition = Vector3.zero;
                newBody.transform.localRotation = Quaternion.identity;
            }

            Transform weaponRoot = _thirdPersonView.WeaponRoot;
            weaponRoot.SetParent(_thirdPersonView.transform, false);
            for (int i = 0; i < _hitboxes.Length; i++)
            {
                Transform parent = isDefault
                    ? _defaultHitboxParents[i]
                    : GetHitboxBone(newAnimator, _hitboxes[i].BodyPart);
                _hitboxes[i].transform.SetParent(parent ? parent : transform, false);
                Vector3 parentScale = _hitboxes[i].transform.parent.lossyScale;
                Vector3 originalScale = _defaultHitboxParentScales[i];
                Vector3 compensation = new Vector3(
                    originalScale.x / Mathf.Max(0.0001f, Mathf.Abs(parentScale.x)),
                    originalScale.y / Mathf.Max(0.0001f, Mathf.Abs(parentScale.y)),
                    originalScale.z / Mathf.Max(0.0001f, Mathf.Abs(parentScale.z)));
                _hitboxes[i].transform.localPosition = Vector3.Scale(_defaultHitboxPositions[i], compensation);
                _hitboxes[i].transform.localRotation = _defaultHitboxRotations[i];
                _hitboxes[i].transform.localScale = Vector3.Scale(_defaultHitboxScales[i], compensation);
            }

            Animator oldAnimator = _thirdPersonView.CharacterAnimator;
            if (oldAnimator && oldAnimator != newAnimator)
            {
                oldAnimator.enabled = false;
            }
            // 移动由 PlayerMoveCtrl 裁决；导入 FBX 的默认 Root Motion 不能再次推动外观。
            newAnimator.applyRootMotion = false;
            newAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            newAnimator.enabled = true;
            _thirdPersonView.SetCharacterAnimator(newAnimator);
            _ragdoll.SetBodyAnimator(newAnimator);
            _weaponViewCtrl.RefreshThirdPersonWeaponGrip();
            _armsRenderer.sharedMesh = mesh;
            _armsRenderer.sharedMaterials = materials;
            _armsRenderer.bones = bones;
            _armsRenderer.rootBone = rootBone;
            SetDefaultBodyVisible(isDefault);
            // 只有本地玩家（持有启用的 PlayerInputCtrl）且相机组件在运行时，才由相机裁决第三人称身体的可见性。
            // AI 变体禁用了 PlayerInputCtrl 与第一人称相机；若它们也调这一句，刚换上的身体会被当成第一人称永久隐藏。
            // 这不是 AI 专属问题：任何非本地玩家实例换模型都不能走这条路径，也不要靠给 AI 启用相机来绕过。
            PlayerInputCtrl inputCtrl = GetComponent<PlayerInputCtrl>();
            if (inputCtrl && inputCtrl.enabled && _cameraCtrl && _cameraCtrl.enabled)
            {
                _cameraCtrl.RefreshCharacterVisibility();
            }

            GameObject previousBody = _currentBodyInstance;
            string previousArmsId = _currentArmsResourceId;
            _currentBodyInstance = newBody;
            _currentArmsResourceId = armsId;
            _currentModelId = modelId;
            newBody = null;
            newArmsAsset = null;
            if (previousBody)
            {
                _resources.ReleaseInstance(previousBody);
            }
            if (!string.IsNullOrWhiteSpace(previousArmsId))
            {
                _resources.Release<GameObject>(previousArmsId);
            }
            if (!string.IsNullOrWhiteSpace(option.VoiceResourceId))
            {
                await _voiceCtrl.SetVoiceProfileAsync(option.VoiceResourceId);
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{ownerName} 切换角色 {modelId} 失败：{exception}", this);
            return false;
        }
        finally
        {
            if (newBody && _resources)
            {
                _resources.ReleaseInstance(newBody);
            }
            if (newArmsAsset && _resources)
            {
                _resources.Release<GameObject>(armsId);
            }
            _isSwitching = false;
        }
    }

    private PlayerCharacterModelOption FindOption(string modelId)
    {
        for (int i = 0; i < _models.Length; i++)
        {
            if (_models[i] != null &&
                string.Equals(_models[i].ModelId, modelId, StringComparison.Ordinal))
            {
                return _models[i];
            }
        }
        return null;
    }

    private bool TryGetArmSkin(GameObject source, bool isDefault,
        out Mesh mesh, out Material[] materials, out Transform[] bones,
        out Transform rootBone)
    {
        mesh = _defaultArmsMesh;
        materials = _defaultArmsMaterials;
        bones = _defaultArmsBones;
        rootBone = _defaultArmsRootBone;
        if (isDefault)
        {
            return mesh && bones != null && bones.Length > 0;
        }

        SkinnedMeshRenderer renderer = source
            ? source.GetComponentInChildren<SkinnedMeshRenderer>(true) : null;
        if (!renderer || !renderer.sharedMesh || renderer.bones.Length == 0)
        {
            return false;
        }
        Transform[] mappedBones = new Transform[renderer.bones.Length];
        for (int i = 0; i < renderer.bones.Length; i++)
        {
            Transform sourceBone = renderer.bones[i];
            if (!sourceBone || !_armBones.TryGetValue(sourceBone.name, out mappedBones[i]))
            {
                return false;
            }
        }
        if (!renderer.rootBone ||
            !_armBones.TryGetValue(renderer.rootBone.name, out Transform mappedRoot))
        {
            return false;
        }

        mesh = renderer.sharedMesh;
        materials = renderer.sharedMaterials;
        bones = mappedBones;
        rootBone = mappedRoot;
        return true;
    }

    private void SetDefaultBodyVisible(bool visible)
    {
        for (int i = 0; i < _defaultBodyRenderers.Length; i++)
        {
            if (_defaultBodyRenderers[i])
            {
                _defaultBodyRenderers[i].enabled = visible && _defaultRendererStates[i];
            }
        }
    }

    private static void RestoreAccessoryBindings(Animator animator, string modelId)
    {
        if (string.Equals(modelId, "character.zhuyuan", StringComparison.Ordinal))
        {
            Transform[] footBones = animator.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < footBones.Length; i++)
            {
                HumanBodyBones foot;
                if (footBones[i].name == "つま先.L")
                {
                    foot = HumanBodyBones.LeftFoot;
                }
                else if (footBones[i].name == "つま先.R")
                {
                    foot = HumanBodyBones.RightFoot;
                }
                else
                {
                    continue;
                }

                // MMD 的脚踝蒙皮由 D 链驱动，但脚趾权重仍在普通链；
                // 丢失 DCC 跟随约束后会把鞋面拉到原地。保持绑定姿态接回脚踝。
                Transform ankle = animator.GetBoneTransform(foot);
                if (ankle)
                {
                    footBones[i].SetParent(ankle, true);
                }
            }
        }
    }

    private static Transform GetHitboxBone(Animator animator, BodyPart bodyPart)
    {
        switch (bodyPart)
        {
            case BodyPart.Head:
                return animator.GetBoneTransform(HumanBodyBones.Head);
            case BodyPart.LowerBody:
                return animator.GetBoneTransform(HumanBodyBones.Hips);
            default:
                Transform upperChest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                return upperChest ? upperChest : animator.GetBoneTransform(HumanBodyBones.Chest);
        }
    }
}
