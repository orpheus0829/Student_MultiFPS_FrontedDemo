using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

[Serializable]
public class PlayerActionTimelineData
{
    [SerializeField] private GJInputAction _action;
    [SerializeField] private TimelineAsset _enterTimeline;
    [SerializeField] private TimelineAsset _exitTimeline;

    public GJInputAction Action => _action;
    public TimelineAsset EnterTimeline => _enterTimeline;
    public TimelineAsset ExitTimeline => _exitTimeline;
}

[CreateAssetMenu(fileName = "PlayerActionSet", menuName = "Gun Demo/玩家/动作 Timeline 集")]
public class PlayerActionSetSO : ScriptableObject
{
    [Header("动作 Timeline")]
    [SerializeField] private List<PlayerActionTimelineData> _actions = new List<PlayerActionTimelineData>();

    public TimelineAsset Get(GJInputAction action, PlayerInputPhase phase)
    {
        PlayerActionTimelineData data = _actions.Find(item => item.Action == action);
        if (data == null)
        {
            return null;
        }

        return phase == PlayerInputPhase.Canceled ? data.ExitTimeline : data.EnterTimeline;
    }
}
