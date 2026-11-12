using UnityEngine;

public sealed class AIAimActuator
{
    private readonly FirstPersonCameraCtrl _camera;
    private readonly Transform _owner;
    private AIDifficultyProfile _profile;
    private AITrackedTarget _target;
    private Vector3 _aimDirection;
    private Vector2 _noiseSeed;
    private float _acquiredTime;
    private float _headAimValue;

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
        _noiseSeed = new Vector2(owner.GetInstanceID() * 0.013f, owner.GetInstanceID() * 0.031f);
    }

    public void SetProfile(AIDifficultyProfile profile)
    {
        _profile = profile;
    }

    public void Tick(AIIntent intent, WeaponDefinitionSO weapon)
    {
        if (!intent.Aim || intent.Target == null)
        {
            _target = null;
            AngularError = 180f;
            return;
        }
        if (_target != intent.Target)
        {
            _target = intent.Target;
            _acquiredTime = Time.time;
            uint hash = (uint)(_owner.GetInstanceID() * 397) ^
                (uint)_target.Actor.GetInstanceID();
            _headAimValue = (hash & 0x00ffffffu) / 16777215f;
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
        float nx = Mathf.PerlinNoise(_noiseSeed.x, Time.time * 0.75f) * 2f - 1f;
        float ny = Mathf.PerlinNoise(_noiseSeed.y, Time.time * 0.65f) * 2f - 1f;
        float compensation = 1f - Mathf.Clamp01(_profile.RecoilCompensation) * 0.65f;
        Vector3 desired = Quaternion.Euler(ny * error * compensation, nx * error, 0f) *
            (point - origin).normalized;
        _aimDirection = Vector3.RotateTowards(
            _aimDirection,
            desired,
            _profile.TurnRate * Mathf.Deg2Rad * Time.deltaTime,
            0f);
        AngularError = Vector3.Angle(_aimDirection, desired);
        _camera.SetAimDirection(_aimDirection);
    }

    private bool ShouldAimHead(AITrackedTarget target)
    {
        return (target.VisiblePoints & AIBodyPointMask.Head) != 0 &&
            _headAimValue < _profile.HeadshotChance;
    }
}
