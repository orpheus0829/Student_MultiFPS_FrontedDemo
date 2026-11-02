using System;
using UnityEngine;

[Serializable]
public class WeaponVectorCurveData
{
    [SerializeField] private AnimationCurve _x = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    [SerializeField] private AnimationCurve _y = AnimationCurve.Linear(0f, 0f, 1f, 0f);
    [SerializeField] private AnimationCurve _z = AnimationCurve.Linear(0f, 0f, 1f, 0f);

    public AnimationCurve X { get { return _x; } }
    public AnimationCurve Y { get { return _y; } }
    public AnimationCurve Z { get { return _z; } }
}

[Serializable]
public class WeaponRecoilProgressionData
{
    [SerializeField] private float _acceleration;
    [SerializeField] private float _damping;
    [SerializeField] private float _amount;

    public float Acceleration { get { return _acceleration; } }
    public float Damping { get { return _damping; } }
    public float Amount { get { return _amount; } }
}

[Serializable]
public class WeaponRecoilSwayData
{
    [SerializeField] private Vector2 _pitch;
    [SerializeField] private Vector2 _yaw;
    [SerializeField] private float _rollMultiplier;
    [SerializeField] private float _damping;
    [SerializeField] private float _acceleration;
    [SerializeField, Range(0f, 1f)] private float _aimScale;
    [SerializeField] private Vector3 _pivotOffset;

    public Vector2 Pitch { get { return _pitch; } }
    public Vector2 Yaw { get { return _yaw; } }
    public float RollMultiplier { get { return _rollMultiplier; } }
    public float Damping { get { return _damping; } }
    public float Acceleration { get { return _acceleration; } }
    public float AimScale { get { return _aimScale; } }
    public Vector3 PivotOffset { get { return _pivotOffset; } }
}

[Serializable]
public class WeaponOriginalRecoilData
{
    [SerializeField] private Vector2 _pitch;
    [SerializeField] private Vector4 _roll;
    [SerializeField] private Vector4 _yaw;
    [SerializeField] private Vector2 _kickback;
    [SerializeField] private Vector2 _kickUp;
    [SerializeField] private Vector2 _kickRight;
    [SerializeField] private Vector3 _aimRotationMultiplier;
    [SerializeField] private Vector3 _aimPositionMultiplier;
    [SerializeField] private Vector3 _smoothRotation;
    [SerializeField] private Vector3 _smoothPosition;
    [SerializeField] private Vector3 _extraRotation;
    [SerializeField] private Vector3 _extraPosition;
    [SerializeField] private Vector2 _noiseX;
    [SerializeField] private Vector2 _noiseY;
    [SerializeField] private Vector2 _noiseAcceleration;
    [SerializeField] private Vector2 _noiseDamping;
    [SerializeField, Range(0f, 1f)] private float _noiseScale = 1f;
    [SerializeField] private float _pushAmount;
    [SerializeField] private float _pushAcceleration;
    [SerializeField] private float _pushDamping;
    [SerializeField] private WeaponRecoilSwayData _sway = new WeaponRecoilSwayData();
    [SerializeField] private WeaponRecoilProgressionData _pitchProgression = new WeaponRecoilProgressionData();
    [SerializeField] private WeaponRecoilProgressionData _upProgression = new WeaponRecoilProgressionData();
    [SerializeField, Range(0f, 1f)] private float _aimProgressAlpha = 1f;
    [SerializeField] private Vector2 _horizontalControllerRecoil;
    [SerializeField] private Vector2 _verticalControllerRecoil;
    [SerializeField, Min(0f)] private float _horizontalSmoothing;
    [SerializeField, Min(0f)] private float _verticalSmoothing;
    [SerializeField, Min(0f)] private float _controllerDamping;
    [SerializeField] private Vector3 _hipPivotOffset;
    [SerializeField] private Vector3 _aimPivotOffset;
    [SerializeField] private bool _smoothRoll;
    [SerializeField, Min(0f)] private float _playRate;
    [SerializeField] private WeaponVectorCurveData _semiRotationCurve = new WeaponVectorCurveData();
    [SerializeField] private WeaponVectorCurveData _semiPositionCurve = new WeaponVectorCurveData();
    [SerializeField] private WeaponVectorCurveData _automaticRotationCurve = new WeaponVectorCurveData();
    [SerializeField] private WeaponVectorCurveData _automaticPositionCurve = new WeaponVectorCurveData();

    public Vector2 Pitch { get { return _pitch; } }
    public Vector4 Roll { get { return _roll; } }
    public Vector4 Yaw { get { return _yaw; } }
    public Vector2 Kickback { get { return _kickback; } }
    public Vector2 KickUp { get { return _kickUp; } }
    public Vector2 KickRight { get { return _kickRight; } }
    public Vector3 AimRotationMultiplier { get { return _aimRotationMultiplier; } }
    public Vector3 AimPositionMultiplier { get { return _aimPositionMultiplier; } }
    public Vector3 SmoothRotation { get { return _smoothRotation; } }
    public Vector3 SmoothPosition { get { return _smoothPosition; } }
    public Vector3 ExtraRotation { get { return _extraRotation; } }
    public Vector3 ExtraPosition { get { return _extraPosition; } }
    public Vector2 NoiseX { get { return _noiseX; } }
    public Vector2 NoiseY { get { return _noiseY; } }
    public Vector2 NoiseAcceleration { get { return _noiseAcceleration; } }
    public Vector2 NoiseDamping { get { return _noiseDamping; } }
    public float NoiseScale { get { return _noiseScale; } }
    public float PushAmount { get { return _pushAmount; } }
    public float PushAcceleration { get { return _pushAcceleration; } }
    public float PushDamping { get { return _pushDamping; } }
    public WeaponRecoilSwayData Sway { get { return _sway; } }
    public WeaponRecoilProgressionData PitchProgression { get { return _pitchProgression; } }
    public WeaponRecoilProgressionData UpProgression { get { return _upProgression; } }
    public float AimProgressAlpha { get { return _aimProgressAlpha; } }
    public Vector2 HorizontalControllerRecoil { get { return _horizontalControllerRecoil; } }
    public Vector2 VerticalControllerRecoil { get { return _verticalControllerRecoil; } }
    public float HorizontalSmoothing { get { return _horizontalSmoothing; } }
    public float VerticalSmoothing { get { return _verticalSmoothing; } }
    public float ControllerDamping { get { return _controllerDamping; } }
    public Vector3 HipPivotOffset { get { return _hipPivotOffset; } }
    public Vector3 AimPivotOffset { get { return _aimPivotOffset; } }
    public bool SmoothRoll { get { return _smoothRoll; } }
    public float PlayRate { get { return _playRate; } }
    public WeaponVectorCurveData SemiRotationCurve { get { return _semiRotationCurve; } }
    public WeaponVectorCurveData SemiPositionCurve { get { return _semiPositionCurve; } }
    public WeaponVectorCurveData AutomaticRotationCurve { get { return _automaticRotationCurve; } }
    public WeaponVectorCurveData AutomaticPositionCurve { get { return _automaticPositionCurve; } }
}
