using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponView : MonoBehaviour
{
    [SerializeField, InspectorName("枪械数据")]
    private WeaponDefinitionSO _weaponData;

    [SerializeField, InspectorName("武器 Animator")]
    private Animator _weaponAnimator;

    public WeaponDefinitionSO WeaponData
    {
        get { return _weaponData; }
    }

    public Animator WeaponAnimator
    {
        get { return _weaponAnimator; }
    }
}
