using UnityEngine;

[CreateAssetMenu(fileName = "PlayerData", menuName = "Gun Demo/玩家/玩家数据")]
public class PlayerDataSO : ScriptableObject
{
    [Header("移动")]
    [SerializeField] private float _moveSpeed = 5f;
    [SerializeField] private float _sprintSpeed = 8f;

    [Header("第一人称视角")]
    [SerializeField] private float _lookSensitivity = 0.1f;
    [SerializeField] private float _minPitch = -85f;
    [SerializeField] private float _maxPitch = 85f;

    public float MoveSpeed => _moveSpeed;
    public float SprintSpeed => _sprintSpeed;
    public float LookSensitivity => _lookSensitivity;
    public float MinPitch => _minPitch;
    public float MaxPitch => _maxPitch;
}
