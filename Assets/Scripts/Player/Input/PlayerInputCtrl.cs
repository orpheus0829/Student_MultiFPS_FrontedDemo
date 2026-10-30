using System;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-1000)]
public class PlayerInputCtrl : MonoBehaviour
{
    [Header("输入数据")]
    [SerializeField] private InputActionCatalogSO _inputCatalog;

    private readonly HashSet<GJInputAction> _activeStates = new HashSet<GJInputAction>();

    public event Action<PlayerInputCommand> CommandIssued;

    private void OnEnable()
    {
        InputMgr.instance.Configure(_inputCatalog);
    }

    private void Update()
    {
        EmitContinuous(GJInputAction.Move, InputMgr.instance.ReadVector2(GJInputAction.Move));
        EmitContinuous(GJInputAction.Look, InputMgr.instance.ReadVector2(GJInputAction.Look));

        EmitButton(GJInputAction.JumpVault);
        EmitButton(GJInputAction.Sprint);
        EmitButton(GJInputAction.Fire);
        EmitButton(GJInputAction.Aim);
        EmitButton(GJInputAction.Reload);
        EmitButton(GJInputAction.Inspect);
        EmitButton(GJInputAction.Scoreboard);
        EmitButton(GJInputAction.Settings);
    }

    private void OnDisable()
    {
        foreach (GJInputAction action in _activeStates)
        {
            CommandIssued?.Invoke(new PlayerInputCommand(action, PlayerInputPhase.Canceled));
        }

        _activeStates.Clear();
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
