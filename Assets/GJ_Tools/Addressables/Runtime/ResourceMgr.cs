using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

public sealed class ResourceMgr : BaseMgr<ResourceMgr>
{
    private readonly Dictionary<ResourceKey, ResourceRecord> _resources = new Dictionary<ResourceKey, ResourceRecord>();
    private readonly Dictionary<string, LabelRecord> _labels = new Dictionary<string, LabelRecord>();
    private readonly Dictionary<GameObject, AsyncOperationHandle<GameObject>> _instances = new Dictionary<GameObject, AsyncOperationHandle<GameObject>>();
    private readonly Dictionary<int, AsyncOperationHandle<SceneInstance>> _scenes = new Dictionary<int, AsyncOperationHandle<SceneInstance>>();

    private Task _initializationTask;

    protected override bool Ddol
    {
        get
        {
            return true;
        }
    }

    public Task InitializeAsync()
    {
        if (_initializationTask == null)
        {
            _initializationTask = InitializeInternalAsync();
        }

        return _initializationTask;
    }

    public async Task<T> LoadAsync<T>(string resourceId) where T : Object
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        await InitializeAsync();

        ResourceKey key = new ResourceKey(resourceId, typeof(T));
        if (_resources.TryGetValue(key, out ResourceRecord existing))
        {
            existing.ReferenceCount++;
            return await existing.Task as T;
        }

        AsyncOperationHandle<T> handle = Addressables.LoadAssetAsync<T>(resourceId);
        ResourceRecord record = new ResourceRecord
        {
            Handle = handle,
            ReferenceCount = 1
        };
        record.Task = CompleteLoadAsync(key, record);
        _resources.Add(key, record);
        return await record.Task as T;
    }

    public bool TryGetLoaded<T>(string resourceId, out T resource) where T : Object
    {
        ResourceKey key = new ResourceKey(resourceId, typeof(T));
        if (_resources.TryGetValue(key, out ResourceRecord record) &&
            record.Handle.IsValid() &&
            record.Handle.Status == AsyncOperationStatus.Succeeded)
        {
            resource = record.Handle.Result as T;
            return resource != null;
        }

        resource = null;
        return false;
    }

    public void Release<T>(string resourceId) where T : Object
    {
        Release(new ResourceKey(resourceId, typeof(T)));
    }

    public void Release(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return;
        }

        List<ResourceKey> keys = new List<ResourceKey>();
        foreach (ResourceKey key in _resources.Keys)
        {
            if (key.ResourceId == resourceId)
            {
                keys.Add(key);
            }
        }

        for (int i = 0; i < keys.Count; i++)
        {
            Release(keys[i]);
        }
    }

    public async Task<GameObject> InstantiateAsync(
        string resourceId,
        Transform parent = null,
        bool instantiateInWorldSpace = false)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        await InitializeAsync();
        AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(
            resourceId,
            parent,
            instantiateInWorldSpace,
            true);

        GameObject instance = await handle.Task;
        if (handle.Status != AsyncOperationStatus.Succeeded || !instance)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
            return null;
        }

        _instances[instance] = handle;
        return instance;
    }

    public async Task<GameObject> InstantiateAsync(
        string resourceId,
        Vector3 position,
        Quaternion rotation,
        Transform parent = null)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        await InitializeAsync();
        AsyncOperationHandle<GameObject> handle = Addressables.InstantiateAsync(
            resourceId,
            position,
            rotation,
            parent,
            true);

        GameObject instance = await handle.Task;
        if (handle.Status != AsyncOperationStatus.Succeeded || !instance)
        {
            if (handle.IsValid())
            {
                Addressables.Release(handle);
            }
            return null;
        }

        _instances[instance] = handle;
        return instance;
    }

    public bool ReleaseInstance(GameObject instance)
    {
        if (!instance || !_instances.TryGetValue(instance, out AsyncOperationHandle<GameObject> handle))
        {
            return false;
        }

        _instances.Remove(instance);
        return Addressables.ReleaseInstance(handle);
    }

    public async Task<IReadOnlyList<Object>> PreloadLabelAsync(string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return System.Array.Empty<Object>();
        }

        await InitializeAsync();
        if (_labels.TryGetValue(label, out LabelRecord existing))
        {
            existing.ReferenceCount++;
            return await existing.Task;
        }

        AsyncOperationHandle<IList<Object>> handle = Addressables.LoadAssetsAsync<Object>(label, null);
        LabelRecord record = new LabelRecord
        {
            Handle = handle,
            ReferenceCount = 1
        };
        record.Task = CompleteLabelLoadAsync(label, record);
        _labels.Add(label, record);
        return await record.Task;
    }

    public void ReleaseLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label) || !_labels.TryGetValue(label, out LabelRecord record))
        {
            return;
        }

        record.ReferenceCount--;
        if (record.ReferenceCount > 0)
        {
            return;
        }

        _labels.Remove(label);
        if (record.Handle.IsValid())
        {
            Addressables.Release(record.Handle);
        }
    }

    public async Task<SceneInstance> LoadSceneAsync(
        string resourceId,
        LoadSceneMode loadMode = LoadSceneMode.Single,
        bool activateOnLoad = true,
        int priority = 100)
    {
        await InitializeAsync();
        AsyncOperationHandle<SceneInstance> handle = Addressables.LoadSceneAsync(
            resourceId,
            loadMode,
            activateOnLoad,
            priority);
        SceneInstance scene = await handle.Task;
        if (handle.Status == AsyncOperationStatus.Succeeded)
        {
            _scenes[scene.Scene.handle] = handle;
        }
        return scene;
    }

    public async Task<bool> UnloadSceneAsync(SceneInstance scene)
    {
        int sceneHandle = scene.Scene.handle;
        if (!_scenes.TryGetValue(sceneHandle, out AsyncOperationHandle<SceneInstance> handle))
        {
            return false;
        }

        _scenes.Remove(sceneHandle);
        AsyncOperationHandle<SceneInstance> unload = Addressables.UnloadSceneAsync(handle, true);
        await unload.Task;
        return unload.Status == AsyncOperationStatus.Succeeded;
    }

    private async Task InitializeInternalAsync()
    {
        AsyncOperationHandle<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator> handle =
            Addressables.InitializeAsync(false);
        await handle.Task;
        if (handle.Status != AsyncOperationStatus.Succeeded)
        {
            throw handle.OperationException;
        }

        Addressables.Release(handle);
    }

    private async Task<Object> CompleteLoadAsync(ResourceKey key, ResourceRecord record)
    {
        await record.Handle.Task;
        if (record.Handle.Status == AsyncOperationStatus.Succeeded)
        {
            return record.Handle.Result as Object;
        }

        _resources.Remove(key);
        System.Exception exception = record.Handle.OperationException;
        if (record.Handle.IsValid())
        {
            Addressables.Release(record.Handle);
        }
        throw exception;
    }

    private async Task<IReadOnlyList<Object>> CompleteLabelLoadAsync(string label, LabelRecord record)
    {
        await record.Handle.Task;
        if (record.Handle.Status == AsyncOperationStatus.Succeeded)
        {
            return new List<Object>(record.Handle.Result);
        }

        _labels.Remove(label);
        System.Exception exception = record.Handle.OperationException;
        if (record.Handle.IsValid())
        {
            Addressables.Release(record.Handle);
        }
        throw exception;
    }

    private void Release(ResourceKey key)
    {
        if (!_resources.TryGetValue(key, out ResourceRecord record))
        {
            return;
        }

        record.ReferenceCount--;
        if (record.ReferenceCount > 0)
        {
            return;
        }

        _resources.Remove(key);
        if (record.Handle.IsValid())
        {
            Addressables.Release(record.Handle);
        }
    }

    private void OnDestroy()
    {
        foreach (ResourceRecord record in _resources.Values)
        {
            if (record.Handle.IsValid())
            {
                Addressables.Release(record.Handle);
            }
        }

        foreach (LabelRecord record in _labels.Values)
        {
            if (record.Handle.IsValid())
            {
                Addressables.Release(record.Handle);
            }
        }

        foreach (AsyncOperationHandle<GameObject> handle in _instances.Values)
        {
            if (handle.IsValid())
            {
                Addressables.ReleaseInstance(handle);
            }
        }

        _resources.Clear();
        _labels.Clear();
        _instances.Clear();
        _scenes.Clear();
    }

    private readonly struct ResourceKey
    {
        public string ResourceId { get; }
        public System.Type ResourceType { get; }

        public ResourceKey(string resourceId, System.Type resourceType)
        {
            ResourceId = resourceId;
            ResourceType = resourceType;
        }

        public override bool Equals(object obj)
        {
            return obj is ResourceKey other &&
                ResourceId == other.ResourceId &&
                ResourceType == other.ResourceType;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((ResourceId != null ? ResourceId.GetHashCode() : 0) * 397) ^
                    (ResourceType != null ? ResourceType.GetHashCode() : 0);
            }
        }
    }

    private sealed class ResourceRecord
    {
        public AsyncOperationHandle Handle;
        public Task<Object> Task;
        public int ReferenceCount;
    }

    private sealed class LabelRecord
    {
        public AsyncOperationHandle<IList<Object>> Handle;
        public Task<IReadOnlyList<Object>> Task;
        public int ReferenceCount;
    }
}
