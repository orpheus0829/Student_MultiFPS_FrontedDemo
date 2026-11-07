using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class WeaponActionMarkerData
{
    [SerializeField] private WeaponActionMarkerType _markerType;
    [SerializeField, Range(0f, 1f)] private float _progress;

    public WeaponActionMarkerData()
    {
    }

    internal WeaponActionMarkerData(WeaponActionMarkerType markerType, float progress)
    {
        _markerType = markerType;
        _progress = Mathf.Clamp01(progress);
    }

    public WeaponActionMarkerType MarkerType
    {
        get
        {
            return _markerType;
        }
    }

    public float Progress
    {
        get
        {
            return _progress;
        }
    }
}

[Serializable]
public sealed class WeaponActionDefinition
{
    [SerializeField] private WeaponActionType _actionType;
    [SerializeField] private bool _enabled = true;
    [SerializeField, Min(0.01f)] private float _duration = 0.3f;
    [SerializeField] private bool _canInterrupt = true;
    [SerializeField, Range(0f, 1f)] private float _interruptStartProgress;

    [SerializeField] private bool _allowsMovement = true;
    [SerializeField, Min(0f)] private float _movementSpeedMultiplier = 1f;
    [SerializeField] private bool _allowsAim;
    [SerializeField] private bool _allowsFire;

    [SerializeField] private bool _overrideAnimatorState;
    [SerializeField] private string _firstPersonStateOverride;
    [SerializeField] private string _thirdPersonStateOverride;
    [SerializeField] private bool _hasAimStateOverride;
    [SerializeField] private string _firstPersonAimStateOverride;
    [SerializeField] private string _thirdPersonAimStateOverride;

    [SerializeField] private List<WeaponActionMarkerData> _markers = new List<WeaponActionMarkerData>();

    public WeaponActionDefinition()
    {
    }

    internal WeaponActionDefinition(
        WeaponActionType actionType,
        bool enabled,
        float duration,
        bool allowsAim,
        bool allowsFire)
    {
        _actionType = actionType;
        _enabled = enabled;
        _duration = duration;
        _allowsAim = allowsAim;
        _allowsFire = allowsFire;

        if (actionType == WeaponActionType.Fire)
        {
            _markers.Add(new WeaponActionMarkerData(
                WeaponActionMarkerType.FireShot,
                0f));
        }
        else if (actionType == WeaponActionType.Reload ||
            actionType == WeaponActionType.EmptyReload)
        {
            _markers.Add(new WeaponActionMarkerData(
                WeaponActionMarkerType.AmmoCommit,
                0.7f));
        }
        else if (actionType == WeaponActionType.PerRoundInsert)
        {
            _markers.Add(new WeaponActionMarkerData(
                WeaponActionMarkerType.InsertRound,
                0.5f));
        }
    }

    public WeaponActionType ActionType
    {
        get
        {
            return _actionType;
        }
    }

    public bool Enabled
    {
        get
        {
            return _enabled;
        }
    }

    public float Duration
    {
        get
        {
            return Mathf.Max(0.01f, _duration);
        }
    }

    public bool CanInterrupt
    {
        get
        {
            return _canInterrupt;
        }
    }

    public float InterruptStartProgress
    {
        get
        {
            return _interruptStartProgress;
        }
    }

    public bool AllowsMovement
    {
        get
        {
            return _allowsMovement;
        }
    }

    public float MovementSpeedMultiplier
    {
        get
        {
            return _movementSpeedMultiplier;
        }
    }

    public bool AllowsAim
    {
        get
        {
            return _allowsAim;
        }
    }

    public bool AllowsFire
    {
        get
        {
            return _allowsFire;
        }
    }

    public IReadOnlyList<WeaponActionMarkerData> Markers
    {
        get
        {
            return _markers;
        }
    }

    public string GetFirstPersonStateName(bool startedWhileAiming)
    {
        if (startedWhileAiming && _hasAimStateOverride &&
            !string.IsNullOrWhiteSpace(_firstPersonAimStateOverride))
        {
            return _firstPersonAimStateOverride;
        }

        if (_overrideAnimatorState && !string.IsNullOrWhiteSpace(_firstPersonStateOverride))
        {
            return _firstPersonStateOverride;
        }

        return WeaponActionStateContract.GetStateName(_actionType);
    }

    public string GetThirdPersonStateName(bool startedWhileAiming)
    {
        if (startedWhileAiming && _hasAimStateOverride &&
            !string.IsNullOrWhiteSpace(_thirdPersonAimStateOverride))
        {
            return _thirdPersonAimStateOverride;
        }

        if (_overrideAnimatorState && !string.IsNullOrWhiteSpace(_thirdPersonStateOverride))
        {
            return _thirdPersonStateOverride;
        }

        return WeaponActionStateContract.GetStateName(_actionType);
    }

    internal void SetActionType(WeaponActionType actionType)
    {
        _actionType = actionType;
        _duration = Mathf.Max(0.01f, _duration);
        _interruptStartProgress = Mathf.Clamp01(_interruptStartProgress);
        _movementSpeedMultiplier = Mathf.Max(0f, _movementSpeedMultiplier);

        if (_markers == null)
        {
            _markers = new List<WeaponActionMarkerData>();
        }

        EnsureRequiredMarker();
    }

    private void EnsureRequiredMarker()
    {
        WeaponActionMarkerType requiredMarker;
        float defaultProgress;
        if (_actionType == WeaponActionType.Fire)
        {
            requiredMarker = WeaponActionMarkerType.FireShot;
            defaultProgress = 0f;
        }
        else if (_actionType == WeaponActionType.Reload ||
            _actionType == WeaponActionType.EmptyReload)
        {
            requiredMarker = WeaponActionMarkerType.AmmoCommit;
            defaultProgress = 0.7f;
        }
        else if (_actionType == WeaponActionType.PerRoundInsert)
        {
            requiredMarker = WeaponActionMarkerType.InsertRound;
            defaultProgress = 0.5f;
        }
        else
        {
            return;
        }

        for (int i = 0; i < _markers.Count; i++)
        {
            WeaponActionMarkerData marker = _markers[i];
            if (marker != null && marker.MarkerType == requiredMarker)
            {
                return;
            }
        }

        _markers.Add(new WeaponActionMarkerData(requiredMarker, defaultProgress));
    }
}

[CreateAssetMenu(fileName = "WeaponActions", menuName = "Gun Demo/枪械/枪械动作集")]
public sealed class WeaponActionSetSO : ScriptableObject
{
    [SerializeField, HideInInspector] private int _dataVersion;
    [SerializeField] private WeaponActionDefinition _fire = new WeaponActionDefinition(
        WeaponActionType.Fire, true, 0.1f, true, true);
    [SerializeField] private WeaponActionDefinition _reload = new WeaponActionDefinition(
        WeaponActionType.Reload, true, 2f, false, false);
    [SerializeField] private WeaponActionDefinition _emptyReload = new WeaponActionDefinition(
        WeaponActionType.EmptyReload, false, 2.5f, false, false);
    [SerializeField] private WeaponActionDefinition _perRoundStart = new WeaponActionDefinition(
        WeaponActionType.PerRoundStart, false, 0.3f, false, false);
    [SerializeField] private WeaponActionDefinition _perRoundInsert = new WeaponActionDefinition(
        WeaponActionType.PerRoundInsert, false, 0.65f, false, false);
    [SerializeField] private WeaponActionDefinition _perRoundEnd = new WeaponActionDefinition(
        WeaponActionType.PerRoundEnd, false, 0.3f, false, false);
    [SerializeField] private WeaponActionDefinition _equip = new WeaponActionDefinition(
        WeaponActionType.Equip, true, 0.45f, false, false);
    [SerializeField] private WeaponActionDefinition _holster = new WeaponActionDefinition(
        WeaponActionType.Holster, true, 0.3f, false, false);
    [SerializeField] private WeaponActionDefinition _inspect = new WeaponActionDefinition(
        WeaponActionType.Inspect, true, 0.75f, false, false);

    public WeaponActionDefinition Fire { get { return _fire; } }
    public WeaponActionDefinition Reload { get { return _reload; } }
    public WeaponActionDefinition EmptyReload { get { return _emptyReload; } }
    public WeaponActionDefinition PerRoundStart { get { return _perRoundStart; } }
    public WeaponActionDefinition PerRoundInsert { get { return _perRoundInsert; } }
    public WeaponActionDefinition PerRoundEnd { get { return _perRoundEnd; } }
    public WeaponActionDefinition Equip { get { return _equip; } }
    public WeaponActionDefinition Holster { get { return _holster; } }
    public WeaponActionDefinition Inspect { get { return _inspect; } }

    public int DataVersion
    {
        get
        {
            return _dataVersion;
        }
    }

    private void OnValidate()
    {
        EnsureDefinitions();
    }

    private void OnEnable()
    {
        EnsureDefinitions();
    }

    public bool TryGetAction(WeaponActionType actionType, out WeaponActionDefinition action)
    {
        action = GetAction(actionType);
        return action != null && action.Enabled;
    }

    private WeaponActionDefinition GetAction(WeaponActionType actionType)
    {
        switch (actionType)
        {
            case WeaponActionType.Fire:
                return _fire;
            case WeaponActionType.Reload:
                return _reload;
            case WeaponActionType.EmptyReload:
                return _emptyReload;
            case WeaponActionType.PerRoundStart:
                return _perRoundStart;
            case WeaponActionType.PerRoundInsert:
                return _perRoundInsert;
            case WeaponActionType.PerRoundEnd:
                return _perRoundEnd;
            case WeaponActionType.Equip:
                return _equip;
            case WeaponActionType.Holster:
                return _holster;
            case WeaponActionType.Inspect:
                return _inspect;
            default:
                return null;
        }
    }

    private void EnsureDefinitions()
    {
        if (_fire == null)
        {
            _fire = new WeaponActionDefinition(
                WeaponActionType.Fire, true, 0.1f, true, true);
        }
        if (_reload == null)
        {
            _reload = new WeaponActionDefinition(
                WeaponActionType.Reload, true, 2f, false, false);
        }
        if (_emptyReload == null)
        {
            _emptyReload = new WeaponActionDefinition(
                WeaponActionType.EmptyReload, false, 2.5f, false, false);
        }
        if (_perRoundStart == null)
        {
            _perRoundStart = new WeaponActionDefinition(
                WeaponActionType.PerRoundStart, false, 0.3f, false, false);
        }
        if (_perRoundInsert == null)
        {
            _perRoundInsert = new WeaponActionDefinition(
                WeaponActionType.PerRoundInsert, false, 0.65f, false, false);
        }
        if (_perRoundEnd == null)
        {
            _perRoundEnd = new WeaponActionDefinition(
                WeaponActionType.PerRoundEnd, false, 0.3f, false, false);
        }
        if (_equip == null)
        {
            _equip = new WeaponActionDefinition(
                WeaponActionType.Equip, true, 0.45f, false, false);
        }
        if (_holster == null)
        {
            _holster = new WeaponActionDefinition(
                WeaponActionType.Holster, true, 0.3f, false, false);
        }
        if (_inspect == null)
        {
            _inspect = new WeaponActionDefinition(
                WeaponActionType.Inspect, true, 0.75f, false, false);
        }

        _fire.SetActionType(WeaponActionType.Fire);
        _reload.SetActionType(WeaponActionType.Reload);
        _emptyReload.SetActionType(WeaponActionType.EmptyReload);
        _perRoundStart.SetActionType(WeaponActionType.PerRoundStart);
        _perRoundInsert.SetActionType(WeaponActionType.PerRoundInsert);
        _perRoundEnd.SetActionType(WeaponActionType.PerRoundEnd);
        _equip.SetActionType(WeaponActionType.Equip);
        _holster.SetActionType(WeaponActionType.Holster);
        _inspect.SetActionType(WeaponActionType.Inspect);
    }
}
