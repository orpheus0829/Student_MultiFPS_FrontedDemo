using System;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public class PlayerActionCtrl : MonoBehaviour
{
    [Header("动作来源")]
    [SerializeField] private PlayerInputCtrl _inputCtrl;

    [Header("动作数据")]
    [SerializeField] private PlayerDataSO _playerData;
    [SerializeField] private PlayerActionSetSO _actionSet;

    [Header("Timeline")]
    [SerializeField] private PlayableDirector _director;

    private Vector2 _moveInput;
    private bool _isSprinting;
    private bool _isFiring;
    private bool _isAiming;

    public event Action<PlayerInputCommand> CommandReceived;

    public PlayerDataSO PlayerData => _playerData;
    public Vector2 MoveInput => _moveInput;
    public bool IsSprinting => _isSprinting;
    public bool IsFiring => _isFiring;
    public bool IsAiming => _isAiming;

    private void Awake()
    {
        if (_director == null)
        {
            _director = GetComponent<PlayableDirector>();
        }
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
        }

        if (command.Action != GJInputAction.Move && command.Action != GJInputAction.Look)
        {
            Play(command.Action, command.Phase);
        }

        CommandReceived?.Invoke(command);
    }

    private void Play(GJInputAction action, PlayerInputPhase phase)
    {
        if (_director == null || _actionSet == null)
        {
            return;
        }

        TimelineAsset timeline = _actionSet.Get(action, phase);
        if (timeline == null)
        {
            return;
        }

        _director.playableAsset = timeline;
        _director.Play();
    }
}
