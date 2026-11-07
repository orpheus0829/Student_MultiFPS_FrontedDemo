using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class CrosshairHitFeedbackUI : MonoBehaviour
{
    [Header("命中反馈引用")]
    [SerializeField, InspectorName("命中反馈画布组")] private CanvasGroup _feedbackGroup;
    [SerializeField, InspectorName("四向反馈线")] private Image[] _feedbackLines;

    [Header("普通命中")]
    [SerializeField, InspectorName("颜色")] private Color _normalColor = Color.white;
    [SerializeField, Min(0f), InspectorName("展开长度")] private float _normalLength = 18f;
    [SerializeField, Min(0.5f), InspectorName("线条粗细")] private float _normalThickness = 2.5f;

    [Header("爆头命中")]
    [SerializeField, InspectorName("颜色")] private Color _headshotColor = new Color(1f, 0.12f, 0.12f, 1f);
    [SerializeField, Min(0f), InspectorName("展开长度")] private float _headshotLength = 51f;
    [SerializeField, Min(0.5f), InspectorName("线条粗细")] private float _headshotThickness = 3.5f;

    [Header("动画")]
    [SerializeField, Min(0f), InspectorName("中心留空")] private float _centerGap = 6f;
    [SerializeField, Min(0.01f), InspectorName("伸展时间")] private float _extendDuration = 0.035f;
    [SerializeField, Min(0f), InspectorName("停留时间")] private float _holdDuration = 0.055f;
    [SerializeField, Min(0.01f), InspectorName("淡出时间")] private float _fadeDuration = 0.11f;
    [SerializeField, Min(0f), InspectorName("触发间隔刻度")] private float _triggerInterval = 0.12f;

    private SmartTimer.TimerTask _animationTask;
    private float _nextAllowedTime;
    private Color _activeColor;
    private float _activeLength;
    private float _activeThickness;
    private float _animationDuration;

    public int FeedbackLineCount
    {
        get
        {
            return _feedbackLines != null ? _feedbackLines.Length : 0;
        }
    }

    public bool IsReady
    {
        get
        {
            return _feedbackGroup && FeedbackLineCount == 4;
        }
    }

    private void Awake()
    {
        if (!_feedbackGroup)
        {
            _feedbackGroup = GetComponentInChildren<CanvasGroup>(true);
        }

        if (!_feedbackGroup || _feedbackLines == null || _feedbackLines.Length != 4)
        {
            Debug.LogError($"{name} 的命中反馈引用不完整。", this);
            enabled = false;
            return;
        }

        HideImmediately();
    }

    private void OnDisable()
    {
        if (_animationTask != null && _animationTask.IsRunning)
        {
            _animationTask.Stop();
        }

        _animationTask = null;
        _nextAllowedTime = 0f;
        HideImmediately();
    }

    public bool ShowNormalHit()
    {
        return ShowHit(false);
    }

    public bool ShowHeadshot()
    {
        return ShowHit(true);
    }

    private bool ShowHit(bool isHeadshot)
    {
        if (!enabled)
        {
            return false;
        }

        float now = Time.unscaledTime;
        if (now < _nextAllowedTime)
        {
            return false;
        }

        _nextAllowedTime = now + _triggerInterval;
        if (_animationTask != null && _animationTask.IsRunning)
        {
            _animationTask.Stop();
        }

        _activeColor = isHeadshot ? _headshotColor : _normalColor;
        _activeLength = isHeadshot ? _headshotLength : _normalLength;
        _activeThickness = isHeadshot ? _headshotThickness : _normalThickness;
        _animationDuration = _extendDuration + _holdDuration + _fadeDuration;
        _animationTask = SmartTimer.instance.SetTimer(
            this,
            SmartTimer.Mode.Realtime,
            _animationDuration,
            BeginAnimation,
            FinishAnimation,
            RefreshAnimation,
            0f);
        return true;
    }

    private void BeginAnimation()
    {
        _feedbackGroup.alpha = 1f;
        ApplyLineStyle(_activeColor, 0f, _activeThickness);
    }

    private void RefreshAnimation(float progress)
    {
        float elapsed = progress * _animationDuration;
        if (elapsed < _extendDuration)
        {
            float extendProgress = Mathf.Clamp01(elapsed / _extendDuration);
            float easedProgress = 1f - Mathf.Pow(1f - extendProgress, 3f);
            ApplyLineStyle(
                _activeColor,
                _activeLength * easedProgress,
                _activeThickness);
            return;
        }

        ApplyLineStyle(_activeColor, _activeLength, _activeThickness);
        float fadeStartTime = _extendDuration + _holdDuration;
        if (elapsed < fadeStartTime)
        {
            _feedbackGroup.alpha = 1f;
            return;
        }

        float fadeProgress = Mathf.Clamp01((elapsed - fadeStartTime) / _fadeDuration);
        _feedbackGroup.alpha = 1f - fadeProgress;
    }

    private void FinishAnimation()
    {
        _animationTask = null;
        HideImmediately();
    }

    private void ApplyLineStyle(Color color, float length, float thickness)
    {
        float visibleLength = Mathf.Max(0f, length);
        for (int index = 0; index < _feedbackLines.Length; index++)
        {
            Image line = _feedbackLines[index];
            if (!line)
            {
                continue;
            }

            line.color = color;
            RectTransform rect = line.rectTransform;
            Vector3 direction = Quaternion.Euler(0f, 0f, rect.localEulerAngles.z) *
                Vector3.right;
            rect.anchoredPosition = (Vector2)direction * _centerGap;
            rect.sizeDelta = new Vector2(visibleLength, thickness);
        }
    }

    private void HideImmediately()
    {
        if (_feedbackGroup)
        {
            _feedbackGroup.alpha = 0f;
        }
    }

    private void OnValidate()
    {
        _normalLength = Mathf.Max(0f, _normalLength);
        _normalThickness = Mathf.Max(0.5f, _normalThickness);
        _headshotLength = Mathf.Max(0f, _headshotLength);
        _headshotThickness = Mathf.Max(0.5f, _headshotThickness);
        _centerGap = Mathf.Max(0f, _centerGap);
        _extendDuration = Mathf.Max(0.01f, _extendDuration);
        _holdDuration = Mathf.Max(0f, _holdDuration);
        _fadeDuration = Mathf.Max(0.01f, _fadeDuration);
        _triggerInterval = Mathf.Max(0f, _triggerInterval);
    }
}
