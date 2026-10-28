using System.Collections.Generic;
using UnityEngine;

public class ObjectPool : BaseMgr<ObjectPool>
{
    protected override bool Ddol => true;

    private readonly Dictionary<GameObject, Bucket> _buckets = new Dictionary<GameObject, Bucket>();
    private readonly List<GameObject> _reparentQueue = new List<GameObject>();
    private Transform _pools;

    private Transform Pools
    {
        get
        {
            if (!_pools)
            {
                _pools = new GameObject("Pools").transform;
                _pools.SetParent(transform, false);
                _pools.gameObject.SetActive(false);
            }
            return _pools;
        }
    }

    private void Update()
    {
        if (_reparentQueue.Count == 0)
        {
            return;
        }
        foreach (GameObject go in _reparentQueue)
        {
            if (!go)
            {
                continue;
            }
            PooledItem pooled = go.GetComponent<PooledItem>();
            if (pooled == null || !pooled.InPool)
            {
                continue;
            }
            Transform t = go.transform;
            t.SetParent(null, false);
            if (pooled.Bucket != null)
            {
                t.SetParent(pooled.Bucket.Root, false);
            }
        }
        _reparentQueue.Clear();
    }

    private void EnqueueReparent(GameObject go)
    {
        _reparentQueue.Add(go);
    }

    public GameObject Spawn(GameObject prefab)
    {
        return SpawnGo(prefab, null, null, null);
    }

    public GameObject Spawn(GameObject prefab, Vector3 position)
    {
        return SpawnGo(prefab, position, null, null);
    }

    public GameObject Spawn(GameObject prefab, Quaternion rotation)
    {
        return SpawnGo(prefab, null, rotation, null);
    }

    public GameObject Spawn(GameObject prefab, Transform parent)
    {
        return SpawnGo(prefab, null, null, parent);
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        return SpawnGo(prefab, position, rotation, null);
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Transform parent)
    {
        return SpawnGo(prefab, position, null, parent);
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        return SpawnGo(prefab, position, rotation, parent);
    }

    public T Spawn<T>(T prefab) where T : Component
    {
        return SpawnComponent(prefab, null, null, null);
    }

    public T Spawn<T>(T prefab, Vector3 position) where T : Component
    {
        return SpawnComponent(prefab, position, null, null);
    }

    public T Spawn<T>(T prefab, Quaternion rotation) where T : Component
    {
        return SpawnComponent(prefab, null, rotation, null);
    }

    public T Spawn<T>(T prefab, Transform parent) where T : Component
    {
        return SpawnComponent(prefab, null, null, parent);
    }

    public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation) where T : Component
    {
        return SpawnComponent(prefab, position, rotation, null);
    }

    public T Spawn<T>(T prefab, Vector3 position, Transform parent) where T : Component
    {
        return SpawnComponent(prefab, position, null, parent);
    }

    public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent) where T : Component
    {
        return SpawnComponent(prefab, position, rotation, parent);
    }

    public void Despawn(GameObject item)
    {
        if (item)
        {
            item.SetActive(false);
        }
    }

    public void Despawn(Component item)
    {
        if (item)
        {
            Despawn(item.gameObject);
        }
    }

    public void Warmup(GameObject prefab, int count)
    {
        if (!prefab)
        {
            return;
        }
        Bucket bucket = GetBucket(prefab);
        int need = count - bucket.Free.Count;
        for (int i = 0; i < need; i++)
        {
            GameObject go = Instantiate(prefab, bucket.Root, false);
            PooledItem pooled = go.AddComponent<PooledItem>();
            pooled.Bucket = bucket;
            pooled.InPool = true;
            go.SetActive(false);
            bucket.Free.Add(go);
        }
    }

    public void Warmup<T>(T prefab, int count) where T : Component
    {
        if (prefab)
        {
            Warmup(prefab.gameObject, count);
        }
    }

    public int GetIdleCount(GameObject prefab)
    {
        if (!prefab)
        {
            return 0;
        }
        Bucket bucket;
        if (_buckets.TryGetValue(prefab, out bucket))
        {
            return bucket.Free.Count;
        }
        return 0;
    }

    public int GetIdleCount<T>(T prefab) where T : Component
    {
        if (!prefab)
        {
            return 0;
        }
        return GetIdleCount(prefab.gameObject);
    }

    public void ClearPool(GameObject prefab)
    {
        if (!prefab)
        {
            return;
        }
        Bucket bucket;
        if (_buckets.TryGetValue(prefab, out bucket))
        {
            _buckets.Remove(prefab);
            if (bucket.Root)
            {
                Destroy(bucket.Root.gameObject);
            }
        }
    }

    public void ClearPool<T>(T prefab) where T : Component
    {
        if (prefab)
        {
            ClearPool(prefab.gameObject);
        }
    }

    public void ClearAll()
    {
        foreach (KeyValuePair<GameObject, Bucket> pair in _buckets)
        {
            if (pair.Value.Root)
            {
                Destroy(pair.Value.Root.gameObject);
            }
        }
        _buckets.Clear();
    }

    private T SpawnComponent<T>(T prefab, Vector3? position, Quaternion? rotation, Transform parent) where T : Component
    {
        if (!prefab)
        {
            Debug.LogWarning("[ObjectPool] prefab 无效");
            return null;
        }
        GameObject go = SpawnGo(prefab.gameObject, position, rotation, parent);
        if (!go)
        {
            return null;
        }
        return go.GetComponent<T>();
    }

    private GameObject SpawnGo(GameObject prefab, Vector3? position, Quaternion? rotation, Transform parent)
    {
        if (!prefab)
        {
            Debug.LogWarning("[ObjectPool] prefab 无效");
            return null;
        }
        Bucket bucket = GetBucket(prefab);
        GameObject go;
        if (bucket.Free.Count > 0)
        {
            go = bucket.Free[bucket.Free.Count - 1];
            bucket.Free.RemoveAt(bucket.Free.Count - 1);
        }
        else
        {
            go = Instantiate(prefab);
            go.AddComponent<PooledItem>().Bucket = bucket;
        }
        PooledItem pooled = go.GetComponent<PooledItem>();
        pooled.InPool = false;

        Transform t = go.transform;
        t.SetParent(null, false);
        if (parent)
        {
            t.SetParent(parent, false);
        }
        Vector3 pos = position ?? prefab.transform.localPosition;
        Quaternion rot = rotation ?? prefab.transform.localRotation;
        t.localPosition = pos;
        t.localRotation = rot;
        go.SetActive(true);
        return go;
    }

    internal sealed class Bucket
    {
        public ObjectPool Owner;
        public Transform Root;
        public List<GameObject> Free = new List<GameObject>();

        public void Return(GameObject go)
        {
            Free.Add(go);
            Owner.EnqueueReparent(go);
        }

        public void Forget(GameObject go)
        {
            Free.Remove(go);
        }
    }

    private Bucket GetBucket(GameObject prefab)
    {
        Bucket bucket;
        if (!_buckets.TryGetValue(prefab, out bucket))
        {
            bucket = new Bucket();
            bucket.Owner = this;
            bucket.Root = new GameObject(prefab.name + "_Pool").transform;
            bucket.Root.SetParent(Pools, false);
            _buckets.Add(prefab, bucket);
        }
        return bucket;
    }

    private sealed class PooledItem : MonoBehaviour
    {
        internal Bucket Bucket;
        internal bool InPool;

        private void OnDisable()
        {
            if (Bucket != null && !InPool)
            {
                Bucket.Return(gameObject);
                InPool = true;
            }
        }

        private void OnDestroy()
        {
            if (Bucket != null)
            {
                Bucket.Forget(gameObject);
            }
        }
    }
}
