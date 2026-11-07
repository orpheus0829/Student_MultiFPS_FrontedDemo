using KINEMATION.FPSAnimationPack.Scripts.Weapon;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponAmmoViewProvider : MonoBehaviour, IAmmoProvider
{
    private WeaponActionCtrl _actionCtrl;
    private WeaponCombatCtrl _combatCtrl;

    private void Awake()
    {
        _actionCtrl = GetComponentInParent<WeaponActionCtrl>();
        _combatCtrl = GetComponentInParent<WeaponCombatCtrl>();
    }

    public int GetActiveAmmo()
    {
        return _combatCtrl ? _combatCtrl.MagazineAmmo : 0;
    }

    public int GetMaxAmmo()
    {
        WeaponDefinitionSO weaponData = _actionCtrl
            ? _actionCtrl.CurrentWeaponData
            : null;
        return weaponData ? weaponData.Ammo.MagazineCapacity : 1;
    }
}
