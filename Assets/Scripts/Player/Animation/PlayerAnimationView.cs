using UnityEngine;

public enum PlayerViewType
{
    [InspectorName("第一人称")] FirstPerson = 0,
    [InspectorName("第三人称")] ThirdPerson = 1
}

[DisallowMultipleComponent]
public sealed class PlayerAnimationView : MonoBehaviour
{
    [Header("视角")]
    [SerializeField, InspectorName("表现视角")] private PlayerViewType _viewType;

    [Header("Animator")]
    [SerializeField, InspectorName("角色 Animator")] private Animator _characterAnimator;
    [SerializeField, InspectorName("当前武器 Animator")] private Animator _weaponAnimator;

    [Header("武器")]
    [SerializeField, InspectorName("武器挂点")] private Transform _weaponRoot;

    public PlayerViewType ViewType
    {
        get
        {
            return _viewType;
        }
    }

    public Animator CharacterAnimator
    {
        get
        {
            return _characterAnimator;
        }
    }

    public Animator WeaponAnimator
    {
        get
        {
            return _weaponAnimator;
        }
    }

    public Transform WeaponRoot
    {
        get
        {
            return _weaponRoot;
        }
    }
}
