using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MapCatalog", menuName = "GunDemo/对局/地图目录")]
public sealed class MapCatalogSO : ScriptableObject
{
    [SerializeField, InspectorName("可选地图")] private List<MapDefinitionSO> _maps =
        new List<MapDefinitionSO>();

    public IReadOnlyList<MapDefinitionSO> Maps { get { return _maps; } }

    public MapDefinitionSO FindByScene(string sceneName)
    {
        for (int i = 0; i < _maps.Count; i++)
        {
            MapDefinitionSO map = _maps[i];
            if (map && string.Equals(
                map.SceneName,
                sceneName,
                System.StringComparison.OrdinalIgnoreCase))
            {
                return map;
            }
        }

        return null;
    }

    public void Configure(IEnumerable<MapDefinitionSO> maps)
    {
        _maps = maps != null
            ? new List<MapDefinitionSO>(maps)
            : new List<MapDefinitionSO>();
    }
}
