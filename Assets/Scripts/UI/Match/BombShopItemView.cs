using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class BombShopItemView : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler,
    IPointerClickHandler
{
    [Header("条目显示")]
    [SerializeField] private Graphic _background;
    [SerializeField] private Image _silhouette;
    [SerializeField] private TMP_Text _nameText;
    [SerializeField] private TMP_Text _priceText;
    [SerializeField] private Outline _hoverGlow;

    private string _itemId;
    private bool _isHovered;
    private bool _isSelected;
    private int _lastClickFrame = -1;

    public event Action<BombShopItemView> HoverEntered;
    public event Action<BombShopItemView> HoverExited;
    public event Action<BombShopItemView> Clicked;

    public string ItemId
    {
        get { return _itemId; }
    }

    public void Configure(
        string itemId,
        string displayName,
        int price,
        Sprite silhouette)
    {
        _itemId = itemId;
        if (_nameText)
        {
            _nameText.text = displayName;
        }
        if (_priceText)
        {
            _priceText.text = "$" + price;
        }
        if (_silhouette)
        {
            _silhouette.sprite = silhouette;
            _silhouette.enabled = silhouette;
        }
        _isSelected = false;
        SetHovered(false);
        gameObject.SetActive(true);
    }

    public void Clear()
    {
        _itemId = string.Empty;
        _isSelected = false;
        SetHovered(false);
        gameObject.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        SetHoveredExternally(true);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        SetHoveredExternally(false);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        InvokeClickOnce();
    }

    public void SetHoveredExternally(bool isHovered)
    {
        if (_isHovered == isHovered)
        {
            return;
        }
        _isHovered = isHovered;
        RefreshHoverVisual();
        if (isHovered)
        {
            HoverEntered?.Invoke(this);
        }
        else
        {
            HoverExited?.Invoke(this);
        }
    }

    public void ClickExternally()
    {
        InvokeClickOnce();
    }

    public void SetSelected(bool isSelected)
    {
        _isSelected = isSelected;
        RefreshHoverVisual();
    }

    private void InvokeClickOnce()
    {
        if (_lastClickFrame == Time.frameCount)
        {
            return;
        }
        _lastClickFrame = Time.frameCount;
        Clicked?.Invoke(this);
    }

    private void SetHovered(bool isHovered)
    {
        _isHovered = isHovered;
        RefreshHoverVisual();
    }

    private void RefreshHoverVisual()
    {
        bool highlighted = _isHovered || _isSelected;
        if (_hoverGlow)
        {
            _hoverGlow.enabled = highlighted;
        }
        if (_background)
        {
            _background.color = highlighted
                ? new Color(0.25f, 0.29f, 0.34f, 0.96f)
                : new Color(0.075f, 0.085f, 0.105f, 0.92f);
        }
    }
}
