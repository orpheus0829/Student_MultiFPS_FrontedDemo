using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerCrosshairUI : MonoBehaviour
{
    [Header("基础准心")]
    [SerializeField, InspectorName("基础准心画布组")] private CanvasGroup _baseCrosshairGroup;
    [SerializeField, InspectorName("上准心线")] private RectTransform _topLine;
    [SerializeField, InspectorName("下准心线")] private RectTransform _bottomLine;
    [SerializeField, InspectorName("左准心线")] private RectTransform _leftLine;
    [SerializeField, InspectorName("右准心线")] private RectTransform _rightLine;

    [Header("散布跟随")]
    [SerializeField, Min(0f), InspectorName("最小中心空隙")] private float _minimumGap = 4f;
    [SerializeField, Min(0f), InspectorName("准心跟随速度")] private float _spreadFollowSpeed = 20f;

    private PlayerAimCtrl _aimCtrl;
    private WeaponCombatCtrl _combatCtrl;
    private FirstPersonCameraCtrl _cameraCtrl;
    private Camera _viewCamera;
    private Canvas _canvas;
    private float _currentGap;
    private bool _isSubscribed;

    public bool IsReady
    {
        get
        {
            return _baseCrosshairGroup;
        }
    }

    private void Awake()
    {
        if (!_baseCrosshairGroup)
        {
            Debug.LogError($"{name} 未配置基础准心画布组。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_aimCtrl)
        {
            Subscribe();
        }
    }

    private void Start()
    {
        if (!_aimCtrl)
        {
            _aimCtrl = FindFirstObjectByType<PlayerAimCtrl>();
        }

        if (!_aimCtrl)
        {
            Debug.LogWarning($"{name} 没有找到本地玩家瞄准组件，基础准心将保持显示。", this);
            SetAimProgress(0f);
            return;
        }

        _combatCtrl = _aimCtrl.GetComponent<WeaponCombatCtrl>();
        _cameraCtrl = _aimCtrl.GetComponentInChildren<FirstPersonCameraCtrl>(true);
        _viewCamera = _cameraCtrl ? _cameraCtrl.GetComponent<Camera>() : Camera.main;
        _canvas = GetComponentInParent<Canvas>();
        _currentGap = _minimumGap;

        Subscribe();
        SetAimProgress(_aimCtrl.AimProgress);
    }

    private void LateUpdate()
    {
        SetAimProgress(_aimCtrl ? _aimCtrl.AimProgress : 0f);
        if (!_combatCtrl || !_viewCamera || !_topLine || !_bottomLine ||
            !_leftLine || !_rightLine)
        {
            return;
        }

        float halfFov = _viewCamera.fieldOfView * Mathf.Deg2Rad * 0.5f;
        float pixelsPerTangent = _viewCamera.pixelHeight * 0.5f / Mathf.Tan(halfFov);
        float canvasScale = _canvas ? _canvas.scaleFactor : 1f;
        float spreadRadius = Mathf.Tan(_combatCtrl.CurrentSpreadDegrees * Mathf.Deg2Rad) *
            pixelsPerTangent / Mathf.Max(0.01f, canvasScale);
        float targetGap = Mathf.Max(_minimumGap, spreadRadius);
        float weight = 1f - Mathf.Exp(-_spreadFollowSpeed * Time.deltaTime);
        _currentGap = Mathf.Lerp(_currentGap, targetGap, weight);

        _topLine.anchoredPosition = new Vector2(0f, _currentGap + _topLine.rect.height * 0.5f);
        _bottomLine.anchoredPosition = new Vector2(0f, -_currentGap - _bottomLine.rect.height * 0.5f);
        _leftLine.anchoredPosition = new Vector2(-_currentGap - _leftLine.rect.width * 0.5f, 0f);
        _rightLine.anchoredPosition = new Vector2(_currentGap + _rightLine.rect.width * 0.5f, 0f);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void Bind(PlayerAimCtrl aimCtrl)
    {
        Unsubscribe();
        _aimCtrl = aimCtrl;
        _combatCtrl = _aimCtrl ? _aimCtrl.GetComponent<WeaponCombatCtrl>() : null;
        _cameraCtrl = _aimCtrl ? _aimCtrl.GetComponentInChildren<FirstPersonCameraCtrl>(true) : null;
        _viewCamera = _cameraCtrl ? _cameraCtrl.GetComponent<Camera>() : Camera.main;
        _canvas = GetComponentInParent<Canvas>();

        if (isActiveAndEnabled)
        {
            Subscribe();
        }

        SetAimProgress(_aimCtrl ? _aimCtrl.AimProgress : 0f);
    }

    public void SetAimProgress(float progress)
    {
        if (_baseCrosshairGroup)
        {
            _baseCrosshairGroup.alpha = _cameraCtrl && _cameraCtrl.IsThirdPerson
                ? 1f : 1f - Mathf.Clamp01(progress);
        }
    }

    private void Subscribe()
    {
        if (_isSubscribed || !_aimCtrl)
        {
            return;
        }

        _aimCtrl.AimProgressChanged += SetAimProgress;
        _isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_isSubscribed || !_aimCtrl)
        {
            return;
        }

        _aimCtrl.AimProgressChanged -= SetAimProgress;
        _isSubscribed = false;
    }
}
