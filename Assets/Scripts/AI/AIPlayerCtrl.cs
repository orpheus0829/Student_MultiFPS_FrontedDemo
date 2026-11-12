using System;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class AIPlayerCtrl : MonoBehaviour, IAIDifficultyControl
{
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
    private readonly AICoverEvaluator _coverEvaluator = new AICoverEvaluator();
    private PlayerActionCtrl _actionCtrl;
    private PlayerMoveCtrl _moveCtrl;
    private PlayerAimCtrl _aimCtrl;
    private WeaponCombatCtrl _combatCtrl;
    private WeaponActionCtrl _weaponActionCtrl;
    private PlayerWeaponInventoryCtrl _inventory;
    private FirstPersonCameraCtrl _cameraCtrl;
    private CharacterHealth _health;
    private AIWorldModel _world;
    private AIAimActuator _aimActuator;
    private AIFireActuator _fireActuator;
    private AIMotionActuator _motionActuator;
    private AIDifficultyProfile _profile;
    private AIIntent _intent;
    private Vector3 _patrolPoint;
    private Vector3 _coverPoint;
    private float _nextCoverEvaluation;
    private bool _hasCover;

    public AIDifficulty Difficulty { get { return _difficulty; } }
    public AIDifficultyProfile Profile { get { return _profile; } }
    public AIBehaviorId CurrentBehavior { get { return _decision.Current; } }
    public AITrackedTarget Target { get { return _world != null ? _world.CurrentTarget : null; } }
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
        TryGetComponent(out _actionCtrl);
        TryGetComponent(out _moveCtrl);
        TryGetComponent(out _aimCtrl);
        TryGetComponent(out _combatCtrl);
        TryGetComponent(out _weaponActionCtrl);
        TryGetComponent(out _inventory);
        TryGetComponent(out _health);
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
            Send, _combatCtrl, _weaponActionCtrl, _inventory, _profile);
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
        RefreshCover();
        bool needsReload = NeedsReload();
        AIBehaviorId previous = _decision.Current;
        _decision.TrySelect(
            _world, _health, _profile, _hasCover, needsReload, out AIBehaviorId selected);
        if (selected != previous)
        {
            BehaviorChanged?.Invoke(selected);
        }
        if (_decision.Current == AIBehaviorId.Patrol &&
            Vector3.Distance(transform.position, _patrolPoint) < 1.2f)
        {
            ChoosePatrolPoint();
        }
        _intent = AIBehaviors.Build(
            _decision.Current, _world, transform, _profile,
            _patrolPoint, _coverPoint, _hasCover);
        EnrichCombatIntent(ref _intent);
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
        if (target == null || Time.time < _nextCoverEvaluation)
        {
            return;
        }
        _nextCoverEvaluation = Time.time + 1f;
        _hasCover = _coverEvaluator.TryFind(
            transform.position,
            target.LastKnownPosition,
            _coverSearchRadius,
            _coverMask,
            _profile.PreferredRange,
            target.Actor,
            out _coverPoint);
    }

    private void ChoosePatrolPoint()
    {
        Vector2 circle = UnityEngine.Random.insideUnitCircle * _patrolRadius;
        _patrolPoint = transform.position + new Vector3(circle.x, 0f, circle.y);
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
        _fireActuator?.Stop();
        _motionActuator?.Stop();
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
