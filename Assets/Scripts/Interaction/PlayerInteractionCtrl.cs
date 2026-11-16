using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerInteractionCtrl : MonoBehaviour
{
    private const string KeySquareResourceId = "ui.interaction.key.square";

    [Header("准心检测")]
    [SerializeField, Min(1f), InspectorName("最大检测距离")] private float _maximumDistance = 8f;
    [SerializeField, InspectorName("遮挡层")] private LayerMask _raycastLayers = ~0;

    private readonly RaycastHit[] _hits = new RaycastHit[32];

    private PlayerActionCtrl _actionCtrl;
    private FirstPersonCameraCtrl _cameraCtrl;
    private ResourceMgr _resources;
    private Camera _camera;
    private WorldInteractable _aimedTarget;
    private WorldInteractable _promptTarget;
    private Canvas _promptCanvas;
    private RectTransform _promptRoot;
    private CanvasGroup _promptGroup;
    private Image _keyImage;
    private TMP_Text _keyText;
    private TMP_Text _actionText;
    private Sprite _keySprite;
    private string _lastBindingText;
    private bool _promptCreated;

    private void Awake()
    {
        TryGetComponent(out _actionCtrl);
        _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);
        _camera = _cameraCtrl ? _cameraCtrl.ViewCamera : null;
        _resources = ResourceMgr.instance;
        if (TryGetComponent(out AIPlayerCtrl _) || !_actionCtrl || !_camera)
        {
            enabled = false;
            return;
        }
    }

    private void OnEnable()
    {
        if (_actionCtrl)
        {
            _actionCtrl.CommandReceived += HandleCommand;
        }
    }

    private void Update()
    {
        if (!_camera || !_camera.gameObject.activeInHierarchy)
        {
            SetPromptVisible(false);
            return;
        }

        EnsurePromptCreated();
        RefreshTarget();
        RefreshPrompt();
    }

    private void OnDisable()
    {
        if (_actionCtrl)
        {
            _actionCtrl.CommandReceived -= HandleCommand;
        }

        SetPromptVisible(false);
    }

    private void OnDestroy()
    {
        if (_keySprite && _resources)
        {
            _resources.Release<Sprite>(KeySquareResourceId);
        }
    }

    private void HandleCommand(PlayerInputCommand command)
    {
        if (command.Phase != PlayerInputPhase.Started || !_camera ||
            !_camera.gameObject.activeInHierarchy)
        {
            return;
        }

        // 输入到达时重新取一次准心命中，避免 Update 顺序或低帧率让缓存目标落后一帧。
        _aimedTarget = FindAimedTarget();
        if (!_aimedTarget || command.Action != _aimedTarget.InputAction)
        {
            return;
        }

        if (!IsStillValid(_aimedTarget))
        {
            return;
        }

        _aimedTarget.Execute(gameObject);
    }

    private void RefreshTarget()
    {
        _aimedTarget = FindAimedTarget();
        _promptTarget = _aimedTarget ? _aimedTarget : FindNearbyPromptTarget();
    }

    private WorldInteractable FindNearbyPromptTarget()
    {
        IReadOnlyList<WorldInteractable> items = WorldInteractable.Items;
        WorldInteractable nearest = null;
        float nearestDistance = float.PositiveInfinity;
        for (int i = 0; i < items.Count; i++)
        {
            WorldInteractable item = items[i];
            if (!IsStillValid(item))
            {
                continue;
            }

            Vector3 promptPosition = item.GetPromptPosition(_camera.transform.position);
            Vector3 viewport = _camera.WorldToViewportPoint(promptPosition);
            if (viewport.z <= 0f || viewport.x < 0f || viewport.x > 1f ||
                viewport.y < 0f || viewport.y > 1f)
            {
                continue;
            }

            float distance = item.GetDistance(transform.position);
            if (distance >= nearestDistance)
            {
                continue;
            }

            nearest = item;
            nearestDistance = distance;
        }

        return nearest;
    }

    private WorldInteractable FindAimedTarget()
    {
        Ray ray = _camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            _hits,
            _maximumDistance,
            _raycastLayers,
            QueryTriggerInteraction.Collide);
        if (hitCount <= 0)
        {
            return null;
        }

        SortHits(hitCount);
        for (int i = 0; i < hitCount; i++)
        {
            Collider collider = _hits[i].collider;
            if (!collider || collider.transform.IsChildOf(transform))
            {
                continue;
            }

            WorldInteractable interactable = collider.GetComponentInParent<WorldInteractable>();
            if (!interactable)
            {
                if (!collider.isTrigger)
                {
                    return null;
                }
                continue;
            }

            return IsStillValid(interactable) ? interactable : null;
        }

        return null;
    }

    private bool IsStillValid(WorldInteractable interactable)
    {
        return interactable && interactable.isActiveAndEnabled &&
            interactable.GetDistance(transform.position) <= interactable.InteractionRadius &&
            interactable.CanInteract(gameObject);
    }

    private void SortHits(int hitCount)
    {
        for (int i = 1; i < hitCount; i++)
        {
            RaycastHit value = _hits[i];
            int index = i - 1;
            while (index >= 0 && _hits[index].distance > value.distance)
            {
                _hits[index + 1] = _hits[index];
                index--;
            }
            _hits[index + 1] = value;
        }
    }

    private void RefreshPrompt()
    {
        if (!_promptTarget)
        {
            SetPromptVisible(false);
            return;
        }

        SetPromptVisible(true);
        Vector3 worldPosition = _promptTarget.GetPromptPosition(_camera.transform.position);
        Vector3 screenPosition = _camera.WorldToScreenPoint(worldPosition);
        RectTransform canvasRect = (RectTransform)_promptCanvas.transform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPosition,
            _camera,
            out Vector2 localPosition);
        _promptRoot.anchoredPosition = localPosition;
        _actionText.text = "[" + _promptTarget.InteractionName + "]";

        string bindingText = GetBindingText(_promptTarget.InputAction);
        if (_lastBindingText != bindingText)
        {
            _lastBindingText = bindingText;
            _keyText.text = bindingText;
        }
    }

    private string GetBindingText(GJInputAction actionId)
    {
        InputAction action = InputMgr.instance.GetAction(actionId);
        if (action == null)
        {
            return actionId.ToString();
        }

        for (int i = 0; i < action.bindings.Count; i++)
        {
            if (action.bindings[i].isComposite || action.bindings[i].isPartOfComposite)
            {
                continue;
            }

            string display = action.GetBindingDisplayString(i);
            if (!string.IsNullOrWhiteSpace(display))
            {
                return display.ToUpperInvariant();
            }
        }

        return actionId.ToString();
    }

    private void CreatePrompt()
    {
        GameObject canvasObject = new GameObject("Interaction Prompt", typeof(RectTransform));
        canvasObject.transform.SetParent(transform, false);
        _promptCanvas = canvasObject.AddComponent<Canvas>();
        _promptCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        _promptCanvas.worldCamera = _camera;
        _promptCanvas.planeDistance = Mathf.Max(_camera.nearClipPlane + 0.05f, 0.1f);
        _promptCanvas.overrideSorting = true;
        _promptCanvas.sortingOrder = -100;
        _promptGroup = canvasObject.AddComponent<CanvasGroup>();
        RectTransform canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.anchorMin = Vector2.zero;
        canvasRect.anchorMax = Vector2.one;
        canvasRect.offsetMin = Vector2.zero;
        canvasRect.offsetMax = Vector2.zero;

        GameObject promptObject = new GameObject("Prompt Content", typeof(RectTransform));
        promptObject.transform.SetParent(canvasObject.transform, false);
        _promptRoot = (RectTransform)promptObject.transform;
        _promptRoot.anchorMin = new Vector2(0.5f, 0.5f);
        _promptRoot.anchorMax = new Vector2(0.5f, 0.5f);
        _promptRoot.pivot = new Vector2(0.5f, 0.5f);
        _promptRoot.sizeDelta = new Vector2(360f, 88f);

        GameObject keyObject = new GameObject("Key", typeof(RectTransform));
        keyObject.transform.SetParent(_promptRoot, false);
        _keyImage = keyObject.AddComponent<Image>();
        _keyImage.color = new Color(0.92f, 0.94f, 0.98f, 0.96f);
        RectTransform keyRect = (RectTransform)keyObject.transform;
        keyRect.anchorMin = new Vector2(0f, 0.5f);
        keyRect.anchorMax = new Vector2(0f, 0.5f);
        keyRect.pivot = new Vector2(0f, 0.5f);
        keyRect.anchoredPosition = Vector2.zero;
        keyRect.sizeDelta = new Vector2(76f, 76f);

        _keyText = CreateText(keyObject.transform, "Key Label", 30f, TextAlignmentOptions.Center);
        RectTransform keyTextRect = (RectTransform)_keyText.transform;
        keyTextRect.anchorMin = Vector2.zero;
        keyTextRect.anchorMax = Vector2.one;
        keyTextRect.offsetMin = Vector2.zero;
        keyTextRect.offsetMax = Vector2.zero;
        _keyText.color = new Color(0.05f, 0.06f, 0.08f, 1f);
        _keyText.fontStyle = FontStyles.Bold;

        _actionText = CreateText(_promptRoot, "Action", 32f, TextAlignmentOptions.Left);
        RectTransform actionRect = (RectTransform)_actionText.transform;
        actionRect.anchorMin = new Vector2(0f, 0.5f);
        actionRect.anchorMax = new Vector2(0f, 0.5f);
        actionRect.pivot = new Vector2(0f, 0.5f);
        actionRect.anchoredPosition = new Vector2(88f, 0f);
        actionRect.sizeDelta = new Vector2(270f, 78f);
        _actionText.color = Color.white;
        _actionText.fontStyle = FontStyles.Bold;
        _actionText.outlineColor = new Color32(0, 0, 0, 220);
        _actionText.outlineWidth = 0.18f;

        SetPromptVisible(false);
        _promptCreated = true;
        _ = LoadKeySpriteAsync();
    }

    private void EnsurePromptCreated()
    {
        if (_promptCreated)
        {
            return;
        }

        CreatePrompt();
    }

    private static TMP_Text CreateText(
        Transform parent,
        string objectName,
        float fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    private async Task LoadKeySpriteAsync()
    {
        try
        {
            Sprite sprite = await _resources.LoadAsync<Sprite>(KeySquareResourceId);
            if (!this || !sprite)
            {
                if (sprite)
                {
                    _resources.Release<Sprite>(KeySquareResourceId);
                }
                return;
            }

            _keySprite = sprite;
            _keyImage.sprite = sprite;
            _keyImage.preserveAspect = true;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning("读取交互按键图失败：" + exception.Message, this);
        }
    }

    private void SetPromptVisible(bool visible)
    {
        if (!_promptGroup)
        {
            return;
        }

        _promptGroup.alpha = visible ? 1f : 0f;
        _promptGroup.interactable = false;
        _promptGroup.blocksRaycasts = false;
    }
}
