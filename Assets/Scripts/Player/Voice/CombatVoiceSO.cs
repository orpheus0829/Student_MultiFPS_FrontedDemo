using System;
using UnityEngine;

public enum CombatVoiceCue
{
    [InspectorName("换弹")] Reload = 0,
    [InspectorName("受伤")] Hurt = 1,
    [InspectorName("击杀")] Kill = 2,
    [InspectorName("三连杀")] TripleKill = 3,
    [InspectorName("复活")] Respawn = 4
}

[Serializable]
public sealed class CombatVoiceLine
{
    [SerializeField, InspectorName("字幕")] private string _subtitle;
    [SerializeField, ResourceId(typeof(AudioClip)), InspectorName("语音资源 ID（可留空）")]
    private string _audioResourceId;

    public string Subtitle { get { return _subtitle; } }
    public string AudioResourceId { get { return _audioResourceId; } }
}

[Serializable]
public sealed class CombatVoiceGroup
{
    [SerializeField, InspectorName("触发时机")] private CombatVoiceCue _cue;
    [SerializeField, Range(0f, 1f), InspectorName("触发概率")] private float _chance = 0.5f;
    [SerializeField, InspectorName("可选台词")] private CombatVoiceLine[] _lines = Array.Empty<CombatVoiceLine>();

    public CombatVoiceCue Cue { get { return _cue; } }
    public float Chance { get { return _chance; } }
    public CombatVoiceLine[] Lines { get { return _lines; } }
}

[CreateAssetMenu(fileName = "角色战斗语音", menuName = "GunDemo/角色/战斗语音盒")]
public sealed class CombatVoiceSO : ScriptableObject
{
    [Header("角色模型")]
    [SerializeField, InspectorName("角色模型 ID")] private string _characterModelId;

    [Header("接收范围")]
    [SerializeField, Min(0f), InspectorName("其他玩家可听距离（米）")]
    private float _hearingDistance = 24f;
    [SerializeField, Min(0.1f), InspectorName("无音频字幕显示时间（秒）")]
    private float _textDuration = 2.2f;
    [SerializeField, Min(0f), InspectorName("自动语音最短间隔（秒）")]
    private float _automaticCooldown = 6f;

    [Header("连杀")]
    [SerializeField, Min(1f), InspectorName("三连杀统计时间（秒）")]
    private float _streakWindow = 15f;

    [Header("语音")]
    [SerializeField, InspectorName("按事件分组的台词")]
    private CombatVoiceGroup[] _groups = Array.Empty<CombatVoiceGroup>();

    public string CharacterModelId { get { return _characterModelId; } }
    public float HearingDistance { get { return _hearingDistance; } }
    public float TextDuration { get { return _textDuration; } }
    public float AutomaticCooldown { get { return _automaticCooldown; } }
    public float StreakWindow { get { return _streakWindow; } }

    public bool TryPickLine(CombatVoiceCue cue, out CombatVoiceLine line)
    {
        line = null;
        for (int i = 0; i < _groups.Length; i++)
        {
            CombatVoiceGroup group = _groups[i];
            if (group == null || group.Cue != cue ||
                UnityEngine.Random.value > group.Chance)
            {
                continue;
            }
            return TryGetLine(group, out line);
        }

        return false;
    }

    public bool TryGetLine(CombatVoiceCue cue, out CombatVoiceLine line)
    {
        line = null;
        for (int i = 0; i < _groups.Length; i++)
        {
            CombatVoiceGroup group = _groups[i];
            if (group != null && group.Cue == cue)
            {
                return TryGetLine(group, out line);
            }
        }
        return false;
    }

    private static bool TryGetLine(CombatVoiceGroup group, out CombatVoiceLine line)
    {
        line = null;
        CombatVoiceLine[] lines = group.Lines;
        if (lines == null || lines.Length == 0)
        {
            return false;
        }
        line = lines[UnityEngine.Random.Range(0, lines.Length)];
        return line != null && !string.IsNullOrWhiteSpace(line.Subtitle);
    }

    private void OnValidate()
    {
        _hearingDistance = Mathf.Max(0f, _hearingDistance);
        _textDuration = Mathf.Max(0.1f, _textDuration);
        _automaticCooldown = Mathf.Max(0f, _automaticCooldown);
        _streakWindow = Mathf.Max(1f, _streakWindow);
    }
}
