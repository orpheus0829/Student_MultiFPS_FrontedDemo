using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BulletImpactCtrl : MonoBehaviour
{
    private const string DecalResourceId = "effect.impact.decal";
    private const string ConcreteResourceId = "effect.impact.texture.concrete";
    private const string MetalResourceId = "effect.impact.texture.metal";
    private const string WoodResourceId = "effect.impact.texture.wood";
    private const string DirtResourceId = "effect.impact.texture.dirt";
    private const string BrickResourceId = "effect.impact.texture.brick";
    private const string GlassResourceId = "effect.impact.texture.glass";

    [Header("弹孔")]
    [SerializeField, Min(1)] private int _maximumDecals = 80;
    [SerializeField, Min(0.01f)] private float _decalLifetime = 25f;
    [SerializeField, Min(0f), InspectorName("结束渐隐时间")]
    private float _decalFadeDuration = 1.5f;
    [SerializeField, Min(0.01f)] private float _decalSize = 0.11f;
    [SerializeField, Min(0.001f)] private float _surfaceOffset = 0.006f;

    private readonly Dictionary<ImpactSurfaceType, Texture2D> _textures =
        new Dictionary<ImpactSurfaceType, Texture2D>();
    private readonly Queue<ActiveDecal> _decals = new Queue<ActiveDecal>();
    private ResourceMgr _resourceMgr;
    private Task _loadTask;
    private int _loadVersion;

    private void OnEnable()
    {
        _resourceMgr = ResourceMgr.instance;
        _loadTask = LoadAssetsAsync(++_loadVersion);
    }

    private void Update()
    {
        float now = Time.time;
        while (_decals.Count > 0 &&
            (!_decals.Peek().Instance || _decals.Peek().ExpireTime <= now))
        {
            ReleaseOldest();
        }

        float fadeDuration = Mathf.Min(_decalFadeDuration, _decalLifetime);
        if (fadeDuration <= 0f)
        {
            return;
        }

        foreach (ActiveDecal decal in _decals)
        {
            if (decal.Visual && now >= decal.ExpireTime - fadeDuration)
            {
                decal.Visual.SetAlpha((decal.ExpireTime - now) / fadeDuration);
            }
        }
    }

    private void OnDisable()
    {
        _loadVersion++;
        while (_decals.Count > 0)
        {
            ReleaseOldest();
        }

        if (_resourceMgr != null)
        {
            _resourceMgr.Release<GameObject>(DecalResourceId);
            _resourceMgr.Release<Texture2D>(ConcreteResourceId);
            _resourceMgr.Release<Texture2D>(MetalResourceId);
            _resourceMgr.Release<Texture2D>(WoodResourceId);
            _resourceMgr.Release<Texture2D>(DirtResourceId);
            _resourceMgr.Release<Texture2D>(BrickResourceId);
            _resourceMgr.Release<Texture2D>(GlassResourceId);
        }

        _textures.Clear();
        _loadTask = null;
    }

    public void ShowImpact(RaycastHit hit)
    {
        if (!isActiveAndEnabled || !hit.collider)
        {
            return;
        }

        ImpactSurfaceType surface = ResolveSurface(hit.collider);
        Vector3 position = hit.point + hit.normal * _surfaceOffset;
        Quaternion rotation = Quaternion.LookRotation(hit.normal) *
            Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(0f, 360f));
        _ = ShowImpactAsync(surface, position, rotation, hit.collider.transform, _loadVersion);
    }

    private async Task LoadAssetsAsync(int loadVersion)
    {
        try
        {
            await _resourceMgr.LoadAsync<GameObject>(DecalResourceId);
            await LoadTextureAsync(ImpactSurfaceType.Concrete, ConcreteResourceId, loadVersion);
            await LoadTextureAsync(ImpactSurfaceType.Metal, MetalResourceId, loadVersion);
            await LoadTextureAsync(ImpactSurfaceType.Wood, WoodResourceId, loadVersion);
            await LoadTextureAsync(ImpactSurfaceType.Dirt, DirtResourceId, loadVersion);
            await LoadTextureAsync(ImpactSurfaceType.Brick, BrickResourceId, loadVersion);
            await LoadTextureAsync(ImpactSurfaceType.Glass, GlassResourceId, loadVersion);
        }
        catch (Exception exception)
        {
            Debug.LogError($"{name} 加载弹孔资源失败：{exception.Message}", this);
        }
    }

    private async Task LoadTextureAsync(
        ImpactSurfaceType surface,
        string resourceId,
        int loadVersion)
    {
        Texture2D texture = await _resourceMgr.LoadAsync<Texture2D>(resourceId);
        if (texture && loadVersion == _loadVersion && this)
        {
            _textures[surface] = texture;
        }
    }

    private async Task ShowImpactAsync(
        ImpactSurfaceType surface,
        Vector3 position,
        Quaternion rotation,
        Transform hitTransform,
        int loadVersion)
    {
        if (_loadTask == null)
        {
            return;
        }

        await _loadTask;
        if (loadVersion != _loadVersion || !this || !isActiveAndEnabled || !hitTransform)
        {
            return;
        }

        if (!_textures.TryGetValue(surface, out Texture2D texture))
        {
            _textures.TryGetValue(ImpactSurfaceType.Concrete, out texture);
        }
        if (!texture)
        {
            return;
        }

        GameObject instance = null;
        try
        {
            instance = await _resourceMgr.InstantiateAsync(DecalResourceId, position, rotation);
            if (!instance)
            {
                return;
            }
            if (loadVersion != _loadVersion || !this || !isActiveAndEnabled || !hitTransform)
            {
                _resourceMgr.ReleaseInstance(instance);
                return;
            }

            instance.transform.SetParent(hitTransform, true);
            instance.transform.localScale = Vector3.one * _decalSize;
            BulletImpactVisual visual = null;
            if (instance.TryGetComponent(out visual))
            {
                visual.SetTexture(texture);
            }

            while (_decals.Count >= _maximumDecals)
            {
                ReleaseOldest();
            }
            _decals.Enqueue(new ActiveDecal(instance, visual, Time.time + _decalLifetime));
        }
        catch (Exception exception)
        {
            if (instance)
            {
                _resourceMgr.ReleaseInstance(instance);
            }
            Debug.LogError($"{name} 生成弹孔失败：{exception.Message}", this);
        }
    }

    private ImpactSurfaceType ResolveSurface(Collider hitCollider)
    {
        SurfaceImpactTag tag = hitCollider.GetComponentInParent<SurfaceImpactTag>();
        if (tag)
        {
            return tag.SurfaceType;
        }

        Renderer renderer = hitCollider.GetComponent<Renderer>();
        if (!renderer)
        {
            renderer = hitCollider.GetComponentInChildren<Renderer>();
        }

        string materialName = renderer && renderer.sharedMaterial
            ? renderer.sharedMaterial.name
            : hitCollider.sharedMaterial ? hitCollider.sharedMaterial.name : string.Empty;
        materialName = materialName.ToLowerInvariant();
        if (materialName.Contains("metal") || materialName.Contains("steel") ||
            materialName.Contains("iron"))
        {
            return ImpactSurfaceType.Metal;
        }
        if (materialName.Contains("wood") || materialName.Contains("timber") ||
            materialName.Contains("bark"))
        {
            return ImpactSurfaceType.Wood;
        }
        if (materialName.Contains("glass") || materialName.Contains("window"))
        {
            return ImpactSurfaceType.Glass;
        }
        if (materialName.Contains("dirt") || materialName.Contains("soil") ||
            materialName.Contains("sand") || materialName.Contains("grass"))
        {
            return ImpactSurfaceType.Dirt;
        }
        if (materialName.Contains("brick") || materialName.Contains("tile"))
        {
            return ImpactSurfaceType.Brick;
        }

        return ImpactSurfaceType.Concrete;
    }

    private void ReleaseOldest()
    {
        ActiveDecal decal = _decals.Dequeue();
        if (decal.Instance && _resourceMgr != null)
        {
            _resourceMgr.ReleaseInstance(decal.Instance);
        }
    }

    private readonly struct ActiveDecal
    {
        public GameObject Instance { get; }
        public BulletImpactVisual Visual { get; }
        public float ExpireTime { get; }

        public ActiveDecal(GameObject instance, BulletImpactVisual visual, float expireTime)
        {
            Instance = instance;
            Visual = visual;
            ExpireTime = expireTime;
        }
    }
}
