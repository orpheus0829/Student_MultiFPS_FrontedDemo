using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-10000)]
public class InputMgr : BaseMgr<InputMgr>
{
    private readonly Dictionary<GJInputAction, InputAction> _actions = new Dictionary<GJInputAction, InputAction>();

    private InputActionCatalogSO _catalog;
    private InputActionAsset _asset;
    private InputBindingService _bindingService;

    protected override bool Ddol
    {
        get
        {
            return true;
        }
    }

    public InputActionAsset Asset
    {
        get
        {
            return _asset;
        }
    }

    public InputBindingService BindingService
    {
        get
        {
            return _bindingService;
        }
    }

    public void Configure(InputActionCatalogSO catalog)
    {
        if (_catalog == catalog && _asset)
        {
            return;
        }

        DisposeRuntime();
        _catalog = catalog;

        if (!_catalog)
        {
            _catalog = ScriptableObject.CreateInstance<InputActionCatalogSO>();
            _catalog.ResetToDefaults();
        }

        _asset = _catalog.BuildAsset();
        _bindingService = new InputBindingService(
            _asset,
            new PlayerPrefsInputBindingStorage(_catalog.BindingStorageKey));
        _actions.Clear();

        foreach (GJInputAction id in System.Enum.GetValues(typeof(GJInputAction)))
        {
            InputAction action = _asset.FindAction(id.ToString());
            if (action != null)
            {
                _actions[id] = action;
            }
        }

        _asset.Enable();
    }

    public Vector2 ReadVector2(GJInputAction id)
    {
        return GetAction(id)?.ReadValue<Vector2>() ?? Vector2.zero;
    }

    public bool IsPressed(GJInputAction id)
    {
        return GetAction(id)?.IsPressed() ?? false;
    }

    public bool WasPressedThisFrame(GJInputAction id)
    {
        return GetAction(id)?.WasPressedThisFrame() ?? false;
    }

    public bool WasReleasedThisFrame(GJInputAction id)
    {
        return GetAction(id)?.WasReleasedThisFrame() ?? false;
    }

    public InputActivationMode GetActivationMode(GJInputAction id)
    {
        return _catalog?.Find(id)?.ActivationMode ?? InputActivationMode.Press;
    }

    public InputAction GetAction(GJInputAction id)
    {
        if (!_asset)
        {
            Configure(_catalog);
        }

        return _actions.TryGetValue(id, out InputAction action) ? action : null;
    }

    private void OnDestroy()
    {
        DisposeRuntime();
    }

    private void DisposeRuntime()
    {
        _bindingService?.Dispose();
        _bindingService = null;

        if (_asset)
        {
            _asset.Disable();
            Destroy(_asset);
            _asset = null;
        }

        _actions.Clear();
    }
}
