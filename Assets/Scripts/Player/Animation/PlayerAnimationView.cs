using System.Collections.Generic;
using UnityEngine;

public enum PlayerViewType
{
    [InspectorName("第一人称")] FirstPerson = 0,
    [InspectorName("第三人称")] ThirdPerson = 1
}

[DisallowMultipleComponent]
public sealed class PlayerAnimationView : MonoBehaviour
{
    private const float FirstPersonForwardThreshold = 0.1f;
    private const string UpperBodyWeaponLayerName = "UpperBodyWeapon";

    [Header("视角")]
    [SerializeField, InspectorName("表现视角")] private PlayerViewType _viewType;

    [Header("Animator")]
    [SerializeField, InspectorName("角色 Animator")] private Animator _characterAnimator;
    [SerializeField, InspectorName("当前武器 Animator")] private Animator _weaponAnimator;

    [Header("武器")]
    [SerializeField, InspectorName("武器挂点")] private Transform _weaponRoot;

    [Header("状态过渡")]
    [SerializeField, Min(0f), InspectorName("动作过渡时间")] private float _actionTransitionDuration = 0.05f;
    [SerializeField, Min(0f), InspectorName("换弹结束融合时间")]
    private float _reloadFinishTransitionDuration = 0.14f;
    [SerializeField, Min(0f), InspectorName("参数平滑时间")] private float _parameterDampTime = 0.08f;
    [SerializeField, InspectorName("角色待机状态名")] private string _characterIdleStateName = "Idle";
    [SerializeField, InspectorName("武器待机状态名")] private string _weaponIdleStateName = "Idle";
    [SerializeField, InspectorName("翻越状态名")] private string _vaultStateName = "Vault";
    [SerializeField, InspectorName("滑铲状态名")] private string _slideStateName = "Slide";

    [Header("第三人称滑铲")]
    [SerializeField, Min(0f), InspectorName("模型下沉高度")]
    private float _slideVisualDrop = 0.4f;
    [SerializeField, Min(0.01f), InspectorName("模型贴地过渡时间")]
    private float _slideVisualBlendDuration = 0.12f;

    private readonly AnimatorParameterCache _characterParameters = new AnimatorParameterCache();
    private readonly AnimatorParameterCache _weaponParameters = new AnimatorParameterCache();

    private RuntimeAnimatorController _cachedCharacterController;
    private RuntimeAnimatorController _cachedWeaponController;
    private int _tacSprintLayerIndex = -1;
    private int _triggerDisciplineLayerIndex = -1;
    private int _rightHandLayerIndex = -1;
    private int _upperBodyWeaponLayerIndex = -1;
    private float _upperBodyWeaponWeight;
    private bool _isUpperBodyWeaponActionActive;
    private bool _isFullBodyMotion;
    private WeaponActionType _upperBodyActionType;
    private Vector3 _restLocalPosition;

    private void Awake()
    {
        _restLocalPosition = transform.localPosition;
    }

    public PlayerViewType ViewType
    {
        get
        {
            return _viewType;
        }
    }

    public Animator CharacterAnimator
    {
        get
        {
            return _characterAnimator;
        }
    }

    public Animator WeaponAnimator
    {
        get
        {
            return _weaponAnimator;
        }
    }

    public Transform WeaponRoot
    {
        get
        {
            return _weaponRoot;
        }
    }

    private void OnEnable()
    {
        RefreshAnimatorBindings();
    }

    public void ApplyFrame(PlayerAnimationFrame frame)
    {
        EnsureAnimatorBindings();

        Vector2 move = frame.Move;
        float speed = frame.Speed;
        bool isGrounded = frame.IsGrounded;
        float verticalSpeed = frame.VerticalSpeed;
        bool isSprinting = frame.IsSprinting;
        bool isVaulting = frame.IsVaulting;
        bool isSliding = frame.IsSliding;

        if (_viewType == PlayerViewType.FirstPerson)
        {
            bool hasForwardMove = !isVaulting && move.y > FirstPersonForwardThreshold;
            move = hasForwardMove
                ? new Vector2(0f, move.y)
                : Vector2.zero;
            speed = hasForwardMove ? speed : 0f;
            isSprinting = !isVaulting && isSprinting;
            isGrounded = isVaulting || frame.IsGrounded;
            verticalSpeed = isVaulting ? 0f : frame.VerticalSpeed;
            isVaulting = false;
            isSliding = false;
        }

        ApplyFloat(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.MoveX,
            move.x);
        ApplyFloat(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.MoveY,
            move.y);
        ApplyFloat(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Speed,
            speed);
        ApplyKinemationGait(move, isSprinting);
        ApplyBool(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Grounded,
            isGrounded);
        ApplyFloat(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.VerticalSpeed,
            verticalSpeed);
        ApplyBool(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Sprinting,
            isSprinting);
        ApplyFloat(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.AimProgress,
            frame.AimProgress);
        ApplyBool(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Vaulting,
            isVaulting);
        ApplyBool(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Sliding,
            isSliding);

        ApplyFloat(
            _weaponAnimator,
            _weaponParameters,
            PlayerAnimatorContract.AimProgress,
            frame.AimProgress);
        _isFullBodyMotion = _viewType == PlayerViewType.ThirdPerson &&
            (isVaulting || isSliding || !isGrounded);
        UpdateUpperBodyWeaponLayer();
        UpdateSlideVisual(isSliding);
    }

    public void SetWeaponAnimator(Animator weaponAnimator)
    {
        _weaponAnimator = weaponAnimator;
        RefreshAnimatorBindings();
    }

    public void SetCharacterAnimator(Animator characterAnimator)
    {
        if (_characterAnimator == characterAnimator || !characterAnimator)
        {
            return;
        }

        RuntimeAnimatorController controller = _characterAnimator
            ? _characterAnimator.runtimeAnimatorController
            : null;
        _characterAnimator = characterAnimator;
        characterAnimator.runtimeAnimatorController = controller;
        _isUpperBodyWeaponActionActive = false;
        _upperBodyWeaponWeight = 0f;
        _upperBodyActionType = WeaponActionType.None;
        _isFullBodyMotion = false;
        RefreshAnimatorBindings();
        PlayStateImmediately(_characterAnimator, _characterIdleStateName);
        _characterAnimator.Update(0f);
    }

    public void SetCharacterAnimatorController(RuntimeAnimatorController controller)
    {
        if (!_characterAnimator || _characterAnimator.runtimeAnimatorController == controller)
        {
            return;
        }

        _characterAnimator.runtimeAnimatorController = controller;
        RefreshAnimatorBindings();
        _isUpperBodyWeaponActionActive = false;
        _upperBodyWeaponWeight = 0f;
        _upperBodyActionType = WeaponActionType.None;
        _isFullBodyMotion = false;
        SetLayerWeight(_upperBodyWeaponLayerIndex, 0f);
        PlayStateImmediately(_characterAnimator, _characterIdleStateName);
        _characterAnimator.Update(0f);
    }

    public void StartVault()
    {
        if (_viewType != PlayerViewType.ThirdPerson)
        {
            return;
        }

        EnsureAnimatorBindings();
        SuppressUpperBodyWeaponLayer();
        ApplyTrigger(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Vault);
        PlayState(_characterAnimator, _vaultStateName);
    }

    public void FinishVault()
    {
        if (_viewType != PlayerViewType.ThirdPerson)
        {
            return;
        }

        EnsureAnimatorBindings();
        ResetTrigger(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Vault);
        PlayState(_characterAnimator, _characterIdleStateName);
    }

    public void StartSlide()
    {
        if (_viewType != PlayerViewType.ThirdPerson)
        {
            return;
        }

        EnsureAnimatorBindings();
        SuppressUpperBodyWeaponLayer();
        ApplyTrigger(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Slide);
        PlayState(_characterAnimator, _slideStateName);
    }

    public void FinishSlide()
    {
        if (_viewType != PlayerViewType.ThirdPerson)
        {
            return;
        }

        EnsureAnimatorBindings();
        ResetTrigger(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Slide);
        PlayState(_characterAnimator, _characterIdleStateName);
    }

    public void StartWeaponAction(
        WeaponActionDefinition action,
        bool startedWhileAiming)
    {
        if (action == null)
        {
            return;
        }

        EnsureAnimatorBindings();
        int actionValue = (int)action.ActionType;
        if (_viewType == PlayerViewType.FirstPerson &&
            (action.ActionType == WeaponActionType.Equip ||
             action.ActionType == WeaponActionType.Holster))
        {
            // 第一人称切枪统一由手臂根节点升降，避免资源包的通用步枪动作扭动手腕。
            return;
        }

        ApplyWeaponActionSignal(
            PlayerAnimatorContract.WeaponActionStarted,
            actionValue);

        string stateName = _viewType == PlayerViewType.FirstPerson
            ? action.GetFirstPersonStateName(startedWhileAiming)
            : action.GetThirdPersonStateName(startedWhileAiming);
        if (_viewType == PlayerViewType.ThirdPerson && _upperBodyWeaponLayerIndex >= 0)
        {
            string fallbackState = GetThirdPersonFallbackState(action.ActionType);
            bool hasActionState = PlayStateOnLayer(
                _characterAnimator,
                _upperBodyWeaponLayerIndex,
                stateName);
            if (!hasActionState)
            {
                hasActionState = PlayStateOnLayer(
                    _characterAnimator,
                    _upperBodyWeaponLayerIndex,
                    fallbackState);
            }
            _isUpperBodyWeaponActionActive = hasActionState;
            _upperBodyActionType = action.ActionType;
            PlayState(_weaponAnimator, stateName);
            return;
        }
        PlayState(_characterAnimator, stateName);
        PlayState(_weaponAnimator, stateName);
    }

    public void CompleteWeaponAction(WeaponActionType actionType)
    {
        EnsureAnimatorBindings();
        ApplyWeaponActionSignal(
            PlayerAnimatorContract.WeaponActionCompleted,
            (int)actionType);
        float transitionDuration = IsReloadAction(actionType)
            ? _reloadFinishTransitionDuration
            : _actionTransitionDuration;
        if (_viewType == PlayerViewType.ThirdPerson && _upperBodyWeaponLayerIndex >= 0)
        {
            _isUpperBodyWeaponActionActive = false;
            PlayState(_weaponAnimator, _weaponIdleStateName, transitionDuration);
            return;
        }
        PlayState(_characterAnimator, _characterIdleStateName, transitionDuration);
        PlayState(_weaponAnimator, _weaponIdleStateName, transitionDuration);
    }

    public void CancelWeaponAction(WeaponActionType actionType)
    {
        EnsureAnimatorBindings();
        if (actionType == WeaponActionType.Inspect || IsPerRoundAction(actionType))
        {
            ResetTrigger(
                _characterAnimator,
                _characterParameters,
                PlayerAnimatorContract.WeaponActionStarted);
            ResetTrigger(
                _weaponAnimator,
                _weaponParameters,
                PlayerAnimatorContract.WeaponActionStarted);
        }
        ApplyWeaponActionSignal(
            PlayerAnimatorContract.WeaponActionCancelled,
            (int)actionType);
        if (_viewType == PlayerViewType.ThirdPerson && _upperBodyWeaponLayerIndex >= 0)
        {
            _isUpperBodyWeaponActionActive = false;
            if (actionType == WeaponActionType.Inspect || IsPerRoundAction(actionType))
            {
                _upperBodyWeaponWeight = 0f;
                SetLayerWeight(_upperBodyWeaponLayerIndex, 0f);
                PlayStateImmediately(_weaponAnimator, _weaponIdleStateName);
                return;
            }

            PlayState(_weaponAnimator, _weaponIdleStateName);
            return;
        }
        if (actionType == WeaponActionType.Inspect)
        {
            // 检视位于 Reload 层；仅重播 Base Layer 的 Idle 不会停止检视。
            PlayStateImmediatelyOnActionLayer(
                _characterAnimator,
                WeaponActionStateContract.Inspect,
                "Empty");
            PlayStateImmediately(_weaponAnimator, _weaponIdleStateName);
            return;
        }
        if (IsPerRoundAction(actionType))
        {
            // 装弹被开火打断时，先清掉 Reload 层的旧姿势，再切入开火状态。
            PlayStateImmediatelyOnActionLayer(
                _characterAnimator,
                WeaponActionStateContract.GetStateName(actionType),
                "Empty");
            PlayStateImmediately(_weaponAnimator, _weaponIdleStateName);
            return;
        }
        PlayState(_characterAnimator, _characterIdleStateName);
        PlayState(_weaponAnimator, _weaponIdleStateName);
    }

    public void ResetPresentation()
    {
        _isUpperBodyWeaponActionActive = false;
        _upperBodyWeaponWeight = 0f;
        _upperBodyActionType = WeaponActionType.None;
        _isFullBodyMotion = false;
        SetLayerWeight(_upperBodyWeaponLayerIndex, 0f);
        if (_viewType == PlayerViewType.ThirdPerson)
        {
            transform.localPosition = _restLocalPosition;
        }
        EnsureAnimatorBindings();
        ApplyBool(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Sprinting,
            false);
        ApplyBool(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Vaulting,
            false);
        ApplyBool(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Sliding,
            false);
        ApplyFloat(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.AimProgress,
            0f);
        ApplyFloat(
            _weaponAnimator,
            _weaponParameters,
            PlayerAnimatorContract.AimProgress,
            0f);
        ResetTrigger(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Vault);
        ResetTrigger(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Slide);
    }

    public void RefreshAnimatorBindings()
    {
        _cachedCharacterController = _characterAnimator
            ? _characterAnimator.runtimeAnimatorController
            : null;
        _cachedWeaponController = _weaponAnimator
            ? _weaponAnimator.runtimeAnimatorController
            : null;
        _characterParameters.Refresh(_characterAnimator);
        _weaponParameters.Refresh(_weaponAnimator);
        RefreshKinemationLayers();
        _upperBodyWeaponLayerIndex = GetLayerIndex(UpperBodyWeaponLayerName);
    }

    private void ApplyKinemationGait(Vector2 move, bool isSprinting)
    {
        if (_viewType != PlayerViewType.FirstPerson || !_characterAnimator)
        {
            return;
        }

        float gait = isSprinting ? 2f : Mathf.Clamp01(move.magnitude);
        ApplyFloat(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.Gait,
            gait);

        SetLayerWeight(_tacSprintLayerIndex, 0f);
        SetLayerWeight(
            _triggerDisciplineLayerIndex,
            ReadFloat(PlayerAnimatorContract.TacSprintWeight));
        SetLayerWeight(
            _rightHandLayerIndex,
            ReadFloat(PlayerAnimatorContract.RightHandWeight));
    }

    private void RefreshKinemationLayers()
    {
        _tacSprintLayerIndex = GetLayerIndex(PlayerAnimatorContract.TacSprintLayerName);
        _triggerDisciplineLayerIndex = GetLayerIndex(
            PlayerAnimatorContract.TriggerDisciplineLayerName);
        _rightHandLayerIndex = GetLayerIndex(PlayerAnimatorContract.RightHandLayerName);
    }

    private int GetLayerIndex(string layerName)
    {
        if (!_characterAnimator || !_characterAnimator.runtimeAnimatorController)
        {
            return -1;
        }

        return _characterAnimator.GetLayerIndex(layerName);
    }

    private float ReadFloat(int parameterHash)
    {
        if (!_characterParameters.HasFloat(parameterHash))
        {
            return 0f;
        }

        return _characterAnimator.GetFloat(parameterHash);
    }

    private void SetLayerWeight(int layerIndex, float weight)
    {
        if (!_characterAnimator || layerIndex < 0)
        {
            return;
        }

        _characterAnimator.SetLayerWeight(layerIndex, Mathf.Clamp01(weight));
    }

    private void ApplyWeaponActionSignal(int triggerHash, int actionValue)
    {
        ApplyInt(
            _characterAnimator,
            _characterParameters,
            PlayerAnimatorContract.WeaponAction,
            actionValue);
        ApplyTrigger(_characterAnimator, _characterParameters, triggerHash);
        ApplyInt(
            _weaponAnimator,
            _weaponParameters,
            PlayerAnimatorContract.WeaponAction,
            actionValue);
        ApplyTrigger(_weaponAnimator, _weaponParameters, triggerHash);
    }

    private void EnsureAnimatorBindings()
    {
        RuntimeAnimatorController characterController = _characterAnimator
            ? _characterAnimator.runtimeAnimatorController
            : null;
        RuntimeAnimatorController weaponController = _weaponAnimator
            ? _weaponAnimator.runtimeAnimatorController
            : null;
        if (characterController == _cachedCharacterController &&
            weaponController == _cachedWeaponController)
        {
            return;
        }

        RefreshAnimatorBindings();
    }

    private void PlayState(Animator animator, string stateName)
    {
        PlayState(animator, stateName, _actionTransitionDuration);
    }

    private void PlayStateImmediately(Animator animator, string stateName)
    {
        if (!animator || !animator.runtimeAnimatorController ||
            string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            int stateHash = Animator.StringToHash(stateName);
            int fullPathHash = Animator.StringToHash(
                animator.GetLayerName(layer) + "." + stateName);
            if (!animator.HasState(layer, fullPathHash) &&
                !animator.HasState(layer, stateHash))
            {
                continue;
            }

            int targetHash = animator.HasState(layer, fullPathHash)
                ? fullPathHash
                : stateHash;
            animator.Play(targetHash, layer, 0f);
            return;
        }
    }

    private void PlayStateImmediatelyOnActionLayer(
        Animator animator,
        string actionStateName,
        string emptyStateName)
    {
        if (!animator || !animator.runtimeAnimatorController)
        {
            return;
        }

        int actionHash = Animator.StringToHash(actionStateName);
        int emptyHash = Animator.StringToHash(emptyStateName);
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            int actionFullPathHash = Animator.StringToHash(
                animator.GetLayerName(layer) + "." + actionStateName);
            int fullPathHash = Animator.StringToHash(
                animator.GetLayerName(layer) + "." + emptyStateName);
            if ((!animator.HasState(layer, actionHash) &&
                 !animator.HasState(layer, actionFullPathHash)) ||
                (!animator.HasState(layer, emptyHash) &&
                 !animator.HasState(layer, fullPathHash)))
            {
                continue;
            }

            animator.Play(
                animator.HasState(layer, fullPathHash) ? fullPathHash : emptyHash,
                layer,
                0f);
            return;
        }
    }

    private void PlayState(Animator animator, string stateName, float transitionDuration)
    {
        if (!animator ||
            !animator.runtimeAnimatorController ||
            animator.layerCount <= 0 ||
            string.IsNullOrWhiteSpace(stateName))
        {
            return;
        }

        int stateHash = Animator.StringToHash(stateName);
        for (int layer = 0; layer < animator.layerCount; layer++)
        {
            string layerName = animator.GetLayerName(layer);
            int fullPathHash = Animator.StringToHash(layerName + "." + stateName);
            if (!animator.HasState(layer, stateHash) &&
                !animator.HasState(layer, fullPathHash))
            {
                continue;
            }

            animator.CrossFadeInFixedTime(
                fullPathHash,
                transitionDuration,
                layer,
                0f);
            return;
        }
    }

    private bool PlayStateOnLayer(Animator animator, int layerIndex, string stateName)
    {
        if (!animator || !animator.runtimeAnimatorController || layerIndex < 0 ||
            layerIndex >= animator.layerCount || string.IsNullOrWhiteSpace(stateName))
        {
            return false;
        }

        int stateHash = Animator.StringToHash(stateName);
        int fullPathHash = Animator.StringToHash(
            animator.GetLayerName(layerIndex) + "." + stateName);
        if (!animator.HasState(layerIndex, stateHash) &&
            !animator.HasState(layerIndex, fullPathHash))
        {
            return false;
        }

        animator.CrossFadeInFixedTime(fullPathHash, _actionTransitionDuration, layerIndex, 0f);
        return true;
    }

    private void UpdateUpperBodyWeaponLayer()
    {
        if (_viewType != PlayerViewType.ThirdPerson || _upperBodyWeaponLayerIndex < 0)
        {
            return;
        }

        if (_isFullBodyMotion)
        {
            SuppressUpperBodyWeaponLayer();
            return;
        }

        float targetWeight = _isUpperBodyWeaponActionActive ? 1f : 0f;
        float duration = _isUpperBodyWeaponActionActive
            ? _actionTransitionDuration
            : IsReloadAction(_upperBodyActionType)
                ? _reloadFinishTransitionDuration
                : _actionTransitionDuration;
        _upperBodyWeaponWeight = Mathf.MoveTowards(
            _upperBodyWeaponWeight,
            targetWeight,
            Time.deltaTime / Mathf.Max(0.01f, duration));
        SetLayerWeight(_upperBodyWeaponLayerIndex, _upperBodyWeaponWeight);
    }

    private void SuppressUpperBodyWeaponLayer()
    {
        _upperBodyWeaponWeight = 0f;
        SetLayerWeight(_upperBodyWeaponLayerIndex, 0f);
    }

    private static string GetThirdPersonFallbackState(WeaponActionType actionType)
    {
        return IsReloadAction(actionType)
            ? WeaponActionStateContract.Reload
            : WeaponActionStateContract.GetStateName(actionType);
    }

    private void UpdateSlideVisual(bool isSliding)
    {
        if (_viewType != PlayerViewType.ThirdPerson)
        {
            return;
        }

        float targetY = _restLocalPosition.y - (isSliding ? _slideVisualDrop : 0f);
        Vector3 position = transform.localPosition;
        position.y = Mathf.MoveTowards(
            position.y,
            targetY,
            _slideVisualDrop / Mathf.Max(0.01f, _slideVisualBlendDuration) * Time.deltaTime);
        transform.localPosition = position;
    }

    private static bool IsReloadAction(WeaponActionType actionType)
    {
        return actionType == WeaponActionType.Reload ||
            actionType == WeaponActionType.EmptyReload ||
            actionType == WeaponActionType.PerRoundStart ||
            actionType == WeaponActionType.PerRoundInsert ||
            actionType == WeaponActionType.PerRoundEnd;
    }

    private static bool IsPerRoundAction(WeaponActionType actionType)
    {
        return actionType == WeaponActionType.PerRoundStart ||
            actionType == WeaponActionType.PerRoundInsert ||
            actionType == WeaponActionType.PerRoundEnd;
    }

    private void ApplyFloat(
        Animator animator,
        AnimatorParameterCache parameters,
        int parameterHash,
        float value)
    {
        if (!animator ||
            !animator.runtimeAnimatorController ||
            !parameters.HasFloat(parameterHash))
        {
            return;
        }

        animator.SetFloat(parameterHash, value, _parameterDampTime, Time.deltaTime);
    }

    private static void ApplyBool(
        Animator animator,
        AnimatorParameterCache parameters,
        int parameterHash,
        bool value)
    {
        if (animator &&
            animator.runtimeAnimatorController &&
            parameters.HasBool(parameterHash))
        {
            animator.SetBool(parameterHash, value);
        }
    }

    private static void ApplyInt(
        Animator animator,
        AnimatorParameterCache parameters,
        int parameterHash,
        int value)
    {
        if (animator &&
            animator.runtimeAnimatorController &&
            parameters.HasInt(parameterHash))
        {
            animator.SetInteger(parameterHash, value);
        }
    }

    private static void ApplyTrigger(
        Animator animator,
        AnimatorParameterCache parameters,
        int parameterHash)
    {
        if (animator &&
            animator.runtimeAnimatorController &&
            parameters.HasTrigger(parameterHash))
        {
            animator.SetTrigger(parameterHash);
        }
    }

    private static void ResetTrigger(
        Animator animator,
        AnimatorParameterCache parameters,
        int parameterHash)
    {
        if (animator &&
            animator.runtimeAnimatorController &&
            parameters.HasTrigger(parameterHash))
        {
            animator.ResetTrigger(parameterHash);
        }
    }

    private sealed class AnimatorParameterCache
    {
        private readonly HashSet<int> _floats = new HashSet<int>();
        private readonly HashSet<int> _bools = new HashSet<int>();
        private readonly HashSet<int> _ints = new HashSet<int>();
        private readonly HashSet<int> _triggers = new HashSet<int>();

        public void Refresh(Animator animator)
        {
            _floats.Clear();
            _bools.Clear();
            _ints.Clear();
            _triggers.Clear();

            if (!animator || !animator.runtimeAnimatorController)
            {
                return;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                AnimatorControllerParameter parameter = parameters[i];
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Float:
                        _floats.Add(parameter.nameHash);
                        break;
                    case AnimatorControllerParameterType.Bool:
                        _bools.Add(parameter.nameHash);
                        break;
                    case AnimatorControllerParameterType.Int:
                        _ints.Add(parameter.nameHash);
                        break;
                    case AnimatorControllerParameterType.Trigger:
                        _triggers.Add(parameter.nameHash);
                        break;
                }
            }
        }

        public bool HasFloat(int parameterHash)
        {
            return _floats.Contains(parameterHash);
        }

        public bool HasBool(int parameterHash)
        {
            return _bools.Contains(parameterHash);
        }

        public bool HasInt(int parameterHash)
        {
            return _ints.Contains(parameterHash);
        }

        public bool HasTrigger(int parameterHash)
        {
            return _triggers.Contains(parameterHash);
        }
    }
}
