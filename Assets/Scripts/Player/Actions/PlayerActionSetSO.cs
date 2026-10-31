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

    public GJInputAction Action
    {
        get
        {
            return _action;
        }
    }

    public TimelineAsset EnterTimeline
    {
        get
        {
            return _enterTimeline;
        }
    }

    public TimelineAsset ExitTimeline
    {
        get
        {
            return _exitTimeline;
        }
    }
}

[CreateAssetMenu(fileName = "PlayerActionSet", menuName = "Gun Demo/玩家/动作 Timeline 集")]
public class PlayerActionSetSO : ScriptableObject
{
    [Header("动作 Timeline")]
    [SerializeField] private List<PlayerActionTimelineData> _actions = new List<PlayerActionTimelineData>();

    public TimelineAsset Get(GJInputAction action, PlayerInputPhase phase)
    {
        PlayerActionTimelineData data = null;
        for (int i = 0; i < _actions.Count; i++)
        {
            if (_actions[i].Action == action)
            {
                data = _actions[i];
                break;
            }
        }
        if (data == null)
        {
            return null;
        }

        return phase == PlayerInputPhase.Canceled ? data.ExitTimeline : data.EnterTimeline;
    }
}
