using UnityEngine;
using UnityEngine.Playables;

public class HitBoxBehaviour : PlayableBehaviour
{
    public HitBoxClip clip;

    private bool _active;
    private float _scanTimer;
    private Transform _bind;
    private ITimelineHitHost _host;
    private bool _warned;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        _active = false;
        _scanTimer = 0f;
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
                _host = bind.GetComponentInParent<ITimelineHitHost>();
            }
        }
        if (_host == null)
        {
            if (bind != null && !_warned)
            {
                Debug.LogWarning("[HitBoxTrack] 绑定物体缺少 ITimelineHitHost，判定不生效", bind);
                _warned = true;
            }
            return;
        }

        float curTime = (float)playable.GetTime();
        float deltaTime = (float)info.deltaTime;
        bool inWindow = curTime >= clip.startTime && curTime <= clip.endTime;
        if (inWindow != _active)
        {
            _active = inWindow;
            _scanTimer = 0f;
            if (_active)
            {
                _host.SetHitBox(new TimelineHitData
                {
                    boxOffset = clip.boxOffset,
                    boxRadius = clip.boxRadius,
                    hitBoxShape = clip.hitBoxShape,
                    hitBoxSize = clip.hitBoxSize,
                    sectorAngle = clip.sectorAngle,
                    sectorInnerRadius = clip.sectorInnerRadius,
                    sectorHeight = clip.sectorHeight,
                    boxEuler = clip.boxEuler,
                    damage = clip.damage,
                    hitForce = clip.HitForce
                });
                if (!clip.useRepeatScan)
                {
                    _host.DoHitScan();
                }
            }
            else
            {
                _host.ClearHitBox();
            }
        }
        if (_active && clip.useRepeatScan)
        {
            _scanTimer += deltaTime;
            if (_scanTimer >= clip.scanInterval)
            {
                _host.DoHitScan();
                _scanTimer = 0f;
            }
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (_host != null)
        {
            _host.ClearHitBox();
        }
        _active = false;
        _scanTimer = 0f;
        _host = null;
        _bind = null;
    }
}
