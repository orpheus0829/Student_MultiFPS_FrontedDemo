using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;

public class EffectAudioBehaviour : PlayableBehaviour
{
    public EffectAudioClip clip;

    private bool _fired;
    private float _spawnTimer;
    private Transform _bind;
    private ITimelineEffectHost _host;
    private bool _warned;
    private readonly List<GameObject> _spawned = new List<GameObject>();

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        _fired = false;
        _spawnTimer = 0f;
        _bind = null;
        _host = null;
        _warned = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        Transform bind = playerData as Transform;
        if (bind != _bind)
        {
            _bind = bind;
            _host = null;
            _warned = false;
            if (bind != null)
            {
                _host = bind.GetComponentInParent<ITimelineEffectHost>();
            }
        }
        if (_host == null)
        {
            if (bind != null && !_warned && (clip.sound != null || clip.effectPrefab != null))
            {
                Debug.LogWarning("[EffectAudioTrack] 绑定物体缺少 ITimelineEffectHost，音效/特效不生效", bind);
                _warned = true;
            }
            return;
        }

        float curTime = (float)playable.GetTime();
        float duration = (float)playable.GetDuration();
        float deltaTime = (float)info.deltaTime;
        bool inTime = curTime >= 0f && curTime <= duration;
        if (inTime != _fired)
        {
            _fired = inTime;
            _spawnTimer = 0f;
            if (inTime && !clip.useRepeatSpawn)
            {
                Fire();
            }
        }
        if (inTime && clip.useRepeatSpawn)
        {
            _spawnTimer += deltaTime;
            if (_spawnTimer >= clip.spawnInterval)
            {
                Fire();
                _spawnTimer = 0f;
            }
        }
    }

    private void Fire()
    {
        if (clip.sound != null)
        {
            _host.PlaySound(clip.sound);
        }
        if (clip.effectPrefab != null)
        {
            Transform bind = _bind;
            if (bind == null)
            {
                return;
            }
            Vector3 worldPos = bind.TransformPoint(clip.spawnOffset);
            Quaternion worldRot = bind.rotation * Quaternion.Euler(clip.spawnEuler);
            GameObject vfx = _host.SpawnEffect(clip.effectPrefab, worldPos, worldRot);
            if (vfx != null)
            {
                vfx.transform.localScale = clip.spawnScale;
                _spawned.Add(vfx);
            }
        }
    }

    private void RecycleAll()
    {
        foreach (GameObject obj in _spawned)
        {
            if (obj != null && _host != null)
            {
                _host.RecycleEffect(obj);
            }
        }
        _spawned.Clear();
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        RecycleAll();
        _fired = false;
        _spawnTimer = 0f;
        _host = null;
        _bind = null;
    }

    public override void OnGraphStop(Playable playable)
    {
        RecycleAll();
        _host = null;
        _bind = null;
    }
}
