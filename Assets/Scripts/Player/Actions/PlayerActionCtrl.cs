using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class PlayerActionCtrl : MonoBehaviour
{
    [Header("动作来源")]
    [SerializeField] private PlayerInputCtrl _inputCtrl;

    [Header("角色移动")]
    [SerializeField] private CharacterController _characterController;

    [Header("动作数据")]
    [SerializeField] private PlayerDataSO _playerData;
    [SerializeField] private PlayerActionSetSO _actionSet;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector _director;

    private Vector2 _moveInput;
    private bool _isSprinting;
    private bool _isFiring;
    private bool _isAiming;
    private bool _isVaulting;
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

        if (!_director)
        {
            _director = GetComponent<PlayableDirector>();
        }
    }

    private void OnEnable()
    {
        if (_inputCtrl)
        {
            _inputCtrl.CommandIssued += Receive;
        }

        if (_director)
        {
            _director.stopped += OnTimelineStopped;
        }
    }

    private void OnDisable()
    {
        if (_inputCtrl)
        {
            _inputCtrl.CommandIssued -= Receive;
        }

        if (_director)
        {
            _director.stopped -= OnTimelineStopped;
        }

        _moveInput = Vector2.zero;
        _isVaulting = false;
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

        if (ShouldPlayTimeline(command.Action))
        {
            Play(command.Action, command.Phase);
        }

        CommandReceived?.Invoke(command);
    }

    private bool Play(GJInputAction action, PlayerInputPhase phase)
    {
        if (!_director || !_actionSet)
        {
            return false;
        }

        TimelineAsset timeline = _actionSet.Get(action, phase);
        if (!timeline)
        {
            return false;
        }

        _director.playableAsset = timeline;
        _director.Play();
        return true;
    }

    private void Move()
    {
        if (!_characterController || !_playerData || _isVaulting)
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

        if (CanVault())
        {
            if (Play(GJInputAction.JumpVault, phase))
            {
                _isVaulting = true;
                _verticalSpeed = 0f;
                return;
            }
        }

        _verticalSpeed = Mathf.Sqrt(
            _playerData.JumpHeight * -2f * _playerData.Gravity);
    }

    private bool CanVault()
    {
        Vector3 lowOrigin = transform.position +
            Vector3.up * _playerData.VaultLowHeight;
        Vector3 highOrigin = transform.position +
            Vector3.up * _playerData.VaultHighHeight;
        bool hasLowObstacle = Physics.Raycast(
            lowOrigin,
            transform.forward,
            _playerData.VaultDistance,
            _playerData.VaultMask,
            QueryTriggerInteraction.Ignore);
        bool hasHighObstacle = Physics.Raycast(
            highOrigin,
            transform.forward,
            _playerData.VaultDistance,
            _playerData.VaultMask,
            QueryTriggerInteraction.Ignore);
        return hasLowObstacle && !hasHighObstacle;
    }

    private bool ShouldPlayTimeline(GJInputAction action)
    {
        return action != GJInputAction.Move &&
            action != GJInputAction.Look &&
            action != GJInputAction.Sprint &&
            action != GJInputAction.JumpVault;
    }

    private void OnTimelineStopped(PlayableDirector director)
    {
        _isVaulting = false;
    }
}
