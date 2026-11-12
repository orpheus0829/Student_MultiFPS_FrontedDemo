using System;
using UnityEngine;

public sealed class AIMotionActuator
{
    private readonly Action<GJInputAction, PlayerInputPhase, Vector2> _send;
    private readonly Transform _owner;
    private readonly FirstPersonCameraCtrl _camera;
    private readonly PlayerMoveCtrl _move;
    private readonly AIPathNavigator _navigator = new AIPathNavigator();
    private Vector3 _destination;
    private Vector3 _lastSamplePosition;
    private float _nextStuckSample;
    private float _nextMobilityTime;
    private int _stuckSamples;
    private bool _hasDestination;
    private bool _sprintHeld;

    public int StuckRecoveryCount { get; private set; }
    public int PathFailureCount { get; private set; }

    public AIMotionActuator(
        Action<GJInputAction, PlayerInputPhase, Vector2> send,
        Transform owner,
        FirstPersonCameraCtrl camera,
        PlayerMoveCtrl move)
    {
        _send = send;
        _owner = owner;
        _camera = camera;
        _move = move;
        _lastSamplePosition = owner.position;
    }

    public void Tick(AIIntent intent, AIDifficultyProfile profile)
    {
        if (!intent.HasMoveDestination)
        {
            Stop();
            return;
        }
        if (!_hasDestination || Vector3.Distance(_destination, intent.MoveDestination) > 1.5f)
        {
            _destination = intent.MoveDestination;
            _hasDestination = _navigator.Calculate(_owner.position, _destination);
            if (!_hasDestination) PathFailureCount++;
        }
        if (!_hasDestination || _navigator.HasArrived(_owner.position, 0.8f))
        {
            Stop();
            return;
        }
        Vector3 world = _navigator.GetDirection(_owner.position);
        Vector3 forward = _camera.GetPlanarLookDirection();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector2 input = new Vector2(Vector3.Dot(world, right), Vector3.Dot(world, forward));
        _send(GJInputAction.Move, PlayerInputPhase.Performed, Vector2.ClampMagnitude(input, 1f));
        SetSprinting(intent.Sprint && !intent.Aim && !intent.Fire);
        TryMobility(intent, profile);
        TickStuckRecovery(profile);
    }

    public void Stop()
    {
        _send(GJInputAction.Move, PlayerInputPhase.Performed, Vector2.zero);
        SetSprinting(false);
    }

    private void TickStuckRecovery(AIDifficultyProfile profile)
    {
        if (Time.time < _nextStuckSample) return;
        _nextStuckSample = Time.time + 0.5f;
        float moved = Vector3.Distance(_owner.position, _lastSamplePosition);
        _lastSamplePosition = _owner.position;
        _stuckSamples = moved < 0.1f ? _stuckSamples + 1 : 0;
        if (_stuckSamples < 2) return;
        _stuckSamples = 0;
        StuckRecoveryCount++;
        _hasDestination = _navigator.Calculate(_owner.position, _destination);
        if (!_hasDestination && Time.time >= _nextMobilityTime)
        {
            _nextMobilityTime = Time.time + 2f;
            _send(GJInputAction.JumpVault, PlayerInputPhase.Started, Vector2.zero);
        }
    }

    private void TryMobility(AIIntent intent, AIDifficultyProfile profile)
    {
        if (Time.time < _nextMobilityTime || !_move.IsGrounded || intent.Aim || intent.Fire)
        {
            return;
        }
        if (profile.CanTacticalSlide && _sprintHeld &&
            (intent.Behavior == AIBehaviorId.Retreat ||
             intent.Behavior == AIBehaviorId.Reposition))
        {
            _nextMobilityTime = Time.time + 2.2f;
            _send(GJInputAction.Slide, PlayerInputPhase.Started, Vector2.zero);
            return;
        }
        if (profile.CanCombatJump && intent.Behavior == AIBehaviorId.Reposition)
        {
            _nextMobilityTime = Time.time + 3f;
            _send(GJInputAction.JumpVault, PlayerInputPhase.Started, Vector2.zero);
        }
    }

    private void SetSprinting(bool active)
    {
        if (_sprintHeld == active) return;
        _sprintHeld = active;
        _send(GJInputAction.Sprint,
            active ? PlayerInputPhase.Started : PlayerInputPhase.Canceled, Vector2.zero);
    }
}
