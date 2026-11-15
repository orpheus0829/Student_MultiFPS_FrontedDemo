using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class PlayerInputCtrl : MonoBehaviour
{
    [Header("输入数据")]
    [SerializeField] private InputActionCatalogSO _inputCatalog;

    private readonly HashSet<GJInputAction> _activeStates = new HashSet<GJInputAction>();
    private readonly HashSet<object> _gameplayInputBlockers = new HashSet<object>();

    public event Action<PlayerInputCommand> CommandIssued;

    private void OnEnable()
    {
        InputMgr.instance.Configure(_inputCatalog);
    }

    private void Update()
    {
        if (_gameplayInputBlockers.Count > 0)
        {
            EmitButton(GJInputAction.Scoreboard);
            EmitButton(GJInputAction.OpenShop);
            EmitButton(GJInputAction.Settings);
            return;
        }

        EmitContinuous(GJInputAction.Move, InputMgr.instance.ReadVector2(GJInputAction.Move));
        EmitContinuous(GJInputAction.Look, InputMgr.instance.ReadVector2(GJInputAction.Look));
        EmitContinuous(
            GJInputAction.CycleWeapon,
            InputMgr.instance.ReadVector2(GJInputAction.CycleWeapon));

        EmitButton(GJInputAction.JumpVault);
        EmitButton(GJInputAction.Sprint);
        EmitButton(GJInputAction.Slide);
        EmitButton(GJInputAction.Fire);
        EmitButton(GJInputAction.Aim);
        EmitButton(GJInputAction.Reload);
        EmitButton(GJInputAction.Inspect);
        EmitButton(GJInputAction.Scoreboard);
        EmitButton(GJInputAction.ToggleView);
        EmitButton(GJInputAction.Punch);
        EmitButton(GJInputAction.Interact);
        EmitButton(GJInputAction.OpenShop);
        EmitButton(GJInputAction.DropWeapon);
        EmitButton(GJInputAction.PickupWeapon);
        EmitButton(GJInputAction.Settings);
    }

    private void OnDisable()
    {
        foreach (GJInputAction action in _activeStates)
        {
            CommandIssued?.Invoke(new PlayerInputCommand(action, PlayerInputPhase.Canceled));
        }

        _activeStates.Clear();
        _gameplayInputBlockers.Clear();
    }

    public void SetGameplayInputBlocked(object owner, bool blocked)
    {
        if (owner == null)
        {
            return;
        }

        bool changed = blocked
            ? _gameplayInputBlockers.Add(owner)
            : _gameplayInputBlockers.Remove(owner);
        if (!changed || !blocked)
        {
            return;
        }

        CommandIssued?.Invoke(new PlayerInputCommand(
            GJInputAction.Fire,
            PlayerInputPhase.Canceled));
        CommandIssued?.Invoke(new PlayerInputCommand(
            GJInputAction.Aim,
            PlayerInputPhase.Canceled));
        CommandIssued?.Invoke(new PlayerInputCommand(
            GJInputAction.Sprint,
            PlayerInputPhase.Canceled));
    }

    private void EmitContinuous(GJInputAction action, Vector2 value)
    {
        CommandIssued?.Invoke(new PlayerInputCommand(action, PlayerInputPhase.Performed, value));
    }

    private void EmitButton(GJInputAction action)
    {
        InputActivationMode activationMode = InputMgr.instance.GetActivationMode(action);
        if (activationMode == InputActivationMode.Toggle)
        {
            EmitToggle(action);
            return;
        }

        if (InputMgr.instance.WasPressedThisFrame(action))
        {
            if (activationMode == InputActivationMode.Hold)
            {
                _activeStates.Add(action);
            }
            CommandIssued?.Invoke(new PlayerInputCommand(action, PlayerInputPhase.Started));
        }

        if (activationMode == InputActivationMode.Hold && InputMgr.instance.WasReleasedThisFrame(action))
        {
            _activeStates.Remove(action);
            CommandIssued?.Invoke(new PlayerInputCommand(action, PlayerInputPhase.Canceled));
        }
    }

    private void EmitToggle(GJInputAction action)
    {
        if (!InputMgr.instance.WasPressedThisFrame(action))
        {
            return;
        }

        if (_activeStates.Remove(action))
        {
            CommandIssued?.Invoke(new PlayerInputCommand(action, PlayerInputPhase.Canceled));
        }
        else
        {
            _activeStates.Add(action);
            CommandIssued?.Invoke(new PlayerInputCommand(action, PlayerInputPhase.Started));
        }
    }
}
