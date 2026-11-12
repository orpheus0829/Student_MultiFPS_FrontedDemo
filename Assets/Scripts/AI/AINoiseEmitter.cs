using UnityEngine;

[DisallowMultipleComponent]
public sealed class AINoiseEmitter : MonoBehaviour
{
    private const float SprintNoiseRadius = 14f;
    private const float SlideNoiseRadius = 20f;
    private const float SprintStepInterval = 0.34f;
    private const float SlideNoiseInterval = 0.55f;

    private WeaponCombatCtrl _combat;
    private PlayerWeaponViewCtrl _weaponView;
    private PlayerMoveCtrl _move;
    private float _nextMovementNoiseTime;

    private void Awake()
    {
        TryGetComponent(out _combat);
        TryGetComponent(out _weaponView);
        TryGetComponent(out _move);
    }

    private void OnEnable()
    {
        if (_combat)
        {
            _combat.ShotFired += HandleShotFired;
        }
    }

    private void Update()
    {
        if (!_move || Time.time < _nextMovementNoiseTime)
        {
            return;
        }
        if (_move.IsSliding)
        {
            AINoiseBus.Emit(transform.position, SlideNoiseRadius, 1.8f, transform);
            _nextMovementNoiseTime = Time.time + SlideNoiseInterval;
        }
        else if (_move.IsSprinting && _move.IsGrounded)
        {
            AINoiseBus.Emit(transform.position, SprintNoiseRadius, 1.25f, transform);
            _nextMovementNoiseTime = Time.time + SprintStepInterval;
        }
    }

    private void OnDisable()
    {
        if (_combat)
        {
            _combat.ShotFired -= HandleShotFired;
        }
    }

    private void HandleShotFired(WeaponDefinitionSO weapon)
    {
        Transform muzzle = _weaponView && _weaponView.ThirdPersonMuzzlePoint
            ? _weaponView.ThirdPersonMuzzlePoint
            : transform;
        AINoiseBus.Emit(muzzle.position, GetGunshotRadius(weapon), 0.75f, transform);
    }

    private static float GetGunshotRadius(WeaponDefinitionSO weapon)
    {
        if (!weapon)
        {
            return 32f;
        }
        if ((weapon.Basic.Tags & WeaponTag.Suppressed) != 0)
        {
            return 16f;
        }
        switch (weapon.Basic.Category)
        {
            case WeaponCategory.SniperRifle:
            case WeaponCategory.Shotgun:
                return 60f;
            case WeaponCategory.Pistol:
            case WeaponCategory.SubmachineGun:
                return 34f;
            default:
                return 48f;
        }
    }
}
