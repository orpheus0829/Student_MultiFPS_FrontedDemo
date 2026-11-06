using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponView : MonoBehaviour
{
    [SerializeField, ResourceId(typeof(WeaponDefinitionSO)), InspectorName("枪械数据 ID")]
    private string _weaponDataResourceId;

    [SerializeField, InspectorName("武器 Animator")]
    private Animator _weaponAnimator;

    public string WeaponDataResourceId
    {
        get { return _weaponDataResourceId; }
    }

    public Animator WeaponAnimator
    {
        get { return _weaponAnimator; }
    }
}
