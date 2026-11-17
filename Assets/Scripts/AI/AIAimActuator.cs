using UnityEngine;

public sealed class AIAimActuator
{
    private readonly FirstPersonCameraCtrl _camera;
    private readonly Transform _owner;
    private AIDifficultyProfile _profile;
    private AITrackedTarget _target;
    private Vector3 _aimDirection;
    private Vector2 _trackingBias;
    private Vector2 _burstOffset;
    private float _acquiredTime;
    private float _nextBurstSampleTime;
    private float _headAimValue;
    private int _burstSequence;

    public float AngularError { get; private set; }

    public AIAimActuator(
        FirstPersonCameraCtrl camera,
        Transform owner,
        AIDifficultyProfile profile)
    {
        _camera = camera;
        _owner = owner;
        _profile = profile;
        _aimDirection = owner.forward;
    }

    public void SetProfile(AIDifficultyProfile profile)
    {
        _profile = profile;
    }

    public void Tick(AIIntent intent, WeaponDefinitionSO weapon)
    {
        if ((!intent.Aim && !intent.Fire) || intent.Target == null)
        {
            _target = null;
            AngularError = 180f;
            return;
        }
        if (_target != intent.Target)
        {
            _target = intent.Target;
            _acquiredTime = Time.time;
            _nextBurstSampleTime = 0f;
            int targetId = _target.Actor ? _target.Actor.GetInstanceID() : 0;
            uint hash = (uint)(_owner.GetInstanceID() * 397) ^ (uint)targetId;
            _headAimValue = (hash & 0x00ffffffu) / 16777215f;
            _trackingBias = new Vector2(
                HashSigned(hash ^ 0x9e3779b9u),
                HashSigned(hash ^ 0x85ebca6bu));
            _burstSequence = 0;
        }
        Vector3 origin = _camera.GetCenterAimRay().origin;
        float height = ShouldAimHead(_target) ? 1.6f : 1.15f;
        Vector3 point = _target.LastKnownPosition + Vector3.up * height;
        if (weapon && weapon.Damage.ProjectileSpeed > 0f)
        {
            float flightTime = _target.Distance / weapon.Damage.ProjectileSpeed;
            point += _target.LastKnownVelocity * flightTime * _profile.LeadFactor;
        }
        float settle = Mathf.Clamp01((Time.time - _acquiredTime) /
            Mathf.Max(0.01f, _profile.AimSettleTime));
        float error = Mathf.Lerp(_profile.AimErrorInitial, _profile.AimErrorSettled, settle);
        RefreshBurstOffset(intent);
        float pressureResistance = _profile.Difficulty == AIDifficulty.Hard
            ? 0.55f
            : _profile.Difficulty == AIDifficulty.Normal ? 0.85f : 1.25f;
        float pressureError = intent.CombatPressure * pressureResistance * 3.5f;
        error += pressureError;
        if (intent.CombatTactic == AICombatTactic.Suppress)
        {
            error += _profile.Difficulty == AIDifficulty.Hard ? 0.8f : 1.6f;
        }
        float compensation = 1f - Mathf.Clamp01(_profile.RecoilCompensation) * 0.65f;
        float horizontalError = (_trackingBias.x + _burstOffset.x) * error;
        float verticalError = (_trackingBias.y + _burstOffset.y) * error * compensation;
        Vector3 desired = Quaternion.Euler(verticalError, horizontalError, 0f) *
            (point - origin).normalized;
        _aimDirection = Vector3.RotateTowards(
            _aimDirection,
            desired,
            _profile.TurnRate * Mathf.Deg2Rad * Time.deltaTime,
            0f);
        AngularError = Vector3.Angle(_aimDirection, desired);
        _camera.SetAimDirection(_aimDirection);
    }

    private void RefreshBurstOffset(AIIntent intent)
    {
        if (!intent.Fire)
        {
            _nextBurstSampleTime = 0f;
            _burstOffset = Vector2.zero;
            return;
        }

        if (Time.time < _nextBurstSampleTime)
        {
            return;
        }

        float interval = _profile.Difficulty == AIDifficulty.Hard
            ? 0.5f
            : _profile.Difficulty == AIDifficulty.Normal ? 0.68f : 0.9f;
        _nextBurstSampleTime = Time.time + interval;
        _burstSequence++;
        int targetId = _target != null && _target.Actor
            ? _target.Actor.GetInstanceID()
            : 0;
        uint hash = (uint)(_owner.GetInstanceID() * 397) ^
            (uint)(targetId * 7919) ^ (uint)(_burstSequence * 104729);
        float x = HashSigned(hash ^ 0xc2b2ae35u);
        float y = HashSigned(hash ^ 0x27d4eb2fu);
        float scale = _profile.Difficulty == AIDifficulty.Hard
            ? 0.12f
            : _profile.Difficulty == AIDifficulty.Normal ? 0.22f : 0.38f;
        _burstOffset = new Vector2(x, y) * scale;
    }

    private bool ShouldAimHead(AITrackedTarget target)
    {
        return target.Visible &&
            (target.VisiblePoints & AIBodyPointMask.Head) != 0 &&
            _headAimValue < _profile.HeadshotChance;
    }

    private static float HashSigned(uint value)
    {
        value ^= value >> 16;
        value *= 2246822519u;
        value ^= value >> 13;
        return ((value & 0x00ffffffu) / 16777215f) * 2f - 1f;
    }
}
