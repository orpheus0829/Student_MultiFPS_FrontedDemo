using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BombShopTabletView : MonoBehaviour
{
    private const float OpenDuration = 0.42f;
    private const float CloseDuration = 0.24f;
    private const float LoadingDuration = 0.475f;
    private const float LoadingLoops = 1.2f;

    [Header("平板动画")]
    [SerializeField] private CanvasGroup _canvasGroup;
    [SerializeField] private RectTransform _motionRoot;
    [SerializeField] private Vector2 _hiddenOffset = new Vector2(0f, -1180f);

    [Header("监控画面")]
    [SerializeField] private RawImage _monitorImage;
    [SerializeField] private Image _loadingImage;
    [SerializeField] private Sprite[] _loadingFrames = Array.Empty<Sprite>();

    [Header("商店信息")]
    [SerializeField] private TMP_Text _balanceText;
    [SerializeField] private BombShopItemView[] _pistolSlots = Array.Empty<BombShopItemView>();
    [SerializeField] private BombShopItemView[] _lightSlots = Array.Empty<BombShopItemView>();
    [SerializeField] private BombShopItemView[] _mediumSlots = Array.Empty<BombShopItemView>();
    [SerializeField] private BombShopItemView[] _heavySlots = Array.Empty<BombShopItemView>();
    [SerializeField] private BombShopItemView[] _protectionSlots = Array.Empty<BombShopItemView>();

    private readonly Dictionary<string, BombShopItemView> _boundSlots =
        new Dictionary<string, BombShopItemView>(StringComparer.Ordinal);
    private Vector2 _shownPosition;
    private Coroutine _motionRoutine;
    private Coroutine _loadingRoutine;
    public event Action<string> ItemHovered;
    public event Action ItemHoverExited;
    public event Action<string> ItemClicked;

    private void Awake()
    {
        if (_motionRoot)
        {
            _shownPosition = _motionRoot.anchoredPosition;
        }
        SetVisibleState(false);
        SubscribeSlots(_pistolSlots);
        SubscribeSlots(_lightSlots);
        SubscribeSlots(_mediumSlots);
        SubscribeSlots(_heavySlots);
        SubscribeSlots(_protectionSlots);
        EnsureEventSystem();
    }

    private void OnDestroy()
    {
        UnsubscribeSlots(_pistolSlots);
        UnsubscribeSlots(_lightSlots);
        UnsubscribeSlots(_mediumSlots);
        UnsubscribeSlots(_heavySlots);
        UnsubscribeSlots(_protectionSlots);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current)
        {
            if (!EventSystem.current.TryGetComponent(out InputSystemUIInputModule _))
            {
                EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            return;
        }

        new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
    }

    public void BindItems(
        IReadOnlyList<BombEconomyService.ShopItem> items,
        IReadOnlyDictionary<string, Sprite> sprites)
    {
        ClearSlots(_pistolSlots);
        ClearSlots(_lightSlots);
        ClearSlots(_mediumSlots);
        ClearSlots(_heavySlots);
        ClearSlots(_protectionSlots);
        _boundSlots.Clear();

        int pistolIndex = 0;
        int lightIndex = 0;
        int mediumIndex = 0;
        int heavyIndex = 0;
        int protectionIndex = 0;
        for (int i = 0; i < items.Count; i++)
        {
            BombEconomyService.ShopItem item = items[i];
            BombShopItemView[] slots = GetSlots(item.Column);
            int slotIndex = GetAndIncrementIndex(
                item.Column,
                ref pistolIndex,
                ref lightIndex,
                ref mediumIndex,
                ref heavyIndex,
                ref protectionIndex);
            if (slotIndex < 0 || slotIndex >= slots.Length || !slots[slotIndex])
            {
                continue;
            }

            sprites.TryGetValue(item.ItemId, out Sprite sprite);
            slots[slotIndex].Configure(item.ItemId, item.Name, item.Price, sprite);
            _boundSlots[item.ItemId] = slots[slotIndex];
        }
        RefreshCompactLayout();
    }

    public void SetBalance(int money)
    {
        if (_balanceText)
        {
            _balanceText.text = "$" + money;
        }
    }

    public void SetMonitorTexture(RenderTexture texture)
    {
        if (_monitorImage)
        {
            _monitorImage.texture = texture;
        }
    }

    public void Open(bool showLoading)
    {
        gameObject.SetActive(true);
        if (_motionRoutine != null)
        {
            StopCoroutine(_motionRoutine);
        }
        _motionRoutine = StartCoroutine(AnimateVisibility(true));
        StartLoading(showLoading);
    }

    public void Close(bool immediate)
    {
        StopLoading();
        if (_motionRoutine != null)
        {
            StopCoroutine(_motionRoutine);
        }
        if (immediate)
        {
            SetVisibleState(false);
            return;
        }
        _motionRoutine = StartCoroutine(AnimateVisibility(false));
    }

    private IEnumerator AnimateVisibility(bool visible)
    {
        if (_canvasGroup)
        {
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
        Vector2 startPosition = _motionRoot
            ? _motionRoot.anchoredPosition
            : Vector2.zero;
        Vector2 targetPosition = visible
            ? _shownPosition
            : _shownPosition + _hiddenOffset;
        float startAlpha = _canvasGroup ? _canvasGroup.alpha : 1f;
        float targetAlpha = visible ? 1f : 0f;
        float duration = visible ? OpenDuration : CloseDuration;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            t = 1f - Mathf.Pow(1f - t, visible ? 3f : 2f);
            if (_motionRoot)
            {
                _motionRoot.anchoredPosition = Vector2.LerpUnclamped(
                    startPosition,
                    targetPosition,
                    t);
            }
            if (_canvasGroup)
            {
                _canvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            }
            yield return null;
        }

        if (!visible)
        {
            gameObject.SetActive(false);
        }
        _motionRoutine = null;
    }

    private void StartLoading(bool showLoading)
    {
        StopLoading();
        if (_monitorImage)
        {
            _monitorImage.enabled = !showLoading;
        }
        if (_loadingImage)
        {
            _loadingImage.gameObject.SetActive(showLoading);
        }
        if (showLoading && _loadingFrames.Length > 0)
        {
            _loadingRoutine = StartCoroutine(PlayLoading());
        }
    }

    private IEnumerator PlayLoading()
    {
        float elapsed = 0f;
        while (elapsed < LoadingDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = elapsed / LoadingDuration;
            int index = Mathf.FloorToInt(
                normalized * _loadingFrames.Length * LoadingLoops) %
                _loadingFrames.Length;
            if (_loadingImage)
            {
                _loadingImage.sprite = _loadingFrames[index];
            }
            yield return null;
        }

        if (_loadingImage)
        {
            _loadingImage.gameObject.SetActive(false);
        }
        if (_monitorImage)
        {
            _monitorImage.enabled = true;
        }
        _loadingRoutine = null;
    }

    private void StopLoading()
    {
        if (_loadingRoutine != null)
        {
            StopCoroutine(_loadingRoutine);
            _loadingRoutine = null;
        }
        if (_loadingImage)
        {
            _loadingImage.gameObject.SetActive(false);
        }
        if (_monitorImage)
        {
            _monitorImage.enabled = true;
        }
    }

    private void SetVisibleState(bool visible)
    {
        if (_motionRoot)
        {
            _motionRoot.anchoredPosition = visible
                ? _shownPosition
                : _shownPosition + _hiddenOffset;
        }
        if (_canvasGroup)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.blocksRaycasts = visible;
            _canvasGroup.interactable = visible;
        }
        if (!visible)
        {
            gameObject.SetActive(false);
        }
    }

    private void SubscribeSlots(BombShopItemView[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i])
            {
                continue;
            }
            slots[i].HoverEntered += HandleHoverEntered;
            slots[i].HoverExited += HandleHoverExited;
            slots[i].Clicked += HandleClicked;
        }
    }

    private void UnsubscribeSlots(BombShopItemView[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i])
            {
                continue;
            }
            slots[i].HoverEntered -= HandleHoverEntered;
            slots[i].HoverExited -= HandleHoverExited;
            slots[i].Clicked -= HandleClicked;
        }
    }

    private void HandleHoverEntered(BombShopItemView item)
    {
        ItemHovered?.Invoke(item.ItemId);
    }

    private void HandleHoverExited(BombShopItemView item)
    {
        ItemHoverExited?.Invoke();
    }

    private void HandleClicked(BombShopItemView item)
    {
        ItemClicked?.Invoke(item.ItemId);
    }

    private static void ClearSlots(BombShopItemView[] slots)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i])
            {
                slots[i].Clear();
            }
        }
    }

    private BombShopItemView[] GetSlots(BombShopColumn column)
    {
        switch (column)
        {
            case BombShopColumn.Pistol:
                return _pistolSlots;
            case BombShopColumn.Light:
                return _lightSlots;
            case BombShopColumn.Medium:
                return _mediumSlots;
            case BombShopColumn.Heavy:
                return _heavySlots;
            case BombShopColumn.Protection:
                return _protectionSlots;
            default:
                return Array.Empty<BombShopItemView>();
        }
    }

    private static int GetAndIncrementIndex(
        BombShopColumn column,
        ref int pistol,
        ref int light,
        ref int medium,
        ref int heavy,
        ref int protection)
    {
        switch (column)
        {
            case BombShopColumn.Pistol:
                return pistol++;
            case BombShopColumn.Light:
                return light++;
            case BombShopColumn.Medium:
                return medium++;
            case BombShopColumn.Heavy:
                return heavy++;
            case BombShopColumn.Protection:
                return protection++;
            default:
                return -1;
        }
    }

    private void RefreshCompactLayout()
    {
        BombShopItemView[][] columns =
        {
            _protectionSlots,
            _pistolSlots,
            _lightSlots,
            _mediumSlots,
            _heavySlots
        };
        float y = 0f;
        RectTransform content = null;
        for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
        {
            BombShopItemView[] slots = columns[columnIndex];
            if (slots.Length == 0 || !slots[0])
            {
                continue;
            }
            RectTransform column = slots[0].transform.parent as RectTransform;
            if (!column)
            {
                continue;
            }
            if (!content)
            {
                content = column.parent as RectTransform;
            }

            int activeCount = 0;
            for (int slotIndex = 0; slotIndex < slots.Length; slotIndex++)
            {
                if (!slots[slotIndex] || !slots[slotIndex].gameObject.activeSelf)
                {
                    continue;
                }
                RectTransform item = slots[slotIndex].transform as RectTransform;
                int row = activeCount / 2;
                int side = activeCount % 2;
                item.anchorMin = new Vector2(side == 0 ? 0f : 0.5f, 1f);
                item.anchorMax = new Vector2(side == 0 ? 0.5f : 1f, 1f);
                item.anchoredPosition = new Vector2(
                    side == 0 ? 2f : -2f,
                    -38f - row * 91f);
                item.sizeDelta = new Vector2(-4f, 87f);
                activeCount++;
            }

            int rows = Mathf.Max(1, Mathf.CeilToInt(activeCount / 2f));
            float height = 42f + rows * 91f;
            column.anchoredPosition = new Vector2(0f, -y);
            column.sizeDelta = new Vector2(0f, height);
            y += height + 6f;
        }
        if (content)
        {
            content.sizeDelta = new Vector2(content.sizeDelta.x, y);
        }
    }
}
