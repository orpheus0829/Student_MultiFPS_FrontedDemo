using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerSpawnZoneMode
{
    [InspectorName("AI 玩家复活区")] Ai = 0,
    [InspectorName("玩家复活区（预留绑定）")] Player = 1
}

[DisallowMultipleComponent]
public sealed class PlayerSpawnZone : MonoBehaviour
{
    [Header("区域")]
    [SerializeField, InspectorName("复活区模式")] private PlayerSpawnZoneMode _mode;
    [SerializeField, InspectorName("区域尺寸")] private Vector3 _size = new Vector3(10f, 3f, 10f);
    [SerializeField, InspectorName("地面层")] private LayerMask _groundMask = ~0;
    [SerializeField, Min(0f), InspectorName("离地高度")] private float _groundOffset = 0.15f;

    [Header("AI 玩家")]
    [SerializeField, InspectorName("AI 玩家预制体资源 ID")]
    private string _aiPrefabResourceId = "character.ai_player.prefab";
    [SerializeField, Min(0), InspectorName("同时存活的 AI 数量")] private int _aiCount = 4;
    [SerializeField, Min(0f), InspectorName("尸体保留秒数")] private float _corpseLifetime = 8f;

    private readonly HashSet<GameObject> _instances = new HashSet<GameObject>();
    private readonly Dictionary<CharacterHealth, GameObject> _living =
        new Dictionary<CharacterHealth, GameObject>();
    private ResourceMgr _resources;
    private bool _isClosing;
    private int _nextNameIndex;

    public PlayerSpawnZoneMode Mode
    {
        get { return _mode; }
    }

    private void Start()
    {
        if (_mode != PlayerSpawnZoneMode.Ai)
        {
            return;
        }

        _resources = ResourceMgr.instance;
        for (int i = 0; i < _aiCount; i++)
        {
            SpawnAi();
        }
    }

    private void OnDestroy()
    {
        _isClosing = true;
        foreach (CharacterHealth health in _living.Keys)
        {
            if (health)
            {
                health.Died -= HandleAiDied;
            }
        }
        foreach (GameObject instance in _instances)
        {
            if (instance)
            {
                _resources?.ReleaseInstance(instance);
            }
        }
        _living.Clear();
        _instances.Clear();
    }

    public bool TryGetRandomPosition(out Vector3 position)
    {
        Vector3 local = new Vector3(
            UnityEngine.Random.Range(-_size.x * 0.5f, _size.x * 0.5f),
            _size.y * 0.5f + 2f,
            UnityEngine.Random.Range(-_size.z * 0.5f, _size.z * 0.5f));
        Vector3 origin = transform.TransformPoint(local);
        float length = Mathf.Max(5f, _size.y + 4f);
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down,
            length, _groundMask, QueryTriggerInteraction.Ignore);
        float closestDistance = float.PositiveInfinity;
        Vector3 groundPoint = Vector3.zero;
        for (int i = 0; i < hits.Length; i++)
        {
            RaycastHit hit = hits[i];
            if (hit.normal.y < 0.5f ||
                hit.collider.GetComponentInParent<CharacterHealth>())
            {
                continue;
            }

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                groundPoint = hit.point;
            }
        }

        if (closestDistance < float.PositiveInfinity)
        {
            position = groundPoint + Vector3.up * _groundOffset;
            return true;
        }

        position = transform.TransformPoint(new Vector3(local.x, 0f, local.z));
        return false;
    }

    private async void SpawnAi()
    {
        if (_isClosing || _mode != PlayerSpawnZoneMode.Ai ||
            string.IsNullOrWhiteSpace(_aiPrefabResourceId))
        {
            return;
        }

        TryGetRandomPosition(out Vector3 position);
        GameObject instance = null;
        try
        {
            instance = await _resources.InstantiateAsync(
                _aiPrefabResourceId, position, transform.rotation);
        }
        catch (Exception exception)
        {
            Debug.LogError($"{name} 生成 AI 玩家失败：{exception}", this);
            return;
        }

        if (!instance)
        {
            Debug.LogError($"{name} 找不到 AI 玩家资源 {_aiPrefabResourceId}。", this);
            return;
        }
        if (_isClosing)
        {
            _resources.ReleaseInstance(instance);
            return;
        }

        CharacterHealth health = instance.GetComponent<CharacterHealth>();
        PlayerLifeCtrl life = instance.GetComponent<PlayerLifeCtrl>();
        if (!health || !life)
        {
            Debug.LogError($"{name} 的 AI 玩家预制体缺少生命或死亡组件。", instance);
            _resources.ReleaseInstance(instance);
            return;
        }

        _instances.Add(instance);
        _living.Add(health, instance);
        health.Died += HandleAiDied;
        life.BindSpawnZone(this);
        PlayerInfo info = instance.GetComponent<PlayerInfo>();
        if (info)
        {
            info.SetPlayerName("AI 玩家 " + (++_nextNameIndex));
        }
    }

    private void HandleAiDied()
    {
        CharacterHealth deadHealth = null;
        GameObject deadObject = null;
        foreach (KeyValuePair<CharacterHealth, GameObject> entry in _living)
        {
            if (entry.Key && entry.Key.IsDead)
            {
                deadHealth = entry.Key;
                deadObject = entry.Value;
                break;
            }
        }
        if (!deadHealth)
        {
            return;
        }

        deadHealth.Died -= HandleAiDied;
        _living.Remove(deadHealth);
        PlayerLifeCtrl life = deadObject.GetComponent<PlayerLifeCtrl>();
        float delay = life ? life.RespawnDelay : 5f;
        SmartTimer.instance.SetTimer(this, delay, SpawnAi);
        SmartTimer.instance.SetTimer(this, Mathf.Max(delay, _corpseLifetime),
            () => ReleaseCorpse(deadObject));
    }

    private void ReleaseCorpse(GameObject corpse)
    {
        if (!corpse)
        {
            return;
        }
        _instances.Remove(corpse);
        _resources?.ReleaseInstance(corpse);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _mode == PlayerSpawnZoneMode.Ai
            ? new Color(1f, 0.35f, 0.2f, 0.75f)
            : new Color(0.25f, 0.75f, 1f, 0.75f);
        Matrix4x4 previous = Gizmos.matrix;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, _size);
        Gizmos.matrix = previous;
    }
}
