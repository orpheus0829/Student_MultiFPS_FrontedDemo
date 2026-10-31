using TMPro;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

public sealed class WordNoteClip : PlayableAsset, ITimelineClipAsset
{
    [Header("文字内容")]
    [SerializeField, TextArea(2, 6)] private string _content;

    [Header("文字样式")]
    [SerializeField] private TMP_FontAsset _font;
    [SerializeField, Min(1f)] private float _fontSize = 42f;
    [SerializeField] private Color _fontColor = Color.white;
    [SerializeField] private bool _centered = true;
    [SerializeField] private Color _outlineColor = Color.black;

    public string Content
    {
        get
        {
            return _content;
        }
    }

    public ClipCaps clipCaps
    {
        get
        {
            return ClipCaps.None;
        }
    }

    public WordNoteStyle Style
    {
        get
        {
            return new WordNoteStyle(
                _font,
                _fontSize,
                _fontColor,
                _centered,
                _outlineColor);
        }
    }

    public override Playable CreatePlayable(
        PlayableGraph graph,
        GameObject owner)
    {
        ScriptPlayable<WordNoteBehaviour> playable =
            ScriptPlayable<WordNoteBehaviour>.Create(graph);
        WordNoteBehaviour behaviour = playable.GetBehaviour();
        behaviour.Clip = this;
        return playable;
    }
}
