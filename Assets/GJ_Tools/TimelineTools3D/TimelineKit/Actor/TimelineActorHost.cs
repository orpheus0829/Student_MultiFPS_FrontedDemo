using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Playables;

[AddComponentMenu("TimelineKit/TimelineActorHost")]
[RequireComponent(typeof(PlayableDirector))]
public class TimelineActorHost : MonoBehaviour, ITimelineHitHost, ITimelineEffectHost
{
    [Header("命中扫描")]
    [Tooltip("判定发射锚点。命中盒从这里往外扫，留空则用角色自身位置")]
    public Transform attackPoint;
    [Tooltip("可命中的物理层。默认所有层，建议只勾敌人所在的层")]
    public LayerMask targetMask = ~0;
    [Tooltip("忽略的标签（例如不误伤友军/自身所属标签）")]
    public string[] ignoreTags;
    [Tooltip("命中后是否把目标刚体沿水平方向击退")]
    public bool useHitForce = true;
    [Tooltip("自动排除自身及所有子物体上的碰撞体，避免自伤")]
    public bool autoExcludeSelf = true;
    [Tooltip("只对能受伤的目标生效：自动跳过墙壁、地面等没有伤害能力的碰撞体，免去配置命中层")]
    public bool ignoreNonDamageable = true;

    [Header("音效")]
    [Tooltip("特效音效轨播放音效用的 AudioSource。留空会自动查找，找不到会自动创建")]
    public AudioSource sfxSource;
    [Tooltip("每次播放随机微调音调，避免重复触发太刺耳")]
    public bool randomizePitch = false;
    [Tooltip("随机音调范围")]
    public Vector2 pitchRange = new Vector2(0.95f, 1.05f);

    public event Action<bool> onHitWindowChanged;
    public event Action<HitInfo> onHitTarget;
    public event Action<TimelineHitData> onHitDataChanged;

    public struct HitInfo
    {
        public Collider target;
        public Vector3 point;
        public float damage;
    }

    public bool HasHitWindow => _inWindow;

    public TimelineHitData CurrentData => _data;

    private TimelineHitData _data;
    private bool _inWindow;
    private readonly List<Collider> _selfColliders = new List<Collider>();
    private readonly HashSet<Collider> _hitThisWindow = new HashSet<Collider>();

    private void Awake()
    {
        EnsureSfxSource();
        if (autoExcludeSelf)
        {
            GetComponentsInChildren(true, _selfColliders);
        }
    }

    private void EnsureSfxSource()
    {
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
        }
        sfxSource.playOnAwake = false;
    }

    private void OnEnable()
    {
        if (autoExcludeSelf && _selfColliders.Count == 0)
        {
            GetComponentsInChildren(true, _selfColliders);
        }
    }

    public void RefreshSelfColliders()
    {
        _selfColliders.Clear();
        if (autoExcludeSelf)
        {
            GetComponentsInChildren(true, _selfColliders);
        }
    }

    public void SetHitBox(TimelineHitData data)
    {
        _data = data;
        _inWindow = true;
        _hitThisWindow.Clear();
        onHitDataChanged?.Invoke(data);
        onHitWindowChanged?.Invoke(true);
    }

    public void ClearHitBox()
    {
        _inWindow = false;
        _hitThisWindow.Clear();
        onHitWindowChanged?.Invoke(false);
    }

    public void DoHitScan()
    {
        if (!_inWindow || _data.damage <= 0f)
        {
            return;
        }
        Transform pivot = attackPoint != null ? attackPoint : transform;
        Vector3 center = pivot.TransformPoint(_data.boxOffset);
        List<Collider> hits = new List<Collider>();
        if (_data.hitBoxShape == HitBoxShape.Sphere)
        {
            hits.AddRange(Physics.OverlapSphere(center, _data.boxRadius, targetMask));
        }
        else if (_data.hitBoxShape == HitBoxShape.Box)
        {
            hits.AddRange(Physics.OverlapBox(center, _data.hitBoxSize * 0.5f, pivot.rotation * Quaternion.Euler(_data.boxEuler), targetMask));
        }
        else if (_data.hitBoxShape == HitBoxShape.Sector)
        {
            Quaternion boxRot = pivot.rotation * Quaternion.Euler(_data.boxEuler);
            Vector3 fwd = boxRot * Vector3.forward;
            Vector3 up = boxRot * Vector3.up;
            Vector3 right = Vector3.Cross(fwd, up);
            float halfAngle = _data.sectorAngle <= 0f ? 180f : _data.sectorAngle * 0.5f;
            float outer = Mathf.Max(0.001f, _data.boxRadius);
            float inner = Mathf.Clamp(_data.sectorInnerRadius, 0f, Mathf.Max(0f, outer - 0.01f));
            float halfH = Mathf.Max(0f, _data.sectorHeight) * 0.5f;
            float scanRadius = Mathf.Sqrt(outer * outer + halfH * halfH) + 0.1f;
            Vector3 midProbe = center + fwd * outer;
            Vector3 topProbe = center + up * halfH;
            Vector3 bottomProbe = center - up * halfH;
            foreach (Collider c in Physics.OverlapSphere(center, scanRadius, targetMask))
            {
                if (IsInsideSectorProbe(c, center, center, fwd, up, right, outer, inner, halfH, halfAngle)
                    || IsInsideSectorProbe(c, center, midProbe, fwd, up, right, outer, inner, halfH, halfAngle)
                    || IsInsideSectorProbe(c, center, topProbe, fwd, up, right, outer, inner, halfH, halfAngle)
                    || IsInsideSectorProbe(c, center, bottomProbe, fwd, up, right, outer, inner, halfH, halfAngle))
                {
                    hits.Add(c);
                }
            }
        }

        foreach (Collider c in hits)
        {
            if (!IsAllowed(c) || !_hitThisWindow.Add(c))
            {
                continue;
            }
            IDamageable dmg = c.GetComponentInParent<IDamageable>();
            if (ignoreNonDamageable && dmg == null)
            {
                continue;
            }
            if (dmg != null)
            {
                dmg.TakeDamage(_data.damage);
            }
            if (useHitForce && _data.hitForce > 0f)
            {
                Rigidbody rb = c.attachedRigidbody;
                if (rb != null)
                {
                    Vector3 dir = (c.transform.position - pivot.position).normalized;
                    dir.y = 0f;
                    if (dir.sqrMagnitude < 0.0001f)
                    {
                        dir = pivot.forward;
                    }
                    rb.AddForce(dir * _data.hitForce, ForceMode.Impulse);
                }
            }
            HitInfo info = new HitInfo
            {
                target = c,
                point = c.ClosestPoint(center),
                damage = _data.damage
            };
            onHitTarget?.Invoke(info);
        }
    }

    private static bool IsInsideSectorProbe(Collider c, Vector3 center, Vector3 probe, Vector3 fwd, Vector3 up, Vector3 right, float outer, float inner, float halfH, float halfAngle)
    {
        Vector3 d = c.ClosestPoint(probe) - center;
        float x = Vector3.Dot(d, right);
        float y = Vector3.Dot(d, up);
        float z = Vector3.Dot(d, fwd);
        if (Mathf.Abs(y) > halfH + 0.02f)
        {
            return false;
        }
        float radius = Mathf.Sqrt(x * x + z * z);
        if (radius < inner - 0.02f || radius > outer + 0.02f)
        {
            return false;
        }
        if (radius > 0.001f)
        {
            float ang = Mathf.Abs(Mathf.Atan2(x, z) * Mathf.Rad2Deg);
            if (ang > halfAngle + 0.5f)
            {
                return false;
            }
        }
        return true;
    }

    private bool IsAllowed(Collider c)
    {
        if (autoExcludeSelf && _selfColliders.Contains(c))
        {
            return false;
        }
        if (ignoreTags != null)
        {
            foreach (string t in ignoreTags)
            {
                if (c.CompareTag(t))
                {
                    return false;
                }
            }
        }
        return true;
    }

    public void PlaySound(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }
        if (sfxSource == null)
        {
            EnsureSfxSource();
        }
        if (sfxSource == null)
        {
            return;
        }
        float oldPitch = sfxSource.pitch;
        if (randomizePitch)
        {
            sfxSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        }
        sfxSource.PlayOneShot(clip);
        sfxSource.pitch = oldPitch;
    }

    private static bool s_poolSearched;
    private static object s_poolInstance;
    private static MethodInfo s_poolSpawn;
    private static MethodInfo s_poolDespawn;

    private static void ResolvePool()
    {
        if (s_poolSearched)
        {
            return;
        }
        s_poolSearched = true;
        Type poolType = FindTypeByName("ObjectPool");
        if (poolType == null)
        {
            return;
        }
        PropertyInfo instanceProperty = poolType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static);
        if (instanceProperty != null)
        {
            s_poolInstance = instanceProperty.GetValue(null);
        }
        if (s_poolInstance == null)
        {
            s_poolInstance = UnityEngine.Object.FindFirstObjectByType(poolType);
        }
        if (s_poolInstance == null)
        {
            return;
        }
        s_poolSpawn = poolType.GetMethod("Spawn", new[] { typeof(GameObject), typeof(Vector3), typeof(Quaternion) });
        s_poolDespawn = poolType.GetMethod("Despawn", new[] { typeof(GameObject) });
    }

    private static Type FindTypeByName(string typeName)
    {
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type found = assembly.GetType(typeName);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }

    public GameObject SpawnEffect(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (prefab == null)
        {
            return null;
        }
        ResolvePool();
        if (s_poolSpawn != null && s_poolInstance != null)
        {
            try
            {
                GameObject pooled = s_poolSpawn.Invoke(s_poolInstance, new object[] { prefab, position, rotation }) as GameObject;
                if (pooled != null)
                {
                    return pooled;
                }
            }
            catch (Exception)
            {
            }
        }
        return Instantiate(prefab, position, rotation);
    }

    public void RecycleEffect(GameObject effect)
    {
        if (effect == null)
        {
            return;
        }
        ResolvePool();
        if (s_poolDespawn != null && s_poolInstance != null)
        {
            try
            {
                s_poolDespawn.Invoke(s_poolInstance, new object[] { effect });
                return;
            }
            catch (Exception)
            {
            }
        }
        Destroy(effect);
    }
}
