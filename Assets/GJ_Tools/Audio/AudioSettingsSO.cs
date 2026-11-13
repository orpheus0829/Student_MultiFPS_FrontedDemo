using UnityEngine;

[CreateAssetMenu(fileName = "AudioSettings", menuName = "GJ Tools/音频/音量设置")]
public sealed class AudioSettingsSO : ScriptableObject
{
    [Header("父级音量")]
    [SerializeField, Range(0f, 1f), InspectorName("总音量")] private float _master = 1f;
    [SerializeField, Range(0f, 1f), InspectorName("音乐")] private float _music = 0.8f;
    [SerializeField, Range(0f, 1f), InspectorName("音效")] private float _sfx = 1f;

    [Header("音效子分类")]
    [SerializeField, Range(0f, 1f), InspectorName("角色语音")] private float _characterVoice = 1f;
    [SerializeField, Range(0f, 1f), InspectorName("战斗音效")] private float _combatSfx = 1f;
    [SerializeField, Range(0f, 1f), InspectorName("反馈音效")] private float _feedbackSfx = 1f;

    public float Master { get { return _master; } }
    public float Music { get { return _music; } }
    public float Sfx { get { return _sfx; } }
    public float CharacterVoice { get { return _characterVoice; } }
    public float CombatSfx { get { return _combatSfx; } }
    public float FeedbackSfx { get { return _feedbackSfx; } }
}
