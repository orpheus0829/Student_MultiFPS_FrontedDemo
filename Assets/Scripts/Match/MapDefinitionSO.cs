using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MapDefinition", menuName = "GunDemo/对局/地图定义")]
public sealed class MapDefinitionSO : ScriptableObject
{
    [Header("地图身份")]
    [SerializeField, InspectorName("稳定地图 ID")] private string _mapId = "map.custom";
    [SerializeField, InspectorName("显示名称")] private string _displayName = "自定义地图";
    [SerializeField, InspectorName("场景名称")] private string _sceneName = string.Empty;

    [Header("支持模式")]
    [SerializeField, InspectorName("可用模式")] private List<ModeKind> _supportedModes =
        new List<ModeKind>();

    public string MapId { get { return _mapId; } }
    public string DisplayName { get { return _displayName; } }
    public string SceneName { get { return _sceneName; } }
    public IReadOnlyList<ModeKind> SupportedModes { get { return _supportedModes; } }

    public bool SupportsMode(ModeKind mode)
    {
        return _supportedModes.Contains(mode);
    }

    public void Configure(
        string mapId,
        string displayName,
        string sceneName,
        IEnumerable<ModeKind> supportedModes)
    {
        _mapId = mapId ?? string.Empty;
        _displayName = displayName ?? string.Empty;
        _sceneName = sceneName ?? string.Empty;
        _supportedModes = supportedModes != null
            ? new List<ModeKind>(supportedModes)
            : new List<ModeKind>();
    }

    private void OnValidate()
    {
        _mapId = string.IsNullOrWhiteSpace(_mapId) ? "map.custom" : _mapId.Trim();
        _displayName = string.IsNullOrWhiteSpace(_displayName)
            ? _mapId
            : _displayName.Trim();
        _sceneName = _sceneName == null ? string.Empty : _sceneName.Trim();
        if (_supportedModes == null)
        {
            _supportedModes = new List<ModeKind>();
        }
    }
}
