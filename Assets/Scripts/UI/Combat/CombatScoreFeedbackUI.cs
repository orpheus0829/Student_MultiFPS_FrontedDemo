using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatScoreFeedbackUI : MonoBehaviour
{
    private enum MainState
    {
        Hidden,
        Entering,
        Showing,
        Exiting
    }

    private enum IncrementState
    {
        Hidden,
        Bouncing,
        Settling,
        Holding,
        Fading
    }

    private enum KillDetailState
    {
        Hidden,
        Entering,
        Holding,
        Exiting
    }

    private static CombatScoreFeedbackUI _instance;

    [Header("显示引用")]
    [SerializeField, InspectorName("整体透明控制")] private CanvasGroup _canvasGroup;
    [SerializeField, InspectorName("整体缩放节点")] private RectTransform _visualRoot;
    [SerializeField, InspectorName("累计分数文字")] private TextMeshProUGUI _scoreText;
    [SerializeField, InspectorName("本次加分文字")] private TextMeshProUGUI _incrementText;
    [SerializeField, InspectorName("击杀信息文字")] private TextMeshProUGUI _killDetailText;

    [Header("主反馈动画")]
    [SerializeField, InspectorName("首次出现缩放"), Min(1f)] private float _enterStartScale = 1.3f;
    [SerializeField, InspectorName("首次出现时间"), Min(0f)] private float _enterDuration = 0.2f;
    [SerializeField, InspectorName("分数追赶速度"), Min(0.01f)] private float _scoreLerpSpeed = 12f;
    [SerializeField, InspectorName("无新分数停留时间"), Min(0f)] private float _mainHoldDuration = 1.6f;
    [SerializeField, InspectorName("整体淡出时间"), Min(0f)] private float _mainFadeDuration = 0.35f;
    [SerializeField, InspectorName("中断淡出后的恢复时间"), Min(0f)] private float _mainRestoreDuration = 0.12f;

    [Header("本次加分动画")]
    [SerializeField, InspectorName("起始缩放"), Min(0f)] private float _incrementStartScale = 0.55f;
    [SerializeField, InspectorName("弹性过冲缩放"), Min(1f)] private float _incrementOvershootScale = 1.25f;
    [SerializeField, InspectorName("弹出时间"), Min(0f)] private float _incrementBounceDuration = 0.09f;
    [SerializeField, InspectorName("回落时间"), Min(0f)] private float _incrementSettleDuration = 0.12f;
    [SerializeField, InspectorName("停留时间"), Min(0f)] private float _incrementHoldDuration = 0.45f;
    [SerializeField, InspectorName("淡出时间"), Min(0f)] private float _incrementFadeDuration = 0.22f;

    [Header("击杀图标名称")]
    [SerializeField, InspectorName("普通击杀骷髅")] private string _normalKillSpriteToken = "skull_white";
    [SerializeField, InspectorName("爆头击杀骷髅")] private string _headshotKillSpriteToken = "skull_red";

    [Header("击杀信息比例")]
    [SerializeField, InspectorName("骷髅相对大分数比例"), Range(0.1f, 2f)] private float _killIconScale = 0.9f;
    [SerializeField, InspectorName("名字相对大分数比例"), Range(0.1f, 2f)] private float _victimNameScale = 0.7f;

    [Header("击杀提示动画")]
    [SerializeField, Min(0f), InspectorName("快速显现时间")] private float _killEnterDuration = 0.07f;
    [SerializeField, Min(0f), InspectorName("显示时间")] private float _killHoldDuration = 1.15f;
    [SerializeField, Min(0f), InspectorName("淡出时间")] private float _killFadeDuration = 0.22f;

    private readonly Queue<KillDetail> _killDetails = new Queue<KillDetail>();
    private MainState _mainState;
    private IncrementState _incrementState;
    private KillDetailState _killDetailState;
    private Vector3 _normalVisualScale;
    private Vector3 _normalIncrementScale;
    private float _targetScore;
    private float _displayedScore;
    private float _mainAnimationTime;
    private float _mainInactivityTime;
    private float _mainExitStartAlpha;
    private float _incrementAnimationTime;
    private float _killDetailTime;

    public static CombatScoreFeedbackUI Instance
    {
        get
        {
            return _instance;
        }
    }

    public int CurrentCycleScore
    {
        get
        {
            return Mathf.RoundToInt(_targetScore);
        }
    }

    private void Awake()
    {
        if (_instance && _instance != this)
        {
            Debug.LogWarning($"场景中存在重复的 {nameof(CombatScoreFeedbackUI)}。", this);
            enabled = false;
            return;
        }

        _instance = this;
        if (!CacheReferences())
        {
            enabled = false;
            return;
        }

        RefreshFontMaterials();
        _normalVisualScale = _visualRoot.localScale;
        _normalIncrementScale = _incrementText.rectTransform.localScale;
        _canvasGroup.interactable = false;
        _canvasGroup.blocksRaycasts = false;
        ResetFeedback();
        _killDetailText.text = string.Empty;
        _killDetailText.alpha = 0f;
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        UpdateDisplayedScore(deltaTime);
        UpdateMainFeedback(deltaTime);
        UpdateIncrementFeedback(deltaTime);
        UpdateKillDetailFeedback(deltaTime);
    }

    private void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
        }
    }

    public static void Request(CombatScoreFeedbackData data)
    {
        if (!_instance)
        {
            Debug.LogWarning("[CombatScoreFeedbackUI] 场景中缺少分数反馈组件。");
            return;
        }

        _instance.Show(data);
    }

    public void Show(CombatScoreFeedbackData data)
    {
        if (data.ScoreDelta <= 0 && !data.IsKill)
        {
            return;
        }

        if (data.ScoreDelta > 0)
        {
            bool wasHidden = _mainState == MainState.Hidden;
            _targetScore += data.ScoreDelta;
            _mainInactivityTime = 0f;

            if (wasHidden)
            {
                StartMainEnter();
            }
            else if (_mainState == MainState.Exiting)
            {
                _mainState = MainState.Showing;
                _mainAnimationTime = 0f;
            }

            StartIncrement(data.ScoreDelta);
        }

        if (data.IsKill)
        {
            _killDetails.Enqueue(new KillDetail(data.DefeatedName, data.SourceName, data.IsHeadshot));
            if (_killDetailState == KillDetailState.Hidden)
            {
                ShowNextKillDetail();
            }
        }
    }

    public void AddScore(int scoreDelta)
    {
        Show(new CombatScoreFeedbackData(
            scoreDelta,
            false,
            string.Empty,
            false));
    }

    public void AddKillScore(
        int scoreDelta,
        string defeatedName,
        bool isHeadshot)
    {
        Show(new CombatScoreFeedbackData(
            scoreDelta,
            true,
            defeatedName,
            isHeadshot));
    }

    public void SetSpriteAsset(TMP_SpriteAsset spriteAsset)
    {
        if (_killDetailText)
        {
            _killDetailText.spriteAsset = spriteAsset;
        }
    }

    private bool CacheReferences()
    {
        if (!_canvasGroup)
        {
            TryGetComponent(out _canvasGroup);
        }

        if (!_visualRoot)
        {
            _visualRoot = transform as RectTransform;
        }

        if (!_canvasGroup ||
            !_visualRoot ||
            !_scoreText ||
            !_incrementText ||
            !_killDetailText)
        {
            Debug.LogError($"{name} 的分数反馈 UI 引用未配置完整。", this);
            return false;
        }

        return true;
    }

    private void RefreshFontMaterials()
    {
        ApplyFontMaterial(_scoreText);
        ApplyFontMaterial(_incrementText);
        ApplyFontMaterial(_killDetailText);
    }

    private static void ApplyFontMaterial(TextMeshProUGUI text)
    {
        if (text && text.font && text.font.material)
        {
            text.fontSharedMaterial = text.font.material;
        }
    }

    private void StartMainEnter()
    {
        _mainState = MainState.Entering;
        _mainAnimationTime = 0f;
        _mainInactivityTime = 0f;
        _displayedScore = 0f;
        _canvasGroup.alpha = 0f;
        _visualRoot.localScale = _normalVisualScale * _enterStartScale;
        if (_enterDuration <= 0f)
        {
            CompleteMainEnter();
        }
    }

    private void UpdateMainFeedback(float deltaTime)
    {
        switch (_mainState)
        {
            case MainState.Entering:
                UpdateMainEnter(deltaTime);
                break;
            case MainState.Showing:
                UpdateMainShowing(deltaTime);
                break;
            case MainState.Exiting:
                UpdateMainExit(deltaTime);
                break;
        }
    }

    private void UpdateMainEnter(float deltaTime)
    {
        _mainAnimationTime += deltaTime;
        float progress = Mathf.Clamp01(_mainAnimationTime / _enterDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        _canvasGroup.alpha = easedProgress;
        _visualRoot.localScale = Vector3.LerpUnclamped(
            _normalVisualScale * _enterStartScale,
            _normalVisualScale,
            easedProgress);
        if (progress >= 1f)
        {
            CompleteMainEnter();
        }
    }

    private void CompleteMainEnter()
    {
        _canvasGroup.alpha = 1f;
        _visualRoot.localScale = _normalVisualScale;
        _mainInactivityTime = 0f;
        _mainState = MainState.Showing;
    }

    private void UpdateMainShowing(float deltaTime)
    {
        _visualRoot.localScale = _normalVisualScale;
        if (_canvasGroup.alpha < 1f)
        {
            float restoreDuration = Mathf.Max(0.0001f, _mainRestoreDuration);
            _canvasGroup.alpha = Mathf.MoveTowards(
                _canvasGroup.alpha,
                1f,
                deltaTime / restoreDuration);
        }

        _mainInactivityTime += deltaTime;
        if (_mainInactivityTime >= _mainHoldDuration)
        {
            StartMainExit();
        }
    }

    private void StartMainExit()
    {
        _mainState = MainState.Exiting;
        _mainAnimationTime = 0f;
        _mainExitStartAlpha = _canvasGroup.alpha;
        if (_mainFadeDuration <= 0f)
        {
            ResetFeedback();
        }
    }

    private void UpdateMainExit(float deltaTime)
    {
        _mainAnimationTime += deltaTime;
        float progress = Mathf.Clamp01(_mainAnimationTime / _mainFadeDuration);
        _canvasGroup.alpha = Mathf.LerpUnclamped(
            _mainExitStartAlpha,
            0f,
            progress);
        if (progress >= 1f)
        {
            ResetFeedback();
        }
    }

    private void UpdateDisplayedScore(float deltaTime)
    {
        if (_mainState == MainState.Hidden)
        {
            return;
        }

        float interpolation = 1f - Mathf.Exp(-_scoreLerpSpeed * deltaTime);
        _displayedScore = Mathf.Lerp(
            _displayedScore,
            _targetScore,
            interpolation);
        if (Mathf.Abs(_displayedScore - _targetScore) < 0.01f)
        {
            _displayedScore = _targetScore;
        }

        _scoreText.text = Mathf.RoundToInt(_displayedScore).ToString();
    }

    private void StartIncrement(int scoreDelta)
    {
        _incrementText.text = "+" + scoreDelta;
        _incrementText.alpha = 1f;
        _incrementText.rectTransform.localScale =
            _normalIncrementScale * _incrementStartScale;
        _incrementAnimationTime = 0f;
        _incrementState = IncrementState.Bouncing;
        if (_incrementBounceDuration <= 0f)
        {
            StartIncrementSettle();
        }
    }

    private void UpdateIncrementFeedback(float deltaTime)
    {
        switch (_incrementState)
        {
            case IncrementState.Bouncing:
                UpdateIncrementBounce(deltaTime);
                break;
            case IncrementState.Settling:
                UpdateIncrementSettle(deltaTime);
                break;
            case IncrementState.Holding:
                UpdateIncrementHold(deltaTime);
                break;
            case IncrementState.Fading:
                UpdateIncrementFade(deltaTime);
                break;
        }
    }

    private void UpdateIncrementBounce(float deltaTime)
    {
        _incrementAnimationTime += deltaTime;
        float progress = Mathf.Clamp01(
            _incrementAnimationTime / _incrementBounceDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        _incrementText.rectTransform.localScale = Vector3.LerpUnclamped(
            _normalIncrementScale * _incrementStartScale,
            _normalIncrementScale * _incrementOvershootScale,
            easedProgress);
        if (progress >= 1f)
        {
            StartIncrementSettle();
        }
    }

    private void StartIncrementSettle()
    {
        _incrementState = IncrementState.Settling;
        _incrementAnimationTime = 0f;
        _incrementText.rectTransform.localScale =
            _normalIncrementScale * _incrementOvershootScale;
        if (_incrementSettleDuration <= 0f)
        {
            StartIncrementHold();
        }
    }

    private void UpdateIncrementSettle(float deltaTime)
    {
        _incrementAnimationTime += deltaTime;
        float progress = Mathf.Clamp01(
            _incrementAnimationTime / _incrementSettleDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        _incrementText.rectTransform.localScale = Vector3.LerpUnclamped(
            _normalIncrementScale * _incrementOvershootScale,
            _normalIncrementScale,
            easedProgress);
        if (progress >= 1f)
        {
            StartIncrementHold();
        }
    }

    private void StartIncrementHold()
    {
        _incrementState = IncrementState.Holding;
        _incrementAnimationTime = 0f;
        _incrementText.rectTransform.localScale = _normalIncrementScale;
        if (_incrementHoldDuration <= 0f)
        {
            StartIncrementFade();
        }
    }

    private void UpdateIncrementHold(float deltaTime)
    {
        _incrementAnimationTime += deltaTime;
        if (_incrementAnimationTime >= _incrementHoldDuration)
        {
            StartIncrementFade();
        }
    }

    private void StartIncrementFade()
    {
        _incrementState = IncrementState.Fading;
        _incrementAnimationTime = 0f;
        if (_incrementFadeDuration <= 0f)
        {
            HideIncrement();
        }
    }

    private void UpdateIncrementFade(float deltaTime)
    {
        _incrementAnimationTime += deltaTime;
        float progress = Mathf.Clamp01(
            _incrementAnimationTime / _incrementFadeDuration);
        _incrementText.alpha = 1f - progress;
        if (progress >= 1f)
        {
            HideIncrement();
        }
    }

    private void HideIncrement()
    {
        _incrementState = IncrementState.Hidden;
        _incrementText.alpha = 0f;
        _incrementText.rectTransform.localScale = _normalIncrementScale;
    }

    private void ShowKillDetail(string defeatedName, string sourceName, bool isHeadshot)
    {
        string spriteToken = isHeadshot
            ? _headshotKillSpriteToken
            : _normalKillSpriteToken;
        string safeName = EscapeName(defeatedName);
        string safeSource = EscapeName(sourceName);
        string killMessage = string.IsNullOrWhiteSpace(safeSource)
            ? "你击杀了" + safeName
            : "你使用" + safeSource + "击杀了" + safeName;

        if (IsSafeSpriteToken(spriteToken))
        {
            _killDetailText.text = "<size=" +
                ToPercent(_killIconScale) +
                "%><sprite name=\"" +
                spriteToken +
                "\"></size>" +
                (string.IsNullOrWhiteSpace(safeName)
                    ? string.Empty
                    : "\n<size=" +
                        ToPercent(_victimNameScale) +
                        "%>" +
                        killMessage +
                        "</size>");
            return;
        }

        _killDetailText.text = "<size=" +
            ToPercent(_victimNameScale) +
            "%>" +
            killMessage +
            "</size>";
    }

    private void ShowNextKillDetail()
    {
        if (_killDetails.Count == 0)
        {
            _killDetailText.text = string.Empty;
            _killDetailText.alpha = 0f;
            _killDetailState = KillDetailState.Hidden;
            return;
        }

        KillDetail detail = _killDetails.Dequeue();
        ShowKillDetail(detail.DefeatedName, detail.SourceName, detail.IsHeadshot);
        _killDetailText.alpha = 0f;
        _killDetailTime = 0f;
        _killDetailState = KillDetailState.Entering;
    }

    private void UpdateKillDetailFeedback(float deltaTime)
    {
        if (_killDetailState == KillDetailState.Hidden)
        {
            return;
        }

        _killDetailTime += deltaTime;
        if (_killDetailState == KillDetailState.Entering)
        {
            _killDetailText.alpha = Mathf.Clamp01(
                _killDetailTime / Mathf.Max(0.0001f, _killEnterDuration));
            if (_killDetailTime >= _killEnterDuration)
            {
                _killDetailText.alpha = 1f;
                _killDetailTime = 0f;
                _killDetailState = KillDetailState.Holding;
            }
            return;
        }

        if (_killDetailState == KillDetailState.Holding)
        {
            if (_killDetailTime >= _killHoldDuration)
            {
                _killDetailTime = 0f;
                _killDetailState = KillDetailState.Exiting;
            }
            return;
        }

        _killDetailText.alpha = 1f - Mathf.Clamp01(
            _killDetailTime / Mathf.Max(0.0001f, _killFadeDuration));
        if (_killDetailTime >= _killFadeDuration)
        {
            ShowNextKillDetail();
        }
    }

    private void ResetFeedback()
    {
        _mainState = MainState.Hidden;
        _incrementState = IncrementState.Hidden;
        _targetScore = 0f;
        _displayedScore = 0f;
        _mainAnimationTime = 0f;
        _mainInactivityTime = 0f;
        _incrementAnimationTime = 0f;
        _canvasGroup.alpha = 0f;
        _visualRoot.localScale = _normalVisualScale;
        _scoreText.text = "0";
        _incrementText.text = string.Empty;
        _incrementText.alpha = 0f;
        _incrementText.rectTransform.localScale = _normalIncrementScale;
    }

    private readonly struct KillDetail
    {
        public string DefeatedName { get; }
        public string SourceName { get; }
        public bool IsHeadshot { get; }

        public KillDetail(string defeatedName, string sourceName, bool isHeadshot)
        {
            DefeatedName = defeatedName;
            SourceName = sourceName;
            IsHeadshot = isHeadshot;
        }
    }

    private static string EscapeName(string playerName)
    {
        if (string.IsNullOrWhiteSpace(playerName))
        {
            return string.Empty;
        }

        return playerName
            .Replace('<', '＜')
            .Replace('>', '＞')
            .Trim();
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

    private static int ToPercent(float scale)
    {
        return Mathf.RoundToInt(Mathf.Max(0.1f, scale) * 100f);
    }
}
