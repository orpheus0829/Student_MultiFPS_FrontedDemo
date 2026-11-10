using System.Text;
using System.Globalization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class KillFeedUI : MonoBehaviour
{
    private static KillFeedUI _instance;

    [Header("播报样式")]
    [SerializeField, InspectorName("使用自定义样式")] private bool _useCustomStyle;
    [SerializeField, InspectorName("自定义样式")] private WordNoteStyle _customStyle;
    [SerializeField, InspectorName("停留时间"), Min(0f)] private float _duration = 4f;

    [Header("图标名称")]
    [SerializeField, InspectorName("爆头骷髅图标名称")] private string _headshotSpriteToken = "skull_red";
    [SerializeField, InspectorName("无武器击杀骷髅图标名称")] private string _normalKillSpriteToken = "skull_white";

    [Header("元素排版")]
    [SerializeField, InspectorName("元素间距（字宽）"), Range(0f, 1f)] private float _elementGapEm = 0.25f;
    [SerializeField, InspectorName("爆头骷髅相对文字大小"), Range(0.5f, 2f)] private float _headshotSpriteScale = 1.15f;

    public static KillFeedUI Instance
    {
        get
        {
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance && _instance != this)
        {
            Debug.LogWarning($"场景中存在重复的 {nameof(KillFeedUI)}。", this);
            enabled = false;
            return;
        }

        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public static WordNoteHandle Request(KillFeedMessage message)
    {
        if (!_instance)
        {
            Debug.LogWarning("[KillFeedUI] 场景中缺少击杀播报组件。");
            return null;
        }

        return _instance.ShowKill(message);
    }

    public WordNoteHandle ShowKill(KillFeedMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.VictimName))
        {
            Debug.LogWarning("[KillFeedUI] 被击杀者名字为空，已跳过播报。", this);
            return null;
        }

        string content = BuildContent(message);
        if (_useCustomStyle)
        {
            return WordNote.Show(
                WordNoteRegion.TopRight,
                content,
                _customStyle,
                _duration);
        }

        return WordNote.Show(
            WordNoteRegion.TopRight,
            content,
            _duration);
    }

    public void SetSpriteAsset(TMP_SpriteAsset spriteAsset)
    {
        WordNoteStyle baseStyle = _customStyle;
        if (!_useCustomStyle && WordNoteUI.Instance)
        {
            baseStyle = WordNoteUI.Instance.DefaultStyle;
        }

        _customStyle = new WordNoteStyle(
            baseStyle.Font,
            baseStyle.FontSize,
            baseStyle.FontColor,
            baseStyle.Centered,
            baseStyle.OutlineColor,
            spriteAsset,
            baseStyle.AlignRight);
        _useCustomStyle = true;
    }

    private string BuildContent(KillFeedMessage message)
    {
        StringBuilder builder = new StringBuilder(96);
        AppendText(builder, EscapeName(message.KillerName));
        bool hasWeaponIcon = IsSafeSpriteToken(message.WeaponSpriteToken);
        AppendSprite(builder, hasWeaponIcon
            ? message.WeaponSpriteToken
            : message.IsHeadshot ? _headshotSpriteToken : _normalKillSpriteToken, 1f);
        if (message.IsHeadshot && hasWeaponIcon)
        {
            AppendSprite(builder, _headshotSpriteToken, _headshotSpriteScale);
        }
        AppendText(builder, EscapeName(message.VictimName));
        return builder.ToString();
    }

    private void AppendText(StringBuilder builder, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        AppendGap(builder);
        builder.Append(text.Trim());
    }

    private void AppendSprite(
        StringBuilder builder,
        string spriteToken,
        float scale)
    {
        if (!IsSafeSpriteToken(spriteToken))
        {
            return;
        }

        AppendGap(builder);
        int scalePercent = Mathf.RoundToInt(Mathf.Max(0.1f, scale) * 100f);
        bool hasCustomScale = scalePercent != 100;
        if (hasCustomScale)
        {
            builder.Append("<size=");
            builder.Append(scalePercent);
            builder.Append("%>");
        }

        builder.Append("<sprite name=\"");
        builder.Append(spriteToken);
        builder.Append("\">");
        if (hasCustomScale)
        {
            builder.Append("</size>");
        }
    }

    private void AppendGap(StringBuilder builder)
    {
        if (builder.Length <= 0 || _elementGapEm <= 0f)
        {
            return;
        }

        builder.Append("<space=");
        builder.Append(_elementGapEm.ToString("0.##", CultureInfo.InvariantCulture));
        builder.Append("em>");
    }

    private static string EscapeName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return string.Empty;
        }

        return playerName
            .Replace('<', '＜')
            .Replace('>', '＞');
    }

    private static bool IsSafeSpriteToken(string spriteToken)
    {
        if (string.IsNullOrWhiteSpace(spriteToken))
        {
            return false;
        }

        for (int i = 0; i < spriteToken.Length; i++)
        {
            char character = spriteToken[i];
            if (!char.IsLetterOrDigit(character) &&
                character != '_' &&
                character != '-' &&
                character != '.')
            {
                return false;
            }
        }

        return true;
    }
}
