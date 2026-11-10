using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatVoiceSubtitleUI : MonoBehaviour
{
    private static CombatVoiceSubtitleUI _instance;

    [Header("字幕样式")]
    [SerializeField, InspectorName("字幕字号"), Min(1f)] private float _fontSize = 34f;
    [SerializeField, InspectorName("字幕颜色")] private Color _textColor = Color.white;
    [SerializeField, InspectorName("发言者颜色")] private Color _speakerColor = new Color(1f, 0.82f, 0.38f, 1f);
    [SerializeField, InspectorName("最短显示时间"), Min(0.1f)] private float _minimumDuration = 1.8f;
    [SerializeField, InspectorName("字幕文字")] private TextMeshProUGUI _subtitleText;
    [SerializeField, InspectorName("淡入时间"), Min(0f)] private float _fadeInDuration = 0.08f;
    [SerializeField, InspectorName("淡出时间"), Min(0f)] private float _fadeOutDuration = 0.18f;

    [Header("字幕堆叠")]
    [SerializeField, InspectorName("同时显示条数"), Range(1, 6)] private int _capacity = 3;
    [SerializeField, InspectorName("等候队列上限"), Min(1)] private int _queueCapacity = 12;
    [SerializeField, InspectorName("每行高度"), Min(1f)] private float _lineHeight = 44f;
    [SerializeField, InspectorName("左右滑动距离"), Min(0f)] private float _slideDistance = 110f;

    private sealed class SubtitleLine
    {
        public string Content;
        public float Duration;
    }

    private sealed class SubtitleView
    {
        public WordNoteItemUI Item;
        public RectTransform Rect;
        public float ShownAt;
        public float HideAt;
    }

    private readonly Queue<SubtitleLine> _pending = new Queue<SubtitleLine>();
    private readonly List<SubtitleView> _active = new List<SubtitleView>();
    private readonly Stack<SubtitleView> _available = new Stack<SubtitleView>();
    private Vector2 _basePosition;

    public int VisibleCount
    {
        get
        {
            return _active.Count;
        }
    }

    public int PendingCount
    {
        get
        {
            return _pending.Count;
        }
    }

    private void Awake()
    {
        if (_instance && _instance != this)
        {
            Debug.LogWarning($"场景中存在重复的 {nameof(CombatVoiceSubtitleUI)}。", this);
            enabled = false;
            return;
        }

        _instance = this;
        if (!_subtitleText)
        {
            Debug.LogError($"{name} 缺少战斗字幕文字引用。", this);
            enabled = false;
            return;
        }
        _basePosition = _subtitleText.rectTransform.anchoredPosition;
        _subtitleText.gameObject.SetActive(false);
        for (int i = 0; i < _capacity; i++)
        {
            TextMeshProUGUI label = Instantiate(_subtitleText, _subtitleText.transform.parent, false);
            label.name = "战斗字幕行" + (i + 1);
            label.raycastTarget = false;
            // 隐藏模板的克隆尚未执行 TMP.Awake，先初始化材质再使用 WordNote 的描边接口。
            label.gameObject.SetActive(true);
            label.ForceMeshUpdate();
            WordNoteItemUI item = label.gameObject.AddComponent<WordNoteItemUI>();
            label.gameObject.SetActive(false);
            _available.Push(new SubtitleView { Item = item, Rect = label.rectTransform });
        }
    }

    private void OnEnable()
    {
        CombatVoiceEvents.LineStarted += HandleLineStarted;
    }

    private void OnDisable()
    {
        CombatVoiceEvents.LineStarted -= HandleLineStarted;
        Clear();
    }

    private void Update()
    {
        float now = Time.unscaledTime;
        for (int i = _active.Count - 1; i >= 0; i--)
        {
            SubtitleView view = _active[i];
            if (now >= view.HideAt)
            {
                view.Item.gameObject.SetActive(false);
                _available.Push(view);
                _active.RemoveAt(i);
                continue;
            }

            float enter = _fadeInDuration <= 0f ? 1f :
                Mathf.Clamp01((now - view.ShownAt) / _fadeInDuration);
            float exit = _fadeOutDuration <= 0f ? 1f :
                Mathf.Clamp01((view.HideAt - now) / _fadeOutDuration);
            float enterEase = Mathf.SmoothStep(0f, 1f, enter);
            float exitEase = Mathf.SmoothStep(0f, 1f, exit);
            view.Item.SetOffset(Vector2.right * _slideDistance * (enterEase - exitEase));
            view.Item.SetAlpha(Mathf.Min(enterEase, exitEase));
        }

        FillAvailableRows();
        RefreshStack();
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public static void Request(string speakerName, string subtitle, bool isLocal, float duration)
    {
        if (!_instance || !_instance.isActiveAndEnabled)
        {
            return;
        }

        _instance.Show(speakerName, subtitle, isLocal, duration);
    }

    public void Show(string speakerName, string subtitle, bool isLocal, float duration)
    {
        if (string.IsNullOrWhiteSpace(subtitle) || !_subtitleText)
        {
            return;
        }

        string displayName = isLocal ? "我" : string.IsNullOrWhiteSpace(speakerName) ? "未知玩家" : speakerName.Trim();
        string content = $"<color=#{ColorUtility.ToHtmlStringRGB(_speakerColor)}>{EscapeText(displayName)}</color>：{EscapeText(subtitle.Trim())}";
        if (_pending.Count >= _queueCapacity)
        {
            // 只丢弃尚未显示的最旧消息，不打断正在阅读的字幕。
            _pending.Dequeue();
        }
        _pending.Enqueue(new SubtitleLine
        {
            Content = content,
            Duration = Mathf.Max(_minimumDuration, duration)
        });
        FillAvailableRows();
        RefreshStack();
    }

    public void Clear()
    {
        _pending.Clear();
        foreach (SubtitleView view in _active)
        {
            view.Item.gameObject.SetActive(false);
            _available.Push(view);
        }
        _active.Clear();
    }

    private void FillAvailableRows()
    {
        while (_pending.Count > 0 && _available.Count > 0)
        {
            SubtitleLine line = _pending.Dequeue();
            SubtitleView view = _available.Pop();
            view.Rect.anchoredPosition = _basePosition;
            view.Item.gameObject.SetActive(true);
            view.Item.Prepare(line.Content, new WordNoteStyle(
                _subtitleText.font, _fontSize, _textColor, true, Color.black));
            view.ShownAt = Time.unscaledTime;
            view.HideAt = view.ShownAt + _fadeInDuration + line.Duration + _fadeOutDuration;
            view.Item.SetOffset(Vector2.left * _slideDistance);
            _active.Add(view);
        }
    }

    private void RefreshStack()
    {
        for (int i = 0; i < _active.Count; i++)
        {
            SubtitleView view = _active[i];
            Vector2 position = view.Rect.anchoredPosition;
            position.y = _basePosition.y + (_active.Count - 1 - i) * _lineHeight;
            view.Rect.anchoredPosition = position;
        }
    }

    private void HandleLineStarted(PlayerInfo speaker, string subtitle, float duration, bool isLocal)
    {
        string speakerName = speaker ? speaker.PlayerName : string.Empty;
        Show(speakerName, subtitle, isLocal, duration);
    }

    private static string EscapeText(string content)
    {
        return content.Replace('<', '＜').Replace('>', '＞');
    }
}
