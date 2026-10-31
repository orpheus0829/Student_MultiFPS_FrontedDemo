using System;
using TMPro;
using UnityEngine;

[Serializable]
public struct WordNoteStyle
{
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField, Min(1f)] private float _fontSize;
    [SerializeField] private Color _fontColor;
    [SerializeField] private bool _centered;
    [SerializeField] private Color _outlineColor;

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

    public Color OutlineColor
    {
        get
        {
            return _outlineColor;
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
        _outlineColor = outlineColor;
    }
}
