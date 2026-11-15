using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public sealed class BombShopPreviewStage
{
    private const int PreviewLayer = 31;
    private readonly PlayerCharacterModelCtrl _models;
    private readonly PlayerWeaponInventoryCtrl _inventory;
    private readonly PlayerWeaponViewCtrl _weaponView;
    private readonly PlayerArmorCtrl _armor;
    private readonly BombEconomyService _service;

    private ResourceMgr _resources;
    private GameObject _root;
    private GameObject _character;
    private GameObject _weapon;
    private GameObject _armorCrate;
    private GameObject _helmetCrate;
    private Transform _rightHand;
    private Transform _weaponMount;
    private RenderTexture _texture;
    private readonly List<Mesh> _bakedMeshes = new List<Mesh>();
    private int _characterVersion;
    private int _weaponVersion;
    private int _crateVersion;

    public RenderTexture Texture { get { return _texture; } }

    public BombShopPreviewStage(
        PlayerCharacterModelCtrl models,
        PlayerWeaponInventoryCtrl inventory,
        PlayerWeaponViewCtrl weaponView,
        PlayerArmorCtrl armor,
        BombEconomyService service)
    {
        _models = models;
        _inventory = inventory;
        _weaponView = weaponView;
        _armor = armor;
        _service = service;
    }

    public void Open()
    {
        Close();
        _resources = ResourceMgr.instance;
        _root = new GameObject("爆破商店预览台");
        _root.transform.position = new Vector3(5000f, 5000f, 5000f);

        _texture = new RenderTexture(900, 900, 24, RenderTextureFormat.ARGB32)
        {
            name = "爆破商店角色与装备预览",
            antiAliasing = 2
        };
        _texture.Create();

        GameObject cameraObject = new GameObject("预览相机");
        cameraObject.transform.SetParent(_root.transform, false);
        cameraObject.transform.localPosition = new Vector3(0f, 1.05f, 4.2f);
        cameraObject.transform.LookAt(_root.transform.position + new Vector3(0f, 1.05f, 0f));
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.018f, 0.023f, 0.03f, 0f);
        camera.cullingMask = 1 << PreviewLayer;
        camera.fieldOfView = 30f;
        camera.targetTexture = _texture;

        GameObject keyLightObject = new GameObject("主光");
        keyLightObject.transform.SetParent(_root.transform, false);
        keyLightObject.transform.localRotation = Quaternion.Euler(35f, -35f, 0f);
        Light keyLight = keyLightObject.AddComponent<Light>();
        keyLight.type = LightType.Directional;
        keyLight.intensity = 1.35f;
        keyLight.color = new Color(1f, 0.92f, 0.8f);

        GameObject fillLightObject = new GameObject("补光");
        fillLightObject.transform.SetParent(_root.transform, false);
        fillLightObject.transform.localPosition = new Vector3(-2f, 1.8f, 2f);
        Light fillLight = fillLightObject.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.range = 7f;
        fillLight.intensity = 2.2f;
        fillLight.color = new Color(0.28f, 0.5f, 1f);

        _ = CloneCurrentCharacterAsync();
    }

    public void Close()
    {
        _weaponVersion++;
        _crateVersion++;
        _characterVersion++;
        ReleaseInstance(ref _weapon);
        ReleaseInstance(ref _armorCrate);
        ReleaseInstance(ref _helmetCrate);
        ReleaseInstance(ref _character);
        if (_root)
        {
            UnityEngine.Object.Destroy(_root);
        }
        _root = null;
        _character = null;
        _rightHand = null;
        _weaponMount = null;
        for (int i = 0; i < _bakedMeshes.Count; i++)
        {
            if (_bakedMeshes[i])
            {
                UnityEngine.Object.Destroy(_bakedMeshes[i]);
            }
        }
        _bakedMeshes.Clear();
        if (_texture)
        {
            _texture.Release();
            UnityEngine.Object.Destroy(_texture);
        }
        _texture = null;
    }

    public void SetHoveredItem(BombEconomyService.ShopItem item)
    {
        if (!_root)
        {
            return;
        }

        if (item.Kind == BombShopItemKind.Armor ||
            item.Kind == BombShopItemKind.Helmet)
        {
            _ = ShowWeaponAsync(GetDefaultWeaponResourceId());
            _ = ShowCratesAsync(item.Kind);
            return;
        }

        _ = ShowWeaponAsync(item.ResourceId);
        _ = ShowCratesAsync(null);
    }

    public void RefreshDefault()
    {
        if (!_root)
        {
            return;
        }

        _ = ShowWeaponAsync(GetDefaultWeaponResourceId());
        _ = ShowCratesAsync(null);
    }

    private async Task CloneCurrentCharacterAsync()
    {
        int version = ++_characterVersion;
        string bodyResourceId = _models
            ? _models.GetCurrentBodyResourceId()
            : string.Empty;
        if (!_root)
        {
            return;
        }
        if (string.IsNullOrWhiteSpace(bodyResourceId))
        {
            CloneCurrentCharacterSnapshot();
            RefreshDefault();
            return;
        }

        GameObject character = null;
        try
        {
            character = await _resources.InstantiateAsync(
                bodyResourceId,
                _root.transform,
                false);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("商店角色预览加载失败：" + exception.Message);
            return;
        }
        if (!character)
        {
            return;
        }
        if (version != _characterVersion || !_root)
        {
            _resources.ReleaseInstance(character);
            return;
        }

        _character = character;
        _character.name = "当前角色模型预览";
        DisableGameplayComponents(_character);
        SetLayerRecursively(_character, PreviewLayer);

        Animator animator = _character.GetComponentInChildren<Animator>(true);
        if (!animator)
        {
            ReleaseInstance(ref _character);
            return;
        }
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        Animator sourceAnimator = _models ? _models.CurrentBodyAnimator : null;
        if (sourceAnimator && sourceAnimator.runtimeAnimatorController)
        {
            animator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
            ApplyArmedIdlePose(animator);
        }
        _rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        CreateWeaponMount(sourceAnimator);

        FrameCharacter(animator);
        RefreshDefault();
    }

    private void CloneCurrentCharacterSnapshot()
    {
        Animator source = _models ? _models.CurrentBodyAnimator : null;
        if (!source)
        {
            return;
        }

        _character = new GameObject("当前角色模型预览");
        _character.transform.SetParent(_root.transform, false);
        Renderer[] renderers = _models.GetCurrentBodyRenderers();
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer sourceRenderer = renderers[i];
            if (!sourceRenderer)
            {
                continue;
            }

            Mesh mesh = null;
            if (sourceRenderer is SkinnedMeshRenderer skinned)
            {
                mesh = new Mesh
                {
                    name = sourceRenderer.name + " 商店预览快照"
                };
                skinned.BakeMesh(mesh);
            }
            else if (sourceRenderer.TryGetComponent(out MeshFilter sourceFilter))
            {
                mesh = sourceFilter.sharedMesh;
            }
            if (!mesh)
            {
                continue;
            }

            GameObject part = new GameObject(sourceRenderer.name);
            part.transform.SetParent(_character.transform, false);
            CopyRelativeTransform(source.transform, sourceRenderer.transform, part.transform);
            MeshFilter filter = part.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = sourceRenderer.sharedMaterials;
            if (sourceRenderer is SkinnedMeshRenderer)
            {
                _bakedMeshes.Add(mesh);
            }
        }

        Transform sourceHand = source.GetBoneTransform(HumanBodyBones.RightHand);
        if (sourceHand)
        {
            GameObject hand = new GameObject("右手武器预览点");
            hand.transform.SetParent(_character.transform, false);
            CopyRelativeTransform(source.transform, sourceHand, hand.transform);
            _rightHand = hand.transform;
            CreateWeaponMount(source);
        }
        SetLayerRecursively(_character, PreviewLayer);
        FrameCharacter(source);
    }

    private void FrameCharacter(Animator poseAnimator)
    {
        if (!_character)
        {
            return;
        }

        _character.transform.localPosition = Vector3.zero;
        _character.transform.localRotation = Quaternion.identity;
        _character.transform.localScale = Vector3.one;
        FaceCharacterTowardCamera(poseAnimator);

        Bounds bounds = CalculateVisualBounds(_character);
        float height = Mathf.Max(0.1f, bounds.size.y);
        float scale = 1.85f / height;
        _character.transform.localScale = Vector3.one * scale;
        bounds = CalculateVisualBounds(_character);
        Vector3 localCenter = _root.transform.InverseTransformPoint(bounds.center);
        float localBottom = _root.transform.InverseTransformPoint(bounds.min).y;
        _character.transform.localPosition += new Vector3(
            -0.55f - localCenter.x,
            -localBottom,
            -localCenter.z);
    }

    private void FaceCharacterTowardCamera(Animator poseAnimator)
    {
        if (!poseAnimator || !poseAnimator.isHuman)
        {
            return;
        }

        Vector3 localForward = poseAnimator.transform.IsChildOf(_character.transform)
            ? _character.transform.InverseTransformDirection(poseAnimator.transform.forward)
            : Vector3.forward;
        localForward.y = 0f;
        if (localForward.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        _character.transform.localRotation = Quaternion.FromToRotation(
            localForward.normalized,
            Vector3.forward);
    }

    private void CreateWeaponMount(Animator sourceAnimator)
    {
        if (!_rightHand)
        {
            return;
        }

        GameObject mountObject = new GameObject("第三人称握枪挂点");
        _weaponMount = mountObject.transform;
        _weaponMount.SetParent(_rightHand, false);

        GameObject sourceWeapon = _weaponView ? _weaponView.ThirdPersonWeapon : null;
        Transform sourceMount = sourceWeapon ? sourceWeapon.transform.parent : null;
        Transform sourceHand = sourceAnimator && sourceAnimator.isHuman
            ? sourceAnimator.GetBoneTransform(HumanBodyBones.RightHand)
            : null;
        if (!sourceMount || !sourceHand || sourceMount.parent != sourceHand)
        {
            return;
        }

        _weaponMount.localPosition = sourceMount.localPosition;
        _weaponMount.localRotation = sourceMount.localRotation;
        _weaponMount.localScale = sourceMount.localScale;
    }

    private static void ApplyArmedIdlePose(Animator animator)
    {
        if (!animator || !animator.runtimeAnimatorController)
        {
            return;
        }

        animator.enabled = true;
        animator.Rebind();
        SetNeutralAnimatorParameters(animator);
        for (int layer = 1; layer < animator.layerCount; layer++)
        {
            animator.SetLayerWeight(layer, 0f);
        }

        int idleHash = Animator.StringToHash("Base Layer.Idle");
        if (animator.HasState(0, idleHash))
        {
            animator.Play(idleHash, 0, 0.35f);
        }
        else
        {
            animator.Play(Animator.StringToHash("Idle"), 0, 0.35f);
        }
        animator.Update(0f);
        animator.speed = 0f;
    }

    private static void SetNeutralAnimatorParameters(Animator animator)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            switch (parameter.type)
            {
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(parameter.nameHash, 0f);
                    break;
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(parameter.nameHash, false);
                    break;
                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(parameter.nameHash, 0);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    animator.ResetTrigger(parameter.nameHash);
                    break;
            }
        }
    }

    private static void CopyRelativeTransform(
        Transform root,
        Transform source,
        Transform target)
    {
        target.localPosition = root.InverseTransformPoint(source.position);
        target.localRotation = Quaternion.Inverse(root.rotation) * source.rotation;
        Vector3 rootScale = root.lossyScale;
        Vector3 sourceScale = source.lossyScale;
        target.localScale = new Vector3(
            rootScale.x == 0f ? 1f : sourceScale.x / rootScale.x,
            rootScale.y == 0f ? 1f : sourceScale.y / rootScale.y,
            rootScale.z == 0f ? 1f : sourceScale.z / rootScale.z);
    }

    private async Task ShowWeaponAsync(string weaponResourceId)
    {
        int version = ++_weaponVersion;
        ReleaseInstance(ref _weapon);
        if (string.IsNullOrWhiteSpace(weaponResourceId) || !_weaponMount)
        {
            return;
        }

        WeaponDefinitionSO weaponData = null;
        try
        {
            weaponData = await _resources.LoadAsync<WeaponDefinitionSO>(weaponResourceId);
            if (!weaponData || version != _weaponVersion || !_root)
            {
                return;
            }

            string modelResourceId = weaponData.Resources.WorldModelPrefabResourceId;
            GameObject instance = await _resources.InstantiateAsync(
                modelResourceId,
                _weaponMount,
                false);
            if (!instance)
            {
                return;
            }
            if (version != _weaponVersion || !_root)
            {
                _resources.ReleaseInstance(instance);
                return;
            }

            _weapon = instance;
            WeaponIkData pose = weaponData.Ik;
            _weapon.transform.localPosition = pose.UseThirdPersonGrip
                ? pose.ThirdPersonGripPosition
                : pose.WeaponPositionOffset;
            _weapon.transform.localRotation = pose.UseThirdPersonGrip
                ? pose.ThirdPersonGripRotation
                : pose.WeaponRotationOffset;
            _weapon.transform.localScale = pose.UseThirdPersonGrip
                ? pose.ThirdPersonGripScale
                : Vector3.one;
            SetLayerRecursively(_weapon, PreviewLayer);
            DisableGameplayComponents(_weapon);
            InitializeWeaponIdle(_weapon.GetComponentInChildren<Animator>(true));
        }
        catch (Exception exception)
        {
            Debug.LogWarning("商店武器预览加载失败：" + exception.Message);
        }
        finally
        {
            if (weaponData)
            {
                _resources.Release<WeaponDefinitionSO>(weaponResourceId);
            }
        }
    }

    private async Task ShowCratesAsync(BombShopItemKind? hoveredEquipment)
    {
        int version = ++_crateVersion;
        ReleaseInstance(ref _armorCrate);
        ReleaseInstance(ref _helmetCrate);

        bool showArmor = hoveredEquipment == BombShopItemKind.Armor ||
            (!hoveredEquipment.HasValue && _armor && _armor.Armor > 0f);
        bool showHelmet = hoveredEquipment == BombShopItemKind.Helmet ||
            (!hoveredEquipment.HasValue && _armor && _armor.HasHelmet);

        if (showArmor)
        {
            _armorCrate = await CreateCrateAsync(
                _service.ArmorCrateResourceId,
                new Vector3(0.73f, 0.02f, 0.08f),
                Quaternion.Euler(0f, -16f, 0f),
                version);
        }
        if (showHelmet)
        {
            Vector3 position = showArmor
                ? new Vector3(0.88f, 0.24f, 0.02f)
                : new Vector3(0.78f, 0.02f, 0.05f);
            Quaternion rotation = showArmor
                ? Quaternion.Euler(-8f, 20f, 7f)
                : Quaternion.Euler(0f, 12f, 0f);
            _helmetCrate = await CreateCrateAsync(
                _service.HelmetCrateResourceId,
                position,
                rotation,
                version);
        }
    }

    private async Task<GameObject> CreateCrateAsync(
        string resourceId,
        Vector3 localPosition,
        Quaternion localRotation,
        int version)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || !_root)
        {
            return null;
        }

        GameObject instance = await _resources.InstantiateAsync(resourceId, _root.transform, false);
        if (!instance)
        {
            return null;
        }
        if (version != _crateVersion || !_root)
        {
            _resources.ReleaseInstance(instance);
            return null;
        }

        instance.transform.localPosition = localPosition;
        instance.transform.localRotation = localRotation;
        instance.transform.localScale = Vector3.one;
        SetLayerRecursively(instance, PreviewLayer);
        DisableGameplayComponents(instance);
        NormalizeAndPlaceCrate(instance, localPosition);
        return instance;
    }

    private string GetDefaultWeaponResourceId()
    {
        if (!_inventory)
        {
            return "weapon.m1911.data";
        }

        string primary = _inventory.PrimaryWeaponResourceId;
        return !string.IsNullOrWhiteSpace(primary)
            ? primary
            : _inventory.GetWeaponResourceId(1);
    }

    private void NormalizeAndPlaceCrate(GameObject crate, Vector3 anchor)
    {
        Bounds bounds = CalculateVisualBounds(crate);
        float longestSide = Mathf.Max(bounds.size.x, bounds.size.z);
        float height = bounds.size.y;
        float referenceSize = Mathf.Max(longestSide, height);
        if (referenceSize <= 0.001f)
        {
            return;
        }

        const float CratePreviewSize = 0.62f;
        crate.transform.localScale *= CratePreviewSize / referenceSize;
        bounds = CalculateVisualBounds(crate);
        Vector3 center = _root.transform.InverseTransformPoint(bounds.center);
        float bottom = _root.transform.InverseTransformPoint(bounds.min).y;
        crate.transform.localPosition += new Vector3(
            anchor.x - center.x,
            anchor.y - bottom,
            anchor.z - center.z);
    }

    private static void InitializeWeaponIdle(Animator animator)
    {
        if (!animator || !animator.runtimeAnimatorController)
        {
            return;
        }

        animator.enabled = true;
        animator.Rebind();
        int idleHash = Animator.StringToHash("Idle");
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            int fullPathHash = Animator.StringToHash(
                animator.GetLayerName(layer) + ".Idle");
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

    private void ReleaseInstance(ref GameObject instance)
    {
        if (!instance)
        {
            return;
        }
        if (!_resources || !_resources.ReleaseInstance(instance))
        {
            UnityEngine.Object.Destroy(instance);
        }
        instance = null;
    }

    private static void DisableGameplayComponents(GameObject root)
    {
        MonoBehaviour[] behaviours = root.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            behaviours[i].enabled = false;
        }
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            transforms[i].gameObject.layer = layer;
        }
    }

    private static Bounds CalculateVisualBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = renderer.bounds;
                hasBounds = true;
                continue;
            }
            bounds.Encapsulate(renderer.bounds);
        }

        return hasBounds
            ? bounds
            : new Bounds(root.transform.position, Vector3.one);
    }

}
