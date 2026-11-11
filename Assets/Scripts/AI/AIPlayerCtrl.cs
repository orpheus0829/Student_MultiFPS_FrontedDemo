using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class AIPlayerCtrl : MonoBehaviour, IAIDifficultyControl
{
    private const float EyeHeight = 1.35f;
    private const float AimHeight = 1.15f;
    private const float PathRefreshDistance = 1.5f;

    [Header("AI 身份")]
    [SerializeField, InspectorName("初始难度")] private AIDifficulty _difficulty = AIDifficulty.Normal;

    [Header("导航与掩体")]
    [SerializeField, Min(3f), InspectorName("巡逻半径")] private float _patrolRadius = 18f;
    [SerializeField, Min(3f), InspectorName("掩体搜索半径")] private float _coverSearchRadius = 14f;
    [SerializeField, InspectorName("掩体障碍层")] private LayerMask _coverMask = ~0;

    [Header("感知")]
    [SerializeField, InspectorName("视线遮挡层")] private LayerMask _sightMask = ~0;

    private readonly Dictionary<AIStateId, AIState> _states = new Dictionary<AIStateId, AIState>();
    private readonly RaycastHit[] _sightHits = new RaycastHit[32];
    private readonly AICoverFinder _coverFinder = new AICoverFinder();

    private AIPathNavigator _navigator;
    private PlayerActionCtrl _actionCtrl;
    private PlayerMoveCtrl _moveCtrl;
    private PlayerAimCtrl _aimCtrl;
    private WeaponCombatCtrl _combatCtrl;
    private WeaponActionCtrl _weaponActionCtrl;
    private FirstPersonCameraCtrl _cameraCtrl;
    private CharacterHealth _health;
    private AIState _currentState;
    private AIDifficultyProfile _profile;
    private CharacterHealth _target;
    private Vector3 _destination;
    private Vector3 _lastPathDestination;
    private Vector2 _aimError;
    private float _nextSenseTime;
    private float _nextAimErrorTime;
    private float _nextSemiShotTime;
    private float _nextMobilityTime;
    private bool _hasDestination;
    private bool _hasCoverDestination;
    private bool _hasVisibleTarget;
    private bool _isFiring;
    private bool _isAiming;
    private bool _isSprintInputHeld;
    private bool _wantsAim;

    public AIDifficulty Difficulty { get { return _difficulty; } }
    public AIDifficultyProfile Profile { get { return _profile; } }
    public AIStateId CurrentState { get { return _currentState != null ? _currentState.Id : AIStateId.Search; } }
    public CharacterHealth Target { get { return _target; } }
    public bool HasLivingTarget { get { return _target && !_target.IsDead; } }
    public bool HasVisibleTarget { get { return _hasVisibleTarget; } }
    public bool HasCoverDestination { get { return _hasCoverDestination; } }
    public Vector3 TargetPosition { get { return _target ? _target.transform.position : transform.position; } }
    public float TargetDistance { get { return _target ? Vector3.Distance(transform.position, _target.transform.position) : float.PositiveInfinity; } }
    public bool ShouldAim { get { return _wantsAim; } }
    public bool ShouldRetreatToCover
    {
        get
        {
            return _health && _health.CurrentHealth / Mathf.Max(1f, _health.MaxHealth) <=
                _profile.RetreatHealthRatio;
        }
    }

    public event Action<AIDifficulty> DifficultyChanged;
    public event Action<AIStateId> StateChanged;

    private void Awake()
    {
        _navigator = new AIPathNavigator();
        TryGetComponent(out _actionCtrl);
        TryGetComponent(out _moveCtrl);
        TryGetComponent(out _aimCtrl);
        TryGetComponent(out _combatCtrl);
        TryGetComponent(out _weaponActionCtrl);
        TryGetComponent(out _health);
        _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);

        if (!_actionCtrl || !_moveCtrl || !_aimCtrl || !_combatCtrl ||
            !_weaponActionCtrl || !_cameraCtrl || !_health)
        {
            Debug.LogError($"{name} 的 AI 马夫缺少玩家动作组件。", this);
            enabled = false;
            return;
        }

        _states.Add(AIStateId.Search, new AISearchState(this));
        _states.Add(AIStateId.Engage, new AIEngageState(this));
        _states.Add(AIStateId.Cover, new AICoverState(this));
        SetDifficulty(_difficulty);
    }

    private void Start()
    {
        _cameraCtrl.SetThirdPerson(true);
        ChangeState(AIStateId.Search);
    }

    private void Update()
    {
        if (_health.IsDead)
        {
            StopAllCommands();
            return;
        }

        _currentState?.OnUpdate();
        RefreshSemiAutomaticFire();
        if (_combatCtrl.MagazineAmmo <= 0 && _combatCtrl.ReserveAmmo > 0)
        {
            SetFiring(false);
            Send(GJInputAction.Reload, PlayerInputPhase.Started);
        }
    }

    private void OnDisable()
    {
        StopAllCommands();
        _currentState?.OnExit();
        _currentState = null;
    }

    public void SetDifficulty(AIDifficulty difficulty)
    {
        _difficulty = difficulty;
        _profile = AIDifficultyProfile.Create(difficulty);
        _nextSenseTime = 0f;
        _nextAimErrorTime = 0f;
        DifficultyChanged?.Invoke(_difficulty);
    }

    public bool TrySetDifficulty(string difficulty)
    {
        if (!Enum.TryParse(difficulty, true, out AIDifficulty parsed))
        {
            return false;
        }

        SetDifficulty(parsed);
        return true;
    }

    public void ChangeState(AIStateId stateId)
    {
        if (_currentState != null && _currentState.Id == stateId)
        {
            return;
        }
        if (!_states.TryGetValue(stateId, out AIState nextState))
        {
            return;
        }

        _currentState?.OnExit();
        _currentState = nextState;
        _currentState.OnEnter();
        StateChanged?.Invoke(stateId);
    }

    public bool TryAcquireTarget()
    {
        if (HasLivingTarget && Time.time < _nextSenseTime)
        {
            return true;
        }

        _nextSenseTime = Time.time + _profile.ReactionTime;
        CharacterHealth[] actors = FindObjectsByType<CharacterHealth>(FindObjectsSortMode.None);
        CharacterHealth bestTarget = null;
        float bestDistance = _profile.SightRange * _profile.SightRange;
        for (int i = 0; i < actors.Length; i++)
        {
            CharacterHealth actor = actors[i];
            if (!actor || actor == _health || actor.IsDead || actor.transform.IsChildOf(transform))
            {
                continue;
            }

            float distance = (actor.transform.position - transform.position).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            bestTarget = actor;
        }

        _target = bestTarget;
        RefreshTargetVisibility();
        return HasLivingTarget;
    }

    public void RefreshTargetVisibility()
    {
        _hasVisibleTarget = false;
        if (!HasLivingTarget)
        {
            return;
        }

        Vector3 origin = transform.position + Vector3.up * EyeHeight;
        Vector3 targetPoint = _target.transform.position + Vector3.up * AimHeight;
        Vector3 direction = targetPoint - origin;
        int hitCount = Physics.RaycastNonAlloc(
            origin, direction.normalized, _sightHits, direction.magnitude,
            _sightMask, QueryTriggerInteraction.Collide);
        float nearest = float.PositiveInfinity;
        CharacterHealth seen = null;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _sightHits[i];
            if (hit.transform.IsChildOf(transform) || hit.distance >= nearest)
            {
                continue;
            }
            nearest = hit.distance;
            BodyHitbox hitbox = hit.collider.GetComponent<BodyHitbox>();
            seen = hitbox ? hitbox.CharacterHealth : hit.collider.GetComponentInParent<CharacterHealth>();
        }
        _hasVisibleTarget = seen == _target;
    }

    public void AimAtTarget()
    {
        if (!HasLivingTarget)
        {
            return;
        }
        if (Time.time >= _nextAimErrorTime)
        {
            _aimError = UnityEngine.Random.insideUnitCircle * _profile.AimError;
            _wantsAim = UnityEngine.Random.value <= _profile.AimChance;
            _nextAimErrorTime = Time.time + Mathf.Max(0.08f, _profile.ReactionTime);
        }

        Vector3 origin = _cameraCtrl.GetCenterAimRay().origin;
        Vector3 targetPoint = _target.transform.position + Vector3.up * AimHeight;
        Quaternion error = Quaternion.Euler(_aimError.y, _aimError.x, 0f);
        _cameraCtrl.SetAimDirection(error * (targetPoint - origin).normalized);
    }

    public void SetAiming(bool active)
    {
        if (active)
        {
            SetSprinting(false);
        }
        if (_isAiming == active)
        {
            return;
        }
        _isAiming = active;
        Send(GJInputAction.Aim, active ? PlayerInputPhase.Started : PlayerInputPhase.Canceled);
    }

    public void SetFiring(bool active)
    {
        if (active)
        {
            SetSprinting(false);
        }
        if (_isFiring == active)
        {
            return;
        }
        _isFiring = active;
        Send(GJInputAction.Fire, active ? PlayerInputPhase.Started : PlayerInputPhase.Canceled);
        _nextSemiShotTime = Time.time;
    }

    public void SetDestination(Vector3 destination)
    {
        if (_hasDestination && Vector3.Distance(destination, _lastPathDestination) < PathRefreshDistance)
        {
            return;
        }

        _destination = destination;
        _lastPathDestination = destination;
        _hasDestination = _navigator.Calculate(transform.position, destination);
    }

    public void FollowPath(bool sprint)
    {
        if (!_hasDestination || _navigator.HasArrived(transform.position, 0.75f))
        {
            StopMoving();
            return;
        }

        Vector3 direction = _navigator.GetDirection(transform.position);
        SetWorldMove(direction, sprint);
    }

    public bool HasArrived(float stoppingDistance)
    {
        return !_hasDestination || _navigator.HasArrived(transform.position, stoppingDistance);
    }

    public void StopMoving()
    {
        Send(GJInputAction.Move, PlayerInputPhase.Performed, Vector2.zero);
        SetSprinting(false);
    }

    public void ChoosePatrolDestination()
    {
        Vector2 circle = UnityEngine.Random.insideUnitCircle * _patrolRadius;
        SetDestination(transform.position + new Vector3(circle.x, 0f, circle.y));
    }

    public void ChooseCombatDestination()
    {
        if (!HasLivingTarget)
        {
            return;
        }
        Vector3 away = (transform.position - _target.transform.position).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, away) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        float rangeError = TargetDistance - _profile.PreferredRange;
        Vector3 destination = transform.position + side * UnityEngine.Random.Range(3f, 7f) -
            away * Mathf.Clamp(rangeError, -4f, 4f);
        SetDestination(destination);
    }

    public bool FindCoverDestination()
    {
        if (!HasLivingTarget || !_coverFinder.TryFind(
            transform.position, _target.transform.position, _coverSearchRadius,
            _coverMask, out Vector3 cover))
        {
            _hasCoverDestination = false;
            ChooseCombatDestination();
            return false;
        }

        _hasCoverDestination = true;
        SetDestination(cover);
        return true;
    }

    public void ChooseCoverPeekDestination()
    {
        if (!_hasCoverDestination || !HasLivingTarget)
        {
            return;
        }
        Vector3 toTarget = (_target.transform.position - transform.position).normalized;
        Vector3 side = Vector3.Cross(Vector3.up, toTarget) *
            (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        SetDestination(transform.position + side * UnityEngine.Random.Range(1.1f, 2.2f));
    }

    public void ClearCoverDestination()
    {
        _hasCoverDestination = false;
    }

    public void TryCombatMobility()
    {
        if (Time.time < _nextMobilityTime || !_moveCtrl.IsGrounded)
        {
            return;
        }

        _nextMobilityTime = Time.time + (_difficulty == AIDifficulty.Hard
            ? UnityEngine.Random.Range(0.7f, 1.5f)
            : UnityEngine.Random.Range(1.8f, 3.2f));
        if (_profile.CanTacticalSlide && _moveCtrl.IsSprinting && TargetDistance > 7f)
        {
            Send(GJInputAction.Slide, PlayerInputPhase.Started);
            return;
        }
        if (_profile.CanCombatJump)
        {
            Send(GJInputAction.JumpVault, PlayerInputPhase.Started);
        }
    }

    private void RefreshSemiAutomaticFire()
    {
        if (!_isFiring || _combatCtrl.FireMode != WeaponFireMode.SemiAutomatic ||
            Time.time < _nextSemiShotTime)
        {
            return;
        }

        WeaponDefinitionSO weapon = _weaponActionCtrl.CurrentWeaponData;
        float roundsPerMinute = weapon ? weapon.Fire.SemiAutomaticRpm : 600f;
        _nextSemiShotTime = Time.time + 60f / Mathf.Max(1f, roundsPerMinute);
        Send(GJInputAction.Fire, PlayerInputPhase.Canceled);
        Send(GJInputAction.Fire, PlayerInputPhase.Started);
    }

    private void SetWorldMove(Vector3 worldDirection, bool sprint)
    {
        if (worldDirection.sqrMagnitude < 0.001f)
        {
            StopMoving();
            return;
        }

        Vector3 forward = _cameraCtrl.IsThirdPerson
            ? _cameraCtrl.GetPlanarLookDirection()
            : transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Vector2 input = new Vector2(
            Vector3.Dot(worldDirection, right),
            Vector3.Dot(worldDirection, forward));
        Send(GJInputAction.Move, PlayerInputPhase.Performed, Vector2.ClampMagnitude(input, 1f));
        SetSprinting(sprint && !_isAiming && !_isFiring);
    }

    private void SetSprinting(bool active)
    {
        if (_isSprintInputHeld == active)
        {
            return;
        }

        _isSprintInputHeld = active;
        Send(GJInputAction.Sprint, active ? PlayerInputPhase.Started : PlayerInputPhase.Canceled);
    }

    private void StopAllCommands()
    {
        SetFiring(false);
        SetAiming(false);
        StopMoving();
    }

    private void Send(
        GJInputAction action,
        PlayerInputPhase phase,
        Vector2 value = default)
    {
        _actionCtrl.Receive(new PlayerInputCommand(action, phase, value));
    }
}
