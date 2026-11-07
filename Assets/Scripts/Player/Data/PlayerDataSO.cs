using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Gun Demo/玩家/玩家数据")]
public class PlayerDataSO : ScriptableObject
{
    [Header("移动")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _sprintSpeed = 8f;

    [Header("跳跃与重力")]
    [SerializeField, Min(0f)] private float _jumpHeight = 1.2f;
    [SerializeField] private float _gravity = -25f;
    [SerializeField] private float _groundedForce = -2f;

    [Header("翻越检测")]
    [SerializeField, Min(0f)] private float _vaultDistance = 0.9f;
    [SerializeField, Min(0f)] private float _vaultLowHeight = 0.45f;
    [SerializeField, Min(0f)] private float _vaultHighHeight = 1.4f;
    [SerializeField, Min(0f), InspectorName("落点前移距离")] private float _vaultLandingForwardOffset = 0.2f;
    [SerializeField, Range(0f, 60f), InspectorName("落点最大坡度")] private float _vaultMaxSurfaceAngle = 45f;
    [SerializeField, Min(0f), InspectorName("站立空间余量")] private float _vaultClearancePadding = 0.03f;
    [SerializeField] private LayerMask _vaultMask = ~0;

    [Header("第一人称视角")]
    [SerializeField] private float _lookSensitivity = 0.1f;
    [SerializeField] private float _minPitch = -85f;
    [SerializeField] private float _maxPitch = 85f;

    public float MoveSpeed
    {
        get
        {
            return _moveSpeed;
        }
    }

    public float SprintSpeed
    {
        get
        {
            return _sprintSpeed;
        }
    }

    public float JumpHeight
    {
        get
        {
            return _jumpHeight;
        }
    }

    public float Gravity
    {
        get
        {
            return _gravity;
        }
    }

    public float GroundedForce
    {
        get
        {
            return _groundedForce;
        }
    }

    public float VaultDistance
    {
        get
        {
            return _vaultDistance;
        }
    }

    public float VaultLowHeight
    {
        get
        {
            return _vaultLowHeight;
        }
    }

    public float VaultHighHeight
    {
        get
        {
            return _vaultHighHeight;
        }
    }

    public float VaultLandingForwardOffset
    {
        get
        {
            return _vaultLandingForwardOffset;
        }
    }

    public float VaultMaxSurfaceAngle
    {
        get
        {
            return _vaultMaxSurfaceAngle;
        }
    }

    public float VaultClearancePadding
    {
        get
        {
            return _vaultClearancePadding;
        }
    }

    public LayerMask VaultMask
    {
        get
        {
            return _vaultMask;
        }
    }

    public float LookSensitivity
    {
        get
        {
            return _lookSensitivity;
        }
    }

    public float MinPitch
    {
        get
        {
            return _minPitch;
        }
    }

    public float MaxPitch
    {
        get
        {
            return _maxPitch;
        }
    }
}
