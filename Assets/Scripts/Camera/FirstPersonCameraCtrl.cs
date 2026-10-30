using UnityEngine;

public class FirstPersonCameraCtrl : MonoBehaviour
{
    [Header("输入来源")]
    [SerializeField] private PlayerInputCtrl _inputCtrl;

    [Header("相机层级")]
    [SerializeField] private Transform _yawRoot;
    [SerializeField] private Transform _pitchPivot;

    [Header("玩家数据")]
    [SerializeField] private PlayerDataSO _playerData;

    private Vector2 _lookInput;
    private float _yaw;
    private float _pitch;
    private bool _wasControlled;

    private void Awake()
    {
        if (_yawRoot == null)
        {
            _yawRoot = transform.root;
        }

        if (_pitchPivot == null)
        {
            _pitchPivot = transform;
        }

        SyncAngles();
    }

    private void OnEnable()
    {
        if (_inputCtrl != null)
        {
            _inputCtrl.CommandIssued += Receive;
        }
    }

    private void OnDisable()
    {
        if (_inputCtrl != null)
        {
            _inputCtrl.CommandIssued -= Receive;
        }
        _lookInput = Vector2.zero;
    }

    private void LateUpdate()
    {
        if (CameraControlAuthority.IsControlled)
        {
            _wasControlled = true;
            _lookInput = Vector2.zero;
            return;
        }

        if (_wasControlled)
        {
            _wasControlled = false;
            SyncAngles();
        }

        float sensitivity = _playerData != null ? _playerData.LookSensitivity : 0.1f;
        float minPitch = _playerData != null ? _playerData.MinPitch : -85f;
        float maxPitch = _playerData != null ? _playerData.MaxPitch : 85f;

        _yaw += _lookInput.x * sensitivity;
        _pitch = Mathf.Clamp(_pitch - _lookInput.y * sensitivity, minPitch, maxPitch);
        _yawRoot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        _pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        _lookInput = Vector2.zero;
    }

    private void Receive(PlayerInputCommand command)
    {
        if (command.Action == GJInputAction.Look)
        {
            _lookInput = command.Value;
        }
    }

    private void SyncAngles()
    {
        _yaw = NormalizeAngle(_yawRoot.localEulerAngles.y);
        _pitch = NormalizeAngle(_pitchPivot.localEulerAngles.x);
    }

    private float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }
}
