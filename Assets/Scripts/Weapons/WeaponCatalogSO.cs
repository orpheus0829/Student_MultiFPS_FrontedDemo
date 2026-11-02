using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponCatalog", menuName = "Gun Demo/枪械/枪械目录")]
public sealed class WeaponCatalogSO : ScriptableObject
{
    [Header("枪械目录")]
    [SerializeField] private List<WeaponDefinitionSO> _weapons = new List<WeaponDefinitionSO>();
    [SerializeField] private WeaponDefinitionSO _defaultWeapon;

    public IReadOnlyList<WeaponDefinitionSO> Weapons { get { return _weapons; } }
    public WeaponDefinitionSO DefaultWeapon { get { return _defaultWeapon; } }

    public bool TryGetWeapon(string weaponId, out WeaponDefinitionSO weapon)
    {
        weapon = null;
        if (string.IsNullOrWhiteSpace(weaponId))
        {
            return false;
        }

        for (int i = 0; i < _weapons.Count; i++)
        {
            WeaponDefinitionSO current = _weapons[i];
            if (!current || current.Basic.WeaponId != weaponId)
            {
                continue;
            }

            weapon = current;
            return true;
        }

        return false;
    }

    private void OnValidate()
    {
        for (int i = _weapons.Count - 1; i >= 0; i--)
        {
            if (!_weapons[i])
            {
                _weapons.RemoveAt(i);
            }
        }

        if (!_defaultWeapon && _weapons.Count > 0)
        {
            _defaultWeapon = _weapons[0];
        }
    }
}
