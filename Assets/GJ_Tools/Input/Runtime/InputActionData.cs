using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[Serializable]
public class InputActionData
{
    [SerializeField] private GJInputAction _id;
    [SerializeField] private InputActionType _type;
    [SerializeField] private string _expectedControlType;
    [SerializeField] private InputActivationMode _activationMode;
    [SerializeField] private List<InputBindingData> _bindings = new List<InputBindingData>();

    public GJInputAction Id => _id;
    public InputActionType Type => _type;
    public string ExpectedControlType => _expectedControlType;
    public InputActivationMode ActivationMode => _activationMode;
    public IReadOnlyList<InputBindingData> Bindings => _bindings;

    public InputActionData(
        GJInputAction id,
        InputActionType type,
        string expectedControlType,
        InputActivationMode activationMode,
        params InputBindingData[] bindings)
    {
        _id = id;
        _type = type;
        _expectedControlType = expectedControlType;
        _activationMode = activationMode;
        _bindings = new List<InputBindingData>(bindings);
    }

    public void SetActivationMode(InputActivationMode activationMode)
    {
        _activationMode = activationMode;
    }
}
