using UnityEngine;

public enum BombShopStageView
{
    All,
    Weapon,
    Defense
}

[DisallowMultipleComponent]
public sealed class BombShopStageRuntime : MonoBehaviour
{
    private const float HoverDelay = 0.1f;
    private const float CameraSharpness = 12f;

    private Transform _cameraTransform;
    private Transform _allView;
    private Transform _weaponView;
    private Transform _defenseView;
    private BombShopStageView _currentView;
    private BombShopStageView _pendingView;
    private float _pendingSince;

    private void Update()
    {
        if (!_cameraTransform)
        {
            return;
        }
        if (_pendingView != _currentView &&
            Time.unscaledTime - _pendingSince >= HoverDelay)
        {
            _currentView = _pendingView;
        }

        Transform target = GetTarget(_currentView);
        float t = 1f - Mathf.Exp(-CameraSharpness * Time.unscaledDeltaTime);
        _cameraTransform.position = Vector3.Lerp(
            _cameraTransform.position,
            target.position,
            t);
        _cameraTransform.rotation = Quaternion.Slerp(
            _cameraTransform.rotation,
            target.rotation,
            t);
    }

    public void Initialize(
        Transform cameraTransform,
        Transform allView,
        Transform weaponView,
        Transform defenseView)
    {
        _cameraTransform = cameraTransform;
        _allView = allView;
        _weaponView = weaponView;
        _defenseView = defenseView;
        _currentView = BombShopStageView.All;
        _pendingView = BombShopStageView.All;
        _cameraTransform.SetPositionAndRotation(
            _allView.position,
            _allView.rotation);
    }

    public void RequestView(BombShopStageView view)
    {
        if (_pendingView == view)
        {
            return;
        }
        _pendingView = view;
        _pendingSince = Time.unscaledTime;
    }

    private Transform GetTarget(BombShopStageView view)
    {
        switch (view)
        {
            case BombShopStageView.Weapon:
                return _weaponView;
            case BombShopStageView.Defense:
                return _defenseView;
            default:
                return _allView;
        }
    }
}
