using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[CreateAssetMenu(fileName = "InputActionCatalog", menuName = "GJ Tools/输入/输入动作数据")]
public class InputActionCatalogSO : ScriptableObject
{
    [Header("运行时改键存档")]
    [SerializeField] private string _bindingStorageKey = "GunDemo.Gameplay";

    [Header("输入动作")]
    [SerializeField] private List<InputActionData> _actions = new List<InputActionData>();

    public string BindingStorageKey
    {
        get
        {
            return string.IsNullOrWhiteSpace(_bindingStorageKey)
                ? name
                : _bindingStorageKey.Trim();
        }
    }

    public IReadOnlyList<InputActionData> Actions
    {
        get
        {
            return _actions;
        }
    }

    public InputActionAsset BuildAsset()
    {
        EnsureDefaults();

        IReadOnlyList<string> errors = GetValidationErrors();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", errors));
        }

        InputActionAsset asset = ScriptableObject.CreateInstance<InputActionAsset>();
        InputActionMap map = new InputActionMap("Gameplay");
        asset.AddActionMap(map);

        foreach (InputActionData actionData in _actions)
        {
            InputAction action = map.AddAction(actionData.Id.ToString(), actionData.Type, expectedControlLayout: actionData.ExpectedControlType);
            foreach (InputBindingData bindingData in actionData.Bindings)
            {
                InputBinding binding = new InputBinding
                {
                    name = bindingData.Name,
                    path = bindingData.Path,
                    interactions = bindingData.Interactions,
                    processors = bindingData.Processors,
                    groups = bindingData.Groups,
                    isComposite = bindingData.IsComposite,
                    isPartOfComposite = bindingData.IsPartOfComposite
                };
                action.AddBinding(binding);
            }
        }

        return asset;
    }

    public void ResetToDefaults()
    {
        _actions = CreateDefaults();
    }

    public bool SetBindingPath(GJInputAction id, int bindingIndex, string path)
    {
        InputActionData action = Find(id);
        if (action == null || bindingIndex < 0 || bindingIndex >= action.Bindings.Count)
        {
            return false;
        }

        action.Bindings[bindingIndex].SetPath(path);
        return true;
    }

    public bool SetActivationMode(GJInputAction id, InputActivationMode activationMode)
    {
        InputActionData action = Find(id);
        if (action == null || action.Type != InputActionType.Button)
        {
            return false;
        }

        action.SetActivationMode(activationMode);
        return true;
    }

    public bool ResetBindingToDefault(GJInputAction id, int bindingIndex)
    {
        InputActionData current = Find(id);
        InputActionData defaults = Find(CreateDefaults(), id);
        if (current == null || defaults == null || bindingIndex < 0 || bindingIndex >= current.Bindings.Count || bindingIndex >= defaults.Bindings.Count)
        {
            return false;
        }

        current.Bindings[bindingIndex].SetPath(defaults.Bindings[bindingIndex].Path);
        return true;
    }

    public IReadOnlyList<string> GetValidationErrors()
    {
        List<string> errors = new List<string>();
        HashSet<GJInputAction> actionIds = new HashSet<GJInputAction>();

        if (string.IsNullOrWhiteSpace(_bindingStorageKey))
        {
            errors.Add("运行时改键存档标识不能为空。");
        }

        foreach (InputActionData action in _actions)
        {
            if (!actionIds.Add(action.Id))
            {
                errors.Add($"动作 {action.Id} 重复。");
            }

            bool hasComposite = false;
            for (int index = 0; index < action.Bindings.Count; index++)
            {
                InputBindingData binding = action.Bindings[index];
                if (string.IsNullOrWhiteSpace(binding.Path))
                {
                    errors.Add($"动作 {action.Id} 的第 {index + 1} 个绑定为空。");
                }

                if (binding.IsComposite)
                {
                    hasComposite = true;
                    continue;
                }

                if (binding.IsPartOfComposite && !hasComposite)
                {
                    errors.Add($"动作 {action.Id} 的组合按键顺序无效。");
                }

                if (!binding.IsPartOfComposite)
                {
                    hasComposite = false;
                }
            }
        }

        foreach (GJInputAction id in Enum.GetValues(typeof(GJInputAction)))
        {
            if (!actionIds.Contains(id))
            {
                errors.Add($"缺少动作 {id}。");
            }
        }

        return errors;
    }

    public InputActionData Find(GJInputAction id)
    {
        return Find(_actions, id);
    }

    private void EnsureDefaults()
    {
        if (_actions == null || _actions.Count == 0)
        {
            ResetToDefaults();
        }
    }

    private static List<InputActionData> CreateDefaults()
    {
        return new List<InputActionData>
        {
            new InputActionData(
                GJInputAction.Move,
                InputActionType.Value,
                "Vector2",
                InputActivationMode.Press,
                new InputBindingData("WASD", "2DVector", true),
                new InputBindingData("up", "<Keyboard>/w", false, true),
                new InputBindingData("down", "<Keyboard>/s", false, true),
                new InputBindingData("left", "<Keyboard>/a", false, true),
                new InputBindingData("right", "<Keyboard>/d", false, true)),
            new InputActionData(GJInputAction.Look, InputActionType.PassThrough, "Vector2", InputActivationMode.Press, new InputBindingData("鼠标", "<Mouse>/delta")),
            new InputActionData(GJInputAction.JumpVault, InputActionType.Button, "Button", InputActivationMode.Press, new InputBindingData("键盘", "<Keyboard>/space")),
            new InputActionData(GJInputAction.Sprint, InputActionType.Button, "Button", InputActivationMode.Hold, new InputBindingData("键盘", "<Keyboard>/leftShift")),
            new InputActionData(GJInputAction.Fire, InputActionType.Button, "Button", InputActivationMode.Hold, new InputBindingData("鼠标", "<Mouse>/leftButton")),
            new InputActionData(GJInputAction.Aim, InputActionType.Button, "Button", InputActivationMode.Hold, new InputBindingData("鼠标", "<Mouse>/rightButton")),
            new InputActionData(GJInputAction.Reload, InputActionType.Button, "Button", InputActivationMode.Press, new InputBindingData("键盘", "<Keyboard>/r")),
            new InputActionData(GJInputAction.Inspect, InputActionType.Button, "Button", InputActivationMode.Press, new InputBindingData("键盘", "<Keyboard>/g")),
            new InputActionData(GJInputAction.Scoreboard, InputActionType.Button, "Button", InputActivationMode.Hold, new InputBindingData("键盘", "<Keyboard>/tab")),
            new InputActionData(GJInputAction.Settings, InputActionType.Button, "Button", InputActivationMode.Press, new InputBindingData("键盘", "<Keyboard>/escape"))
        };
    }

    private static InputActionData Find(
        List<InputActionData> actions,
        GJInputAction id)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            if (actions[i].Id == id)
            {
                return actions[i];
            }
        }

        return null;
    }
}
