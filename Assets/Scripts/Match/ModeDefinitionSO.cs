using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ModeDefinition", menuName = "GunDemo/对局/模式定义")]
public sealed class ModeDefinitionSO : ScriptableObject
{
    [Header("身份")]
    [SerializeField, InspectorName("模式类型")] private ModeKind _kind;
    [SerializeField, InspectorName("稳定模式 ID")] private string _modeId = "mode.custom";
    [SerializeField, InspectorName("显示名称")] private string _displayName = "自定义模式";
    [SerializeField, InspectorName("内置模式")] private bool _isBuiltIn;

    [Header("队伍与复活")]
    [SerializeField, Min(1), InspectorName("队伍数量")] private int _teamCount = 1;
    [SerializeField, Min(1), InspectorName("每队人数")] private int _teamSize = 1;
    [SerializeField, InspectorName("允许复活")] private bool _respawnEnabled = true;
    [SerializeField, Min(0f), InspectorName("复活延迟")] private float _respawnDelay = 5f;

    [Header("胜负")]
    [SerializeField, InspectorName("胜利条件")] private WinConditionType _winCondition;
    [SerializeField, Min(1), InspectorName("胜利分数")] private int _scoreToWin = 30;
    [SerializeField, Min(0f), InspectorName("回合时限")] private float _roundTimeLimit = 300f;

    [Header("扩展")]
    [SerializeField, ResourceId(typeof(Texture2D)), InspectorName("模式 Logo 资源 ID")]
    private string _logoResourceId = string.Empty;
    [SerializeField, InspectorName("AI 模式补丁 ID")] private string _aiPatchId = string.Empty;
    [SerializeField, InspectorName("配置项")] private List<ModeConfigItem> _items =
        new List<ModeConfigItem>();

    public ModeKind Kind { get { return _kind; } }
    public string ModeId { get { return _modeId; } }
    public string DisplayName { get { return _displayName; } }
    public bool IsBuiltIn { get { return _isBuiltIn; } }
    public int TeamCount { get { return _teamCount; } }
    public int TeamSize { get { return _teamSize; } }
    public bool RespawnEnabled { get { return _respawnEnabled; } }
    public float RespawnDelay { get { return _respawnDelay; } }
    public WinConditionType WinCondition { get { return _winCondition; } }
    public int ScoreToWin { get { return _scoreToWin; } }
    public float RoundTimeLimit { get { return _roundTimeLimit; } }
    public string LogoResourceId { get { return _logoResourceId; } }
    public string AIPatchId { get { return _aiPatchId; } }
    public IReadOnlyList<ModeConfigItem> Items { get { return _items; } }

    public void ConfigureBuiltIn(
        ModeKind kind,
        string modeId,
        string displayName,
        int teamCount,
        int teamSize,
        bool respawnEnabled,
        float respawnDelay,
        WinConditionType winCondition,
        int scoreToWin,
        float roundTimeLimit,
        string logoResourceId,
        string aiPatchId,
        IEnumerable<ModeConfigItem> items)
    {
        _kind = kind;
        _modeId = modeId;
        _displayName = displayName;
        _isBuiltIn = true;
        _teamCount = Mathf.Max(1, teamCount);
        _teamSize = Mathf.Max(1, teamSize);
        _respawnEnabled = respawnEnabled;
        _respawnDelay = Mathf.Max(0f, respawnDelay);
        _winCondition = winCondition;
        _scoreToWin = Mathf.Max(1, scoreToWin);
        _roundTimeLimit = Mathf.Max(0f, roundTimeLimit);
        _logoResourceId = logoResourceId ?? string.Empty;
        _aiPatchId = aiPatchId ?? string.Empty;
        _items = items != null
            ? new List<ModeConfigItem>(items)
            : new List<ModeConfigItem>();
        for (int i = 0; i < _items.Count; i++)
        {
            _items[i]?.LockRemoval();
        }
    }

    private void OnValidate()
    {
        _modeId = string.IsNullOrWhiteSpace(_modeId) ? "mode.custom" : _modeId.Trim();
        _displayName = string.IsNullOrWhiteSpace(_displayName) ? _modeId : _displayName.Trim();
        _teamCount = Mathf.Max(1, _teamCount);
        _teamSize = Mathf.Max(1, _teamSize);
        _respawnDelay = Mathf.Max(0f, _respawnDelay);
        _scoreToWin = Mathf.Max(1, _scoreToWin);
        _roundTimeLimit = Mathf.Max(0f, _roundTimeLimit);
        _logoResourceId = _logoResourceId == null ? string.Empty : _logoResourceId.Trim();
        if (_items == null)
        {
            _items = new List<ModeConfigItem>();
        }

        if (!_isBuiltIn)
        {
            return;
        }

        for (int i = 0; i < _items.Count; i++)
        {
            _items[i]?.LockRemoval();
        }
    }
}
