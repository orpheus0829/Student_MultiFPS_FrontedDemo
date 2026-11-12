using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponActionCtrl : MonoBehaviour
{
    private const float MinimumInspectDuration = 1f;
    [Header("初始枪械")]
    [SerializeField, ResourceId(typeof(WeaponDefinitionSO)), InspectorName("初始枪械数据 ID")]
    private string _startingWeaponResourceId = "weapon.ak.data";

    private ResourceMgr _resourceMgr;
    private WeaponDefinitionSO _currentWeaponData;
    private WeaponActionSetSO _currentActionSet;
    private WeaponActionDefinition _currentAction;
    private bool[] _triggeredMarkers = Array.Empty<bool>();
    private string _ownedWeaponResourceId;
    private string _ownedActionSetResourceId;
    private float _elapsed;
    private float _progress;
    private bool _startedWhileAiming;
    private bool _perRoundSessionActive;
    private int _loadVersion;

    public event Action<WeaponDefinitionSO> WeaponChanged;
    public event Action<WeaponActionDefinition, bool> ActionStarted;
    public event Action<WeaponActionType, WeaponActionMarkerType> MarkerReached;
    public event Action<WeaponActionType> ActionCompleted;
    public event Action<WeaponActionType, WeaponActionCancelReason> ActionCanceled;

    public string StartingWeaponResourceId
    {
        get
        {
            return _startingWeaponResourceId;
        }
    }

    public WeaponDefinitionSO CurrentWeaponData
    {
        get
        {
            return _currentWeaponData;
        }
    }

    public WeaponActionSetSO CurrentActionSet
    {
        get
        {
            return _currentActionSet;
        }
    }

    public WeaponActionDefinition CurrentAction
    {
        get
        {
            return _currentAction;
        }
    }

    public bool IsBusy
    {
        get
        {
            return _currentAction != null;
        }
    }

    public float Progress
    {
        get
        {
            return _progress;
        }
    }

    public bool StartedWhileAiming
    {
        get
        {
            return _startedWhileAiming;
        }
    }

    public bool CanResumePerRoundReload
    {
        get
        {
            return _perRoundSessionActive && !IsBusy;
        }
    }

    public bool AllowsMovement
    {
        get
        {
            return _currentAction == null || _currentAction.AllowsMovement;
        }
    }

    public float MovementSpeedMultiplier
    {
        get
        {
            return _currentAction == null ? 1f : _currentAction.MovementSpeedMultiplier;
        }
    }

    public bool AllowsAim
    {
        get
        {
            return _currentAction == null || _currentAction.AllowsAim;
        }
    }

    public bool AllowsFire
    {
        get
        {
            return _currentAction == null || _currentAction.AllowsFire;
        }
    }

    public bool CanFireAfterReloadCommit
    {
        get
        {
            if (_currentAction == null ||
                (_currentAction.ActionType != WeaponActionType.Reload &&
                _currentAction.ActionType != WeaponActionType.EmptyReload))
            {
                return false;
            }

            IReadOnlyList<WeaponActionMarkerData> markers = _currentAction.Markers;
            for (int i = 0; i < markers.Count; i++)
            {
                if (markers[i].MarkerType == WeaponActionMarkerType.AmmoCommit &&
                    _triggeredMarkers[i])
                {
                    return true;
                }
            }

            return false;
        }
    }

    private void Update()
    {
        TickAction(Time.deltaTime);
    }

    private void Start()
    {
        if (!string.IsNullOrWhiteSpace(_startingWeaponResourceId))
        {
            _ = LoadWeaponAsync(_startingWeaponResourceId);
        }
    }

    private void OnDisable()
    {
        CancelCurrentAction(WeaponActionCancelReason.Disabled, true);
    }

    private void OnDestroy()
    {
        _loadVersion++;
        ReleaseOwnedResources();
    }

    public async Task<bool> LoadWeaponAsync(string weaponResourceId)
    {
        int loadVersion = ++_loadVersion;
        if (string.IsNullOrWhiteSpace(weaponResourceId))
        {
            return false;
        }

        _resourceMgr = ResourceMgr.instance;
        WeaponDefinitionSO weaponData = null;
        WeaponActionSetSO actionSet = null;

        try
        {
            weaponData = await _resourceMgr.LoadAsync<WeaponDefinitionSO>(weaponResourceId);
            if (!weaponData || loadVersion != _loadVersion || !this)
            {
                ReleaseLoadedWeapon(weaponResourceId);
                return false;
            }

            string actionSetResourceId = weaponData.ActionSetResourceId;
            actionSet = await _resourceMgr.LoadAsync<WeaponActionSetSO>(actionSetResourceId);
            if (!actionSet || loadVersion != _loadVersion || !this)
            {
                ReleaseLoadedActionSet(actionSetResourceId);
                ReleaseLoadedWeapon(weaponResourceId);
                return false;
            }

            // 新枪完整就绪后才撤下旧枪，加载失败时仍能把旧枪举回屏幕。
            CancelCurrentAction(WeaponActionCancelReason.WeaponChanged, true);
            ReleaseOwnedResources();
            _ownedWeaponResourceId = weaponResourceId;
            _ownedActionSetResourceId = actionSetResourceId;
            SetCurrentWeapon(weaponData, actionSet);
            return true;
        }
        catch (Exception exception)
        {
            ReleaseLoadedActionSet(
                weaponData ? weaponData.ActionSetResourceId : string.Empty);
            ReleaseLoadedWeapon(weaponResourceId);
            Debug.LogWarning($"枪械动作数据加载失败：{exception.Message}", this);
            return false;
        }
    }

    public void SetWeaponData(WeaponDefinitionSO weaponData, WeaponActionSetSO actionSet)
    {
        _loadVersion++;
        CancelCurrentAction(WeaponActionCancelReason.WeaponChanged, true);
        ReleaseOwnedResources();
        SetCurrentWeapon(weaponData, actionSet);
    }

    public bool TryStartAction(WeaponActionType actionType, bool startedWhileAiming = false)
    {
        if (!_currentActionSet ||
            !_currentActionSet.TryGetAction(actionType, out WeaponActionDefinition action))
        {
            return false;
        }

        if (IsBusy)
        {
            if (actionType != WeaponActionType.Fire ||
                _currentAction.ActionType != WeaponActionType.Fire)
            {
                return false;
            }

            ClearCurrentAction();
        }

        BeginAction(action, startedWhileAiming);
        return true;
    }

    public bool TryStartPerRoundReload(bool startedWhileAiming = false)
    {
        if (IsBusy || !_currentActionSet)
        {
            return false;
        }

        WeaponActionType actionType = _perRoundSessionActive
            ? WeaponActionType.PerRoundInsert
            : WeaponActionType.PerRoundStart;

        if (!_currentActionSet.TryGetAction(actionType, out WeaponActionDefinition action))
        {
            if (actionType == WeaponActionType.PerRoundStart)
            {
                actionType = WeaponActionType.PerRoundInsert;
            }

            if (!_currentActionSet.TryGetAction(actionType, out action))
            {
                return false;
            }
        }

        _perRoundSessionActive = true;
        BeginAction(action, startedWhileAiming);
        return true;
    }

    public bool ContinuePerRoundReload()
    {
        if (!_perRoundSessionActive || IsBusy)
        {
            return false;
        }

        return TryStartAction(WeaponActionType.PerRoundInsert, false);
    }

    public bool FinishPerRoundReload()
    {
        if (!_perRoundSessionActive || IsBusy || !_currentActionSet)
        {
            return false;
        }

        if (!_currentActionSet.TryGetAction(
            WeaponActionType.PerRoundEnd,
            out WeaponActionDefinition action))
        {
            _perRoundSessionActive = false;
            return true;
        }

        BeginAction(action, false);
        return true;
    }

    public bool CancelCurrentAction(
        WeaponActionCancelReason reason = WeaponActionCancelReason.Manual,
        bool force = false)
    {
        if (_currentAction == null)
        {
            return false;
        }

        if (!force &&
            (!_currentAction.CanInterrupt || _progress < _currentAction.InterruptStartProgress))
        {
            return false;
        }

        WeaponActionType canceledType = _currentAction.ActionType;
        if (!IsPerRoundAction(canceledType))
        {
            _perRoundSessionActive = false;
        }
        else if (reason != WeaponActionCancelReason.Vault ||
            canceledType == WeaponActionType.PerRoundStart)
        {
            _perRoundSessionActive = false;
        }

        ClearCurrentAction();
        ActionCanceled?.Invoke(canceledType, reason);
        return true;
    }

    public bool CancelForVault()
    {
        return CancelCurrentAction(WeaponActionCancelReason.Vault, true);
    }

    private void TickAction(float deltaTime)
    {
        if (_currentAction == null)
        {
            return;
        }

        float previousProgress = _progress;
        _elapsed += Mathf.Max(0f, deltaTime);
        // 默认检视动画为 1 秒；旧动作资源仍配置成 0.75 秒时，最后四分之一秒
        // 会提前放行疾跑层，表现为检视后半段被疾跑打断。
        float duration = _currentAction.ActionType == WeaponActionType.Inspect
            ? Mathf.Max(MinimumInspectDuration, _currentAction.Duration)
            : _currentAction.Duration;
        _progress = Mathf.Clamp01(_elapsed / Mathf.Max(0.01f, duration));
        TriggerMarkers(previousProgress, _progress);

        if (_progress < 1f)
        {
            return;
        }

        CompleteCurrentAction();
    }

    private void BeginAction(WeaponActionDefinition action, bool startedWhileAiming)
    {
        _currentAction = action;
        _startedWhileAiming = startedWhileAiming;
        _elapsed = 0f;
        _progress = 0f;
        _triggeredMarkers = new bool[action.Markers.Count];
        ActionStarted?.Invoke(action, _startedWhileAiming);
        TriggerMarkers(-1f, 0f);
    }

    private void CompleteCurrentAction()
    {
        WeaponActionType completedType = _currentAction.ActionType;
        if (completedType == WeaponActionType.PerRoundEnd)
        {
            _perRoundSessionActive = false;
        }

        ClearCurrentAction();
        ActionCompleted?.Invoke(completedType);
    }

    private void TriggerMarkers(float previousProgress, float currentProgress)
    {
        IReadOnlyList<WeaponActionMarkerData> markers = _currentAction.Markers;
        for (int i = 0; i < markers.Count; i++)
        {
            if (_triggeredMarkers[i])
            {
                continue;
            }

            WeaponActionMarkerData marker = markers[i];
            if (marker.Progress <= previousProgress || marker.Progress > currentProgress)
            {
                continue;
            }

            _triggeredMarkers[i] = true;
            MarkerReached?.Invoke(_currentAction.ActionType, marker.MarkerType);
        }
    }

    private void ClearCurrentAction()
    {
        _currentAction = null;
        _triggeredMarkers = Array.Empty<bool>();
        _elapsed = 0f;
        _progress = 0f;
        _startedWhileAiming = false;
    }

    private void SetCurrentWeapon(WeaponDefinitionSO weaponData, WeaponActionSetSO actionSet)
    {
        _currentWeaponData = weaponData;
        _currentActionSet = actionSet;
        _perRoundSessionActive = false;
        WeaponChanged?.Invoke(_currentWeaponData);
    }

    private void ReleaseOwnedResources()
    {
        if (!_resourceMgr)
        {
            _ownedWeaponResourceId = string.Empty;
            _ownedActionSetResourceId = string.Empty;
            return;
        }

        if (!string.IsNullOrWhiteSpace(_ownedActionSetResourceId))
        {
            _resourceMgr.Release<WeaponActionSetSO>(_ownedActionSetResourceId);
        }
        if (!string.IsNullOrWhiteSpace(_ownedWeaponResourceId))
        {
            _resourceMgr.Release<WeaponDefinitionSO>(_ownedWeaponResourceId);
        }

        _ownedWeaponResourceId = string.Empty;
        _ownedActionSetResourceId = string.Empty;
    }

    private void ReleaseLoadedWeapon(string resourceId)
    {
        if (_resourceMgr && !string.IsNullOrWhiteSpace(resourceId))
        {
            _resourceMgr.Release<WeaponDefinitionSO>(resourceId);
        }
    }

    private void ReleaseLoadedActionSet(string resourceId)
    {
        if (_resourceMgr && !string.IsNullOrWhiteSpace(resourceId))
        {
            _resourceMgr.Release<WeaponActionSetSO>(resourceId);
        }
    }

    private bool IsPerRoundAction(WeaponActionType actionType)
    {
        return actionType == WeaponActionType.PerRoundStart ||
            actionType == WeaponActionType.PerRoundInsert ||
            actionType == WeaponActionType.PerRoundEnd;
    }
}
