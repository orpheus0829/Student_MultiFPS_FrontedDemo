using System;
using System.Collections.Generic;
using UnityEngine;

public enum WeaponCategory
{
    [InspectorName("突击步枪")] AssaultRifle = 0,
    [InspectorName("战斗步枪")] BattleRifle = 1,
    [InspectorName("冲锋枪")] SubmachineGun = 2,
    [InspectorName("轻机枪")] LightMachineGun = 3,
    [InspectorName("霰弹枪")] Shotgun = 4,
    [InspectorName("精确射手步枪")] MarksmanRifle = 5,
    [InspectorName("狙击步枪")] SniperRifle = 6,
    [InspectorName("手枪")] Pistol = 7,
    [InspectorName("左轮手枪")] Revolver = 8,
    [InspectorName("近战武器")] Melee = 9
}

[Serializable]
public class WeaponMeleeData
{
    [SerializeField, InspectorName("启用近战命中")] private bool _enabled;
    [SerializeField, Min(0.1f), InspectorName("攻击距离")] private float _range = 1.65f;
    [SerializeField, Min(0.05f), InspectorName("攻击半径")] private float _radius = 0.55f;
    [SerializeField, Min(0f), InspectorName("出拳至命中的延迟")] private float _impactDelay = 0.11f;
    [SerializeField, Min(0.01f), InspectorName("完整动作时间")] private float _actionDuration = 0.78f;

    public bool Enabled { get { return _enabled; } }
    public float Range { get { return _range; } }
    public float Radius { get { return _radius; } }
    public float ImpactDelay { get { return _impactDelay; } }
    public float ActionDuration { get { return _actionDuration; } }
}

[Flags]
public enum WeaponTag
{
    [InspectorName("无")] None = 0,
    [InspectorName("半自动")] SemiAutomatic = 1 << 0,
    [InspectorName("全自动")] Automatic = 1 << 1,
    [InspectorName("可切换模式")] SwitchableFireMode = 1 << 2,
    [InspectorName("栓动")] BoltAction = 1 << 3,
    [InspectorName("泵动")] PumpAction = 1 << 4,
    [InspectorName("逐发装填")] PerRoundReload = 1 << 5,
    [InspectorName("弹链供弹")] BeltFed = 1 << 6,
    [InspectorName("弹巢供弹")] CylinderFed = 1 << 7,
    [InspectorName("双管")] DoubleBarrel = 1 << 8,
    [InspectorName("消音")] Suppressed = 1 << 9,
    [InspectorName("无托结构")] Bullpup = 1 << 10
}

[Flags]
public enum WeaponFireModeMask
{
    [InspectorName("无")] None = 0,
    [InspectorName("半自动")] SemiAutomatic = 1 << 0,
    [InspectorName("全自动")] Automatic = 1 << 1,
    [InspectorName("连发")] Burst = 1 << 2
}

public enum WeaponFireMode
{
    [InspectorName("半自动")] SemiAutomatic = 0,
    [InspectorName("全自动")] Automatic = 1,
    [InspectorName("连发")] Burst = 2
}

public enum WeaponReloadStyle
{
    [InspectorName("整匣换弹")] Magazine = 0,
    [InspectorName("逐发装填")] PerRound = 1,
    [InspectorName("弹链换弹")] Belt = 2,
    [InspectorName("左轮装填")] Revolver = 3
}

public enum WeaponHoldStyle
{
    [InspectorName("步枪式持枪")] Rifle = 0,
    [InspectorName("手枪式持枪")] Pistol = 1,
    [InspectorName("霰弹枪式持枪")] Shotgun = 2,
    [InspectorName("狙击枪式持枪")] Sniper = 3,
    [InspectorName("轻机枪式持枪")] LightMachineGun = 4
}

public enum WeaponSpecialType
{
    [InspectorName("无")] None = 0,
    [InspectorName("弹链表现")] AmmoBelt = 1,
    [InspectorName("PDW90 机械表现")] Pdw90Mechanism = 2
}

[Serializable]
public class WeaponBasicData
{
    [SerializeField] private string _weaponId;
    [SerializeField] private string _weaponName;
    [SerializeField] private WeaponCategory _category;
    [SerializeField] private WeaponTag _tags;
    [SerializeField, ResourceId(typeof(Sprite))] private string _iconResourceId;
    [SerializeField, ResourceId(typeof(Sprite))] private string _silhouetteResourceId;

    public string WeaponId { get { return _weaponId; } }
    public string WeaponName { get { return _weaponName; } }
    public WeaponCategory Category { get { return _category; } }
    public WeaponTag Tags { get { return _tags; } }
    public string IconResourceId { get { return _iconResourceId; } }
    public string SilhouetteResourceId { get { return _silhouetteResourceId; } }
}

[Serializable]
public class WeaponFireData
{
    [SerializeField] private WeaponFireModeMask _allowedModes = WeaponFireModeMask.SemiAutomatic;
    [SerializeField] private WeaponFireMode _defaultMode;
    [SerializeField] private bool _canSwitchMode;
    [SerializeField, Min(1f)] private float _semiAutomaticRpm = 600f;
    [SerializeField, Min(1f)] private float _automaticRpm = 600f;
    [SerializeField, Min(1f)] private float _burstRpm = 750f;
    [SerializeField, Min(2)] private int _burstCount = 3;
    [SerializeField, Min(0f)] private float _burstCooldown = 0.12f;
    [SerializeField, Min(1)] private int _pelletsPerShot = 1;
    [SerializeField, Min(0f)] private float _fireDelay;
    [SerializeField] private bool _dryFireEnabled = true;

    public WeaponFireModeMask AllowedModes { get { return _allowedModes; } }
    public WeaponFireMode DefaultMode { get { return _defaultMode; } }
    public bool CanSwitchMode { get { return _canSwitchMode; } }
    public float SemiAutomaticRpm { get { return _semiAutomaticRpm; } }
    public float AutomaticRpm { get { return _automaticRpm; } }
    public float BurstRpm { get { return _burstRpm; } }
    public int BurstCount { get { return _burstCount; } }
    public float BurstCooldown { get { return _burstCooldown; } }
    public int PelletsPerShot { get { return _pelletsPerShot; } }
    public float FireDelay { get { return _fireDelay; } }
    public bool DryFireEnabled { get { return _dryFireEnabled; } }
}

[Serializable]
public class WeaponDamageData
{
    [SerializeField, Min(0f)] private float _closeDamage = 30f;
    [SerializeField, Min(0f)] private float _farDamage = 20f;
    [SerializeField, Min(0f)] private float _closeDamageRange = 20f;
    [SerializeField, Min(0f)] private float _farDamageRange = 60f;
    [SerializeField, Min(0f)] private float _maximumHitRange = 200f;
    [SerializeField, Min(0.01f)] private float _projectileSpeed = 320f;
    [SerializeField, Min(0f)] private float _projectileRadius = 0.015f;
    [SerializeField, Min(0f)] private float _headMultiplier = 1.5f;
    [SerializeField, Min(0f)] private float _upperBodyMultiplier = 1f;
    [SerializeField, Min(0f)] private float _lowerBodyMultiplier = 0.9f;
    [SerializeField, Min(0f)] private float _baseHitScore = 10f;
    [SerializeField, Min(0f)] private float _damageScorePerHealth = 1f;
    [SerializeField, Min(0f)] private float _weaponScoreFactor = 1f;
    [SerializeField, Min(0f)] private float _closeDistanceScoreFactor = 1f;
    [SerializeField, Min(0f)] private float _farDistanceScoreFactor = 1f;
    [SerializeField, Min(0f)] private float _killBonus = 100f;

    public float CloseDamage { get { return _closeDamage; } }
    public float FarDamage { get { return _farDamage; } }
    public float CloseDamageRange { get { return _closeDamageRange; } }
    public float FarDamageRange { get { return _farDamageRange; } }
    public float MaximumHitRange { get { return _maximumHitRange; } }
    public float ProjectileSpeed { get { return _projectileSpeed; } }
    public float ProjectileRadius { get { return _projectileRadius; } }
    public float HeadMultiplier { get { return _headMultiplier; } }
    public float UpperBodyMultiplier { get { return _upperBodyMultiplier; } }
    public float LowerBodyMultiplier { get { return _lowerBodyMultiplier; } }
    public float BaseHitScore { get { return _baseHitScore; } }
    public float DamageScorePerHealth { get { return _damageScorePerHealth; } }
    public float WeaponScoreFactor { get { return _weaponScoreFactor; } }
    public float CloseDistanceScoreFactor { get { return _closeDistanceScoreFactor; } }
    public float FarDistanceScoreFactor { get { return _farDistanceScoreFactor; } }
    public float KillBonus { get { return _killBonus; } }

    public float GetDistanceScoreFactor(float distance)
    {
        if (_farDamageRange <= _closeDamageRange)
        {
            return distance <= _closeDamageRange
                ? _closeDistanceScoreFactor
                : _farDistanceScoreFactor;
        }

        float progress = Mathf.InverseLerp(_closeDamageRange, _farDamageRange, distance);
        return Mathf.Lerp(_closeDistanceScoreFactor, _farDistanceScoreFactor, progress);
    }

    public float GetDamageAtDistance(float distance)
    {
        if (_farDamageRange <= _closeDamageRange)
        {
            return distance <= _closeDamageRange ? _closeDamage : _farDamage;
        }

        float progress = Mathf.InverseLerp(_closeDamageRange, _farDamageRange, distance);
        return Mathf.Lerp(_closeDamage, _farDamage, progress);
    }
}

[Serializable]
public class WeaponAmmoData
{
    [SerializeField, Min(1)] private int _magazineCapacity = 30;
    [SerializeField, Min(0)] private int _startingReserveAmmo = 90;
    [SerializeField, Min(0)] private int _maximumReserveAmmo = 180;
    [SerializeField] private WeaponReloadStyle _reloadStyle;
    [SerializeField] private bool _autoReloadWhenEmpty = true;
    [SerializeField, Min(0f)] private float _originalAmmoResetTimeScale = 1f;

    public int MagazineCapacity { get { return _magazineCapacity; } }
    public int StartingReserveAmmo { get { return _startingReserveAmmo; } }
    public int MaximumReserveAmmo { get { return _maximumReserveAmmo; } }
    public WeaponReloadStyle ReloadStyle { get { return _reloadStyle; } }
    public bool AutoReloadWhenEmpty { get { return _autoReloadWhenEmpty; } }
    public float OriginalAmmoResetTimeScale { get { return _originalAmmoResetTimeScale; } }
}

[Serializable]
public class WeaponAccuracyData
{
    [SerializeField, Min(0f)] private float _hipSpread = 2.5f;
    [SerializeField, Min(0f)] private float _aimSpread = 0.15f;
    [SerializeField, Min(0f)] private float _maximumSpread = 7f;
    [SerializeField, Min(0f)] private float _spreadPerShot = 0.35f;
    [SerializeField, Min(0f)] private float _spreadRecoveryDelay = 0.08f;
    [SerializeField, Min(0f)] private float _spreadRecoverySpeed = 8f;
    [SerializeField, Min(0f)] private float _movingSpreadMultiplier = 1.35f;
    [SerializeField, Min(0f)] private float _sprintingSpreadMultiplier = 2f;
    [SerializeField, Min(0f)] private float _airborneSpreadMultiplier = 2.4f;
    [SerializeField, Min(0f)] private float _crouchingSpreadMultiplier = 0.8f;
    [SerializeField] private bool _firstShotAccurate = true;
    [SerializeField, Min(0f)] private float _pelletSpread = 4f;

    public float HipSpread { get { return _hipSpread; } }
    public float AimSpread { get { return _aimSpread; } }
    public float MaximumSpread { get { return _maximumSpread; } }
    public float SpreadPerShot { get { return _spreadPerShot; } }
    public float SpreadRecoveryDelay { get { return _spreadRecoveryDelay; } }
    public float SpreadRecoverySpeed { get { return _spreadRecoverySpeed; } }
    public float MovingSpreadMultiplier { get { return _movingSpreadMultiplier; } }
    public float SprintingSpreadMultiplier { get { return _sprintingSpreadMultiplier; } }
    public float AirborneSpreadMultiplier { get { return _airborneSpreadMultiplier; } }
    public float CrouchingSpreadMultiplier { get { return _crouchingSpreadMultiplier; } }
    public bool FirstShotAccurate { get { return _firstShotAccurate; } }
    public float PelletSpread { get { return _pelletSpread; } }
}

[Serializable]
public class WeaponGameplayRecoilData
{
    [SerializeField] private Vector2 _verticalRecoil = new Vector2(1f, 1.4f);
    [SerializeField] private Vector2 _horizontalRecoil = new Vector2(-0.4f, 0.4f);
    [SerializeField, Range(0f, 1f)] private float _randomness = 0.35f;
    [SerializeField, Min(0f)] private float _firstShotMultiplier = 1.15f;
    [SerializeField, Min(0f)] private float _aimMultiplier = 0.8f;
    [SerializeField, Min(0f)] private float _crouchMultiplier = 0.85f;
    [SerializeField, Min(0f)] private float _recoveryDelay = 0.08f;
    [SerializeField, Min(0f)] private float _recoverySpeed = 12f;
    [SerializeField] private List<Vector2> _pattern = new List<Vector2>();
    [SerializeField, Min(0f)] private float _patternResetTime = 0.25f;
    [SerializeField, Min(0.01f)] private float _kickSpeed = 45f;

    public Vector2 VerticalRecoil { get { return _verticalRecoil; } }
    public Vector2 HorizontalRecoil { get { return _horizontalRecoil; } }
    public float Randomness { get { return _randomness; } }
    public float FirstShotMultiplier { get { return _firstShotMultiplier; } }
    public float AimMultiplier { get { return _aimMultiplier; } }
    public float CrouchMultiplier { get { return _crouchMultiplier; } }
    public float RecoveryDelay { get { return _recoveryDelay; } }
    public float RecoverySpeed { get { return _recoverySpeed; } }
    public IReadOnlyList<Vector2> Pattern { get { return _pattern; } }
    public float PatternResetTime { get { return _patternResetTime; } }
    public float KickSpeed { get { return _kickSpeed; } }
}

[Serializable]
public class WeaponHandlingData
{
    [SerializeField] private bool _canAim = true;
    [SerializeField, Min(1f)] private float _aimFov = 60f;
    [SerializeField, Min(0f)] private float _aimInDuration = 0.22f;
    [SerializeField, Min(0f)] private float _aimOutDuration = 0.18f;
    [SerializeField, Min(0f)] private float _aimSensitivityMultiplier = 0.75f;
    [SerializeField, Min(0f)] private float _aimMovementSpeedMultiplier = 0.85f;
    [SerializeField, Min(0f)] private float _movementSpeedMultiplier = 1f;
    [SerializeField, Min(0f)] private float _sprintToFireDuration = 0.18f;
    [SerializeField, Min(0f)] private float _aimToFireDuration = 0.08f;
    [SerializeField] private bool _canFireWhileSprinting;
    [SerializeField] private bool _canFireWhileAirborne = true;

    public bool CanAim { get { return _canAim; } }
    public float AimFov { get { return _aimFov; } }
    public float AimInDuration { get { return _aimInDuration; } }
    public float AimOutDuration { get { return _aimOutDuration; } }
    public float AimSensitivityMultiplier { get { return _aimSensitivityMultiplier; } }
    public float AimMovementSpeedMultiplier { get { return _aimMovementSpeedMultiplier; } }
    public float MovementSpeedMultiplier { get { return _movementSpeedMultiplier; } }
    public float SprintToFireDuration { get { return _sprintToFireDuration; } }
    public float AimToFireDuration { get { return _aimToFireDuration; } }
    public bool CanFireWhileSprinting { get { return _canFireWhileSprinting; } }
    public bool CanFireWhileAirborne { get { return _canFireWhileAirborne; } }
}
