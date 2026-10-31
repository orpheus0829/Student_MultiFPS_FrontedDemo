using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WordNoteUI : MonoBehaviour
{
    private enum DisplayState
    {
        Hidden,
        Entering,
        Showing,
        Exiting
    }

    private sealed class Request
    {
        public int Id;
        public string Content;
        public WordNoteStyle Style;
        public float Duration;
        public float Remaining;
    }

    private static WordNoteUI _instance;

    [Header("默认文字样式")]
    [SerializeField] private TMP_FontAsset _defaultFont;
    [SerializeField, Min(1f)] private float _defaultFontSize = 42f;
    [SerializeField] private Color _defaultFontColor = Color.white;
    [SerializeField] private bool _defaultCentered = true;
    [SerializeField] private Color _defaultOutlineColor = Color.black;
    [SerializeField, Min(0f)] private float _defaultDuration = 2f;

    [Header("上浮动画")]
    [SerializeField, Min(0f)] private float _floatDistance = 36f;
    [SerializeField, Min(0f)] private float _enterDuration = 0.2f;
    [SerializeField, Min(0f)] private float _exitDuration = 0.18f;

    [Header("屏幕适配")]
    [SerializeField] private RectTransform _safeArea;
    [SerializeField] private TextMeshProUGUI _text;
    [SerializeField] private bool _dontDestroyOnLoad = true;

    private readonly List<Request> _queue = new List<Request>();
    private Request _active;
    private int _nextId;
    private int _screenWidth;
    private int _screenHeight;
    private Rect _lastSafeArea;
    private TMP_FontAsset _prefabFont;
    private DisplayState _displayState;
    private Vector2 _basePosition;
    private Vector2 _exitStartPosition;
    private float _exitStartAlpha;
    private float _animationTime;

    public static WordNoteUI Instance
    {
        get
        {
            return _instance;
        }
    }

    public float DefaultDuration
    {
        get
        {
            return _defaultDuration;
        }
    }

    public int QueueCount
    {
        get
        {
            return _queue.Count;
        }
    }

    public WordNoteStyle DefaultStyle
    {
        get
        {
            return new WordNoteStyle(
                _defaultFont,
                _defaultFontSize,
                _defaultFontColor,
                _defaultCentered,
                _defaultOutlineColor);
        }
    }

    private void Awake()
    {
        if (_instance && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        if (_dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        if (!_safeArea || !_text)
        {
            Debug.LogError($"{name} 缺少安全区或文字引用。", this);
            enabled = false;
            return;
        }

        _prefabFont = _text.font;
        _basePosition = _text.rectTransform.anchoredPosition;
        _text.gameObject.SetActive(false);
        _displayState = DisplayState.Hidden;
        RefreshSafeArea(true);
    }

    private void Update()
    {
        RefreshSafeArea(false);

        switch (_displayState)
        {
            case DisplayState.Entering:
                UpdateEnter();
                break;
            case DisplayState.Showing:
                UpdateShowing();
                break;
            case DisplayState.Exiting:
                UpdateExit();
                break;
        }
    }

    private void OnDisable()
    {
        Clear();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public WordNoteHandle Enqueue(
        string content,
        WordNoteStyle style,
        float duration)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        _nextId++;
        Request request = new Request
        {
            Id = _nextId,
            Content = content,
            Style = style,
            Duration = Mathf.Max(0f, duration),
            Remaining = Mathf.Max(0f, duration)
        };
        _queue.Add(request);
        TryShowNext();
        return new WordNoteHandle(this, request.Id);
    }

    public bool Contains(int id)
    {
        for (int i = 0; i < _queue.Count; i++)
        {
            if (_queue[i].Id == id)
            {
                return true;
            }
        }

        return false;
    }

    public bool Close(int id)
    {
        int index = FindIndex(id);
        if (index < 0)
        {
            return false;
        }

        bool wasActive = _active != null && _active.Id == id;
        if (wasActive)
        {
            StartExit();
        }
        else
        {
            _queue.RemoveAt(index);
        }

        return true;
    }

    public void Clear()
    {
        _queue.Clear();
        _active = null;
        HideText();
    }

    private void TryShowNext()
    {
        if (_active != null || _queue.Count == 0 || !_text)
        {
            return;
        }

        _active = _queue[0];
        _active.Remaining = _active.Duration;
        ApplyStyle(_active.Style);
        _text.text = _active.Content;
        _text.gameObject.SetActive(true);
        _animationTime = 0f;
        _text.rectTransform.anchoredPosition =
            _basePosition + Vector2.down * _floatDistance;
        _text.alpha = 0f;

        if (_enterDuration <= 0f)
        {
            CompleteEnter();
            return;
        }

        _displayState = DisplayState.Entering;
    }

    private void UpdateEnter()
    {
        if (_active == null)
        {
            return;
        }

        _animationTime += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(_animationTime / _enterDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        _text.rectTransform.anchoredPosition = Vector2.LerpUnclamped(
            _basePosition + Vector2.down * _floatDistance,
            _basePosition,
            easedProgress);
        _text.alpha = easedProgress;
        if (progress >= 1f)
        {
            CompleteEnter();
        }
    }

    private void CompleteEnter()
    {
        _text.rectTransform.anchoredPosition = _basePosition;
        _text.alpha = 1f;
        _active.Remaining = _active.Duration;
        _displayState = DisplayState.Showing;
    }

    private void UpdateShowing()
    {
        if (_active == null || _active.Duration <= 0f)
        {
            return;
        }

        _active.Remaining -= Time.unscaledDeltaTime;
        if (_active.Remaining <= 0f)
        {
            StartExit();
        }
    }

    private void StartExit()
    {
        if (_active == null || _displayState == DisplayState.Exiting)
        {
            return;
        }

        _animationTime = 0f;
        _exitStartPosition = _text.rectTransform.anchoredPosition;
        _exitStartAlpha = _text.alpha;
        if (_exitDuration <= 0f)
        {
            CompleteExit();
            return;
        }

        _displayState = DisplayState.Exiting;
    }

    private void UpdateExit()
    {
        _animationTime += Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(_animationTime / _exitDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        _text.rectTransform.anchoredPosition = Vector2.LerpUnclamped(
            _exitStartPosition,
            _exitStartPosition + Vector2.up * _floatDistance,
            easedProgress);
        _text.alpha = Mathf.LerpUnclamped(
            _exitStartAlpha,
            0f,
            easedProgress);
        if (progress >= 1f)
        {
            CompleteExit();
        }
    }

    private void CompleteExit()
    {
        if (_active == null)
        {
            HideText();
            return;
        }

        int id = _active.Id;
        _active = null;
        int index = FindIndex(id);
        if (index >= 0)
        {
            _queue.RemoveAt(index);
        }

        HideText();
        TryShowNext();
    }

    private void ApplyStyle(WordNoteStyle style)
    {
        TMP_FontAsset font = style.Font
            ? style.Font
            : _defaultFont
                ? _defaultFont
                : _prefabFont;
        float fontSize = style.FontSize > 0f
            ? style.FontSize
            : _defaultFontSize;

        if (font)
        {
            _text.font = font;
        }

        _text.fontSize = fontSize;
        _text.color = style.FontColor;
        _text.alignment = style.Centered
            ? TextAlignmentOptions.Center
            : TextAlignmentOptions.Left;
        _text.outlineColor = style.OutlineColor;
        _text.outlineWidth = 0.2f;
    }

    private void HideText()
    {
        if (!_text)
        {
            return;
        }

        _text.text = string.Empty;
        _text.alpha = 0f;
        _text.rectTransform.anchoredPosition = _basePosition;
        _text.gameObject.SetActive(false);
        _displayState = DisplayState.Hidden;
    }

    private void RefreshSafeArea(bool force)
    {
        if (!_safeArea || Screen.width <= 0 || Screen.height <= 0)
        {
            return;
        }

        Rect safeArea = Screen.safeArea;
        if (!force &&
            _screenWidth == Screen.width &&
            _screenHeight == Screen.height &&
            _lastSafeArea == safeArea)
        {
            return;
        }

        _screenWidth = Screen.width;
        _screenHeight = Screen.height;
        _lastSafeArea = safeArea;
        _safeArea.anchorMin = new Vector2(
            safeArea.xMin / Screen.width,
            safeArea.yMin / Screen.height);
        _safeArea.anchorMax = new Vector2(
            safeArea.xMax / Screen.width,
            safeArea.yMax / Screen.height);
        _safeArea.offsetMin = Vector2.zero;
        _safeArea.offsetMax = Vector2.zero;
    }

    private int FindIndex(int id)
    {
        for (int i = 0; i < _queue.Count; i++)
        {
            if (_queue[i].Id == id)
            {
                return i;
            }
        }

        return -1;
    }
}
