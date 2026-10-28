using UnityEngine;
using UnityEngine.Playables;

public class EffectAudioClip : PlayableAsset
{
    [Header("特效音效盒参数")]
    [Tooltip("触发时播放的音效(可空，只播特效)")]
    public AudioClip sound;
    [Tooltip("触发生成的特效预制体(可空，只播音效)")]
    public GameObject effectPrefab;
    [Tooltip("特效/音效发声点相对角色的本地偏移")]
    public Vector3 spawnOffset;
    [Tooltip("生成特效的本地旋转(欧拉角)")]
    public Vector3 spawnEuler;
    [Tooltip("生成特效的缩放")]
    public Vector3 spawnScale = Vector3.one;

    [Header("重复触发设置")]
    [Tooltip("片段持续期间按间隔重复触发")]
    public bool useRepeatSpawn;
    [Tooltip("重复触发间隔(秒)")]
    public float spawnInterval = 0.1f;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        var b = new EffectAudioBehaviour();
        b.clip = this;
        return ScriptPlayable<EffectAudioBehaviour>.Create(graph, b);
    }
}
