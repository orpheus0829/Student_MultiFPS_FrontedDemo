using System;
using System.Collections.Generic;
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
    private static readonly List<BombInteractCtrl> ActiveControllers =
        new List<BombInteractCtrl>();

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
    [SerializeField, Min(0f), InspectorName("安装镜头下沉")]
    private float _plantCameraDrop = 0.34f;

    [Header("第三人称携带表现")]
    [SerializeField, InspectorName("双手中点偏移")]
    private Vector3 _thirdPersonBombPosition = new Vector3(0f, -0.04f, 0.02f);
    [SerializeField, InspectorName("C4 相对双手挂点位置")]
    private Vector3 _thirdPersonBombLocalPosition =
        new Vector3(0.046f, 0.025f, 0f);
    [SerializeField, InspectorName("双手间 C4 旋转")]
    private Vector3 _thirdPersonBombEuler =
        new Vector3(0.341683f, -118.0651f, 3.297782f);
    [SerializeField, Range(0.1f, 1f), InspectorName("第三人称 C4 尺寸")]
    private float _thirdPersonBombScale = 0.42f;

    private PlayerInfo _player;
    private PlayerActionCtrl _actionCtrl;
    private PlayerMoveCtrl _moveCtrl;
    private PlayerWeaponViewCtrl _weaponViewCtrl;
    private PlayerWeaponInventoryCtrl _weaponInventoryCtrl;
    private PlayerAnimationCtrl _animationCtrl;
    private PlayerInputCtrl _inputCtrl;
    private PlayerInteractionCtrl _worldInteractionCtrl;
    private FirstPersonCameraCtrl _cameraCtrl;
    private CharacterHealth _health;
    private BombEntity _presentedBomb;
    private GameObject _thirdPersonBombVisual;
    private Transform _thirdPersonCarryAnchor;
    private Animator _thirdPersonCarryAnimator;
    private Transform _thirdPersonLeftHand;
    private Transform _thirdPersonRightHand;
    private BombInteractionType _interaction;
    private BombSite _plantSite;
    private float _progress;
    private bool _lockApplied;
    private bool _plantInputHeld;
    private bool _pendingPlantAfterSelection;
    private bool _suppressFireCancel;

    public bool IsInteracting { get { return _interaction != BombInteractionType.None; } }
    public BombInteractionType Interaction { get { return _interaction; } }
    public float Progress { get { return _progress; } }
    public PlayerInfo Player { get { return _player; } }
    public static IReadOnlyList<BombInteractCtrl> Active
    {
        get { return ActiveControllers; }
    }

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
        TryGetComponent(out _weaponInventoryCtrl);
        TryGetComponent(out _animationCtrl);
        TryGetComponent(out _inputCtrl);
        TryGetComponent(out _worldInteractionCtrl);
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
        if (!ActiveControllers.Contains(this))
        {
            ActiveControllers.Add(this);
        }
        if (_actionCtrl)
        {
            _actionCtrl.CommandReceived += HandleCommand;
        }
        if (_weaponInventoryCtrl)
        {
            _weaponInventoryCtrl.BombSelectionChanged += HandleBombSelectionChanged;
        }
        if (_cameraCtrl)
        {
            _cameraCtrl.ViewChanged += HandleViewChanged;
        }
        if (_health)
        {
            _health.Died += HandleDied;
        }
        SubscribeRules();
        SyncBombOwnership();
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

    private void LateUpdate()
    {
        if (_weaponInventoryCtrl && _weaponInventoryCtrl.IsBombSelected &&
            _rules && _rules.BombCarrier == _player)
        {
            UpdateThirdPersonCarryAnchor();
        }
    }

    private void OnDisable()
    {
        ActiveControllers.Remove(this);
        if (_actionCtrl)
        {
            _actionCtrl.CommandReceived -= HandleCommand;
        }
        if (_weaponInventoryCtrl)
        {
            _weaponInventoryCtrl.BombSelectionChanged -= HandleBombSelectionChanged;
        }
        if (_cameraCtrl)
        {
            _cameraCtrl.ViewChanged -= HandleViewChanged;
        }
        if (_health)
        {
            _health.Died -= HandleDied;
        }
        CancelInteraction();
        ApplyInteractionLock(false);
        UnsubscribeRules();
        ClearCarryPresentation();
        DestroyThirdPersonCarryAnchor();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        ActiveControllers.Clear();
    }

    public void Configure(BombRules rules)
    {
        if (_rules == rules)
        {
            SyncBombOwnership();
            return;
        }

        if (IsInteracting)
        {
            CancelInteraction();
        }
        UnsubscribeRules();
        _rules = rules;
        SubscribeRules();
        SyncBombOwnership();
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
            if (!_weaponInventoryCtrl || !_weaponInventoryCtrl.IsBombSelected)
            {
                _pendingPlantAfterSelection = _plantInputHeld;
                return _weaponInventoryCtrl && _weaponInventoryCtrl.TrySelectBomb();
            }

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
        _interaction = BombInteractionType.None;
        _plantSite = null;
        _progress = 0f;
        _pendingPlantAfterSelection = false;
        // 先结束权威交互状态，再刷新携带表现。否则 RefreshCarryPresentation
        // 仍会把本次交互识别为 Plant，造成角色站起后下一帧再次蹲下。
        EndPlantPresentation(false);
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

        _interaction = BombInteractionType.None;
        _plantSite = null;
        _progress = 1f;
        EndPlantPresentation(completed == BombInteractionType.Plant);
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
        if (command.Action == GJInputAction.DropWeapon &&
            command.Phase == PlayerInputPhase.Started &&
            _weaponInventoryCtrl && _weaponInventoryCtrl.IsBombSelected)
        {
            CancelInteraction();
            _rules?.TryDrop(_player);
            return;
        }

        bool plantCommand = command.Action == GJInputAction.Interact ||
            (command.Action == GJInputAction.Fire &&
             _weaponInventoryCtrl && _weaponInventoryCtrl.IsBombSelected);
        if (!plantCommand)
        {
            return;
        }

        if (command.Phase == PlayerInputPhase.Started)
        {
            _plantInputHeld = true;
            // 同一个按键也负责门和掉落物；显式世界交互优先，避免在包点开门时误开始安放。
            if (command.Action == GJInputAction.Interact && !_worldInteractionCtrl)
            {
                TryGetComponent(out _worldInteractionCtrl);
            }
            if (command.Action == GJInputAction.Interact &&
                _worldInteractionCtrl && _worldInteractionCtrl.HasCommandTarget)
            {
                _plantInputHeld = false;
                return;
            }

            TryBeginInteraction();
        }
        else if (command.Phase == PlayerInputPhase.Canceled)
        {
            if (command.Action == GJInputAction.Fire && _suppressFireCancel)
            {
                return;
            }
            _plantInputHeld = false;
            _pendingPlantAfterSelection = false;
            CancelInteraction();
        }
    }

    private void HandleDied()
    {
        CancelInteraction();
    }

    private void SubscribeRules()
    {
        if (!_rules)
        {
            return;
        }

        _rules.BombSpawned -= HandleBombSpawned;
        _rules.BombCarrierChanged -= HandleBombCarrierChanged;
        _rules.BombSpawned += HandleBombSpawned;
        _rules.BombCarrierChanged += HandleBombCarrierChanged;
    }

    private void UnsubscribeRules()
    {
        if (!_rules)
        {
            return;
        }

        _rules.BombSpawned -= HandleBombSpawned;
        _rules.BombCarrierChanged -= HandleBombCarrierChanged;
    }

    private void HandleBombSpawned(BombEntity bomb)
    {
        SyncBombOwnership();
    }

    private void HandleBombCarrierChanged(PlayerInfo carrier)
    {
        SyncBombOwnership();
    }

    private void SyncBombOwnership()
    {
        bool ownsBomb = _rules && _rules.BombCarrier == _player;
        if (ownsBomb)
        {
            _weaponInventoryCtrl?.AddBombSlot();
        }
        else if (_weaponInventoryCtrl && _weaponInventoryCtrl.HasBombSlot)
        {
            _ = _weaponInventoryCtrl.RemoveBombSlotAsync();
        }

        if (ownsBomb && _weaponInventoryCtrl && _weaponInventoryCtrl.IsBombSelected)
        {
            RefreshCarryPresentation();
            return;
        }

        ClearCarryPresentation();
    }

    private void HandleBombSelectionChanged(bool selected)
    {
        if (selected && _rules && _rules.BombCarrier == _player)
        {
            RefreshCarryPresentation();
            if (_pendingPlantAfterSelection && _plantInputHeld)
            {
                _pendingPlantAfterSelection = false;
                TryBeginInteraction();
            }
            return;
        }

        _pendingPlantAfterSelection = false;
        ClearCarryPresentation();
    }

    private void HandleViewChanged(bool isThirdPerson)
    {
        if (_weaponInventoryCtrl && _weaponInventoryCtrl.IsBombSelected)
        {
            RefreshCarryPresentation();
        }
    }

    private void RefreshCarryPresentation()
    {
        BombEntity bomb = _rules ? _rules.Bomb : null;
        if (!bomb || bomb.Carrier != _player || !_weaponInventoryCtrl ||
            !_weaponInventoryCtrl.IsBombSelected)
        {
            ClearCarryPresentation();
            return;
        }

        _weaponViewCtrl?.SetWeaponModelsVisible(false);
        _animationCtrl?.SetBombCarryState(true, IsInteracting &&
            _interaction == BombInteractionType.Plant);
        Transform thirdPersonAnchor = GetThirdPersonCarryAnchor();
        bool isLocalFirstPerson = _inputCtrl && _inputCtrl.enabled &&
            _cameraCtrl && !_cameraCtrl.IsThirdPerson;
        if (isLocalFirstPerson)
        {
            _weaponViewCtrl?.SetFirstPersonArmsVisible(false);
            Transform cameraTransform = _cameraCtrl.ViewCamera
                ? _cameraCtrl.ViewCamera.transform
                : _cameraCtrl.transform;
            bomb.BeginFirstPersonCarryPresentation(
                cameraTransform,
                _firstPersonBombPosition,
                _firstPersonBombEuler);
            CreateThirdPersonBombVisual(bomb, thirdPersonAnchor);
            _cameraCtrl.RefreshCharacterVisibility();
            bomb.RefreshPresentationVisibility();
            return;
        }

        DestroyThirdPersonBombVisual();
        bomb.BeginThirdPersonCarryPresentation(
            thirdPersonAnchor,
            _thirdPersonBombLocalPosition,
            _thirdPersonBombEuler,
            _thirdPersonBombScale);
    }

    private void ClearCarryPresentation()
    {
        BombEntity bomb = _rules ? _rules.Bomb : null;
        if (bomb && bomb.Carrier == _player)
        {
            bomb.CancelCarryPresentation();
        }

        DestroyThirdPersonBombVisual();
        _animationCtrl?.SetBombCarryState(false, false);
        bool bombSlotSelected = _weaponInventoryCtrl &&
            _weaponInventoryCtrl.IsBombSelected;
        _weaponViewCtrl?.SetWeaponModelsVisible(!bombSlotSelected);
        if (!_lockApplied && !bombSlotSelected && _weaponViewCtrl &&
            _inputCtrl && _inputCtrl.enabled)
        {
            _weaponViewCtrl.SetFirstPersonArmsVisible(true);
        }
    }

    private Transform GetThirdPersonCarryAnchor()
    {
        Animator animator = _animationCtrl && _animationCtrl.ThirdPersonView
            ? _animationCtrl.ThirdPersonView.CharacterAnimator
            : null;
        if (_thirdPersonCarryAnchor && _thirdPersonCarryAnimator == animator)
        {
            UpdateThirdPersonCarryAnchor();
            return _thirdPersonCarryAnchor;
        }

        DestroyThirdPersonCarryAnchor();
        GameObject anchorObject = new GameObject("C4第三人称双手挂点");
        _thirdPersonCarryAnchor = anchorObject.transform;
        _thirdPersonCarryAnchor.SetParent(transform, false);
        _thirdPersonCarryAnimator = animator;
        if (animator && animator.isHuman)
        {
            _thirdPersonLeftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            _thirdPersonRightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
        }
        SetLayerRecursively(anchorObject, 8);
        UpdateThirdPersonCarryAnchor();
        return _thirdPersonCarryAnchor;
    }

    private void UpdateThirdPersonCarryAnchor()
    {
        if (!_thirdPersonCarryAnchor)
        {
            return;
        }

        Vector3 position = transform.position + transform.up * 1.35f;
        if (_thirdPersonLeftHand && _thirdPersonRightHand)
        {
            position = (_thirdPersonLeftHand.position + _thirdPersonRightHand.position) * 0.5f;
        }
        else if (_thirdPersonCarryAnimator && _thirdPersonCarryAnimator.isHuman)
        {
            Transform chest = _thirdPersonCarryAnimator.GetBoneTransform(HumanBodyBones.Chest);
            if (chest)
            {
                position = chest.position;
            }
        }

        position += transform.rotation * _thirdPersonBombPosition;
        _thirdPersonCarryAnchor.SetPositionAndRotation(position, transform.rotation);
    }

    private void DestroyThirdPersonCarryAnchor()
    {
        if (_thirdPersonCarryAnchor)
        {
            Destroy(_thirdPersonCarryAnchor.gameObject);
        }
        _thirdPersonCarryAnchor = null;
        _thirdPersonCarryAnimator = null;
        _thirdPersonLeftHand = null;
        _thirdPersonRightHand = null;
    }

    private void CreateThirdPersonBombVisual(BombEntity bomb, Transform anchor)
    {
        DestroyThirdPersonBombVisual();
        if (!bomb || !anchor || bomb.transform.childCount == 0)
        {
            return;
        }

        Transform source = bomb.transform.GetChild(0);
        _thirdPersonBombVisual = Instantiate(source.gameObject, anchor, false);
        _thirdPersonBombVisual.name = "C4第三人称携带表现";
        _thirdPersonBombVisual.transform.SetLocalPositionAndRotation(
            _thirdPersonBombLocalPosition,
            Quaternion.Euler(_thirdPersonBombEuler));
        ApplyCloneWorldScale(
            _thirdPersonBombVisual.transform,
            source.lossyScale * _thirdPersonBombScale);
        SetLayerRecursively(_thirdPersonBombVisual, 8);

        Renderer[] renderers = _thirdPersonBombVisual.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = true;
            renderers[i].forceRenderingOff = false;
        }
        Collider[] colliders = _thirdPersonBombVisual.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
        Light[] lights = _thirdPersonBombVisual.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights.Length; i++)
        {
            lights[i].enabled = false;
        }
    }

    private void DestroyThirdPersonBombVisual()
    {
        if (_thirdPersonBombVisual)
        {
            Destroy(_thirdPersonBombVisual);
            _thirdPersonBombVisual = null;
        }
    }

    private static void ApplyCloneWorldScale(Transform target, Vector3 worldScale)
    {
        Transform parent = target.parent;
        if (!parent)
        {
            target.localScale = worldScale;
            return;
        }

        Vector3 parentScale = parent.lossyScale;
        target.localScale = new Vector3(
            DivideScale(worldScale.x, parentScale.x),
            DivideScale(worldScale.y, parentScale.y),
            DivideScale(worldScale.z, parentScale.z));
    }

    private static float DivideScale(float target, float parentScale)
    {
        return Mathf.Abs(parentScale) > 0.00001f
            ? target / parentScale
            : target;
    }

    private static void SetLayerRecursively(GameObject root, int layer)
    {
        root.layer = layer;
        Transform rootTransform = root.transform;
        for (int i = 0; i < rootTransform.childCount; i++)
        {
            SetLayerRecursively(rootTransform.GetChild(i).gameObject, layer);
        }
    }

    private void BeginPlantPresentation()
    {
        _animationCtrl?.SetBombCarryState(true, true);
        if (!_inputCtrl || !_inputCtrl.enabled || !_cameraCtrl || !_rules ||
            _cameraCtrl.IsThirdPerson)
        {
            return;
        }

        _cameraCtrl.BeginForcedPitch(_plantLookDownAngle, _plantCameraLerpSpeed);
        _cameraCtrl.BeginForcedHeightDrop(_plantCameraDrop, _plantCameraLerpSpeed);
        _presentedBomb = _rules.Bomb;
        if (_presentedBomb)
        {
            _presentedBomb.BeginFirstPersonPlantPresentation(
                _cameraCtrl.ViewCamera ? _cameraCtrl.ViewCamera.transform : _cameraCtrl.transform,
                _firstPersonBombPosition,
                _firstPersonBombEuler,
                Vector3.zero,
                _firstPersonBombLerpSpeed);
        }
    }

    private void EndPlantPresentation(bool planted)
    {
        if (_cameraCtrl)
        {
            _cameraCtrl.EndForcedPitchImmediate();
            _cameraCtrl.EndForcedHeightDropImmediate();
        }

        if (!planted && _presentedBomb)
        {
            _presentedBomb.CancelFirstPersonPlantPresentation();
        }

        _presentedBomb = null;
        bool stillCarrying = !planted && _rules && _rules.BombCarrier == _player &&
            _weaponInventoryCtrl && _weaponInventoryCtrl.IsBombSelected;
        _animationCtrl?.SetBombCarryState(stillCarrying, false);
        if (stillCarrying)
        {
            RefreshCarryPresentation();
        }
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
            _suppressFireCancel = true;
            _actionCtrl.Receive(new PlayerInputCommand(
                GJInputAction.Fire,
                PlayerInputPhase.Canceled));
            _suppressFireCancel = false;
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
            bool showWeaponPresentation = !locked &&
                (!_weaponInventoryCtrl || !_weaponInventoryCtrl.IsBombSelected);
            _weaponViewCtrl.SetFirstPersonArmsVisible(showWeaponPresentation);
        }

        _lockApplied = locked;
    }
}
