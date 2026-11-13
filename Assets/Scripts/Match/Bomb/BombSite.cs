using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class BombSite : MonoBehaviour
{
    private static readonly List<BombSite> ActiveSites = new List<BombSite>();

    [Header("包点")]
    [SerializeField, InspectorName("包点 ID")] private string _siteId = "site.a";
    [SerializeField, InspectorName("显示名称")] private string _displayName = "A 点";
    [SerializeField, Min(0.5f), InspectorName("判定半径")] private float _radius = 3f;

    private Collider _area;

    public string SiteId { get { return _siteId; } }
    public string DisplayName { get { return _displayName; } }
    public float Radius { get { return _radius; } }
    public Vector3 Position { get { return transform.position; } }
    public static IReadOnlyList<BombSite> Sites { get { return ActiveSites; } }

    private void Awake()
    {
        if (!TryGetComponent(out _area))
        {
            Debug.LogError($"{name} 缺少包点区域 Collider。", this);
            enabled = false;
            return;
        }

        _area.isTrigger = true;
    }

    private void OnEnable()
    {
        if (!ActiveSites.Contains(this))
        {
            ActiveSites.Add(this);
        }
    }

    private void OnDisable()
    {
        ActiveSites.Remove(this);
    }

    public bool Contains(Vector3 worldPosition)
    {
        if (_area && _area.enabled)
        {
            Vector3 closest = _area.ClosestPoint(worldPosition);
            if ((closest - worldPosition).sqrMagnitude <= 0.0001f)
            {
                return true;
            }
        }

        Vector3 offset = worldPosition - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= _radius * _radius;
    }

    public void Configure(string siteId, string displayName, float radius)
    {
        _siteId = string.IsNullOrWhiteSpace(siteId) ? "site.unknown" : siteId.Trim();
        _displayName = string.IsNullOrWhiteSpace(displayName)
            ? _siteId
            : displayName.Trim();
        _radius = Mathf.Max(0.5f, radius);
        if (_area is SphereCollider sphere)
        {
            sphere.radius = _radius;
        }
    }

    private void OnValidate()
    {
        _siteId = string.IsNullOrWhiteSpace(_siteId) ? "site.unknown" : _siteId.Trim();
        _displayName = string.IsNullOrWhiteSpace(_displayName) ? _siteId : _displayName.Trim();
        _radius = Mathf.Max(0.5f, _radius);

        if (!TryGetComponent(out Collider area))
        {
            return;
        }

        area.isTrigger = true;
        if (area is SphereCollider sphere)
        {
            sphere.radius = _radius;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.65f, 0f, 0.35f);
        Gizmos.DrawSphere(transform.position, _radius);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveSites.Clear();
    }
}
