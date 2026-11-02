using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerAnimationCtrl : MonoBehaviour
{
    [Header("唯一枪械数据")]
    [SerializeField, InspectorName("当前枪械数据")] private WeaponDefinitionSO _weaponData;

    [Header("双视角表现")]
    [SerializeField, InspectorName("第一人称表现")] private PlayerAnimationView _firstPersonView;
    [SerializeField, InspectorName("第三人称表现")] private PlayerAnimationView _thirdPersonView;

    public WeaponDefinitionSO WeaponData
    {
        get
        {
            return _weaponData;
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
