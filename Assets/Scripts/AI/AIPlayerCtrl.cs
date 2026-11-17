using System;
using UnityEngine;
using UnityEngine.AI;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class AIPlayerCtrl : MonoBehaviour, IAIDifficultyControl
{
    private const float ModeInteractionRadius = 3.5f;
    private const float ModeInteractionRetryInterval = 0.25f;
    private const float WorldInteractionRetryInterval = 0.35f;
    private static readonly System.Collections.Generic.List<AIPlayerCtrl> ActiveAI =
        new System.Collections.Generic.List<AIPlayerCtrl>();
    [Header("AI 身份")]
    [SerializeField, InspectorName("初始难度")] private AIDifficulty _difficulty = AIDifficulty.Normal;

    [Header("导航与感知")]
    [SerializeField, Min(3f), InspectorName("巡逻半径")] private float _patrolRadius = 18f;
    [SerializeField, Min(3f), InspectorName("掩体搜索半径")] private float _coverSearchRadius = 14f;
    [SerializeField, InspectorName("掩体障碍层")] private LayerMask _coverMask = ~0;
    [SerializeField, InspectorName("视线遮挡层")] private LayerMask _sightMask = ~0;

    private readonly AIDecisionCore _decision = new AIDecisionCore();
    private readonly AIBehaviors _behaviors = new AIBehaviors();
    private AICoverEvaluator _coverEvaluator;
    private NavMeshPath _patrolPath;
    private PlayerActionCtrl _actionCtrl;
    private PlayerMoveCtrl _moveCtrl;
    private PlayerAimCtrl _aimCtrl;
    private WeaponCombatCtrl _combatCtrl;
    private WeaponActionCtrl _weaponActionCtrl;
    private PlayerWeaponInventoryCtrl _inventory;
    private FirstPersonCameraCtrl _cameraCtrl;
    private CharacterHealth _health;
    private BombInteractCtrl _bombInteractCtrl;
    private PlayerInteractionCtrl _interactionCtrl;
    private AIWorldModel _world;
    private AIAimActuator _aimActuator;
    private AIFireActuator _fireActuator;
    private AIMotionActuator _motionActuator;
    private AIDifficultyProfile _profile;
    private AIIntent _intent;
    private Vector3 _patrolPoint;
    private Vector3 _coverPoint;
    private Vector3 _coverPeekPoint;
    private CharacterHealth _coverThreat;
    private float _nextCoverEvaluation;
    private float _lastObservedDamageTime = float.NegativeInfinity;
    private float _lastCombatMovementTime = float.NegativeInfinity;
    private float _nextModeInteractionAttempt;
    private float _modeInteractionRecoveryUntil;
    private float _nextWorldInteractionScan;
    private float _nextWorldInteractionAttempt;
    private bool _hasCover;
    private bool _isHoldingInteract;
    private int _modeInteractionFailures;
    private Vector3 _modeInteractionRecoveryPoint;
    private WorldInteractable _worldInteractionTarget;
    private IAIModePatchSession _modePatchSession;

    public AIDifficulty Difficulty { get { return _difficulty; } }
    public AIDifficultyProfile Profile { get { return _profile; } }
    public PlayerInfo Info { get { return _health ? _health.PlayerInfo : null; } }
    public AIBehaviorId CurrentBehavior { get { return _decision.Current; } }
    public AITrackedTarget Target { get { return _world != null ? _world.CurrentTarget : null; } }
    public float RecentDamagePressure
    {
        get { return _world != null ? _world.RecentDamagePressure : 0f; }
    }
    public float TimeSinceDamaged
    {
        get { return _world != null ? _world.TimeSinceDamaged : float.PositiveInfinity; }
    }
    public AIIntent CurrentIntent { get { return _intent; } }
    public string FirePhase { get { return _fireActuator != null ? _fireActuator.PhaseName : "None"; } }
    public int StuckRecoveryCount
    {
        get { return _motionActuator != null ? _motionActuator.StuckRecoveryCount : 0; }
    }
    public int PathFailureCount
    {
        get { return _motionActuator != null ? _motionActuator.PathFailureCount : 0; }
    }

    public event Action<AIDifficulty> DifficultyChanged;
    public event Action<AIBehaviorId> BehaviorChanged;

    public static System.Collections.Generic.IReadOnlyList<AIPlayerCtrl> All
    {
        get { return ActiveAI; }
    }

    private void Awake()
    {
        // AICoverEvaluator 会创建 NavMeshPath；Unity 禁止在 MonoBehaviour 字段初始化期调用。
        _coverEvaluator = new AICoverEvaluator();
        _patrolPath = new NavMeshPath();
        TryGetComponent(out _actionCtrl);
        TryGetComponent(out _moveCtrl);
        TryGetComponent(out _aimCtrl);
        TryGetComponent(out _combatCtrl);
        TryGetComponent(out _weaponActionCtrl);
        TryGetComponent(out _inventory);
        TryGetComponent(out _health);
        if (!TryGetComponent(out _interactionCtrl))
        {
            _interactionCtrl = gameObject.AddComponent<PlayerInteractionCtrl>();
        }
        _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);
        if (!_actionCtrl || !_moveCtrl || !_aimCtrl || !_combatCtrl ||
            !_weaponActionCtrl || !_inventory || !_cameraCtrl || !_health)
        {
            Debug.LogError($"{name} 的 AI 操控脚本缺少玩家共用功能组件。", this);
            enabled = false;
            return;
        }
        SetDifficulty(_difficulty);
        _world = new AIWorldModel(_health, transform, _sightMask, _profile);
        _aimActuator = new AIAimActuator(_cameraCtrl, transform, _profile);
        _fireActuator = new AIFireActuator(
            Send, _combatCtrl, _weaponActionCtrl, _inventory, _moveCtrl, _profile);
        _motionActuator = new AIMotionActuator(Send, transform, _cameraCtrl, _moveCtrl);
        ChoosePatrolPoint();
        if (!TryGetComponent<AIDebugHud>(out _))
        {
            gameObject.AddComponent<AIDebugHud>();
        }
    }

    private void OnEnable()
    {
        if (!ActiveAI.Contains(this)) ActiveAI.Add(this);
        _world?.Enable();
        _fireActuator?.Enable();
    }

    private void Start()
    {
        _cameraCtrl.SetThirdPerson(true);
    }

    private void Update()
    {
        if (_health.IsDead)
        {
            StopAllCommands();
            return;
        }
        _world.Sense();
        _modePatchSession?.Tick();
        RefreshCover();
        bool needsReload = NeedsReload();
        AIBehaviorId previous = _decision.Current;
        _decision.TrySelect(
            _world, _health, _profile, _hasCover, needsReload,
            _modePatchSession != null
                ? _modePatchSession.GetUtilityBias()
                : default,
            out AIBehaviorId selected);
        if (selected != previous)
        {
            BehaviorChanged?.Invoke(selected);
        }
        if (_decision.Current == AIBehaviorId.Patrol &&
            Vector3.Distance(transform.position, _patrolPoint) < 1.2f)
        {
            ChoosePatrolPoint();
        }
        int targetSlot = _world.CurrentTarget != null && _world.CurrentTarget.Actor
            ? GetTargetSlot(_world.CurrentTarget.Actor)
            : 0;
        _intent = _behaviors.Build(
            _decision.Current, _world, transform, _profile,
            _patrolPoint, _coverPoint, _coverPeekPoint, _hasCover, targetSlot);
        ApplyModeObjective(ref _intent);
        _modePatchSession?.ModifyIntent(ref _intent);
        EnrichCombatIntent(ref _intent);
        ApplyModeInteraction(ref _intent);
        StabilizeCombatIntent(ref _intent);
        _aimActuator.Tick(_intent, _weaponActionCtrl.CurrentWeaponData);
        _fireActuator.Tick(
            _intent,
            _aimActuator.AngularError,
            _intent.ProtectedByCover);
        _motionActuator.Tick(_intent, _profile);
    }

    private void OnDisable()
    {
        ActiveAI.Remove(this);
        _world?.Disable();
        _fireActuator?.Disable();
        StopAllCommands();
    }

    public void SetDifficulty(AIDifficulty difficulty)
    {
        _difficulty = difficulty;
        _profile = AIDifficultyProfile.Create(difficulty);
        _world?.SetProfile(_profile);
        _aimActuator?.SetProfile(_profile);
        _fireActuator?.SetProfile(_profile);
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

    public void InstallModePatch(IAIModePatch patch, AIModeContext context)
    {
        UninstallModePatch();
        if (patch == null)
        {
            return;
        }
        _modePatchSession = patch.CreateSession(this, context);
        _world?.SetTargetFilter(context != null ? context.TargetFilter : null);
        _modePatchSession?.OnMatchStart();
    }

    public void UninstallModePatch()
    {
        CancelModeInteraction();
        _modePatchSession = null;
        _world?.SetTargetFilter(null);
    }

    public void NotifyRoundStart(int roundIndex)
    {
        CancelModeInteraction();
        _modeInteractionFailures = 0;
        _modeInteractionRecoveryUntil = 0f;
        TryGetComponent(out _bombInteractCtrl);
        _modePatchSession?.OnRoundStart(roundIndex);
    }

    public void NotifyRoundEnd(bool won)
    {
        CancelModeInteraction();
        _modePatchSession?.OnRoundEnd(won);
    }

    public void NotifyTargetKilled(PlayerInfo victim)
    {
        _modePatchSession?.OnTargetKilled(victim);
    }

    private void ApplyModeObjective(ref AIIntent intent)
    {
        if (_modePatchSession == null ||
            !_modePatchSession.TryGetObjective(out AIObjective objective))
        {
            return;
        }

        bool hasVisibleTarget = intent.Target != null && intent.Target.Visible;
        if (!objective.ForceCommit &&
            (intent.Behavior == AIBehaviorId.Retreat || hasVisibleTarget))
        {
            return;
        }

        if (objective.Kind == AIObjectiveKind.Regroup &&
            intent.Behavior != AIBehaviorId.Patrol)
        {
            return;
        }

        Vector3 objectivePosition = objective.Position;
        if (objective.AllowInteraction && Time.time < _modeInteractionRecoveryUntil)
        {
            objectivePosition = _modeInteractionRecoveryPoint;
        }

        Vector3 offset = objectivePosition - transform.position;
        offset.y = 0f;
        if (offset.sqrMagnitude <= objective.ArrivalRadius * objective.ArrivalRadius)
        {
            // 模式目标到达后应就地执行职责，不能继续消费基础巡逻目的地而离岗。
            intent.HasMoveDestination = false;
            intent.Sprint = false;
            intent.Mobility = AIMobilityIntent.None;
            intent.Aim = false;
            intent.Fire = false;
            return;
        }

        intent.HasMoveDestination = true;
        intent.MoveDestination = objectivePosition;
        intent.Sprint = objective.Sprint &&
            offset.magnitude > objective.ArrivalRadius + 2f;
        intent.Aim = false;
        intent.Fire = false;
    }

    public void ReceiveSharedTarget(AITrackedTarget target, float confidence)
    {
        _world?.ReceiveSharedTarget(target, confidence);
    }

    public bool PrioritizeModeTarget(CharacterHealth target, float awarenessRange)
    {
        return _world != null && _world.PrioritizeModeTarget(target, awarenessRange);
    }

    private void ApplyModeInteraction(ref AIIntent intent)
    {
        if (_isHoldingInteract && (!_bombInteractCtrl || !_bombInteractCtrl.IsInteracting))
        {
            ReleaseModeInteraction();
        }

        bool hasVisibleTarget = intent.Target != null && intent.Target.Visible;
        if (_modePatchSession != null &&
            _modePatchSession.TryGetObjective(out AIObjective objective) &&
            (!hasVisibleTarget || objective.ForceCommit) &&
            CanInteractWithObjective(objective))
        {
            TryBeginModeInteraction(ref intent, objective);
            return;
        }

        CancelModeInteraction();
        TryApplyWorldInteraction(ref intent);
    }

    private void TryBeginModeInteraction(ref AIIntent intent, AIObjective objective)
    {
        if (_bombInteractCtrl && _bombInteractCtrl.IsInteracting)
        {
            SuppressIntentForInteraction(ref intent);
            return;
        }

        if (Time.time < _nextModeInteractionAttempt)
        {
            return;
        }

        _nextModeInteractionAttempt = Time.time + ModeInteractionRetryInterval;
        if (!_bombInteractCtrl && !TryGetComponent(out _bombInteractCtrl))
        {
            return;
        }

        _interactionCtrl?.ClearCommandTarget();
        Send(GJInputAction.Interact, PlayerInputPhase.Started, Vector2.zero);
        if (!_bombInteractCtrl.IsInteracting)
        {
            _modeInteractionFailures++;
            if (_modeInteractionFailures >= 3)
            {
                BeginModeInteractionRecovery(ref intent, objective);
            }
            return;
        }

        _modeInteractionFailures = 0;
        _modeInteractionRecoveryUntil = 0f;
        _isHoldingInteract = true;
        SuppressIntentForInteraction(ref intent);
    }

    private void BeginModeInteractionRecovery(
        ref AIIntent intent,
        AIObjective objective)
    {
        _modeInteractionFailures = 0;
        int step = Mathf.Abs(GetInstanceID() + Mathf.FloorToInt(Time.time * 3f));
        float angle = step % 8 * 45f;
        Vector3 raw = objective.Position +
            Quaternion.Euler(0f, angle, 0f) * Vector3.forward * 1.1f;
        _modeInteractionRecoveryPoint = NavMesh.SamplePosition(
            raw,
            out NavMeshHit hit,
            1.5f,
            NavMesh.AllAreas)
            ? hit.position
            : objective.Position;
        _modeInteractionRecoveryUntil = Time.time + 1.2f;
        intent.HasMoveDestination = true;
        intent.MoveDestination = _modeInteractionRecoveryPoint;
        intent.Sprint = false;
        intent.Aim = false;
        intent.Fire = false;
    }

    private void TryApplyWorldInteraction(ref AIIntent intent)
    {
        if (!_interactionCtrl || intent.Target != null && intent.Target.Visible ||
            intent.Behavior == AIBehaviorId.Retreat)
        {
            ClearWorldInteractionTarget();
            return;
        }

        if (!IsWorldInteractionTargetUseful(_worldInteractionTarget, intent))
        {
            ClearWorldInteractionTarget();
        }

        if (!_worldInteractionTarget && Time.time >= _nextWorldInteractionScan)
        {
            _nextWorldInteractionScan = Time.time + GetWorldInteractionScanInterval();
            _worldInteractionTarget = FindWorldInteractionTarget(intent);
        }

        if (!_worldInteractionTarget)
        {
            return;
        }

        float distance = _worldInteractionTarget.GetDistance(transform.position);
        if (distance > _worldInteractionTarget.InteractionRadius)
        {
            intent.HasMoveDestination = true;
            intent.MoveDestination = _worldInteractionTarget.GetPromptPosition(transform.position);
            intent.Sprint = distance > 5f && _profile.Difficulty != AIDifficulty.Easy;
            intent.Aim = false;
            intent.Fire = false;
            return;
        }

        SuppressIntentForInteraction(ref intent);
        if (Time.time < _nextWorldInteractionAttempt)
        {
            return;
        }

        WorldInteractable target = _worldInteractionTarget;
        _nextWorldInteractionAttempt = Time.time + WorldInteractionRetryInterval;
        _interactionCtrl.SetCommandTarget(target);
        Send(GJInputAction.Interact, PlayerInputPhase.Started, Vector2.zero);
        Send(GJInputAction.Interact, PlayerInputPhase.Canceled, Vector2.zero);
        _interactionCtrl.ClearCommandTarget(target);
        ClearWorldInteractionTarget();
    }

    private WorldInteractable FindWorldInteractionTarget(AIIntent intent)
    {
        System.Collections.Generic.IReadOnlyList<WorldInteractable> items =
            WorldInteractable.Items;
        WorldInteractable bestLoot = null;
        WorldInteractable bestDoor = null;
        float bestLootDistance = float.PositiveInfinity;
        float bestDoorDistance = float.PositiveInfinity;
        for (int i = 0; i < items.Count; i++)
        {
            WorldInteractable item = items[i];
            if (!item || !item.isActiveAndEnabled ||
                item.InputAction != GJInputAction.Interact ||
                !item.CanInteract(gameObject))
            {
                continue;
            }

            float distance = item.GetDistance(transform.position);
            if (item.TryGetInteraction(out DroppedWeaponEntity droppedWeapon) &&
                distance <= GetLootSearchRadius() &&
                ShouldLoot(droppedWeapon) &&
                !HasCloserAiClaim(item, droppedWeapon) &&
                distance < bestLootDistance)
            {
                bestLoot = item;
                bestLootDistance = distance;
                continue;
            }

            if (item.TryGetInteraction(out DoorRotationInteraction door) &&
                ShouldOpenDoor(item, door, intent, distance) &&
                distance < bestDoorDistance)
            {
                bestDoor = item;
                bestDoorDistance = distance;
            }
        }

        if (bestDoor && (bestDoorDistance <= 3.2f || !bestLoot))
        {
            return bestDoor;
        }

        return bestLoot;
    }

    private bool IsWorldInteractionTargetUseful(
        WorldInteractable target,
        AIIntent intent)
    {
        if (!target || !target.isActiveAndEnabled ||
            target.InputAction != GJInputAction.Interact ||
            !target.CanInteract(gameObject))
        {
            return false;
        }

        if (target.TryGetInteraction(out DroppedWeaponEntity droppedWeapon))
        {
            return target.GetDistance(transform.position) <= GetLootSearchRadius() &&
                ShouldLoot(droppedWeapon) &&
                !HasCloserAiClaim(target, droppedWeapon);
        }

        if (target.TryGetInteraction(out DoorRotationInteraction door))
        {
            return ShouldOpenDoor(
                target,
                door,
                intent,
                target.GetDistance(transform.position));
        }

        return false;
    }

    private bool ShouldLoot(DroppedWeaponEntity droppedWeapon)
    {
        if (!droppedWeapon || string.IsNullOrWhiteSpace(droppedWeapon.WeaponResourceId) ||
            _inventory.IsSwitching)
        {
            return false;
        }

        int droppedAmmo = droppedWeapon.MagazineAmmo + droppedWeapon.ReserveAmmo;
        int currentAmmo = _combatCtrl.MagazineAmmo + _combatCtrl.ReserveAmmo;
        if (string.IsNullOrWhiteSpace(_inventory.PrimaryWeaponResourceId))
        {
            return true;
        }

        if (string.Equals(
            droppedWeapon.WeaponResourceId,
            _inventory.CurrentWeaponResourceId,
            StringComparison.Ordinal))
        {
            return droppedAmmo > currentAmmo + 8;
        }

        return currentAmmo <= 0;
    }

    private bool HasCloserAiClaim(
        WorldInteractable item,
        DroppedWeaponEntity droppedWeapon)
    {
        float ownDistance = Vector3.SqrMagnitude(transform.position - item.transform.position);
        for (int i = 0; i < ActiveAI.Count; i++)
        {
            AIPlayerCtrl other = ActiveAI[i];
            if (!other || other == this || !other.isActiveAndEnabled ||
                !other._health || other._health.IsDead ||
                !other.ShouldLoot(droppedWeapon) ||
                !item.CanInteract(other.gameObject))
            {
                continue;
            }

            float otherDistance = Vector3.SqrMagnitude(
                other.transform.position - item.transform.position);
            if (otherDistance + 0.01f < ownDistance)
            {
                return true;
            }
        }

        return false;
    }

    private bool ShouldOpenDoor(
        WorldInteractable item,
        DoorRotationInteraction door,
        AIIntent intent,
        float distance)
    {
        if (!door || door.IsOpen || door.IsRotating ||
            !intent.HasMoveDestination || distance > 4.5f)
        {
            return false;
        }

        Vector3 toDestination = intent.MoveDestination - transform.position;
        Vector3 toDoor = item.GetPromptPosition(transform.position) - transform.position;
        toDestination.y = 0f;
        toDoor.y = 0f;
        if (toDestination.sqrMagnitude < 0.25f || toDoor.sqrMagnitude < 0.01f ||
            toDoor.sqrMagnitude > toDestination.sqrMagnitude + 1f)
        {
            return false;
        }

        return Vector3.Dot(toDestination.normalized, toDoor.normalized) >= 0.58f;
    }

    private float GetLootSearchRadius()
    {
        if (_profile.Difficulty == AIDifficulty.Hard)
        {
            return 12f;
        }

        return _profile.Difficulty == AIDifficulty.Normal ? 8f : 4f;
    }

    private float GetWorldInteractionScanInterval()
    {
        if (_profile.Difficulty == AIDifficulty.Hard)
        {
            return 0.18f;
        }

        return _profile.Difficulty == AIDifficulty.Normal ? 0.35f : 0.65f;
    }

    private void ClearWorldInteractionTarget()
    {
        if (_interactionCtrl)
        {
            _interactionCtrl.ClearCommandTarget(_worldInteractionTarget);
        }

        _worldInteractionTarget = null;
    }

    private bool CanInteractWithObjective(AIObjective objective)
    {
        if (!objective.AllowInteraction ||
            objective.Kind != AIObjectiveKind.AttackSite &&
            objective.Kind != AIObjectiveKind.Bomb)
        {
            return false;
        }

        Vector3 offset = objective.Position - transform.position;
        offset.y = 0f;
        return offset.sqrMagnitude <= ModeInteractionRadius * ModeInteractionRadius;
    }

    private void SuppressIntentForInteraction(ref AIIntent intent)
    {
        intent.HasMoveDestination = false;
        intent.Sprint = false;
        intent.Mobility = AIMobilityIntent.None;
        intent.Aim = false;
        intent.Fire = false;
        intent.Reload = false;
        intent.Melee = false;
        intent.SwitchWeapon = false;
    }

    private void EnrichCombatIntent(ref AIIntent intent)
    {
        if (intent.Target == null)
        {
            return;
        }
        float distance = intent.Target.Distance;
        WeaponDefinitionSO weapon = _weaponActionCtrl.CurrentWeaponData;
        float meleeRange = weapon && weapon.Melee.Enabled ? weapon.Melee.Range + 0.4f : 2.05f;
        intent.Melee = _profile.CanMelee && intent.Target.Visible &&
            distance <= meleeRange &&
            (_combatCtrl.MagazineAmmo <= 0 || _weaponActionCtrl.IsBusy ||
             intent.Target.Actor.CurrentHealth <= 25f);
        intent.Reload = NeedsReload();
        intent.SwitchWeapon = _profile.CanSwitchWeapon &&
            _combatCtrl.MagazineAmmo <= 0 && _combatCtrl.ReserveAmmo <= 0;
    }

    private void StabilizeCombatIntent(ref AIIntent intent)
    {
        bool canSlideFire = _moveCtrl.IsSliding && intent.Target != null &&
            intent.Target.Visible && _profile.Difficulty != AIDifficulty.Easy &&
            Time.time - intent.Target.VisibleSince >= _profile.ReactionTime;
        if (canSlideFire)
        {
            intent.Aim = false;
            intent.Fire = true;
            intent.AllowMovingFire = true;
            return;
        }

        bool movingByPlan = intent.HasMoveDestination ||
            intent.Mobility != AIMobilityIntent.None;
        if ((movingByPlan || _moveCtrl.IsMoving) && !intent.AllowMovingFire)
        {
            _lastCombatMovementTime = Time.time;
            intent.Fire = false;
            return;
        }

        if (intent.AllowMovingFire)
        {
            _lastCombatMovementTime = Time.time;
            return;
        }

        bool firingPosture = intent.CombatTactic == AICombatTactic.HoldAngle ||
            intent.CombatTactic == AICombatTactic.CoverPeek ||
            intent.CombatTactic == AICombatTactic.Suppress;
        if (!firingPosture ||
            Time.time - _lastCombatMovementTime < _profile.CombatSettleTime)
        {
            intent.Fire = false;
        }
    }

    private bool NeedsReload()
    {
        WeaponDefinitionSO weapon = _weaponActionCtrl.CurrentWeaponData;
        if (!weapon || _combatCtrl.ReserveAmmo <= 0)
        {
            return false;
        }
        if (_combatCtrl.MagazineAmmo <= 0)
        {
            return true;
        }
        float ratio = _combatCtrl.MagazineAmmo /
            Mathf.Max(1f, weapon.Ammo.MagazineCapacity);
        return _profile.TacticalReloadRatio > 0f &&
            ratio < _profile.TacticalReloadRatio &&
            (_world.CurrentTarget == null || !_world.CurrentTarget.Visible || _hasCover);
    }

    private void RefreshCover()
    {
        AITrackedTarget target = _world.CurrentTarget;
        if (target == null)
        {
            _hasCover = false;
            _coverThreat = null;
            return;
        }

        float damageTime = Time.time - _world.TimeSinceDamaged;
        bool wasJustDamaged = !float.IsInfinity(damageTime) &&
            damageTime > _lastObservedDamageTime + 0.001f;
        bool targetChanged = _coverThreat != target.Actor;
        if (!wasJustDamaged && !targetChanged && Time.time < _nextCoverEvaluation)
        {
            return;
        }

        _lastObservedDamageTime = Mathf.Max(_lastObservedDamageTime, damageTime);
        _coverThreat = target.Actor;
        _nextCoverEvaluation = Time.time + GetCoverEvaluationInterval();
        _hasCover = _coverEvaluator.TryFind(
            transform.position,
            target.LastKnownPosition,
            _coverSearchRadius,
            _coverMask,
            _profile.PreferredRange,
            target.Actor,
            out _coverPoint,
            out _coverPeekPoint);
    }

    private float GetCoverEvaluationInterval()
    {
        if (_profile.Difficulty == AIDifficulty.Hard)
        {
            return 0.3f;
        }
        return _profile.Difficulty == AIDifficulty.Normal ? 0.55f : 0.9f;
    }

    private void ChoosePatrolPoint()
    {
        if (!NavMesh.SamplePosition(
            transform.position,
            out NavMeshHit origin,
            4f,
            NavMesh.AllAreas))
        {
            _patrolPoint = transform.position;
            return;
        }

        for (int i = 0; i < 8; i++)
        {
            Vector2 circle = UnityEngine.Random.insideUnitCircle * _patrolRadius;
            Vector3 candidate = transform.position +
                new Vector3(circle.x, 0f, circle.y);
            if (!NavMesh.SamplePosition(
                candidate,
                out NavMeshHit destination,
                4f,
                NavMesh.AllAreas))
            {
                continue;
            }
            if (!NavMesh.CalculatePath(
                origin.position,
                destination.position,
                NavMesh.AllAreas,
                _patrolPath) ||
                _patrolPath.status != NavMeshPathStatus.PathComplete ||
                _patrolPath.corners.Length <= 1)
            {
                continue;
            }

            _patrolPoint = destination.position;
            return;
        }

        _patrolPoint = origin.position;
    }

    public int GetTargetSlot(CharacterHealth target)
    {
        int slot = 0;
        for (int i = 0; i < ActiveAI.Count; i++)
        {
            AIPlayerCtrl other = ActiveAI[i];
            if (!other || other == this || other.Target == null ||
                other.Target.Actor != target)
            {
                continue;
            }
            if (other.GetInstanceID() < GetInstanceID()) slot++;
        }
        return slot;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveAI.Clear();
    }

    private void StopAllCommands()
    {
        CancelModeInteraction();
        ClearWorldInteractionTarget();
        _fireActuator?.Stop();
        _motionActuator?.Stop();
    }

    private void CancelModeInteraction()
    {
        if (_isHoldingInteract || _bombInteractCtrl && _bombInteractCtrl.IsInteracting)
        {
            ReleaseModeInteraction();
        }
    }

    private void ReleaseModeInteraction()
    {
        Send(GJInputAction.Interact, PlayerInputPhase.Canceled, Vector2.zero);
        _isHoldingInteract = false;
    }

    private void Send(
        GJInputAction action,
        PlayerInputPhase phase,
        Vector2 value)
    {
        if (_actionCtrl)
        {
            _actionCtrl.Receive(new PlayerInputCommand(action, phase, value));
        }
    }
}
