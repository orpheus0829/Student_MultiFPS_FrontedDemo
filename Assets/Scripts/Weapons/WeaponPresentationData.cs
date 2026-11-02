using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeaponAnimationData
{
    [SerializeField] private RuntimeAnimatorController _characterAnimatorController;
    [SerializeField] private RuntimeAnimatorController _weaponAnimatorController;
    [SerializeField] private RuntimeAnimatorController _thirdPersonAnimatorController;
    [SerializeField] private RuntimeAnimatorController _thirdPersonWeaponAnimatorController;
    [SerializeField] private WeaponHoldStyle _holdStyle;
    [SerializeField] private bool _canInspect;
    [SerializeField] private bool _hasFireAnimation;
    [SerializeField] private bool _hasLastRoundFireAnimation;
    [SerializeField] private bool _hasTacticalReloadAnimation = true;
    [SerializeField] private bool _hasEmptyReloadAnimation = true;
    [SerializeField] private bool _hasPerRoundReloadAnimation;
    [SerializeField] private bool _hasEquipOverrideAnimation;
    [SerializeField] private bool _hasFireModeSwitchAnimation;
    [SerializeField] private bool _hasManualCycleAnimation;
    [SerializeField] private bool _usesSprintTriggerDiscipline = true;

    public RuntimeAnimatorController CharacterAnimatorController { get { return _characterAnimatorController; } }
    public RuntimeAnimatorController WeaponAnimatorController { get { return _weaponAnimatorController; } }
    public RuntimeAnimatorController ThirdPersonAnimatorController { get { return _thirdPersonAnimatorController; } }
    public RuntimeAnimatorController ThirdPersonWeaponAnimatorController { get { return _thirdPersonWeaponAnimatorController; } }
    public WeaponHoldStyle HoldStyle { get { return _holdStyle; } }
    public bool CanInspect { get { return _canInspect; } }
    public bool HasFireAnimation { get { return _hasFireAnimation; } }
    public bool HasLastRoundFireAnimation { get { return _hasLastRoundFireAnimation; } }
    public bool HasTacticalReloadAnimation { get { return _hasTacticalReloadAnimation; } }
    public bool HasEmptyReloadAnimation { get { return _hasEmptyReloadAnimation; } }
    public bool HasPerRoundReloadAnimation { get { return _hasPerRoundReloadAnimation; } }
    public bool HasEquipOverrideAnimation { get { return _hasEquipOverrideAnimation; } }
    public bool HasFireModeSwitchAnimation { get { return _hasFireModeSwitchAnimation; } }
    public bool HasManualCycleAnimation { get { return _hasManualCycleAnimation; } }
    public bool UsesSprintTriggerDiscipline { get { return _usesSprintTriggerDiscipline; } }
}

[Serializable]
public class WeaponIkData
{
    [SerializeField] private Quaternion _weaponRotationOffset = Quaternion.Euler(90f, 0f, 0f);
    [SerializeField] private Vector3 _weaponPositionOffset;
    [SerializeField] private Vector3 _leftClavicleOffset;
    [SerializeField] private Vector3 _rightClavicleOffset;
    [SerializeField] private Vector3 _aimPointOffset;
    [SerializeField] private Quaternion _sprintRightHandRotationOffset = Quaternion.identity;
    [SerializeField, Range(0f, 1f)] private float _adsBlend;
    [SerializeField] private string _aimPointPath;
    [SerializeField] private string _muzzlePointPath;
    [SerializeField] private string _shellEjectionPointPath;
    [SerializeField] private string _magazinePointPath;
    [SerializeField] private string _leftHandIkPointPath;

    public Quaternion WeaponRotationOffset { get { return _weaponRotationOffset; } }
    public Vector3 WeaponPositionOffset { get { return _weaponPositionOffset; } }
    public Vector3 LeftClavicleOffset { get { return _leftClavicleOffset; } }
    public Vector3 RightClavicleOffset { get { return _rightClavicleOffset; } }
    public Vector3 AimPointOffset { get { return _aimPointOffset; } }
    public Quaternion SprintRightHandRotationOffset { get { return _sprintRightHandRotationOffset; } }
    public float AdsBlend { get { return _adsBlend; } }
    public string AimPointPath { get { return _aimPointPath; } }
    public string MuzzlePointPath { get { return _muzzlePointPath; } }
    public string ShellEjectionPointPath { get { return _shellEjectionPointPath; } }
    public string MagazinePointPath { get { return _magazinePointPath; } }
    public string LeftHandIkPointPath { get { return _leftHandIkPointPath; } }
}

[Serializable]
public class WeaponAudioData
{
    [SerializeField] private List<AudioClip> _fireSounds = new List<AudioClip>();
    [SerializeField] private Vector2 _firePitchRange = Vector2.one;
    [SerializeField] private Vector2 _fireVolumeRange = Vector2.one;
    [SerializeField] private List<AudioClip> _weaponEventSounds = new List<AudioClip>();
    [SerializeField] private AudioClip _dryFireSound;
    [SerializeField] private AudioClip _equipSound;
    [SerializeField] private AudioClip _unequipSound;
    [SerializeField] private List<AudioClip> _reloadSounds = new List<AudioClip>();
    [SerializeField] private List<AudioClip> _mechanicalSounds = new List<AudioClip>();

    public IReadOnlyList<AudioClip> FireSounds { get { return _fireSounds; } }
    public Vector2 FirePitchRange { get { return _firePitchRange; } }
    public Vector2 FireVolumeRange { get { return _fireVolumeRange; } }
    public IReadOnlyList<AudioClip> WeaponEventSounds { get { return _weaponEventSounds; } }
    public AudioClip DryFireSound { get { return _dryFireSound; } }
    public AudioClip EquipSound { get { return _equipSound; } }
    public AudioClip UnequipSound { get { return _unequipSound; } }
    public IReadOnlyList<AudioClip> ReloadSounds { get { return _reloadSounds; } }
    public IReadOnlyList<AudioClip> MechanicalSounds { get { return _mechanicalSounds; } }
}

[Serializable]
public class WeaponEffectData
{
    [SerializeField] private GameObject _muzzleFlashPrefab;
    [SerializeField] private GameObject _muzzleSmokePrefab;
    [SerializeField] private GameObject _shellPrefab;
    [SerializeField] private GameObject _tracerPrefab;
    [SerializeField] private GameObject _impactEffectPrefab;

    public GameObject MuzzleFlashPrefab { get { return _muzzleFlashPrefab; } }
    public GameObject MuzzleSmokePrefab { get { return _muzzleSmokePrefab; } }
    public GameObject ShellPrefab { get { return _shellPrefab; } }
    public GameObject TracerPrefab { get { return _tracerPrefab; } }
    public GameObject ImpactEffectPrefab { get { return _impactEffectPrefab; } }
}

[Serializable]
public class WeaponCameraFeedbackData
{
    [SerializeField] private WeaponVectorCurveData _sourceCurve = new WeaponVectorCurveData();
    [SerializeField] private Vector2 _sourcePitch;
    [SerializeField] private Vector2 _sourceYaw;
    [SerializeField] private Vector2 _sourceRoll;
    [SerializeField, Min(0f)] private float _sourceSmoothSpeed;
    [SerializeField, Min(0f)] private float _sourcePlayRate = 1f;
    [SerializeField] private Vector3 _cameraKickRotation;
    [SerializeField] private Vector3 _cameraKickPosition;
    [SerializeField, Min(0f)] private float _cameraKickDuration = 0.05f;
    [SerializeField, Min(0f)] private float _cameraReturnDuration = 0.12f;
    [SerializeField, Min(0f)] private float _aimCameraKickMultiplier = 0.75f;

    public WeaponVectorCurveData SourceCurve { get { return _sourceCurve; } }
    public Vector2 SourcePitch { get { return _sourcePitch; } }
    public Vector2 SourceYaw { get { return _sourceYaw; } }
    public Vector2 SourceRoll { get { return _sourceRoll; } }
    public float SourceSmoothSpeed { get { return _sourceSmoothSpeed; } }
    public float SourcePlayRate { get { return _sourcePlayRate; } }
    public Vector3 CameraKickRotation { get { return _cameraKickRotation; } }
    public Vector3 CameraKickPosition { get { return _cameraKickPosition; } }
    public float CameraKickDuration { get { return _cameraKickDuration; } }
    public float CameraReturnDuration { get { return _cameraReturnDuration; } }
    public float AimCameraKickMultiplier { get { return _aimCameraKickMultiplier; } }
}

[Serializable]
public class WeaponResourceData
{
    [SerializeField] private GameObject _firstPersonPrefab;
    [SerializeField] private GameObject _worldModelPrefab;

    public GameObject FirstPersonPrefab { get { return _firstPersonPrefab; } }
    public GameObject WorldModelPrefab { get { return _worldModelPrefab; } }
}

[Serializable]
public class WeaponSpecialData
{
    [SerializeField] private WeaponSpecialType _specialType;
    [SerializeField, Min(0)] private int _beltRoundCount;
    [SerializeField, Min(0f)] private float _beltResetTime;
    [SerializeField] private List<string> _beltNodePaths = new List<string>();
    [SerializeField] private AnimationClip _specialAnimation;
    [SerializeField] private AnimationCurve _specialCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public WeaponSpecialType SpecialType { get { return _specialType; } }
    public int BeltRoundCount { get { return _beltRoundCount; } }
    public float BeltResetTime { get { return _beltResetTime; } }
    public IReadOnlyList<string> BeltNodePaths { get { return _beltNodePaths; } }
    public AnimationClip SpecialAnimation { get { return _specialAnimation; } }
    public AnimationCurve SpecialCurve { get { return _specialCurve; } }
}
