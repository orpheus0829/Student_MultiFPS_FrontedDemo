using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WordNoteItemUI : MonoBehaviour
{
    [Header("显示引用")]
    [SerializeField, InspectorName("滑动节点")] private RectTransform _visualRoot;
    [SerializeField, InspectorName("透明控制")] private CanvasGroup _canvasGroup;
    [SerializeField, InspectorName("文字")] private TextMeshProUGUI _text;

    private Vector2 _basePosition;

    public RectTransform RectTransform
    {
        get
        {
            return transform as RectTransform;
        }
    }

    private void Awake()
    {
        CacheReferences();
    }

    public bool Prepare(string content, WordNoteStyle style)
    {
        CacheReferences();
        if (!_visualRoot || !_text)
        {
            Debug.LogError($"{name} 缺少滑动节点或文字引用。", this);
            return false;
        }

        _basePosition = _visualRoot.anchoredPosition;
        _text.text = content;
        ApplyStyle(style);
        SetAlpha(0f);
        SetOffset(Vector2.zero);
        return true;
    }

    public void SetOffset(Vector2 offset)
    {
        if (_visualRoot)
        {
            _visualRoot.anchoredPosition = _basePosition + offset;
        }
    }

    public void SetAlpha(float alpha)
    {
        float clampedAlpha = Mathf.Clamp01(alpha);
        if (_canvasGroup)
        {
            _canvasGroup.alpha = clampedAlpha;
        }
        else if (_text)
        {
            _text.alpha = clampedAlpha;
        }
    }

    private void CacheReferences()
    {
        if (!_visualRoot)
        {
            _visualRoot = transform as RectTransform;
        }

        if (!_text)
        {
            _text = GetComponentInChildren<TextMeshProUGUI>(true);
        }

        if (!_canvasGroup)
        {
            TryGetComponent(out _canvasGroup);
        }
    }

    private void ApplyStyle(WordNoteStyle style)
    {
        if (style.Font)
        {
            _text.font = style.Font;
        }

        if (style.FontSize > 0f)
        {
            _text.fontSize = style.FontSize;
        }

        _text.color = style.FontColor;
        _text.alignment = style.AlignRight
            ? TextAlignmentOptions.Right
            : style.Centered
                ? TextAlignmentOptions.Center
                : TextAlignmentOptions.Left;
        _text.outlineColor = style.OutlineColor;
        _text.outlineWidth = 0.08f;
        if (style.SpriteAsset)
        {
            _text.spriteAsset = style.SpriteAsset;
        }
    }
}
