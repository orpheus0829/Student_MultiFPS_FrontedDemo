using UnityEngine;

[CreateAssetMenu(fileName = "CombatFeedbackAudio", menuName = "Gun Demo/音频/战斗反馈声音盒")]
public sealed class CombatFeedbackAudioSO : ScriptableObject
{
    [Header("正常命中")]
    [SerializeField, ResourceId(typeof(AudioClip)), InspectorName("声音资源 ID")]
    private string _hitResourceId = "audio.feedback.hit";
    [SerializeField, Range(0f, 2f), InspectorName("音量")] private float _hitVolume = 0.55f;
    [SerializeField, InspectorName("随机音调")]
    private Vector2 _hitPitchRange = new Vector2(0.96f, 1.02f);

    [Header("普通击杀")]
    [SerializeField, ResourceId(typeof(AudioClip)), InspectorName("声音资源 ID")]
    private string _killResourceId = "audio.feedback.kill";
    [SerializeField, Range(0f, 2f), InspectorName("音量")] private float _killVolume = 1f;
    [SerializeField, InspectorName("随机音调")]
    private Vector2 _killPitchRange = new Vector2(0.98f, 1.02f);

    [Header("爆头击杀")]
    [SerializeField, ResourceId(typeof(AudioClip)), InspectorName("声音资源 ID")]
    private string _headshotKillResourceId = "audio.feedback.headshot-kill";
    [SerializeField, Range(0f, 2f), InspectorName("音量")]
    private float _headshotKillVolume = 1.08f;
    [SerializeField, InspectorName("随机音调")]
    private Vector2 _headshotKillPitchRange = new Vector2(1.02f, 1.07f);

    public string HitResourceId { get { return _hitResourceId; } }
    public float HitVolume { get { return _hitVolume; } }
    public Vector2 HitPitchRange { get { return _hitPitchRange; } }
    public string KillResourceId { get { return _killResourceId; } }
    public float KillVolume { get { return _killVolume; } }
    public Vector2 KillPitchRange { get { return _killPitchRange; } }
    public string HeadshotKillResourceId { get { return _headshotKillResourceId; } }
    public float HeadshotKillVolume { get { return _headshotKillVolume; } }
    public Vector2 HeadshotKillPitchRange { get { return _headshotKillPitchRange; } }
}
