using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WordNoteStackRegionUI : MonoBehaviour
{
    private enum ItemState
    {
        Entering,
        Showing,
        Exiting
    }

    private sealed class ActiveItem
    {
        public WordNoteRequest Request;
        public WordNoteItemUI View;
        public ItemState State;
        public float AnimationTime;
        public Vector2 CurrentOffset;
        public float CurrentAlpha;
        public Vector2 ExitStartOffset;
        public float ExitStartAlpha;
    }

    [Header("区域")]
    [SerializeField, InspectorName("所属区域")] private WordNoteRegion _region = WordNoteRegion.TopRight;
    [SerializeField, InspectorName("消息容器")] private RectTransform _contentRoot;
    [SerializeField, InspectorName("消息预制体")] private WordNoteItemUI _itemPrefab;
    [SerializeField, InspectorName("最多显示数量"), Min(1)] private int _capacity = 5;

    [Header("动画")]
    [SerializeField, InspectorName("进入偏移")] private Vector2 _enterOffset = new Vector2(-160f, 0f);
    [SerializeField, InspectorName("离开偏移")] private Vector2 _exitOffset = new Vector2(160f, 0f);
    [SerializeField, InspectorName("进入时间"), Min(0f)] private float _enterDuration = 0.2f;
    [SerializeField, InspectorName("离开时间"), Min(0f)] private float _exitDuration = 0.2f;

    private readonly List<ActiveItem> _items = new List<ActiveItem>();

    public WordNoteRegion Region
    {
        get
        {
            return _region;
        }
    }

    public bool IsReady
    {
        get
        {
            return _contentRoot && _itemPrefab;
        }
    }

    private void Update()
    {
        float deltaTime = Time.unscaledDeltaTime;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            ActiveItem item = _items[i];
            if (item == null || !item.View)
            {
                _items.RemoveAt(i);
                continue;
            }

            switch (item.State)
            {
                case ItemState.Entering:
                    UpdateEnter(item, deltaTime);
                    break;
                case ItemState.Showing:
                    UpdateShowing(item, deltaTime);
                    break;
                case ItemState.Exiting:
                    UpdateExit(item, deltaTime);
                    break;
            }
        }
    }

    private void OnDisable()
    {
        Clear();
    }

    internal bool Add(WordNoteRequest request)
    {
        if (request == null || !IsReady)
        {
            return false;
        }

        WordNoteItemUI view = Instantiate(_itemPrefab, _contentRoot, false);
        view.gameObject.SetActive(true);
        if (!view.Prepare(request.Content, request.Style))
        {
            Destroy(view.gameObject);
            return false;
        }

        view.transform.SetAsFirstSibling();
        view.SetOffset(_enterOffset);
        ActiveItem item = new ActiveItem
        {
            Request = request,
            View = view,
            State = ItemState.Entering,
            AnimationTime = 0f,
            CurrentOffset = _enterOffset,
            CurrentAlpha = 0f
        };
        _items.Insert(0, item);

        if (_enterDuration <= 0f)
        {
            CompleteEnter(item);
        }

        EnforceCapacity();
        return true;
    }

    internal bool Contains(int id)
    {
        return FindItem(id) != null;
    }

    internal bool Close(int id)
    {
        ActiveItem item = FindItem(id);
        if (item == null)
        {
            return false;
        }

        StartExit(item);
        return true;
    }

    internal void Clear()
    {
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            ActiveItem item = _items[i];
            if (item != null && item.View)
            {
                Destroy(item.View.gameObject);
            }
        }

        _items.Clear();
    }

    private void UpdateEnter(ActiveItem item, float deltaTime)
    {
        item.AnimationTime += deltaTime;
        float progress = Mathf.Clamp01(item.AnimationTime / _enterDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        item.CurrentOffset = Vector2.LerpUnclamped(
            _enterOffset,
            Vector2.zero,
            easedProgress);
        item.CurrentAlpha = easedProgress;
        item.View.SetOffset(item.CurrentOffset);
        item.View.SetAlpha(item.CurrentAlpha);
        if (progress >= 1f)
        {
            CompleteEnter(item);
        }
    }

    private void CompleteEnter(ActiveItem item)
    {
        item.View.SetOffset(Vector2.zero);
        item.View.SetAlpha(1f);
        item.CurrentOffset = Vector2.zero;
        item.CurrentAlpha = 1f;
        item.Request.Remaining = item.Request.Duration;
        item.State = ItemState.Showing;
    }

    private void UpdateShowing(ActiveItem item, float deltaTime)
    {
        if (item.Request.Duration <= 0f)
        {
            return;
        }

        item.Request.Remaining -= deltaTime;
        if (item.Request.Remaining <= 0f)
        {
            StartExit(item);
        }
    }

    private void StartExit(ActiveItem item)
    {
        if (item.State == ItemState.Exiting)
        {
            return;
        }

        item.State = ItemState.Exiting;
        item.AnimationTime = 0f;
        item.ExitStartOffset = item.CurrentOffset;
        item.ExitStartAlpha = item.CurrentAlpha;
        if (_exitDuration <= 0f)
        {
            CompleteExit(item);
        }
    }

    private void UpdateExit(ActiveItem item, float deltaTime)
    {
        item.AnimationTime += deltaTime;
        float progress = Mathf.Clamp01(item.AnimationTime / _exitDuration);
        float easedProgress = Mathf.SmoothStep(0f, 1f, progress);
        item.CurrentOffset = Vector2.LerpUnclamped(
            item.ExitStartOffset,
            _exitOffset,
            easedProgress);
        item.CurrentAlpha = Mathf.LerpUnclamped(
            item.ExitStartAlpha,
            0f,
            easedProgress);
        item.View.SetOffset(item.CurrentOffset);
        item.View.SetAlpha(item.CurrentAlpha);
        if (progress >= 1f)
        {
            CompleteExit(item);
        }
    }

    private void CompleteExit(ActiveItem item)
    {
        _items.Remove(item);
        if (item.View)
        {
            Destroy(item.View.gameObject);
        }
    }

    private void EnforceCapacity()
    {
        int visibleCount = 0;
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].State != ItemState.Exiting)
            {
                visibleCount++;
            }
        }

        for (int i = _items.Count - 1; i >= 0 && visibleCount > _capacity; i--)
        {
            ActiveItem item = _items[i];
            if (item.State == ItemState.Exiting)
            {
                continue;
            }

            StartExit(item);
            visibleCount--;
        }
    }

    private ActiveItem FindItem(int id)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            ActiveItem item = _items[i];
            if (item.Request != null && item.Request.Id == id)
            {
                return item;
            }
        }

        return null;
    }
}
