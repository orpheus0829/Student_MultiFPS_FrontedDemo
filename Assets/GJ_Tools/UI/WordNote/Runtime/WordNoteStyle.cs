using System;
using TMPro;
using UnityEngine;

[Serializable]
public struct WordNoteStyle
{
    [SerializeField, InspectorName("字体")] private TMP_FontAsset _font;
    [SerializeField, InspectorName("字号"), Min(1f)] private float _fontSize;
    [SerializeField, InspectorName("文字颜色")] private Color _fontColor;
    [SerializeField, InspectorName("文字居中")] private bool _centered;
    [SerializeField, InspectorName("文字靠右")] private bool _alignRight;
    [SerializeField, InspectorName("描边颜色")] private Color _outlineColor;
    [SerializeField, InspectorName("TMP 图标资源")] private TMP_SpriteAsset _spriteAsset;

    public TMP_FontAsset Font
    {
        get
        {
            return _font;
        }
    }

    public float FontSize
    {
        get
        {
            return _fontSize;
        }
    }

    public Color FontColor
    {
        get
        {
            return _fontColor;
        }
    }

    public bool Centered
    {
        get
        {
            return _centered;
        }
    }

    public bool AlignRight
    {
        get
        {
            return _alignRight;
        }
    }

    public Color OutlineColor
    {
        get
        {
            return _outlineColor;
        }
    }

    public TMP_SpriteAsset SpriteAsset
    {
        get
        {
            return _spriteAsset;
        }
    }

    public WordNoteStyle(
        TMP_FontAsset font,
        float fontSize,
        Color fontColor,
        bool centered,
        Color outlineColor)
    {
        _font = font;
        _fontSize = fontSize;
        _fontColor = fontColor;
        _centered = centered;
        _alignRight = false;
        _outlineColor = outlineColor;
        _spriteAsset = null;
    }

    public WordNoteStyle(
        TMP_FontAsset font,
        float fontSize,
        Color fontColor,
        bool centered,
        Color outlineColor,
        TMP_SpriteAsset spriteAsset,
        bool alignRight = false)
    {
        _font = font;
        _fontSize = fontSize;
        _fontColor = fontColor;
        _centered = centered;
        _alignRight = alignRight;
        _outlineColor = outlineColor;
        _spriteAsset = spriteAsset;
    }
}
