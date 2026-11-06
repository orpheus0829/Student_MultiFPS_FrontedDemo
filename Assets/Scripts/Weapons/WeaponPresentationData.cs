using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class WeaponAnimationData
{
    [SerializeField, ResourceId(typeof(RuntimeAnimatorController))] private string _characterAnimatorControllerResourceId;
    [SerializeField, ResourceId(typeof(RuntimeAnimatorController))] private string _weaponAnimatorControllerResourceId;
    [SerializeField, ResourceId(typeof(RuntimeAnimatorController))] private string _thirdPersonAnimatorControllerResourceId;
    [SerializeField, ResourceId(typeof(RuntimeAnimatorController))] private string _thirdPersonWeaponAnimatorControllerResourceId;
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

    public string CharacterAnimatorControllerResourceId { get { return _characterAnimatorControllerResourceId; } }
    public string WeaponAnimatorControllerResourceId { get { return _weaponAnimatorControllerResourceId; } }
    public string ThirdPersonAnimatorControllerResourceId { get { return _thirdPersonAnimatorControllerResourceId; } }
    public string ThirdPersonWeaponAnimatorControllerResourceId { get { return _thirdPersonWeaponAnimatorControllerResourceId; } }
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
    [SerializeField, ResourceId(typeof(AudioClip))] private List<string> _fireSoundResourceIds = new List<string>();
    [SerializeField] private Vector2 _firePitchRange = Vector2.one;
    [SerializeField] private Vector2 _fireVolumeRange = Vector2.one;
    [SerializeField, ResourceId(typeof(AudioClip))] private List<string> _weaponEventSoundResourceIds = new List<string>();
    [SerializeField, ResourceId(typeof(AudioClip))] private string _dryFireSoundResourceId;
    [SerializeField, ResourceId(typeof(AudioClip))] private string _equipSoundResourceId;
    [SerializeField, ResourceId(typeof(AudioClip))] private string _unequipSoundResourceId;
    [SerializeField, ResourceId(typeof(AudioClip))] private List<string> _reloadSoundResourceIds = new List<string>();
    [SerializeField, ResourceId(typeof(AudioClip))] private List<string> _mechanicalSoundResourceIds = new List<string>();

    public IReadOnlyList<string> FireSoundResourceIds { get { return _fireSoundResourceIds; } }
    public Vector2 FirePitchRange { get { return _firePitchRange; } }
    public Vector2 FireVolumeRange { get { return _fireVolumeRange; } }
    public IReadOnlyList<string> WeaponEventSoundResourceIds { get { return _weaponEventSoundResourceIds; } }
    public string DryFireSoundResourceId { get { return _dryFireSoundResourceId; } }
    public string EquipSoundResourceId { get { return _equipSoundResourceId; } }
    public string UnequipSoundResourceId { get { return _unequipSoundResourceId; } }
    public IReadOnlyList<string> ReloadSoundResourceIds { get { return _reloadSoundResourceIds; } }
    public IReadOnlyList<string> MechanicalSoundResourceIds { get { return _mechanicalSoundResourceIds; } }
}

[Serializable]
public class WeaponEffectData
{
    [SerializeField, ResourceId(typeof(GameObject))] private string _muzzleFlashResourceId;
    [SerializeField, ResourceId(typeof(GameObject))] private string _muzzleSmokeResourceId;
    [SerializeField, ResourceId(typeof(GameObject))] private string _shellResourceId;
    [SerializeField, ResourceId(typeof(GameObject))] private string _tracerResourceId;
    [SerializeField, ResourceId(typeof(GameObject))] private string _impactEffectResourceId;

    public string MuzzleFlashResourceId { get { return _muzzleFlashResourceId; } }
    public string MuzzleSmokeResourceId { get { return _muzzleSmokeResourceId; } }
    public string ShellResourceId { get { return _shellResourceId; } }
    public string TracerResourceId { get { return _tracerResourceId; } }
    public string ImpactEffectResourceId { get { return _impactEffectResourceId; } }
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
    [SerializeField, ResourceId(typeof(GameObject))] private string _firstPersonPrefabResourceId;
    [SerializeField, ResourceId(typeof(GameObject))] private string _worldModelPrefabResourceId;

    public string FirstPersonPrefabResourceId { get { return _firstPersonPrefabResourceId; } }
    public string WorldModelPrefabResourceId { get { return _worldModelPrefabResourceId; } }
}

[Serializable]
public class WeaponSpecialData
{
    [SerializeField] private WeaponSpecialType _specialType;
    [SerializeField, Min(0)] private int _beltRoundCount;
    [SerializeField, Min(0f)] private float _beltResetTime;
    [SerializeField] private List<string> _beltNodePaths = new List<string>();
    [SerializeField, ResourceId(typeof(AnimationClip))] private string _specialAnimationResourceId;
    [SerializeField] private AnimationCurve _specialCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

    public WeaponSpecialType SpecialType { get { return _specialType; } }
    public int BeltRoundCount { get { return _beltRoundCount; } }
    public float BeltResetTime { get { return _beltResetTime; } }
    public IReadOnlyList<string> BeltNodePaths { get { return _beltNodePaths; } }
    public string SpecialAnimationResourceId { get { return _specialAnimationResourceId; } }
    public AnimationCurve SpecialCurve { get { return _specialCurve; } }
}
