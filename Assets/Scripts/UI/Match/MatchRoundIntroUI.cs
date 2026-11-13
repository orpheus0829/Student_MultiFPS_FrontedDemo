using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class MatchRoundIntroUI : MonoBehaviour
{
    private const float CountdownEnterDuration = 0.18f;
    private const float CountdownHoldDuration = 0.42f;
    private const float CountdownExitDuration = 0.16f;
    private const float StartEnterDuration = 0.28f;
    private const float StartHoldDuration = 0.85f;
    private const float StartExitDuration = 0.24f;
    private const float StartTextWithLogoY = -250f;

    private GameObject _root;
    private CanvasGroup _countdownGroup;
    private RectTransform _countdownRoot;
    private TMP_Text _countdownText;
    private CanvasGroup _startGroup;
    private RectTransform _startRoot;
    private RawImage _modeLogo;
    private TMP_Text _startText;
    private MatchManager _manager;
    private ResourceMgr _resources;
    private string _loadedLogoId;
    private int _roundIndex;
    private int _sequenceVersion;

    private void Awake()
    {
        BuildInterface();
        HideImmediate();
    }

    private void OnEnable()
    {
        _manager = MatchManager.Instance;
        _manager.RoundStateChanged += HandleRoundStateChanged;
        _resources = ResourceMgr.instance;
    }

    private void OnDisable()
    {
        _sequenceVersion++;
        if (_manager)
        {
            _manager.RoundStateChanged -= HandleRoundStateChanged;
            _manager = null;
        }

        ReleaseLogo();
        HideImmediate();
    }

    private void HandleRoundStateChanged(MatchState state)
    {
        if (state == MatchState.Preparing || state == MatchState.Idle)
        {
            _roundIndex = 0;
            _sequenceVersion++;
            ReleaseLogo();
            HideImmediate();
            return;
        }

        if (state != MatchState.InRound)
        {
            _sequenceVersion++;
            HideImmediate();
            return;
        }

        _roundIndex++;
        PlayRoundIntro(_sequenceVersion + 1, _roundIndex == 1);
    }

    private async void PlayRoundIntro(int version, bool showModeLogo)
    {
        _sequenceVersion = version;
        try
        {
            for (int number = 5; number >= 1; number--)
            {
                if (!CanContinue(version))
                {
                    return;
                }

                _countdownText.text = number.ToString();
                _countdownGroup.gameObject.SetActive(true);
                await Animate(
                    _countdownGroup,
                    _countdownRoot,
                    1.65f,
                    1f,
                    0f,
                    1f,
                    CountdownEnterDuration,
                    version);
                await WaitUnscaled(CountdownHoldDuration, version);
                await Animate(
                    _countdownGroup,
                    _countdownRoot,
                    1f,
                    0.68f,
                    1f,
                    0f,
                    CountdownExitDuration,
                    version);
                _countdownGroup.gameObject.SetActive(false);
            }

            if (!CanContinue(version))
            {
                return;
            }

            await LoadModeLogo(showModeLogo, version);
            _modeLogo.gameObject.SetActive(showModeLogo && _modeLogo.texture);
            _startText.rectTransform.anchoredPosition = showModeLogo
                ? new Vector2(0f, StartTextWithLogoY)
                : Vector2.zero;
            _startGroup.gameObject.SetActive(true);
            await Animate(
                _startGroup,
                _startRoot,
                1.55f,
                1f,
                0f,
                1f,
                StartEnterDuration,
                version);
            await WaitUnscaled(StartHoldDuration, version);
            await Animate(
                _startGroup,
                _startRoot,
                1f,
                0.7f,
                1f,
                0f,
                StartExitDuration,
                version);
            _startGroup.gameObject.SetActive(false);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            HideImmediate();
        }
        finally
        {
            if (showModeLogo)
            {
                ReleaseLogo();
            }
        }
    }

    private async Task LoadModeLogo(bool showModeLogo, int version)
    {
        _modeLogo.texture = null;
        if (!showModeLogo || !_manager || _manager.Config == null ||
            !_manager.Config.Mode || string.IsNullOrWhiteSpace(_manager.Config.Mode.LogoResourceId))
        {
            return;
        }

        string resourceId = _manager.Config.Mode.LogoResourceId;
        Texture2D texture = await _resources.LoadAsync<Texture2D>(resourceId);
        if (!CanContinue(version))
        {
            _resources.Release<Texture2D>(resourceId);
            return;
        }

        _loadedLogoId = resourceId;
        _modeLogo.texture = texture;
    }

    private async Task Animate(
        CanvasGroup group,
        RectTransform target,
        float fromScale,
        float toScale,
        float fromAlpha,
        float toAlpha,
        float duration,
        int version)
    {
        float elapsed = 0f;
        while (elapsed < duration && CanContinue(version))
        {
            elapsed += Time.unscaledDeltaTime;
            float progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
            float eased = 1f - Mathf.Pow(1f - progress, 3f);
            target.localScale = Vector3.one * Mathf.LerpUnclamped(fromScale, toScale, eased);
            group.alpha = Mathf.Lerp(fromAlpha, toAlpha, eased);
            await Task.Yield();
        }

        if (!CanContinue(version))
        {
            return;
        }

        target.localScale = Vector3.one * toScale;
        group.alpha = toAlpha;
    }

    private async Task WaitUnscaled(float duration, int version)
    {
        float elapsed = 0f;
        while (elapsed < duration && CanContinue(version))
        {
            elapsed += Time.unscaledDeltaTime;
            await Task.Yield();
        }
    }

    private bool CanContinue(int version)
    {
        return this && isActiveAndEnabled && version == _sequenceVersion &&
            _manager && _manager.State == MatchState.InRound;
    }

    private void ReleaseLogo()
    {
        if (!string.IsNullOrWhiteSpace(_loadedLogoId) && _resources)
        {
            _resources.Release<Texture2D>(_loadedLogoId);
        }

        _loadedLogoId = string.Empty;
        if (_modeLogo)
        {
            _modeLogo.texture = null;
        }
    }

    private void HideImmediate()
    {
        if (_countdownGroup)
        {
            _countdownGroup.alpha = 0f;
            _countdownGroup.gameObject.SetActive(false);
        }

        if (_startGroup)
        {
            _startGroup.alpha = 0f;
            _startGroup.gameObject.SetActive(false);
        }
    }

    private void BuildInterface()
    {
        _root = new GameObject("回合开场提示", typeof(RectTransform));
        _root.transform.SetParent(transform, false);
        RectTransform rootRect = _root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.SetAsLastSibling();

        GameObject countdown = new GameObject(
            "回合倒计时",
            typeof(RectTransform),
            typeof(CanvasGroup));
        countdown.transform.SetParent(_root.transform, false);
        _countdownRoot = countdown.GetComponent<RectTransform>();
        _countdownRoot.anchorMin = new Vector2(0.5f, 1f);
        _countdownRoot.anchorMax = new Vector2(0.5f, 1f);
        _countdownRoot.pivot = new Vector2(0.5f, 1f);
        _countdownRoot.sizeDelta = new Vector2(360f, 180f);
        _countdownRoot.anchoredPosition = new Vector2(0f, -70f);
        _countdownGroup = countdown.GetComponent<CanvasGroup>();
        _countdownGroup.blocksRaycasts = false;
        _countdownGroup.interactable = false;
        _countdownText = CreateText("数字", countdown.transform, 112f, FontStyles.Bold);
        Stretch(_countdownText.rectTransform);
        _countdownText.alignment = TextAlignmentOptions.Center;
        _countdownText.color = Color.white;

        GameObject start = new GameObject(
            "战斗开始组合",
            typeof(RectTransform),
            typeof(CanvasGroup));
        start.transform.SetParent(_root.transform, false);
        _startRoot = start.GetComponent<RectTransform>();
        _startRoot.anchorMin = new Vector2(0.5f, 1f);
        _startRoot.anchorMax = new Vector2(0.5f, 1f);
        _startRoot.pivot = new Vector2(0.5f, 1f);
        _startRoot.sizeDelta = new Vector2(520f, 360f);
        _startRoot.anchoredPosition = new Vector2(0f, -48f);
        _startGroup = start.GetComponent<CanvasGroup>();
        _startGroup.blocksRaycasts = false;
        _startGroup.interactable = false;

        GameObject logo = new GameObject("模式Logo", typeof(RectTransform), typeof(RawImage));
        logo.transform.SetParent(start.transform, false);
        RectTransform logoRect = logo.GetComponent<RectTransform>();
        logoRect.anchorMin = new Vector2(0.5f, 1f);
        logoRect.anchorMax = new Vector2(0.5f, 1f);
        logoRect.pivot = new Vector2(0.5f, 1f);
        logoRect.sizeDelta = new Vector2(230f, 230f);
        logoRect.anchoredPosition = Vector2.zero;
        _modeLogo = logo.GetComponent<RawImage>();
        _modeLogo.color = Color.white;
        _modeLogo.raycastTarget = false;

        _startText = CreateText("战斗开始文字", start.transform, 54f, FontStyles.Bold);
        RectTransform startTextRect = _startText.rectTransform;
        startTextRect.anchorMin = new Vector2(0.5f, 1f);
        startTextRect.anchorMax = new Vector2(0.5f, 1f);
        startTextRect.pivot = new Vector2(0.5f, 1f);
        startTextRect.sizeDelta = new Vector2(520f, 90f);
        startTextRect.anchoredPosition = new Vector2(0f, StartTextWithLogoY);
        _startText.alignment = TextAlignmentOptions.Center;
        _startText.color = Color.white;
        _startText.outlineColor = Color.black;
        _startText.outlineWidth = 0.22f;
        _startText.text = "战斗开始！";
    }

    private static TMP_Text CreateText(
        string objectName,
        Transform parent,
        float fontSize,
        FontStyles style)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
