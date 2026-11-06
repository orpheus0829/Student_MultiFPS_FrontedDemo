using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponCatalog", menuName = "Gun Demo/枪械/枪械目录")]
public sealed class WeaponCatalogSO : ScriptableObject
{
    [Header("枪械目录")]
    [SerializeField, ResourceId(typeof(WeaponDefinitionSO))] private List<string> _weaponResourceIds = new List<string>();
    [SerializeField, ResourceId(typeof(WeaponDefinitionSO))] private string _defaultWeaponResourceId;

    public IReadOnlyList<string> WeaponResourceIds { get { return _weaponResourceIds; } }
    public string DefaultWeaponResourceId { get { return _defaultWeaponResourceId; } }

    public bool TryGetWeaponResourceId(string weaponId, out string resourceId)
    {
        resourceId = string.Empty;
        if (string.IsNullOrWhiteSpace(weaponId))
        {
            return false;
        }

        string expected = "weapon." + weaponId.Trim().ToLowerInvariant() + ".data";
        for (int i = 0; i < _weaponResourceIds.Count; i++)
        {
            if (!string.Equals(_weaponResourceIds[i], expected, System.StringComparison.Ordinal))
            {
                continue;
            }

            resourceId = expected;
            return true;
        }

        return false;
    }

    private void OnValidate()
    {
        HashSet<string> seen = new HashSet<string>(System.StringComparer.Ordinal);
        for (int i = _weaponResourceIds.Count - 1; i >= 0; i--)
        {
            string resourceId = _weaponResourceIds[i];
            if (string.IsNullOrWhiteSpace(resourceId) || !seen.Add(resourceId))
            {
                _weaponResourceIds.RemoveAt(i);
            }
        }

        if (string.IsNullOrWhiteSpace(_defaultWeaponResourceId) && _weaponResourceIds.Count > 0)
        {
            _defaultWeaponResourceId = _weaponResourceIds[0];
        }
    }
}
