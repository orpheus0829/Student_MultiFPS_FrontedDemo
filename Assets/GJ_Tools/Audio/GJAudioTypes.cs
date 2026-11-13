using UnityEngine;

public enum GJAudioBus
{
    [InspectorName("音乐")] Music = 0,
    [InspectorName("角色语音")] CharacterVoice = 1,
    [InspectorName("战斗音效")] CombatSfx = 2,
    [InspectorName("反馈音效")] FeedbackSfx = 3
}

public enum GJAudioVolumeChannel
{
    [InspectorName("总音量")] Master = 0,
    [InspectorName("音乐")] Music = 1,
    [InspectorName("音效")] Sfx = 2,
    [InspectorName("角色语音")] CharacterVoice = 3,
    [InspectorName("战斗音效")] CombatSfx = 4,
    [InspectorName("反馈音效")] FeedbackSfx = 5
}
