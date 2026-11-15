using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldInteractable : MonoBehaviour
{
    private static readonly List<WorldInteractable> AllItems = new List<WorldInteractable>();

    [Header("交互内容")]
    [SerializeField, InspectorName("交互名称")] private string _interactionName = "交互";
    [SerializeField, InspectorName("交互按键")] private GJInputAction _inputAction = GJInputAction.Interact;
    [SerializeField, InspectorName("交互行为")] private MonoBehaviour _interactionBehaviour;

    [Header("交互范围")]
    [SerializeField, Min(0.1f), InspectorName("可行范围半径")] private float _interactionRadius = 3f;
    [SerializeField, InspectorName("提示锚点（可选）")] private Transform _promptAnchor;

    private Collider _collider;
    private Renderer _renderer;
    private IWorldInteraction _interaction;

    public static IReadOnlyList<WorldInteractable> Items
    {
        get
        {
            return AllItems;
        }
    }

    public string InteractionName
    {
        get
        {
            return _interactionName;
        }
    }

    public GJInputAction InputAction
    {
        get
        {
            return _inputAction;
        }
    }

    public float InteractionRadius
    {
        get
        {
            return _interactionRadius;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        AllItems.Clear();
    }

    private void Awake()
    {
        TryGetComponent(out _collider);
        TryGetComponent(out _renderer);
        ResolveInteraction();
    }

    private void OnEnable()
    {
        if (!AllItems.Contains(this))
        {
            AllItems.Add(this);
        }
    }

    private void OnDisable()
    {
        AllItems.Remove(this);
    }

    public bool CanInteract(GameObject interactor)
    {
        if (_interaction == null)
        {
            ResolveInteraction();
        }

        return _interaction != null && _interaction.CanInteract(interactor);
    }

    public void Execute(GameObject interactor)
    {
        if (!CanInteract(interactor))
        {
            return;
        }

        _interaction.Interact(interactor);
    }

    public void Configure(
        string interactionName,
        GJInputAction inputAction,
        MonoBehaviour interactionBehaviour,
        float interactionRadius,
        Transform promptAnchor = null)
    {
        _interactionName = string.IsNullOrWhiteSpace(interactionName)
            ? "交互"
            : interactionName.Trim();
        _inputAction = inputAction;
        _interactionBehaviour = interactionBehaviour;
        _interactionRadius = Mathf.Max(0.1f, interactionRadius);
        _promptAnchor = promptAnchor;
        ResolveInteraction();
    }

    public Vector3 GetPromptPosition(Vector3 viewerPosition)
    {
        if (_promptAnchor)
        {
            return _promptAnchor.position;
        }

        Bounds bounds = GetBounds();
        Vector3 center = bounds.center;
        Vector3 direction = center - viewerPosition;
        if (direction.sqrMagnitude < 0.0001f)
        {
            return center;
        }

        Ray ray = new Ray(viewerPosition, direction.normalized);
        if (bounds.IntersectRay(ray, out float distance))
        {
            return ray.GetPoint(distance) - direction.normalized * 0.025f;
        }

        return center;
    }

    public float GetDistance(Vector3 position)
    {
        if (_collider)
        {
            if (_collider is MeshCollider meshCollider && !meshCollider.convex)
            {
                return Vector3.Distance(position, _collider.bounds.ClosestPoint(position));
            }

            return Vector3.Distance(position, _collider.ClosestPoint(position));
        }

        return Vector3.Distance(position, GetBounds().center);
    }

    public bool OwnsCollider(Collider target)
    {
        return target && (target.transform == transform || target.transform.IsChildOf(transform));
    }

    private Bounds GetBounds()
    {
        if (_collider)
        {
            return _collider.bounds;
        }

        if (_renderer)
        {
            return _renderer.bounds;
        }

        return new Bounds(transform.position, Vector3.one * 0.2f);
    }

    private void ResolveInteraction()
    {
        _interaction = _interactionBehaviour as IWorldInteraction;
        if (_interaction != null)
        {
            return;
        }

        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IWorldInteraction interaction)
            {
                _interactionBehaviour = behaviours[i];
                _interaction = interaction;
                return;
            }
        }

        Debug.LogError($"{name} 的交互激发脚本没有引用实现 IWorldInteraction 的行为。", this);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        _interactionRadius = Mathf.Max(0.1f, _interactionRadius);
        if (_interactionBehaviour && !(_interactionBehaviour is IWorldInteraction))
        {
            Debug.LogError($"{name} 的交互行为必须实现 IWorldInteraction。", this);
            _interactionBehaviour = null;
        }
    }
#endif
}
