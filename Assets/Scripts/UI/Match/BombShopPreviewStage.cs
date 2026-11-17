using System;
using System.Threading.Tasks;
using UnityEngine;

public sealed class BombShopPreviewStage
{
    private const int PreviewLayer = 31;
    private const string StageResourceId = "gameplay.bomb_shop.stage";
    private const string AkResourceId = "weapon.ak.data";
    private const string SvdResourceId = "weapon.svd.data";
    private const string Kar98kResourceId = "weapon.kar98k.data";
    private const float PistolDisplayScale = 0.7f;
    private const float OtherWeaponDisplayScale = 1.3f;

    private readonly PlayerWeaponInventoryCtrl _inventory;
    private readonly PlayerArmorCtrl _armor;
    private readonly BombEconomyService _service;

    private ResourceMgr _resources;
    private GameObject _root;
    private GameObject _weapon;
    private GameObject _armorCrate;
    private GameObject _helmetCrate;
    private Transform _gunPos;
    private Transform _armorPos;
    private Transform _helmetPos;
    private BombShopStageRuntime _runtime;
    private RenderTexture _texture;
    private int _contentVersion;

    public RenderTexture Texture { get { return _texture; } }

    public BombShopPreviewStage(
        PlayerWeaponInventoryCtrl inventory,
        PlayerArmorCtrl armor,
        BombEconomyService service)
    {
        _inventory = inventory;
        _armor = armor;
        _service = service;
    }

    public async Task<bool> OpenAsync()
    {
        Close();
        _resources = ResourceMgr.instance;
        try
        {
            _root = await _resources.InstantiateAsync(StageResourceId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("爆破商店实体监控台加载失败：" + exception.Message);
            return false;
        }
        if (!_root)
        {
            return false;
        }

        _root.name = "本地爆破商店监控台";
        _root.transform.position = new Vector3(5000f, 5000f, 5000f);
        PrepareStageEnvironment(_root);
        SetLayerRecursively(_root, PreviewLayer);
        Transform camAll = FindChild("CamAll");
        Transform camWeapon = FindChild("CamWeapon");
        Transform camDefense = FindChild("CamDefense");
        _gunPos = FindChild("GunPos");
        _armorPos = FindChild("DefensePos");
        _helmetPos = FindChild("HelmatPos");
        if (!camAll || !camWeapon || !camDefense || !_gunPos ||
            !_armorPos || !_helmetPos)
        {
            Debug.LogError("爆破商店实体监控台缺少机位或展示点。", _root);
            Close();
            return false;
        }

        _texture = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32)
        {
            name = "爆破商店高清监控画面",
            antiAliasing = 2
        };
        _texture.Create();
        GameObject cameraObject = new GameObject("商店监控相机");
        cameraObject.layer = PreviewLayer;
        cameraObject.transform.SetParent(_root.transform, false);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.Skybox;
        camera.cullingMask = 1 << PreviewLayer;
        camera.fieldOfView = 55f;
        camera.targetTexture = _texture;
        _runtime = cameraObject.AddComponent<BombShopStageRuntime>();
        _runtime.Initialize(camera.transform, camAll, camWeapon, camDefense);
        await RefreshDefaultAsync();
        return _root;
    }

    public void Close()
    {
        _contentVersion++;
        ReleaseInstance(ref _weapon);
        ReleaseInstance(ref _armorCrate);
        ReleaseInstance(ref _helmetCrate);
        if (_root)
        {
            if (!_resources || !_resources.ReleaseInstance(_root))
            {
                UnityEngine.Object.Destroy(_root);
            }
        }
        _root = null;
        _runtime = null;
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
        _runtime.RequestView(
            item.Kind == BombShopItemKind.Armor ||
            item.Kind == BombShopItemKind.Helmet
                ? BombShopStageView.Defense
                : BombShopStageView.Weapon);
        _ = ShowHoveredItemAsync(item);
    }

    public void RefreshDefault()
    {
        if (!_root)
        {
            return;
        }
        _runtime.RequestView(BombShopStageView.All);
        _ = RefreshDefaultAsync();
    }

    private async Task ShowHoveredItemAsync(BombEconomyService.ShopItem item)
    {
        int version = ++_contentVersion;
        if (item.Kind == BombShopItemKind.Armor)
        {
            await ShowEquipmentAsync(true, false, version, true);
        }
        else if (item.Kind == BombShopItemKind.Helmet)
        {
            await ShowEquipmentAsync(false, true, version, true);
        }
        else
        {
            await ShowWeaponAsync(item.ResourceId, version);
            if (version == _contentVersion)
            {
                ReleaseInstance(ref _armorCrate);
                ReleaseInstance(ref _helmetCrate);
            }
        }
    }

    private async Task RefreshDefaultAsync()
    {
        int version = ++_contentVersion;
        await ShowWeaponAsync(GetDefaultWeaponResourceId(), version);
        if (version != _contentVersion)
        {
            return;
        }
        await ShowEquipmentAsync(
            _armor && _armor.Armor > 0f,
            _armor && _armor.HasHelmet,
            version,
            false);
    }

    private async Task ShowWeaponAsync(string resourceId, int version)
    {
        ReleaseInstance(ref _weapon);
        if (string.IsNullOrWhiteSpace(resourceId) || !_gunPos)
        {
            return;
        }
        WeaponDefinitionSO data = null;
        try
        {
            data = await _resources.LoadAsync<WeaponDefinitionSO>(resourceId);
            if (!data || version != _contentVersion || !_root)
            {
                return;
            }
            GameObject instance = await _resources.InstantiateAsync(
                data.Resources.WorldModelPrefabResourceId,
                _gunPos,
                false);
            if (!instance)
            {
                return;
            }
            if (version != _contentVersion || !_root)
            {
                _resources.ReleaseInstance(instance);
                return;
            }
            _weapon = instance;
            Quaternion displayRotation = Quaternion.identity;
            bool needsUprightSideways = string.Equals(
                    resourceId, AkResourceId, StringComparison.Ordinal) ||
                string.Equals(resourceId, SvdResourceId, StringComparison.Ordinal);
            if (needsUprightSideways)
            {
                displayRotation = Quaternion.Euler(-90f, 90f, -90f);
            }
            if (string.Equals(resourceId, Kar98kResourceId, StringComparison.Ordinal))
            {
                HideKar98kDisplayAmmo(_weapon);
            }
            float displayScale = data.Basic.Category == WeaponCategory.Pistol
                ? PistolDisplayScale
                : OtherWeaponDisplayScale;
            PrepareDisplayObject(
                _weapon,
                _gunPos,
                0.86f * displayScale,
                displayRotation);
        }
        catch (Exception exception)
        {
            Debug.LogWarning("商店监控武器展示失败：" + exception.Message);
        }
        finally
        {
            if (data)
            {
                _resources.Release<WeaponDefinitionSO>(resourceId);
            }
        }
    }

    private async Task ShowEquipmentAsync(
        bool showArmor,
        bool showHelmet,
        int version,
        bool clearWeapon)
    {
        ReleaseInstance(ref _armorCrate);
        ReleaseInstance(ref _helmetCrate);
        if (clearWeapon)
        {
            ReleaseInstance(ref _weapon);
        }
        if (showArmor)
        {
            _armorCrate = await CreateDisplayAsync(
                _service.ArmorCrateResourceId,
                _armorPos,
                0.58f,
                version);
        }
        if (showHelmet && version == _contentVersion)
        {
            _helmetCrate = await CreateDisplayAsync(
                _service.HelmetCrateResourceId,
                _helmetPos,
                0.5f,
                version);
        }
    }

    private async Task<GameObject> CreateDisplayAsync(
        string resourceId,
        Transform parent,
        float targetSize,
        int version)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || !parent)
        {
            return null;
        }
        GameObject instance = await _resources.InstantiateAsync(
            resourceId,
            parent,
            false);
        if (!instance)
        {
            return null;
        }
        if (version != _contentVersion || !_root)
        {
            _resources.ReleaseInstance(instance);
            return null;
        }
        PrepareDisplayObject(instance, parent, targetSize);
        return instance;
    }

    private void PrepareDisplayObject(
        GameObject instance,
        Transform anchor,
        float targetSize,
        Quaternion displayRotation = default)
    {
        DisableStagePhysics(instance);
        SetLayerRecursively(instance, PreviewLayer);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = displayRotation == default
            ? Quaternion.identity
            : displayRotation;
        instance.transform.localScale = Vector3.one;
        Renderer primaryRenderer = FindPrimaryRenderer(instance);
        Bounds bounds = primaryRenderer
            ? primaryRenderer.bounds
            : CalculateBounds(instance);
        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (size > 0.0001f)
        {
            instance.transform.localScale *= targetSize / size;
        }
        instance.transform.position = anchor.position;
        instance.transform.SetParent(anchor.root, true);
        BoxCollider collider = CreatePrimaryVisualCollider(instance, primaryRenderer);
        Physics.SyncTransforms();
        Vector3 position = instance.transform.position;
        position.y += anchor.position.y + 0.08f - collider.bounds.min.y;
        instance.transform.position = position;
        Physics.SyncTransforms();
        bounds = collider.bounds;
        Rigidbody rigidbody = instance.GetComponent<Rigidbody>();
        if (!rigidbody)
        {
            rigidbody = instance.AddComponent<Rigidbody>();
        }
        rigidbody.isKinematic = false;
        rigidbody.detectCollisions = true;
        rigidbody.useGravity = true;
        rigidbody.mass = Mathf.Clamp(bounds.size.magnitude, 0.4f, 4f);
        rigidbody.linearDamping = 0.25f;
        rigidbody.angularDamping = 0.8f;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
    }

    private static Renderer FindPrimaryRenderer(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Renderer primary = null;
        float largestVolume = 0f;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (!renderer || !renderer.enabled ||
                !(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer))
            {
                continue;
            }
            Vector3 size = renderer.bounds.size;
            float volume = size.x * size.y * size.z;
            if (volume <= largestVolume)
            {
                continue;
            }
            largestVolume = volume;
            primary = renderer;
        }
        return primary;
    }

    private static void HideKar98kDisplayAmmo(GameObject root)
    {
        WeaponBoneVisibilityCtrl[] visibilityCtrls =
            root.GetComponentsInChildren<WeaponBoneVisibilityCtrl>(true);
        for (int i = 0; i < visibilityCtrls.Length; i++)
        {
            visibilityCtrls[i].HideConfiguredBonesNow();
        }

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform child = transforms[i];
            if (child == root.transform ||
                !child.name.StartsWith("Cartridge", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            child.localScale = Vector3.zero;
            Renderer[] renderers = child.GetComponentsInChildren<Renderer>(true);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                renderers[rendererIndex].enabled = false;
            }
            child.gameObject.SetActive(false);
        }
    }

    private static BoxCollider CreatePrimaryVisualCollider(
        GameObject root,
        Renderer primaryRenderer)
    {
        if (primaryRenderer)
        {
            Mesh mesh = null;
            if (primaryRenderer is SkinnedMeshRenderer skinned)
            {
                mesh = skinned.sharedMesh;
            }
            else if (primaryRenderer.TryGetComponent(out MeshFilter filter))
            {
                mesh = filter.sharedMesh;
            }
            if (mesh)
            {
                BoxCollider visualCollider =
                    primaryRenderer.gameObject.AddComponent<BoxCollider>();
                visualCollider.center = mesh.bounds.center;
                visualCollider.size = mesh.bounds.size;
                return visualCollider;
            }
        }

        BoxCollider fallback = root.AddComponent<BoxCollider>();
        Bounds bounds = CalculateBounds(root);
        fallback.center = root.transform.InverseTransformPoint(bounds.center);
        Vector3 scale = root.transform.lossyScale;
        fallback.size = new Vector3(
            bounds.size.x / Mathf.Max(0.0001f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.0001f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(0.0001f, Mathf.Abs(scale.z)));
        return fallback;
    }

    private string GetDefaultWeaponResourceId()
    {
        if (!_inventory)
        {
            return "weapon.m1911.data";
        }
        return !string.IsNullOrWhiteSpace(_inventory.PrimaryWeaponResourceId)
            ? _inventory.PrimaryWeaponResourceId
            : _inventory.GetWeaponResourceId(1);
    }

    private Transform FindChild(string childName)
    {
        Transform[] transforms = _root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            if (string.Equals(transforms[i].name, childName, StringComparison.Ordinal))
            {
                return transforms[i];
            }
        }
        return null;
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

    private static void DisableStagePhysics(GameObject root)
    {
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].enabled = false;
        }
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = false;
        }
    }

    private static void PrepareStageEnvironment(GameObject root)
    {
        Animator[] animators = root.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].enabled = false;
        }
        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = true;
        }
        Rigidbody[] rigidbodies = root.GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < rigidbodies.Length; i++)
        {
            rigidbodies[i].isKinematic = true;
            rigidbodies[i].detectCollisions = true;
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

    private static Bounds CalculateBounds(GameObject root)
    {
        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        Bounds bounds = new Bounds(root.transform.position, Vector3.zero);
        bool initialized = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (!renderers[i].enabled)
            {
                continue;
            }
            if (!initialized)
            {
                bounds = renderers[i].bounds;
                initialized = true;
            }
            else
            {
                bounds.Encapsulate(renderers[i].bounds);
            }
        }
        return bounds;
    }
}
