using System;
using UnityEngine;
using UnityEngine.AI;

public sealed class AIMotionActuator
{
    private const float SlideClearanceDistance = 2.8f;

    private readonly Action<GJInputAction, PlayerInputPhase, Vector2> _send;
    private readonly Transform _owner;
    private readonly FirstPersonCameraCtrl _camera;
    private readonly PlayerMoveCtrl _move;
    private readonly AIPathNavigator _navigator = new AIPathNavigator();
    private readonly RaycastHit[] _slideHits = new RaycastHit[12];
    private readonly CharacterController _characterController;
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
        _characterController = owner.GetComponent<CharacterController>();
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
        TryMobility(intent, profile, world);
        TickStuckRecovery(profile);
    }

    public void Stop()
    {
        _send(GJInputAction.Move, PlayerInputPhase.Performed, Vector2.zero);
        SetSprinting(false);
        _hasDestination = false;
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

    private void TryMobility(
        AIIntent intent,
        AIDifficultyProfile profile,
        Vector3 movementDirection)
    {
        if (Time.time < _nextMobilityTime || !_move.IsGrounded || intent.Aim || intent.Fire)
        {
            return;
        }
        float distance = Vector3.Distance(_owner.position, _destination);
        if (profile.CanTacticalSlide && _sprintHeld && distance > 5f &&
            (intent.Behavior == AIBehaviorId.Retreat ||
             intent.Behavior == AIBehaviorId.Reposition) &&
            CanSlideSafely(movementDirection))
        {
            _nextMobilityTime = Time.time + 2.2f;
            _send(GJInputAction.Slide, PlayerInputPhase.Started, Vector2.zero);
            return;
        }
        if (profile.CanCombatJump && intent.Behavior == AIBehaviorId.Reposition &&
            distance >= 2.5f && distance <= 8f)
        {
            _nextMobilityTime = Time.time +
                (profile.Difficulty == AIDifficulty.Hard ? 2.4f : 4.5f);
            _send(GJInputAction.JumpVault, PlayerInputPhase.Started, Vector2.zero);
        }
    }

    private bool CanSlideSafely(Vector3 movementDirection)
    {
        movementDirection.y = 0f;
        if (!_characterController || movementDirection.sqrMagnitude < 0.01f)
        {
            return false;
        }

        Vector3 direction = movementDirection.normalized;
        Vector3 start = _owner.position;
        if (NavMesh.Raycast(
            start,
            start + direction * SlideClearanceDistance,
            out _,
            NavMesh.AllAreas))
        {
            return false;
        }

        Bounds bounds = _characterController.bounds;
        float radius = Mathf.Max(0.1f, _characterController.radius * 0.82f);
        float slideHeight = Mathf.Max(radius * 2f, bounds.size.y * 0.55f);
        float segment = Mathf.Max(0f, slideHeight * 0.5f - radius);
        Vector3 bottom = bounds.center - Vector3.up * segment;
        Vector3 top = bounds.center + Vector3.up * segment;
        int count = Physics.CapsuleCastNonAlloc(
            bottom,
            top,
            radius,
            direction,
            _slideHits,
            SlideClearanceDistance,
            Physics.AllLayers,
            QueryTriggerInteraction.Ignore);
        for (int i = 0; i < count; i++)
        {
            Transform hit = _slideHits[i].transform;
            if (!hit || hit == _owner || hit.IsChildOf(_owner))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private void SetSprinting(bool active)
    {
        if (_sprintHeld == active) return;
        _sprintHeld = active;
        _send(GJInputAction.Sprint,
            active ? PlayerInputPhase.Started : PlayerInputPhase.Canceled, Vector2.zero);
    }
}
