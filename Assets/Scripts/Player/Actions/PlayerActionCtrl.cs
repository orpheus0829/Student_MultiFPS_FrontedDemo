using System;
using UnityEngine;

public class PlayerActionCtrl : MonoBehaviour
{
    [Header("动作来源")]
    [SerializeField] private PlayerInputCtrl _inputCtrl;

    [Header("角色移动")]
    [SerializeField] private CharacterController _characterController;

    [Header("玩家数据")]
    [SerializeField] private PlayerDataSO _playerData;

    private Vector2 _moveInput;
    private bool _isSprinting;
    private bool _isFiring;
    private bool _isAiming;
    private float _verticalSpeed;

    public event Action<PlayerInputCommand> CommandReceived;

    public PlayerDataSO PlayerData
    {
        get
        {
            return _playerData;
        }
    }

    public Vector2 MoveInput
    {
        get
        {
            return _moveInput;
        }
    }

    public bool IsSprinting
    {
        get
        {
            return _isSprinting;
        }
    }

    public bool IsFiring
    {
        get
        {
            return _isFiring;
        }
    }

    public bool IsAiming
    {
        get
        {
            return _isAiming;
        }
    }

    private void Awake()
    {
        if (!_characterController)
        {
            _characterController = GetComponent<CharacterController>();
        }

    }

    private void OnEnable()
    {
        if (_inputCtrl)
        {
            _inputCtrl.CommandIssued += Receive;
        }

    }

    private void OnDisable()
    {
        if (_inputCtrl)
        {
            _inputCtrl.CommandIssued -= Receive;
        }

        _moveInput = Vector2.zero;
    }

    private void Update()
    {
        Move();
    }

    public void Receive(PlayerInputCommand command)
    {
        switch (command.Action)
        {
            case GJInputAction.Move:
                _moveInput = command.Value;
                break;
            case GJInputAction.Sprint:
                _isSprinting = command.Phase != PlayerInputPhase.Canceled;
                break;
            case GJInputAction.Fire:
                _isFiring = command.Phase != PlayerInputPhase.Canceled;
                break;
            case GJInputAction.Aim:
                _isAiming = command.Phase != PlayerInputPhase.Canceled;
                break;
            case GJInputAction.JumpVault:
                HandleJumpVault(command.Phase);
                break;
        }

        CommandReceived?.Invoke(command);
    }

    private void Move()
    {
        if (!_characterController || !_playerData)
        {
            return;
        }

        if (_characterController.isGrounded && _verticalSpeed < 0f)
        {
            _verticalSpeed = _playerData.GroundedForce;
        }

        Vector3 horizontal = transform.right * _moveInput.x +
            transform.forward * _moveInput.y;
        if (horizontal.sqrMagnitude > 1f)
        {
            horizontal.Normalize();
        }

        float speed = _isSprinting
            ? _playerData.SprintSpeed
            : _playerData.MoveSpeed;
        _verticalSpeed += _playerData.Gravity * Time.deltaTime;
        Vector3 velocity = horizontal * speed + Vector3.up * _verticalSpeed;
        _characterController.Move(velocity * Time.deltaTime);
    }

    private void HandleJumpVault(PlayerInputPhase phase)
    {
        if (phase != PlayerInputPhase.Started ||
            !_characterController ||
            !_playerData ||
            !_characterController.isGrounded)
        {
            return;
        }

        _verticalSpeed = Mathf.Sqrt(
            _playerData.JumpHeight * -2f * _playerData.Gravity);
    }
}
