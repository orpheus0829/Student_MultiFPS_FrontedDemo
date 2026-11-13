using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class MatchSlotConfig
{
    [SerializeField, Min(0), InspectorName("槽位编号")] private int _slot;
    [SerializeField, Min(0), InspectorName("队伍编号")] private int _team;
    [SerializeField, InspectorName("显示名称")] private string _displayName = string.Empty;
    [SerializeField, InspectorName("AI 玩家")] private bool _isAI;
    [SerializeField, InspectorName("本地玩家")] private bool _isLocalPlayer;
    [SerializeField, InspectorName("角色模型 ID")] private string _modelId = string.Empty;
    [SerializeField, InspectorName("AI 难度")] private AIDifficulty _difficulty = AIDifficulty.Normal;

    public int Slot { get { return _slot; } }
    public int Team { get { return _team; } }
    public string DisplayName { get { return _displayName; } }
    public bool IsAI { get { return _isAI; } }
    public bool IsLocalPlayer { get { return _isLocalPlayer; } }
    public string ModelId { get { return _modelId; } }
    public AIDifficulty Difficulty { get { return _difficulty; } }

    public MatchSlotConfig()
    {
    }

    public MatchSlotConfig(
        int slot,
        int team,
        string displayName,
        bool isAI,
        bool isLocalPlayer = false,
        string modelId = "",
        AIDifficulty difficulty = AIDifficulty.Normal)
    {
        _slot = Mathf.Max(0, slot);
        _team = Mathf.Max(0, team);
        _displayName = displayName ?? string.Empty;
        _isAI = isAI;
        _isLocalPlayer = isLocalPlayer;
        _modelId = modelId ?? string.Empty;
        _difficulty = difficulty;
    }
}

[Serializable]
public sealed class MatchConfig
{
    [SerializeField, InspectorName("模式定义")] private ModeDefinitionSO _mode;
    [SerializeField, InspectorName("名册槽位")] private List<MatchSlotConfig> _slots =
        new List<MatchSlotConfig>();

    private ModeConfigRegistry _registry;

    public ModeDefinitionSO Mode { get { return _mode; } }
    public IReadOnlyList<MatchSlotConfig> Slots { get { return _slots; } }

    public ModeConfigRegistry Registry
    {
        get
        {
            if (_registry == null)
            {
                _registry = new ModeConfigRegistry(_mode);
            }
            return _registry;
        }
    }

    public MatchConfig()
    {
    }

    public MatchConfig(ModeDefinitionSO mode, IEnumerable<MatchSlotConfig> slots = null)
    {
        _mode = mode;
        _slots = slots != null
            ? new List<MatchSlotConfig>(slots)
            : new List<MatchSlotConfig>();
        _registry = new ModeConfigRegistry(mode);
    }
}
