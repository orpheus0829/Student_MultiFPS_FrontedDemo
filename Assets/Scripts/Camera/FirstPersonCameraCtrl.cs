using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-20)]
public class FirstPersonCameraCtrl : MonoBehaviour
{
    private const int ThirdPersonLayer = 8;
    private const int FirstPersonLayer = 9;
    private readonly RaycastHit[] _cameraHits = new RaycastHit[16];

    [Header("输入来源")]
    [SerializeField] private PlayerInputCtrl _inputCtrl;

    [Header("相机层级")]
    [SerializeField] private Transform _yawRoot;
    [SerializeField] private Transform _pitchPivot;
    [SerializeField] private Camera _viewCamera;

    [Header("瞄准")]
    [SerializeField] private PlayerAimCtrl _aimCtrl;
    [SerializeField, Min(1f)] private float _hipFov = 80f;

    [Header("玩家数据")]
    [SerializeField] private PlayerDataSO _playerData;

    [Header("视角切换")]
    [SerializeField, InspectorName("第三人称镜头偏移")]
    private Vector3 _thirdPersonOffset = new Vector3(0.55f, 0.25f, -2.05f);
    [SerializeField, Min(0f), InspectorName("镜头避障半径")]
    private float _cameraCollisionRadius = 0.16f;
    [SerializeField, Min(0f), InspectorName("滑铲镜头降低")]
    private float _slideCameraDrop = 0.48f;
    [SerializeField, Min(0f), InspectorName("滑铲镜头过渡速度")]
    private float _slideCameraSpeed = 14f;

    private EventMgr.Subscription _inputSubscription;
    private FirstPersonCameraMotionCtrl _motionCtrl;
    private PlayerMoveCtrl _moveCtrl;
    private PlayerActionCtrl _actionCtrl;
    private Vector3 _pitchPivotBasePosition;
    private int _firstPersonCullingMask;
    private bool _isThirdPerson;
    private Vector2 _lookInput;
    private float _yaw;
    private float _pitch;
    private bool _wasControlled;
    private bool _hasCursorControl;

    public bool IsThirdPerson => _isThirdPerson;
    public event Action<bool> ViewChanged;

    private void Awake()
    {
        if (!_yawRoot)
        {
            _yawRoot = transform.root;
        }

        if (!_pitchPivot)
        {
            _pitchPivot = transform;
        }

        if (!_viewCamera)
        {
            _viewCamera = GetComponentInChildren<Camera>(true);
        }

        if (!_aimCtrl)
        {
            _aimCtrl = GetComponentInParent<PlayerAimCtrl>();
        }

        _motionCtrl = GetComponent<FirstPersonCameraMotionCtrl>();
        _moveCtrl = GetComponentInParent<PlayerMoveCtrl>();
        _actionCtrl = GetComponentInParent<PlayerActionCtrl>();
        _pitchPivotBasePosition = _pitchPivot.localPosition;
        if (_viewCamera)
        {
            _firstPersonCullingMask = _viewCamera.cullingMask;
            _firstPersonCullingMask &= ~(1 << ThirdPersonLayer);
            _firstPersonCullingMask |= 1 << FirstPersonLayer;
        }

        PlayerAnimationCtrl animationCtrl = GetComponentInParent<PlayerAnimationCtrl>();
        if (animationCtrl)
        {
            foreach (PlayerAnimationView view in animationCtrl.GetComponentsInChildren<PlayerAnimationView>(true))
            {
                SetLayerRecursively(view.transform, view.ViewType == PlayerViewType.FirstPerson
                    ? FirstPersonLayer : ThirdPersonLayer);
            }
        }

        SyncAngles();
        if (!_inputCtrl)
        {
            Debug.LogError($"{name} 未配置玩家输入来源。", this);
            enabled = false;
            return;
        }

    }

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _hasCursorControl = true;
        }
        if (_inputCtrl)
        {
            _inputSubscription = EventMgr.Bind(this, StartListening, StopListening);
        }
        if (_actionCtrl)
        {
            _actionCtrl.FireShot += FaceAimDirection;
        }
    }

    private void OnDisable()
    {
        if (_hasCursorControl)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _hasCursorControl = false;
        }
        if (_inputSubscription != null)
        {
            _inputSubscription.Dispose();
            _inputSubscription = null;
        }
        if (_actionCtrl)
        {
            _actionCtrl.FireShot -= FaceAimDirection;
        }

        _lookInput = Vector2.zero;
        _motionCtrl?.SetViewOffset(Vector3.zero);
        _isThirdPerson = false;
        if (_viewCamera)
        {
            _viewCamera.cullingMask = _firstPersonCullingMask;
        }
    }

    private void LateUpdate()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Cursor.lockState != CursorLockMode.Locked &&
            Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        if (CameraControlAuthority.IsControlled)
        {
            _wasControlled = true;
            _lookInput = Vector2.zero;
            return;
        }

        if (_wasControlled)
        {
            _wasControlled = false;
            SyncAngles();
        }

        float sensitivity = _playerData ? _playerData.LookSensitivity : 0.1f;
        if (_aimCtrl)
        {
            sensitivity *= _aimCtrl.LookSensitivityMultiplier;
        }
        float minPitch = _playerData ? _playerData.MinPitch : -85f;
        float maxPitch = _playerData ? _playerData.MaxPitch : 85f;

        _yaw += _lookInput.x * sensitivity;
        _pitch = Mathf.Clamp(_pitch - _lookInput.y * sensitivity, minPitch, maxPitch);
        if (!_isThirdPerson || (_aimCtrl && _aimCtrl.IsAimRequested) ||
            (_actionCtrl && _actionCtrl.IsFiring))
        {
            FaceAimDirection();
        }
        else
        {
            float relativeYaw = Mathf.DeltaAngle(_yawRoot.localEulerAngles.y, _yaw);
            _pitchPivot.localRotation = Quaternion.Euler(_pitch, relativeYaw, 0f);
        }
        _lookInput = Vector2.zero;

        RefreshFieldOfView();
        RefreshCameraHeight();
        RefreshViewOffset();
    }

    public Ray GetCenterAimRay()
    {
        if (_viewCamera)
        {
            return _viewCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        }

        return new Ray(_pitchPivot.position, _pitchPivot.forward);
    }

    public Vector3 GetPlanarLookDirection()
    {
        Vector3 direction = Vector3.ProjectOnPlane(_pitchPivot.forward, Vector3.up);
        return direction.sqrMagnitude > 0.001f ? direction.normalized : _yawRoot.forward;
    }

    public bool IsFacingAimDirection()
    {
        return _isThirdPerson &&
            ((_aimCtrl && _aimCtrl.IsAimRequested) ||
             (_actionCtrl && _actionCtrl.IsFiring));
    }

    public void KeepThirdPersonLookDirection()
    {
        if (!_isThirdPerson || !_pitchPivot)
        {
            return;
        }

        float relativeYaw = Mathf.DeltaAngle(_yawRoot.localEulerAngles.y, _yaw);
        _pitchPivot.localRotation = Quaternion.Euler(_pitch, relativeYaw, 0f);
    }

    private void Receive(PlayerInputCommand command)
    {
        if (command.Action == GJInputAction.Look)
        {
            _lookInput = command.Value;
        }
        else if (command.Action == GJInputAction.ToggleView &&
            command.Phase == PlayerInputPhase.Started)
        {
            SetThirdPerson(!_isThirdPerson);
        }
    }

    public void SetThirdPerson(bool enabledView)
    {
        if (_isThirdPerson == enabledView)
        {
            return;
        }

        _isThirdPerson = enabledView;
        if (!enabledView)
        {
            FaceAimDirection();
        }
        if (_viewCamera)
        {
            _viewCamera.cullingMask = enabledView
                ? (_firstPersonCullingMask | 1 << ThirdPersonLayer) & ~(1 << FirstPersonLayer)
                : _firstPersonCullingMask;
        }
        RefreshViewOffset();
        ViewChanged?.Invoke(enabledView);
    }

    private void RefreshCameraHeight()
    {
        if (!_pitchPivot)
        {
            return;
        }

        float drop = _moveCtrl && _moveCtrl.IsSliding ? _slideCameraDrop : 0f;
        Vector3 target = _pitchPivotBasePosition + Vector3.down * drop;
        float weight = 1f - Mathf.Exp(-_slideCameraSpeed * Time.deltaTime);
        _pitchPivot.localPosition = Vector3.Lerp(_pitchPivot.localPosition, target, weight);
    }

    private void RefreshViewOffset()
    {
        if (!_motionCtrl)
        {
            return;
        }

        if (!_isThirdPerson)
        {
            _motionCtrl.SetViewOffset(Vector3.zero);
            return;
        }

        Vector3 offset = _thirdPersonOffset;
        Vector3 worldOffset = _pitchPivot.TransformDirection(offset);
        float distance = worldOffset.magnitude;
        if (distance > 0.01f && _cameraCollisionRadius > 0f)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                _pitchPivot.position,
                _cameraCollisionRadius,
                worldOffset / distance,
                _cameraHits,
                distance,
                ~(1 << ThirdPersonLayer | 1 << FirstPersonLayer),
                QueryTriggerInteraction.Ignore);
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _cameraHits[index];
                if (hit.transform.IsChildOf(_yawRoot))
                {
                    continue;
                }
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.08f));
            }
            offset *= distance / worldOffset.magnitude;
        }
        _motionCtrl.SetViewOffset(offset);
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int index = 0; index < root.childCount; index++)
        {
            SetLayerRecursively(root.GetChild(index), layer);
        }
    }

    private void SyncAngles()
    {
        _yaw = NormalizeAngle(_yawRoot.localEulerAngles.y +
            (_isThirdPerson ? _pitchPivot.localEulerAngles.y : 0f));
        _pitch = NormalizeAngle(_pitchPivot.localEulerAngles.x);
    }

    public void FaceAimDirection()
    {
        if (!_yawRoot || !_pitchPivot)
        {
            return;
        }

        _yawRoot.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        _pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
    }

    private void RefreshFieldOfView()
    {
        if (!_viewCamera)
        {
            return;
        }

        float aimProgress = _aimCtrl ? _aimCtrl.AimProgress : 0f;
        float aimFov = _aimCtrl ? _aimCtrl.AimFov : _hipFov;
        _viewCamera.fieldOfView = Mathf.Lerp(_hipFov, aimFov, aimProgress);
    }

    private float NormalizeAngle(float angle)
    {
        return angle > 180f ? angle - 360f : angle;
    }

    private void StartListening()
    {
        _inputCtrl.CommandIssued += Receive;
    }

    private void StopListening()
    {
        _inputCtrl.CommandIssued -= Receive;
    }

    private void OnValidate()
    {
        _hipFov = Mathf.Max(1f, _hipFov);
        _cameraCollisionRadius = Mathf.Max(0f, _cameraCollisionRadius);
        _slideCameraDrop = Mathf.Max(0f, _slideCameraDrop);
        _slideCameraSpeed = Mathf.Max(0f, _slideCameraSpeed);
    }
}
