using UnityEngine;

[DisallowMultipleComponent]
public sealed class DoorRotationInteraction : MonoBehaviour, IWorldInteraction
{
    [Header("门体旋转")]
    [SerializeField, InspectorName("旋转门体（留空为自身）")] private Transform _doorBody;
    [SerializeField, InspectorName("打开角度")] private float _openAngle = 95f;
    [SerializeField, Min(0.01f), InspectorName("旋转耗时")] private float _rotationDuration = 0.32f;

    private Quaternion _closedRotation;
    private Quaternion _openedRotation;
    private Quaternion _startRotation;
    private float _rotationProgress = 1f;
    private bool _isOpen;

    private void Awake()
    {
        if (!_doorBody)
        {
            _doorBody = transform;
        }

        _closedRotation = _doorBody.localRotation;
        _openedRotation = _closedRotation * Quaternion.Euler(0f, _openAngle, 0f);
    }

    private void Update()
    {
        if (_rotationProgress >= 1f)
        {
            return;
        }

        _rotationProgress = Mathf.Min(
            1f,
            _rotationProgress + Time.deltaTime / Mathf.Max(0.01f, _rotationDuration));
        float easedProgress = Mathf.SmoothStep(0f, 1f, _rotationProgress);
        Quaternion targetRotation = _isOpen ? _openedRotation : _closedRotation;
        _doorBody.localRotation = Quaternion.Slerp(
            _startRotation,
            targetRotation,
            easedProgress);
    }

    public bool CanInteract(GameObject interactor)
    {
        return enabled && _doorBody;
    }

    public void Interact(GameObject interactor)
    {
        _isOpen = !_isOpen;
        _startRotation = _doorBody.localRotation;
        _rotationProgress = 0f;
    }
}
