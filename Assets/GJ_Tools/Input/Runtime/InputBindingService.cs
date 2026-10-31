using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class InputBindingService : IDisposable
{
    private readonly InputActionAsset _asset;
    private readonly IInputBindingStorage _storage;
    private InputActionRebindingExtensions.RebindingOperation _operation;
    private InputAction _rebindAction;
    private GJInputAction _rebindId;
    private int _rebindBindingIndex;
    private bool _wasActionEnabled;
    private string _previousOverridePath;
    private Action<InputRebindResult> _completed;
    private Action _canceled;

    public InputActionAsset Asset
    {
        get
        {
            return _asset;
        }
    }

    public bool IsRebinding
    {
        get
        {
            return _operation != null;
        }
    }

    public event Action Changed;

    public InputBindingService(InputActionAsset asset, IInputBindingStorage storage = null)
    {
        _asset = asset ?? throw new ArgumentNullException(nameof(asset));
        _storage = storage;
        if (_storage != null)
        {
            Load();
        }
    }

    public string GetDisplayString(GJInputAction id, int bindingIndex)
    {
        InputAction action = GetAction(id);
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            return string.Empty;
        }

        return action.GetBindingDisplayString(bindingIndex);
    }

    public IReadOnlyList<InputBindingInfo> GetBindings(GJInputAction id)
    {
        List<InputBindingInfo> result = new List<InputBindingInfo>();
        InputAction action = GetAction(id);
        if (action == null)
        {
            return result;
        }

        for (int index = 0; index < action.bindings.Count; index++)
        {
            InputBinding binding = action.bindings[index];
            result.Add(new InputBindingInfo(
                id,
                index,
                binding.name,
                binding.effectivePath,
                action.GetBindingDisplayString(index),
                binding.isComposite,
                binding.isPartOfComposite));
        }

        return result;
    }

    public bool TryFindConflict(GJInputAction id, int bindingIndex, string path, out InputBindingConflict conflict)
    {
        conflict = default;
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        foreach (InputActionMap map in _asset.actionMaps)
        {
            foreach (InputAction action in map.actions)
            {
                if (!Enum.TryParse(action.name, out GJInputAction otherId))
                {
                    continue;
                }

                for (int index = 0; index < action.bindings.Count; index++)
                {
                    InputBinding binding = action.bindings[index];
                    if (binding.isComposite || otherId == id && index == bindingIndex)
                    {
                        continue;
                    }

                    if (!string.Equals(binding.effectivePath, path, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    conflict = new InputBindingConflict(otherId, index, action.GetBindingDisplayString(index));
                    return true;
                }
            }
        }

        return false;
    }

    public InputActionRebindingExtensions.RebindingOperation StartRebind(
        GJInputAction id,
        int bindingIndex,
        Action<InputRebindResult> completed = null,
        Action canceled = null)
    {
        CancelRebind();

        InputAction action = GetAction(id);
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count || action.bindings[bindingIndex].isComposite)
        {
            return null;
        }

        _rebindAction = action;
        _rebindId = id;
        _rebindBindingIndex = bindingIndex;
        _wasActionEnabled = action.enabled;
        _previousOverridePath = action.bindings[bindingIndex].overridePath;
        _completed = completed;
        _canceled = canceled;
        action.Disable();
        InputActionRebindingExtensions.RebindingOperation operation = action.PerformInteractiveRebinding(bindingIndex)
            .WithControlsExcluding("<Mouse>/position")
            .OnMatchWaitForAnother(0.08f)
            .OnComplete(CompleteRebind)
            .OnCancel(CancelRebindOperation);

        if (id != GJInputAction.Settings)
        {
            operation.WithCancelingThrough("<Keyboard>/escape");
        }

        _operation = operation;
        _operation.Start();
        return _operation;
    }

    public bool ResetBinding(GJInputAction id, int bindingIndex)
    {
        InputAction action = GetAction(id);
        if (action == null || bindingIndex < 0 || bindingIndex >= action.bindings.Count)
        {
            return false;
        }

        action.RemoveBindingOverride(bindingIndex);
        Save();
        Changed?.Invoke();
        return true;
    }

    public bool ResetAction(GJInputAction id)
    {
        InputAction action = GetAction(id);
        if (action == null)
        {
            return false;
        }

        action.RemoveAllBindingOverrides();
        Save();
        Changed?.Invoke();
        return true;
    }

    public void ResetAll()
    {
        CancelRebind();
        _asset.RemoveAllBindingOverrides();
        _storage?.Delete();
        Changed?.Invoke();
    }

    public void Save()
    {
        _storage?.Save(_asset.SaveBindingOverridesAsJson());
    }

    public void Load()
    {
        if (_storage == null || !_storage.TryLoad(out string json))
        {
            return;
        }

        _asset.LoadBindingOverridesFromJson(json);
    }

    public void CancelRebind()
    {
        _operation?.Cancel();
    }

    public void Dispose()
    {
        CancelRebind();
    }

    private InputAction GetAction(GJInputAction id)
    {
        return _asset.FindAction(id.ToString());
    }

    private void RestoreOverride(InputAction action, int bindingIndex, string previousOverridePath)
    {
        if (string.IsNullOrEmpty(previousOverridePath))
        {
            action.RemoveBindingOverride(bindingIndex);
            return;
        }

        action.ApplyBindingOverride(bindingIndex, previousOverridePath);
    }

    private void CompleteRebind(
        InputActionRebindingExtensions.RebindingOperation operation)
    {
        string path = _rebindAction.bindings[_rebindBindingIndex].effectivePath;
        operation.Dispose();
        _operation = null;

        if (TryFindConflict(
                _rebindId,
                _rebindBindingIndex,
                path,
                out InputBindingConflict conflict))
        {
            RestoreOverride(
                _rebindAction,
                _rebindBindingIndex,
                _previousOverridePath);
            EnableRebindAction();
            _completed?.Invoke(InputRebindResult.Rejected(path, conflict));
            ClearRebindContext();
            return;
        }

        EnableRebindAction();
        Save();
        Changed?.Invoke();
        _completed?.Invoke(InputRebindResult.Success(path));
        ClearRebindContext();
    }

    private void CancelRebindOperation(
        InputActionRebindingExtensions.RebindingOperation operation)
    {
        operation.Dispose();
        _operation = null;
        EnableRebindAction();
        _canceled?.Invoke();
        ClearRebindContext();
    }

    private void EnableRebindAction()
    {
        if (_wasActionEnabled)
        {
            _rebindAction.Enable();
        }
    }

    private void ClearRebindContext()
    {
        _rebindAction = null;
        _previousOverridePath = null;
        _completed = null;
        _canceled = null;
    }
}
