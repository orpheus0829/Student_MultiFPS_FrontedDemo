using UnityEngine;
using UnityEngine.Playables;

public class CameraTimelineBehaviour : PlayableBehaviour
{
    public CameraTimelineClip clip;

    private TimelineCamRig _rig;
    private bool _inited;
    private bool _rigSearched;
    private bool _warned;
    private Vector3 _originWorldPos;
    private Quaternion _originWorldRot;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        _inited = false;
        _rigSearched = false;
        _warned = false;
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        if (clip == null)
        {
            return;
        }
        TimelineCamRig rig = playerData as TimelineCamRig;
        if (rig == null && !_rigSearched)
        {
            _rigSearched = true;
            rig = Object.FindFirstObjectByType<TimelineCamRig>();
        }
        if (rig == null)
        {
            if (!_warned)
            {
                Debug.LogWarning("[CameraTimeline] 未找到 TimelineCamRig，运镜不生效");
                _warned = true;
            }
            return;
        }
        _rig = rig;
        rig.userManualAllowed = clip.allowManualCamera;

        float totalDuration = (float)playable.GetDuration();
        float currentTime = (float)playable.GetTime();
        if (totalDuration <= 0.0001f)
        {
            return;
        }

        Transform anchor = rig.target != null ? rig.target : rig.transform;
        Vector3 lookPoint = anchor.position + Vector3.up * rig.lookHeight;

        if (!_inited)
        {
            _inited = true;
            rig.Acquire();
            if (!clip.useLastFrameAsOrigin)
            {
                _originWorldRot = rig.transform.rotation;
                _originWorldPos = rig.GetNormalOrbitPosition(anchor);
            }
        }

        if (rig.userDriving)
        {
            return;
        }

        if (clip.cameraMoveMode == CamMoveMode.ResetOrigin)
        {
            if (!rig.HasNormalState)
            {
                return;
            }
            Vector3 normalPos = rig.NormalPosition;
            if (clip.resetSubMode == ResetCamSubMode.Teleport)
            {
                rig.transform.position = normalPos;
            }
            else
            {
                rig.transform.position = Vector3.Lerp(rig.transform.position, normalPos, clip.resetLerpFactor * Time.deltaTime);
            }
            if (clip.lockLookAtPlayer)
            {
                rig.transform.LookAt(lookPoint);
            }
            return;
        }

        float baseProgress;
        if (clip.useVariableSpeed)
        {
            baseProgress = GetProgress(currentTime, totalDuration, clip.startSpeed, clip.endSpeed);
        }
        else
        {
            baseProgress = Mathf.Clamp01(currentTime / totalDuration);
        }

        Vector3 worldTargetPos = anchor.TransformPoint(clip.cameraTargetLocalPos);
        Quaternion worldTargetRot = anchor.rotation * Quaternion.Euler(clip.cameraTargetEuler);
        Vector3 originWorldPos;
        if (clip.useLastFrameAsOrigin)
        {
            originWorldPos = rig.transform.position;
        }
        else
        {
            originWorldPos = _originWorldPos;
        }

        Vector3 finalTargetPos;
        if (!clip.useSurroundMode)
        {
            finalTargetPos = worldTargetPos;
        }
        else
        {
            Vector3 foot = anchor.position;
            Vector3 circleCenter = new Vector3(foot.x, foot.y + clip.surroundFixedHeight, foot.z);
            Vector3 originFlat = originWorldPos - foot;
            originFlat.y = 0f;
            float startAngle = Mathf.Atan2(originFlat.x, originFlat.z) * Mathf.Rad2Deg;
            float curAngle = startAngle + clip.surroundTotalAngle * baseProgress;
            float rad = curAngle * Mathf.Deg2Rad;
            finalTargetPos = circleCenter + new Vector3(Mathf.Sin(rad) * clip.surroundRadius, 0f, Mathf.Cos(rad) * clip.surroundRadius);
        }

        if (clip.cameraMoveMode == CamMoveMode.Teleport)
        {
            if (baseProgress > 0f)
            {
                rig.transform.position = finalTargetPos;
                SetLook(rig, lookPoint, worldTargetRot);
            }
        }
        else
        {
            Vector3 lerpPos = Vector3.Lerp(originWorldPos, finalTargetPos, baseProgress);
            rig.transform.position = Vector3.Lerp(rig.transform.position, lerpPos, clip.smoothLerpFactor * Time.deltaTime);
            Quaternion lerpRot;
            if (clip.useLastFrameAsOrigin)
            {
                lerpRot = Quaternion.Lerp(rig.transform.rotation, worldTargetRot, baseProgress);
            }
            else
            {
                lerpRot = Quaternion.Lerp(_originWorldRot, worldTargetRot, baseProgress);
            }
            SetLook(rig, lookPoint, lerpRot);
        }
    }

    private void SetLook(TimelineCamRig rig, Vector3 lookPoint, Quaternion fallbackRot)
    {
        if (clip.lockLookAtPlayer)
        {
            rig.transform.LookAt(lookPoint);
        }
        else
        {
            rig.transform.rotation = fallbackRot;
        }
    }

    private float GetProgress(float curTime, float duration, float startSpeed, float endSpeed)
    {
        float totalDisp = (startSpeed + endSpeed) * 0.5f * duration;
        if (totalDisp <= 0.0001f)
        {
            return Mathf.Clamp01(curTime / duration);
        }
        float travel = startSpeed * curTime + (endSpeed - startSpeed) * curTime * curTime / (2f * duration);
        return Mathf.Clamp01(travel / totalDisp);
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (_rig != null)
        {
            _rig.Release(clip != null && clip.restoreOriginOnEnd);
        }
        _rig = null;
        _inited = false;
        _rigSearched = false;
        _warned = false;
    }

    public override void OnGraphStop(Playable playable)
    {
        if (_rig != null)
        {
            _rig.Release(clip != null && clip.restoreOriginOnEnd);
        }
        _rig = null;
    }
}
