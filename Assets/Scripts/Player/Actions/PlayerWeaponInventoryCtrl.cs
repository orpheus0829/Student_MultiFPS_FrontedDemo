using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public sealed class PlayerWeaponInventoryCtrl : MonoBehaviour
{
    [Header("枪械栏")]
    [SerializeField, Min(1), InspectorName("枪械栏容量")] private int _slotCount = 7;
    [SerializeField, ResourceId(typeof(WeaponDefinitionSO)), InspectorName("持有枪械数据 ID")]
    private List<string> _weaponResourceIds = new List<string>
    {
        "weapon.ak.data",
        "weapon.as-val.data",
        "weapon.m1911.data",
        "weapon.kar98k.data",
        "weapon.mps5.data",
        "weapon.kxg-12.data",
        "weapon.pdw90.data"
    };

    [Header("动作执行")]
    [SerializeField, InspectorName("枪械动作")] private WeaponActionCtrl _weaponActionCtrl;
    [SerializeField, InspectorName("枪械表现")] private PlayerWeaponViewCtrl _weaponViewCtrl;

    private ResourceMgr _resourceMgr;
    private Task<bool> _prepareTask;
    private string _preparedWeaponResourceId;
    private string _preparedActionSetResourceId;
    private int _currentIndex;
    private int _pendingIndex = -1;
    private int _switchVersion;
    private bool _isSwitching;

    public event Action<int, string> ActiveSlotChanged;

    public int SlotCount
    {
        get
        {
            return _slotCount;
        }
    }

    public IReadOnlyList<string> WeaponResourceIds
    {
        get
        {
            return _weaponResourceIds;
        }
    }

    public int CurrentIndex
    {
        get
        {
            return _currentIndex;
        }
    }

    public int ConfiguredWeaponCount
    {
        get
        {
            return GetUsableWeaponCount();
        }
    }

    public bool IsSwitching
    {
        get
        {
            return _isSwitching;
        }
    }

    private void Awake()
    {
        if (!_weaponActionCtrl)
        {
            TryGetComponent(out _weaponActionCtrl);
        }
        if (!_weaponViewCtrl)
        {
            TryGetComponent(out _weaponViewCtrl);
        }

        TrimSlots();
        _currentIndex = FindInitialIndex();
        if (_weaponActionCtrl && GetUsableWeaponCount() > 0)
        {
            return;
        }

        Debug.LogError($"{name} 的枪械栏没有可用枪械或缺少枪械动作组件。", this);
        enabled = false;
    }

    private void OnEnable()
    {
        if (!_weaponActionCtrl)
        {
            return;
        }

        _weaponActionCtrl.ActionCompleted += HandleActionCompleted;
        _weaponActionCtrl.ActionCanceled += HandleActionCanceled;
    }

    private void OnDisable()
    {
        if (_weaponActionCtrl)
        {
            _weaponActionCtrl.ActionCompleted -= HandleActionCompleted;
            _weaponActionCtrl.ActionCanceled -= HandleActionCanceled;
        }

        _switchVersion++;
        ReleasePreparedData();
        FinishSwitch(false);
    }

    public bool TryCycle(int direction)
    {
        if (direction == 0 || _isSwitching || !_weaponActionCtrl ||
            GetUsableWeaponCount() <= 1)
        {
            return false;
        }

        int targetIndex = FindNextIndex(direction > 0 ? 1 : -1);
        if (targetIndex < 0 || targetIndex == _currentIndex)
        {
            return false;
        }

        _weaponActionCtrl.CancelCurrentAction(
            WeaponActionCancelReason.WeaponChanged,
            true);
        _pendingIndex = targetIndex;
        _isSwitching = true;
        int switchVersion = ++_switchVersion;
        _prepareTask = PrepareSwitchAsync(_weaponResourceIds[targetIndex], switchVersion);

        if (!_weaponActionCtrl.TryStartAction(WeaponActionType.Holster))
        {
            _ = ChangeWeaponAsync();
        }

        return true;
    }

    private async Task ChangeWeaponAsync()
    {
        int switchVersion = _switchVersion;
        int targetIndex = _pendingIndex;
        if (!IsIndexUsable(targetIndex))
        {
            FinishSwitch(false);
            return;
        }

        string resourceId = _weaponResourceIds[targetIndex];
        bool prepared = _prepareTask != null && await _prepareTask;
        if (!this || !isActiveAndEnabled || switchVersion != _switchVersion)
        {
            return;
        }
        if (!prepared)
        {
            _weaponActionCtrl.TryStartAction(WeaponActionType.Equip);
            ReleasePreparedData();
            FinishSwitch(false);
            return;
        }

        bool loaded = await _weaponActionCtrl.LoadWeaponAsync(resourceId);
        ReleasePreparedData();
        if (!this || !isActiveAndEnabled || switchVersion != _switchVersion)
        {
            return;
        }
        if (!loaded)
        {
            _weaponActionCtrl.TryStartAction(WeaponActionType.Equip);
            FinishSwitch(false);
            return;
        }

        _currentIndex = targetIndex;
        _pendingIndex = -1;
        ActiveSlotChanged?.Invoke(_currentIndex, resourceId);
    }

    private void HandleActionCompleted(WeaponActionType actionType)
    {
        if (!_isSwitching)
        {
            return;
        }
        if (actionType == WeaponActionType.Holster)
        {
            _ = ChangeWeaponAsync();
        }
        else if (actionType == WeaponActionType.Equip)
        {
            FinishSwitch(true);
        }
    }

    private void HandleActionCanceled(
        WeaponActionType actionType,
        WeaponActionCancelReason reason)
    {
        if (_isSwitching && actionType == WeaponActionType.Equip)
        {
            FinishSwitch(true);
        }
    }

    private async Task<bool> PrepareSwitchAsync(string weaponResourceId, int switchVersion)
    {
        _resourceMgr = ResourceMgr.instance;
        try
        {
            WeaponDefinitionSO weaponData =
                await _resourceMgr.LoadAsync<WeaponDefinitionSO>(weaponResourceId);
            if (!weaponData || !IsPreparationCurrent(switchVersion))
            {
                if (weaponData)
                {
                    _resourceMgr.Release<WeaponDefinitionSO>(weaponResourceId);
                }
                return false;
            }

            _preparedWeaponResourceId = weaponResourceId;
            string actionSetResourceId = weaponData.ActionSetResourceId;
            WeaponActionSetSO actionSet =
                await _resourceMgr.LoadAsync<WeaponActionSetSO>(actionSetResourceId);
            if (!actionSet || !IsPreparationCurrent(switchVersion))
            {
                if (actionSet)
                {
                    _resourceMgr.Release<WeaponActionSetSO>(actionSetResourceId);
                }
                return false;
            }

            _preparedActionSetResourceId = actionSetResourceId;
            return !_weaponViewCtrl ||
                await _weaponViewCtrl.PrepareWeaponViewsAsync(weaponData);
        }
        catch (Exception exception)
        {
            if (IsPreparationCurrent(switchVersion))
            {
                Debug.LogWarning($"预载枪械 {weaponResourceId} 失败：{exception.Message}", this);
            }
            return false;
        }
    }

    private bool IsPreparationCurrent(int switchVersion)
    {
        return this && isActiveAndEnabled && _isSwitching &&
            switchVersion == _switchVersion;
    }

    private void ReleasePreparedData()
    {
        if (_resourceMgr)
        {
            if (!string.IsNullOrWhiteSpace(_preparedActionSetResourceId))
            {
                _resourceMgr.Release<WeaponActionSetSO>(_preparedActionSetResourceId);
            }
            if (!string.IsNullOrWhiteSpace(_preparedWeaponResourceId))
            {
                _resourceMgr.Release<WeaponDefinitionSO>(_preparedWeaponResourceId);
            }
        }

        _preparedActionSetResourceId = string.Empty;
        _preparedWeaponResourceId = string.Empty;
        _prepareTask = null;
    }

    private int FindInitialIndex()
    {
        string startingId = _weaponActionCtrl
            ? _weaponActionCtrl.StartingWeaponResourceId
            : string.Empty;
        for (int i = 0; i < _weaponResourceIds.Count; i++)
        {
            if (string.Equals(
                    _weaponResourceIds[i],
                    startingId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return FindNextIndexFrom(-1, 1);
    }

    private int FindNextIndex(int direction)
    {
        return FindNextIndexFrom(_currentIndex, direction);
    }

    private int FindNextIndexFrom(int startIndex, int direction)
    {
        int count = Mathf.Min(_slotCount, _weaponResourceIds.Count);
        if (count <= 0)
        {
            return -1;
        }

        int index = startIndex;
        for (int i = 0; i < count; i++)
        {
            index = (index + direction + count) % count;
            if (IsIndexUsable(index))
            {
                return index;
            }
        }

        return -1;
    }

    private int GetUsableWeaponCount()
    {
        int usableCount = 0;
        int count = Mathf.Min(_slotCount, _weaponResourceIds.Count);
        for (int i = 0; i < count; i++)
        {
            if (IsIndexUsable(i))
            {
                usableCount++;
            }
        }

        return usableCount;
    }

    private bool IsIndexUsable(int index)
    {
        return index >= 0 &&
            index < _weaponResourceIds.Count &&
            index < _slotCount &&
            !string.IsNullOrWhiteSpace(_weaponResourceIds[index]);
    }

    private void FinishSwitch(bool keepCurrentIndex)
    {
        if (!keepCurrentIndex && _pendingIndex >= 0)
        {
            _pendingIndex = -1;
        }

        _isSwitching = false;
    }

    private void TrimSlots()
    {
        _slotCount = Mathf.Max(1, _slotCount);
        if (_weaponResourceIds == null)
        {
            _weaponResourceIds = new List<string>();
        }

        while (_weaponResourceIds.Count > _slotCount)
        {
            _weaponResourceIds.RemoveAt(_weaponResourceIds.Count - 1);
        }
        while (_weaponResourceIds.Count < _slotCount)
        {
            _weaponResourceIds.Add(string.Empty);
        }
    }

    private void OnValidate()
    {
        TrimSlots();
    }
}
