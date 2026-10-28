using UnityEngine;
using UnityEngine.Playables;

public class TransformBehaviour : PlayableBehaviour
{
    public TransformTimelineClip clip;

    private bool _inited;
    private bool _hasTeleported;
    private Transform _trans;
    private Rigidbody _rb;
    private CapsuleCollider _selfCol;

    private Quaternion _startRot;
    private Vector3 _startPos;
    private Vector3 _curPos;
    private Vector3 _moveDir;
    private Vector3 _circleCenterWorld;

    public override void OnBehaviourPlay(Playable playable, FrameData info)
    {
        _inited = false;
        _hasTeleported = false;
        _trans = null;
        _rb = null;
        _selfCol = null;
        _curPos = Vector3.zero;
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (!_inited || clip == null)
        {
            return;
        }
        if (clip.moveMode == MoveMode.CircleRotate)
        {
            SetPosition(_curPos);
        }
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        Transform trans = playerData as Transform;
        if (trans == null || clip == null)
        {
            return;
        }
        if (trans != _trans)
        {
            _trans = trans;
            _rb = trans.GetComponent<Rigidbody>();
            _selfCol = trans.GetComponent<CapsuleCollider>();
        }

        float curTime = (float)playable.GetTime();
        float duration = (float)playable.GetDuration();
        float deltaTime = (float)info.deltaTime;
        if (duration <= 0f || deltaTime <= 0f)
        {
            return;
        }
        if (!_inited)
        {
            Init();
        }

        switch (clip.moveMode)
        {
            case MoveMode.FixedEndPos:
                TickFixed();
                break;
            case MoveMode.CircleRotate:
                TickCircle(curTime, duration);
                break;
            default:
                TickLinear(curTime, duration, deltaTime);
                break;
        }
    }

    private void Init()
    {
        _startRot = _trans.rotation;
        _startPos = _trans.position;
        _curPos = _startPos;

        if (clip.moveMode == MoveMode.CircleRotate)
        {
            _circleCenterWorld = _trans.TransformPoint(clip.circleCenterLocal);
        }
        else
        {
            Vector3 dir = _trans.TransformDirection(clip.direction);
            dir.y = 0f;
            _moveDir = dir.sqrMagnitude < 0.0001f ? FlatForward() : dir.normalized;
        }
        _inited = true;
    }

    private Vector3 FlatForward()
    {
        Vector3 forward = _trans.forward;
        forward.y = 0f;
        return forward.sqrMagnitude < 0.0001f ? Vector3.forward : forward.normalized;
    }

    private void TickFixed()
    {
        if (!_hasTeleported)
        {
            Vector3 targetPos = _startPos + _startRot * clip.endPos;
            Quaternion targetRot = _startRot * Quaternion.Euler(clip.endEuler);
            if (_rb != null)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            SetPosition(targetPos);
            _trans.rotation = targetRot;
            _curPos = targetPos;
            _hasTeleported = true;
        }
        else
        {
            _trans.rotation = _startRot * Quaternion.Euler(clip.endEuler);
        }
    }

    private void TickCircle(float curTime, float duration)
    {
        float t = Mathf.Clamp01(curTime / duration);
        float total = clip.circleTotalAngle * (clip.circleClockwise ? -1f : 1f);
        float angle = clip.circleVariableSpeed ? GetVariableProgress(curTime, duration, clip.circleStartAngSpeed, clip.circleEndAngSpeed) * total
                                              : total * t;
        Vector3 initDir = _startPos - _circleCenterWorld;
        initDir.y = 0f;
        if (initDir.sqrMagnitude < 0.000001f)
        {
            initDir = Vector3.right;
        }
        else
        {
            initDir.Normalize();
        }
        Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (initDir * clip.circleRadius);
        _curPos = _circleCenterWorld + offset;
        SetPosition(_curPos);
        _trans.rotation = _startRot;
    }

    private void TickLinear(float curTime, float duration, float deltaTime)
    {
        float speed = GetLinearSpeed(curTime, duration);
        Vector3 step = _moveDir * speed * deltaTime;

        if (clip.totalDistance > 0f)
        {
            float moved = Mathf.Max(0f, Vector3.Dot(_curPos - _startPos, _moveDir));
            float remain = clip.totalDistance - moved;
            if (remain <= 0.0001f)
            {
                step = Vector3.zero;
            }
            else if (step.magnitude > remain)
            {
                step = _moveDir * remain;
            }
        }

        Vector3 next;
        if (clip.useCollision && step.sqrMagnitude > 0.0000001f)
        {
            next = SlideMove(_curPos, step);
        }
        else
        {
            next = _curPos + step;
        }
        _curPos = next;

        if (_rb != null && !_rb.isKinematic)
        {
            _rb.MovePosition(_curPos);
        }
        else
        {
            SetPosition(_curPos);
        }
        _trans.rotation = _startRot;
    }

    private float GetLinearSpeed(float curTime, float duration)
    {
        float t = Mathf.Clamp01(curTime / duration);
        if (clip.moveMode == MoveMode.VariableSpeed)
        {
            return Mathf.Lerp(clip.startSpeed, clip.endSpeed, t);
        }
        return clip.moveSpeed;
    }

    private float GetVariableProgress(float curTime, float duration, float startSpeed, float endSpeed)
    {
        float totalDisp = (startSpeed + endSpeed) * 0.5f * duration;
        if (totalDisp <= 0.0001f)
        {
            return Mathf.Clamp01(curTime / duration);
        }
        float travel = startSpeed * curTime + (endSpeed - startSpeed) * curTime * curTime / (2f * duration);
        return Mathf.Clamp01(travel / totalDisp);
    }

    private Vector3 SlideMove(Vector3 from, Vector3 step)
    {
        float dist = step.magnitude;
        Vector3 dir = step / dist;
        Vector3 castStart = from + Vector3.up * (_selfCol != null ? _selfCol.radius : 0.1f);
        if (!Physics.SphereCast(castStart, clip.castRadius, dir, out RaycastHit hit, dist, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            return from + step;
        }

        float walk1 = Mathf.Max(0f, hit.distance - 0.02f);
        Vector3 basePos = from + dir * walk1;

        Vector3 n = hit.normal;
        n.y = 0f;
        if (n.sqrMagnitude < 0.0001f)
        {
            return basePos;
        }
        n.Normalize();
        Vector3 slide = step - n * Vector3.Dot(step, n);
        if (slide.sqrMagnitude < 0.0001f)
        {
            return basePos;
        }

        Vector3 sDir = slide.normalized;
        float sDist = slide.magnitude;
        Vector3 sStart = basePos + Vector3.up * (_selfCol != null ? _selfCol.radius : 0.1f);
        if (Physics.SphereCast(sStart, clip.castRadius, sDir, out RaycastHit hit2, sDist, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            float walk2 = Mathf.Max(0f, hit2.distance - 0.02f);
            return basePos + sDir * walk2;
        }
        return basePos + slide;
    }

    private void SetPosition(Vector3 position)
    {
        if (_trans == null)
        {
            return;
        }
        if (_rb != null && !_rb.isKinematic)
        {
            _rb.position = position;
        }
        _trans.position = position;
    }
}
