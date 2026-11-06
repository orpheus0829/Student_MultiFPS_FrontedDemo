using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAnimationCtrl : MonoBehaviour
{
    [Header("唯一枪械数据")]
    [SerializeField, ResourceId(typeof(WeaponDefinitionSO)), InspectorName("当前枪械数据 ID")]
    private string _weaponDataResourceId = "weapon.ak.data";

    [Header("双视角表现")]
    [SerializeField, InspectorName("第一人称表现")] private PlayerAnimationView _firstPersonView;
    [SerializeField, InspectorName("第三人称表现")] private PlayerAnimationView _thirdPersonView;

    public string WeaponDataResourceId
    {
        get
        {
            return _weaponDataResourceId;
        }
    }

    public PlayerAnimationView FirstPersonView
    {
        get
        {
            return _firstPersonView;
        }
    }

    public PlayerAnimationView ThirdPersonView
    {
        get
        {
            return _thirdPersonView;
        }
    }
}
