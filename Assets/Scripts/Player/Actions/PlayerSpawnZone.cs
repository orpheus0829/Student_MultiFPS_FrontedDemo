using System;
using System.Collections.Generic;
using UnityEngine;

public enum PlayerSpawnZoneMode
{
    [InspectorName("AI 玩家复活区")] Ai = 0,
    [InspectorName("玩家复活区（预留绑定）")] Player = 1
}

public enum AICharacterModel
{
    [InspectorName("随机模型")] Random = 0,
    [InspectorName("安比")] Anbi = 1,
    [InspectorName("艾莲")] Ellen = 2,
    [InspectorName("朱鸢")] ZhuYuan = 3,
    [InspectorName("女主")] Belle = 4
}

[Serializable]
public sealed class AISpawnDefinition
{
    [SerializeField, InspectorName("角色模型")] private AICharacterModel _model;
    [SerializeField, InspectorName("AI 难度")] private AIDifficulty _difficulty = AIDifficulty.Normal;

    public AICharacterModel Model { get { return _model; } }
    public AIDifficulty Difficulty { get { return _difficulty; } }
}

[DisallowMultipleComponent]
public sealed class PlayerSpawnZone : MonoBehaviour
{
    public event Action<CharacterHealth, int> Spawned;
    [Header("区域")]
    [SerializeField, InspectorName("复活区模式")] private PlayerSpawnZoneMode _mode;
    [SerializeField, InspectorName("区域尺寸")] private Vector3 _size = new Vector3(10f, 3f, 10f);
    [SerializeField, InspectorName("地面层")] private LayerMask _groundMask = ~0;
    [SerializeField, Min(0f), InspectorName("离地高度")] private float _groundOffset = 0.15f;
    [SerializeField, Min(0), InspectorName("对局槽位起始编号")]
    private int _matchSlotOffset;

    [Header("AI 玩家")]
    [SerializeField, InspectorName("AI 玩家预制体资源 ID")]
    private string _aiPrefabResourceId = "character.ai_player.prefab";
    [SerializeField, Min(0), InspectorName("同时存活的 AI 数量")] private int _aiCount = 4;
    [SerializeField, InspectorName("逐个人机配置")]
    private List<AISpawnDefinition> _aiDefinitions = new List<AISpawnDefinition>();
    private readonly HashSet<GameObject> _instances = new HashSet<GameObject>();
    private readonly Dictionary<CharacterHealth, GameObject> _living =
        new Dictionary<CharacterHealth, GameObject>();
    private readonly HashSet<CharacterHealth> _respawning =
        new HashSet<CharacterHealth>();
    private ResourceMgr _resources;
    private bool _isClosing;
    private int _nextNameIndex;

    public PlayerSpawnZoneMode Mode
    {
        get { return _mode; }
    }

    public int MatchSlotOffset { get { return _matchSlotOffset; } }

    public bool OwnsMatchSlot(int slot)
    {
        if (slot < _matchSlotOffset)
        {
            return false;
        }

        int capacity = _mode == PlayerSpawnZoneMode.Ai ? Mathf.Max(1, _aiCount) : 1;
        return slot < _matchSlotOffset + capacity;
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
            SpawnAi(i);
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
        _respawning.Clear();
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

    private async void SpawnAi(int slotIndex)
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
        AIPlayerCtrl aiCtrl = instance.GetComponent<AIPlayerCtrl>();
        PlayerCharacterModelCtrl models = instance.GetComponent<PlayerCharacterModelCtrl>();
        if (!health || !life || !aiCtrl || !models)
        {
            Debug.LogError($"{name} 的 AI 玩家预制体缺少生命、模型或 AI 马夫。", instance);
            _resources.ReleaseInstance(instance);
            return;
        }

        AISpawnDefinition definition = GetDefinition(slotIndex);
        aiCtrl.SetDifficulty(definition.Difficulty);
        string modelId = ResolveModelId(models, definition);
        if (!string.IsNullOrWhiteSpace(modelId))
        {
            await models.SwitchModelAsync(modelId);
        }
        if (_isClosing || !instance)
        {
            if (instance)
            {
                _resources.ReleaseInstance(instance);
            }
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
        Spawned?.Invoke(health, _matchSlotOffset + slotIndex);
    }

    private void HandleAiDied()
    {
        CharacterHealth deadHealth = null;
        GameObject deadObject = null;
        foreach (KeyValuePair<CharacterHealth, GameObject> entry in _living)
        {
            if (entry.Key && entry.Key.IsDead && !_respawning.Contains(entry.Key))
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

        PlayerLifeCtrl life = deadObject.GetComponent<PlayerLifeCtrl>();
        if (!life || !life.AutoRespawnEnabled)
        {
            return;
        }

        _respawning.Add(deadHealth);
        float delay = life ? life.RespawnDelay : 5f;
        SmartTimer.instance.SetTimer(this, delay, () => RespawnAi(deadHealth));
    }

    private AISpawnDefinition GetDefinition(int slotIndex)
    {
        if (slotIndex >= 0 && slotIndex < _aiDefinitions.Count &&
            _aiDefinitions[slotIndex] != null)
        {
            return _aiDefinitions[slotIndex];
        }

        return new AISpawnDefinition();
    }

    private static string ResolveModelId(
        PlayerCharacterModelCtrl models,
        AISpawnDefinition definition)
    {
        switch (definition.Model)
        {
            case AICharacterModel.Anbi:
                return "character.anbi";
            case AICharacterModel.Ellen:
                return "character.ellen";
            case AICharacterModel.ZhuYuan:
                return "character.zhuyuan";
            case AICharacterModel.Belle:
                return "character.belle";
        }
        if (!models || models.ModelCount <= 0)
        {
            return string.Empty;
        }

        return models.GetModelId(UnityEngine.Random.Range(0, models.ModelCount));
    }

    private void OnValidate()
    {
        _aiCount = Mathf.Max(0, _aiCount);
        _matchSlotOffset = Mathf.Max(0, _matchSlotOffset);
        if (_aiDefinitions == null)
        {
            _aiDefinitions = new List<AISpawnDefinition>();
        }

        while (_aiDefinitions.Count < _aiCount)
        {
            _aiDefinitions.Add(new AISpawnDefinition());
        }
        while (_aiDefinitions.Count > _aiCount)
        {
            _aiDefinitions.RemoveAt(_aiDefinitions.Count - 1);
        }
    }

    private void RespawnAi(CharacterHealth health)
    {
        if (_isClosing || !health || !_living.TryGetValue(health, out GameObject instance))
        {
            _respawning.Remove(health);
            return;
        }

        PlayerLifeCtrl life = instance.GetComponent<PlayerLifeCtrl>();
        if (!life || !life.AutoRespawnEnabled)
        {
            _respawning.Remove(health);
            return;
        }
        if (!life.TryRespawn())
        {
            Debug.LogError($"{name} 无法复活 AI 玩家 {instance.name}。", instance);
        }
        _respawning.Remove(health);
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
