using System;
using UnityEngine;

public enum BombInteractionType
{
    None = 0,
    Plant = 1,
    Defuse = 2
}

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInfo))]
[RequireComponent(typeof(PlayerActionCtrl))]
public sealed class BombInteractCtrl : MonoBehaviour
{
    [Header("爆破交互")]
    [SerializeField, InspectorName("爆破规则")] private BombRules _rules;

    [Header("第一人称安放表现")]
    [SerializeField, InspectorName("C4 胸前位置")]
    private Vector3 _firstPersonBombPosition = new Vector3(0f, -0.3f, 0.62f);
    [SerializeField, InspectorName("C4 仪表盘朝向")]
    private Vector3 _firstPersonBombEuler = new Vector3(90f, 0f, 180f);
    [SerializeField, InspectorName("C4 入场偏移")]
    private Vector3 _firstPersonBombEnterOffset = new Vector3(0f, -0.22f, -0.08f);
    [SerializeField, Min(0.01f), InspectorName("C4 入场速度")]
    private float _firstPersonBombLerpSpeed = 8f;
    [SerializeField, Range(0f, 85f), InspectorName("安放低头角度")]
    private float _plantLookDownAngle = 52f;
    [SerializeField, Min(0.01f), InspectorName("安装低头速度")]
    private float _plantCameraLerpSpeed = 12f;

    private PlayerInfo _player;
    private PlayerActionCtrl _actionCtrl;
    private PlayerMoveCtrl _moveCtrl;
    private PlayerWeaponViewCtrl _weaponViewCtrl;
    private PlayerInputCtrl _inputCtrl;
    private FirstPersonCameraCtrl _cameraCtrl;
    private CharacterHealth _health;
    private BombEntity _presentedBomb;
    private BombInteractionType _interaction;
    private BombSite _plantSite;
    private float _progress;
    private bool _lockApplied;
    private bool _restoreThirdPersonView;

    public bool IsInteracting { get { return _interaction != BombInteractionType.None; } }
    public BombInteractionType Interaction { get { return _interaction; } }
    public float Progress { get { return _progress; } }

    public event Action<BombInteractionType> InteractionStarted;
    public event Action<BombInteractionType, float> ProgressChanged;
    public event Action<BombInteractionType> InteractionCompleted;
    public event Action<BombInteractionType> InteractionCanceled;
    public event Action<bool> InteractionLockChanged;

    private void Awake()
    {
        TryGetComponent(out _player);
        TryGetComponent(out _actionCtrl);
        TryGetComponent(out _moveCtrl);
        TryGetComponent(out _weaponViewCtrl);
        TryGetComponent(out _inputCtrl);
        _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);
        TryGetComponent(out _health);
        if (!_player || !_actionCtrl || !_moveCtrl || !_health)
        {
            Debug.LogError($"{name} 的炸弹交互依赖未配置完整。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_actionCtrl)
        {
            _actionCtrl.CommandReceived += HandleCommand;
        }
        if (_health)
        {
            _health.Died += HandleDied;
        }
    }

    private void Update()
    {
        if (!IsInteracting)
        {
            return;
        }

        if (!IsStillValid())
        {
            CancelInteraction();
            return;
        }

        float duration = _interaction == BombInteractionType.Plant
            ? _rules.PlantDuration
            : _rules.DefuseDuration;
        _progress = Mathf.Clamp01(_progress + Time.deltaTime / Mathf.Max(0.1f, duration));
        ProgressChanged?.Invoke(_interaction, _progress);
        if (_progress >= 1f)
        {
            CompleteInteraction();
        }
    }

    private void OnDisable()
    {
        if (_actionCtrl)
        {
            _actionCtrl.CommandReceived -= HandleCommand;
        }
        if (_health)
        {
            _health.Died -= HandleDied;
        }
        CancelInteraction();
        ApplyInteractionLock(false);
    }

    public void Configure(BombRules rules)
    {
        if (IsInteracting)
        {
            CancelInteraction();
        }
        _rules = rules;
    }

    public void SetRules(BombRules rules)
    {
        Configure(rules);
    }

    public bool TryBeginInteraction()
    {
        if (IsInteracting || !_rules || !_rules.IsRoundActive || _health.IsDead)
        {
            return false;
        }

        if (_rules.CanPlant(_player, out BombSite site))
        {
            BeginInteraction(BombInteractionType.Plant, site);
            return true;
        }
        if (_rules.CanDefuse(_player))
        {
            BeginInteraction(BombInteractionType.Defuse, null);
            return true;
        }

        return false;
    }

    public void CancelInteraction()
    {
        if (!IsInteracting)
        {
            return;
        }

        BombInteractionType canceled = _interaction;
        EndPlantPresentation(false);
        _interaction = BombInteractionType.None;
        _plantSite = null;
        _progress = 0f;
        ApplyInteractionLock(false);
        InteractionLockChanged?.Invoke(false);
        InteractionCanceled?.Invoke(canceled);
    }

    private void BeginInteraction(BombInteractionType interaction, BombSite site)
    {
        _interaction = interaction;
        _plantSite = site;
        _progress = 0f;
        ApplyInteractionLock(true);
        if (interaction == BombInteractionType.Plant)
        {
            BeginPlantPresentation();
        }
        InteractionLockChanged?.Invoke(true);
        InteractionStarted?.Invoke(interaction);
        ProgressChanged?.Invoke(interaction, 0f);
    }

    private void CompleteInteraction()
    {
        BombInteractionType completed = _interaction;
        bool succeeded = completed == BombInteractionType.Plant
            ? _rules.TryPlant(_player, _plantSite)
            : _rules.TryDefuse(_player);
        if (!succeeded)
        {
            CancelInteraction();
            return;
        }

        EndPlantPresentation(completed == BombInteractionType.Plant);
        _interaction = BombInteractionType.None;
        _plantSite = null;
        _progress = 1f;
        ApplyInteractionLock(false);
        InteractionLockChanged?.Invoke(false);
        InteractionCompleted?.Invoke(completed);
    }

    private bool IsStillValid()
    {
        if (!_rules || !_rules.IsRoundActive || _health.IsDead)
        {
            return false;
        }
        if (_interaction == BombInteractionType.Plant)
        {
            return _rules.CanPlant(_player, out BombSite site) && site == _plantSite;
        }
        return _interaction == BombInteractionType.Defuse && _rules.CanDefuse(_player);
    }

    private void HandleCommand(PlayerInputCommand command)
    {
        if (command.Action != GJInputAction.Interact)
        {
            return;
        }

        if (command.Phase == PlayerInputPhase.Started)
        {
            TryBeginInteraction();
        }
        else if (command.Phase == PlayerInputPhase.Canceled)
        {
            CancelInteraction();
        }
    }

    private void HandleDied()
    {
        CancelInteraction();
    }

    private void BeginPlantPresentation()
    {
        if (!_inputCtrl || !_inputCtrl.enabled || !_cameraCtrl || !_rules)
        {
            return;
        }

        _restoreThirdPersonView = _cameraCtrl.IsThirdPerson;
        if (_restoreThirdPersonView)
        {
            _cameraCtrl.SetThirdPerson(false);
        }

        _cameraCtrl.BeginForcedPitch(_plantLookDownAngle, _plantCameraLerpSpeed);
        _presentedBomb = _rules.Bomb;
        if (_presentedBomb)
        {
            _presentedBomb.BeginFirstPersonPlantPresentation(
                _cameraCtrl.ViewCamera ? _cameraCtrl.ViewCamera.transform : _cameraCtrl.transform,
                _firstPersonBombPosition,
                _firstPersonBombEuler,
                _firstPersonBombEnterOffset,
                _firstPersonBombLerpSpeed);
        }
    }

    private void EndPlantPresentation(bool planted)
    {
        if (_cameraCtrl)
        {
            _cameraCtrl.EndForcedPitchImmediate();
            if (_restoreThirdPersonView)
            {
                _cameraCtrl.SetThirdPerson(true);
            }
        }

        if (!planted && _presentedBomb)
        {
            _presentedBomb.CancelFirstPersonPlantPresentation();
        }

        _presentedBomb = null;
        _restoreThirdPersonView = false;
    }

    private void ApplyInteractionLock(bool locked)
    {
        if (_lockApplied == locked)
        {
            return;
        }

        if (locked && _actionCtrl)
        {
            // 取消已经开始的持续动作，再锁住后续输入，避免长按开火残留到交互期间。
            _actionCtrl.Receive(new PlayerInputCommand(
                GJInputAction.Fire,
                PlayerInputPhase.Canceled));
            _actionCtrl.Receive(new PlayerInputCommand(
                GJInputAction.Aim,
                PlayerInputPhase.Canceled));
            _actionCtrl.Receive(new PlayerInputCommand(
                GJInputAction.Sprint,
                PlayerInputPhase.Canceled));
        }

        if (_moveCtrl)
        {
            _moveCtrl.SetExternalLock(locked);
        }

        // AI 本来就不显示第一人称层，不能在解锁时误把它重新显示出来。
        if (_weaponViewCtrl && _inputCtrl && _inputCtrl.enabled)
        {
            _weaponViewCtrl.SetFirstPersonArmsVisible(!locked);
        }

        _lockApplied = locked;
    }
}
