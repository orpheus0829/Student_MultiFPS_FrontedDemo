using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerMoveCtrl : MonoBehaviour
{
    private const float InputThreshold = 0.1f;
    private const int ClearanceBufferSize = 16;
    private const float VaultCompletionGrace = 0.2f;
    private const float AirSlideInputBuffer = 1.2f;

    [Header("角色移动")]
    [SerializeField, InspectorName("角色控制器")] private CharacterController _characterController;

    [Header("玩家数据")]
    [SerializeField, InspectorName("玩家基础数据")] private PlayerDataSO _playerData;
    [SerializeField, InspectorName("角色动作集")] private CharacterActionSetSO _actionSet;

    [Header("空中疾跑惯性")]
    [SerializeField, Min(0f), InspectorName("地面加速")] private float _groundAcceleration = 32f;
    [SerializeField, Min(0f), InspectorName("地面减速")] private float _groundDeceleration = 42f;
    [SerializeField, Min(0f), InspectorName("空中转向加速")] private float _airAcceleration = 9f;
    [SerializeField, Min(0f), InspectorName("空中松键减速")] private float _airDeceleration = 3f;
    [SerializeField, Min(0f), InspectorName("疾跑取消后空中保速时间")]
    private float _airSprintCarryDuration = 0.42f;
    [SerializeField, Range(0f, 1f), InspectorName("空中保速占比")]
    private float _airSprintCarryHoldFraction = 0.55f;
    [SerializeField, Min(0f), InspectorName("第三人称移动转身速度")]
    private float _thirdPersonTurnSpeed = 720f;

    private readonly Collider[] _clearanceBuffer = new Collider[ClearanceBufferSize];

    private Vector2 _moveInput;
    private Vector3 _localVelocity;
    private Vector3 _horizontalVelocity;
    private Vector3 _airSprintCarryVelocity;
    private FirstPersonCameraCtrl _cameraCtrl;
    private PlayerAimCtrl _aimCtrl;
    private Vector3 _queuedDisplacement;
    private Vector3 _vaultStartPosition;
    private Vector3 _vaultTargetPosition;
    private Vector3 _slideDirection;
    private bool _sprintRequested;
    private bool _isSprinting;
    private bool _isVaulting;
    private bool _isSliding;
    private bool _isGrounded;
    private bool _airSlideEligible;
    private float _verticalSpeed;
    private float _vaultElapsed;
    private float _slideElapsed;
    private float _standingHeight;
    private Vector3 _standingCenter;
    private float _externalSpeedMultiplier = 1f;
    private float _airSprintCarryRemaining;
    private float _queuedSlideRemaining;

    public event Action VaultStarted;
    public event Action VaultFinished;
    public event Action SlideStarted;
    public event Action SlideFinished;

    public PlayerDataSO PlayerData
    {
        get
        {
            return _playerData;
        }
    }

    public CharacterActionSetSO ActionSet
    {
        get
        {
            return _actionSet;
        }
    }

    public Vector2 MoveInput
    {
        get
        {
            return _moveInput;
        }
    }

    public Vector3 LocalMoveDirection
    {
        get
        {
            return new Vector3(_moveInput.x, 0f, _moveInput.y);
        }
    }

    public Vector3 LocalVelocity
    {
        get
        {
            return _localVelocity;
        }
    }

    public bool IsMoving
    {
        get
        {
            Vector2 horizontalVelocity = new Vector2(_localVelocity.x, _localVelocity.z);
            return horizontalVelocity.sqrMagnitude > InputThreshold * InputThreshold;
        }
    }

    public bool IsSprinting
    {
        get
        {
            return _isSprinting;
        }
    }

    public bool IsVaulting
    {
        get
        {
            return _isVaulting;
        }
    }

    public bool IsSliding
    {
        get
        {
            return _isSliding;
        }
    }

    public bool IsOperationLocked
    {
        get
        {
            return _isVaulting;
        }
    }

    public bool IsGrounded
    {
        get
        {
            return _isGrounded;
        }
    }

    public bool IsJumping
    {
        get
        {
            return !_isGrounded && !_isVaulting && _verticalSpeed > 0f;
        }
    }

    public bool IsFalling
    {
        get
        {
            return !_isGrounded && !_isVaulting && _verticalSpeed <= 0f;
        }
    }

    public bool HasFirstPersonForwardMove
    {
        get
        {
            return !_isVaulting && _moveInput.y > InputThreshold;
        }
    }

    public float VerticalSpeed
    {
        get
        {
            return _verticalSpeed;
        }
    }

    public float ExternalSpeedMultiplier
    {
        get
        {
            return _externalSpeedMultiplier;
        }
    }

    private void Awake()
    {
        _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);
        TryGetComponent(out _aimCtrl);
        if (!_characterController)
        {
            TryGetComponent(out _characterController);
        }
        if (_characterController)
        {
            _standingHeight = _characterController.height;
            _standingCenter = _characterController.center;
        }
    }

    private void Start()
    {
        if (_characterController && _playerData && _actionSet)
        {
            return;
        }

        Debug.LogError($"{name} 的玩家移动引用未配置完整。", this);
        enabled = false;
    }

    private void Update()
    {
        if (_isVaulting)
        {
            UpdateVault();
            return;
        }
        if (_isSliding)
        {
            UpdateSlide();
            return;
        }

        UpdateMovement();
    }

    private void OnDisable()
    {
        ResetMotion();
    }

    public void SetMoveInput(Vector2 moveInput)
    {
        _moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        RefreshSprintState();
    }

    public void SetSprintInput(bool isPressed)
    {
        _sprintRequested = isPressed;
        RefreshSprintState();
    }

    public bool TryJumpOrVault()
    {
        if (!_characterController || !_playerData || _isVaulting || _isSliding ||
            !_characterController.isGrounded)
        {
            return false;
        }

        if (TryStartVault())
        {
            return true;
        }

        _verticalSpeed = Mathf.Sqrt(_playerData.JumpHeight * -2f * _playerData.Gravity);
        _airSlideEligible = _isSprinting || _airSprintCarryRemaining > 0f;
        _isGrounded = false;
        return true;
    }

    public bool TryStartSlide()
    {
        if (!_characterController || !_actionSet || _isVaulting || _isSliding)
        {
            return false;
        }

        if (!_characterController.isGrounded || _verticalSpeed > 0f)
        {
            if (!_airSlideEligible)
            {
                return false;
            }

            _queuedSlideRemaining = AirSlideInputBuffer;
            return true;
        }

        return StartSlide(_isSprinting);
    }

    private bool StartSlide(bool hasSprintMomentum)
    {
        if (!hasSprintMomentum)
        {
            return false;
        }

        _isSliding = true;
        _isSprinting = false;
        _airSlideEligible = false;
        _queuedSlideRemaining = 0f;
        _slideElapsed = 0f;
        _slideDirection = _horizontalVelocity.sqrMagnitude > 0.25f
            ? _horizontalVelocity.normalized
            : (transform.right * _moveInput.x + transform.forward * _moveInput.y).normalized;
        if (_slideDirection.sqrMagnitude < 0.01f)
        {
            _slideDirection = transform.forward;
        }
        SetSlideHeight();
        SlideStarted?.Invoke();
        return true;
    }

    public void AddFrameDisplacement(Vector3 worldDisplacement)
    {
        if (_isVaulting)
        {
            return;
        }

        _queuedDisplacement += worldDisplacement;
    }

    public void SetExternalSpeedMultiplier(float multiplier)
    {
        _externalSpeedMultiplier = Mathf.Max(0f, multiplier);
    }

    public void ResetInput()
    {
        _moveInput = Vector2.zero;
        _sprintRequested = false;
        _isSprinting = false;
        _airSprintCarryRemaining = 0f;
        _airSlideEligible = false;
        _queuedSlideRemaining = 0f;
    }

    private void UpdateMovement()
    {
        _isGrounded = _characterController.isGrounded;
        if (_isGrounded && _verticalSpeed < 0f)
        {
            _verticalSpeed = _playerData.GroundedForce;
        }

        RefreshSprintState();
        if (!_isGrounded && _isSprinting)
        {
            _airSlideEligible = true;
        }
        if (_queuedSlideRemaining > 0f)
        {
            _queuedSlideRemaining = Mathf.Max(0f, _queuedSlideRemaining - Time.deltaTime);
        }

        Vector3 horizontalDirection = GetWorldMoveDirection();
        if (horizontalDirection.sqrMagnitude > 1f)
        {
            horizontalDirection.Normalize();
        }
        UpdateThirdPersonFacing(horizontalDirection);

        float moveSpeed = _isSprinting
            ? _playerData.SprintSpeed
            : _playerData.MoveSpeed;
        moveSpeed *= _externalSpeedMultiplier;
        _verticalSpeed += _playerData.Gravity * Time.deltaTime;

        Vector3 desiredVelocity = horizontalDirection * moveSpeed;
        bool airborne = !_isGrounded || _verticalSpeed > 0f;
        if (!airborne)
        {
            _airSprintCarryRemaining = 0f;
        }
        else if (_airSprintCarryRemaining > 0f)
        {
            _airSprintCarryRemaining = Mathf.Max(
                0f,
                _airSprintCarryRemaining - Time.deltaTime);
            float elapsed = _airSprintCarryDuration - _airSprintCarryRemaining;
            float holdTime = _airSprintCarryDuration * _airSprintCarryHoldFraction;
            float releaseProgress = Mathf.Clamp01(
                (elapsed - holdTime) / Mathf.Max(0.001f, _airSprintCarryDuration - holdTime));
            desiredVelocity = Vector3.Lerp(
                _airSprintCarryVelocity,
                desiredVelocity,
                Mathf.SmoothStep(0f, 1f, releaseProgress));
        }

        float acceleration = airborne
            ? (horizontalDirection.sqrMagnitude > 0f ? _airAcceleration : _airDeceleration)
            : (horizontalDirection.sqrMagnitude > 0f ? _groundAcceleration : _groundDeceleration);
        _horizontalVelocity = Vector3.MoveTowards(
            _horizontalVelocity,
            desiredVelocity,
            acceleration * Time.deltaTime);

        Vector3 displacement = _horizontalVelocity * Time.deltaTime;
        displacement += Vector3.up * _verticalSpeed * Time.deltaTime;
        displacement += ConsumeQueuedDisplacement();
        MoveCharacter(displacement);

        _isGrounded = _characterController.isGrounded;
        if (_isGrounded)
        {
            bool startBufferedSlide = _queuedSlideRemaining > 0f && _airSlideEligible;
            _airSlideEligible = false;
            _queuedSlideRemaining = 0f;
            if (startBufferedSlide)
            {
                StartSlide(true);
            }
        }
    }

    private void UpdateVault()
    {
        _queuedDisplacement = Vector3.zero;
        _vaultElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_vaultElapsed / _actionSet.VaultDuration);
        Vector3 targetPosition = _actionSet.EvaluateVaultPosition(
            _vaultStartPosition,
            _vaultTargetPosition,
            progress);
        float pathLength = Vector3.Distance(_vaultStartPosition, _vaultTargetPosition) +
            _playerData.VaultHighHeight;
        float maxStep = Mathf.Max(1.5f, pathLength / _actionSet.VaultDuration * 1.8f) *
            Time.deltaTime;
        MoveCharacter(Vector3.ClampMagnitude(targetPosition - transform.position, maxStep));

        if (progress < 1f ||
            (Vector3.Distance(transform.position, _vaultTargetPosition) > 0.08f &&
             _vaultElapsed < _actionSet.VaultDuration + VaultCompletionGrace))
        {
            return;
        }

        FinishVault();
    }

    private void UpdateSlide()
    {
        _slideElapsed += Time.deltaTime;
        float progress = Mathf.Clamp01(_slideElapsed / _actionSet.SlideDuration);
        float speed = _actionSet.EvaluateSlideSpeed(progress) * _externalSpeedMultiplier;
        _verticalSpeed += _playerData.Gravity * Time.deltaTime;
        Vector3 displacement = _slideDirection * speed * Time.deltaTime;
        displacement += Vector3.up * _verticalSpeed * Time.deltaTime;
        MoveCharacter(displacement);
        _isGrounded = _characterController.isGrounded;

        if (progress >= 1f || !_isGrounded)
        {
            FinishSlide();
        }
    }

    private void SetSlideHeight()
    {
        float height = _standingHeight * _actionSet.SlideHeightScale;
        float bottom = _standingCenter.y - _standingHeight * 0.5f;
        _characterController.height = height;
        _characterController.center = new Vector3(
            _standingCenter.x,
            bottom + height * 0.5f,
            _standingCenter.z);
    }

    private void FinishSlide()
    {
        _isSliding = false;
        _slideElapsed = 0f;
        _characterController.height = _standingHeight;
        _characterController.center = _standingCenter;
        RefreshSprintState();
        SlideFinished?.Invoke();
    }

    private bool TryStartVault()
    {
        if (!TryFindVaultTarget(out Vector3 targetPosition))
        {
            return false;
        }

        _isVaulting = true;
        _isGrounded = false;
        _isSprinting = false;
        _sprintRequested = false;
        _verticalSpeed = 0f;
        _vaultElapsed = 0f;
        _vaultStartPosition = transform.position;
        _vaultTargetPosition = targetPosition;
        VaultStarted?.Invoke();
        return true;
    }

    private bool TryFindVaultTarget(out Vector3 targetPosition)
    {
        targetPosition = Vector3.zero;

        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude <= 0f)
        {
            return false;
        }

        Bounds controllerBounds = _characterController.bounds;
        float footHeight = controllerBounds.min.y;
        Vector3 obstacleOrigin = new Vector3(
            controllerBounds.center.x,
            footHeight + _playerData.VaultLowHeight,
            controllerBounds.center.z);

        if (!Physics.Raycast(
                obstacleOrigin,
                forward,
                out RaycastHit obstacleHit,
                _playerData.VaultDistance,
                _playerData.VaultMask,
                QueryTriggerInteraction.Ignore) ||
            IsOwnCollider(obstacleHit.collider))
        {
            return false;
        }

        float landingOffset = _characterController.radius + _playerData.VaultLandingForwardOffset;
        Vector3 topProbeOrigin = obstacleHit.point + forward * landingOffset;
        topProbeOrigin.y = footHeight + _playerData.VaultHighHeight + _playerData.VaultClearancePadding;
        float topProbeDistance = _playerData.VaultHighHeight - _playerData.VaultLowHeight +
            _playerData.VaultClearancePadding * 2f;

        if (!Physics.Raycast(
                topProbeOrigin,
                Vector3.down,
                out RaycastHit topHit,
                topProbeDistance,
                _playerData.VaultMask,
                QueryTriggerInteraction.Ignore) ||
            IsOwnCollider(topHit.collider))
        {
            return false;
        }

        float obstacleHeight = topHit.point.y - footHeight;
        if (obstacleHeight < _playerData.VaultLowHeight ||
            obstacleHeight > _playerData.VaultHighHeight)
        {
            return false;
        }

        float minimumGroundNormal = Mathf.Cos(_playerData.VaultMaxSurfaceAngle * Mathf.Deg2Rad);
        if (Vector3.Dot(topHit.normal, Vector3.up) < minimumGroundNormal)
        {
            return false;
        }

        float footOffset = controllerBounds.min.y - transform.position.y;
        targetPosition = new Vector3(
            topHit.point.x,
            topHit.point.y - footOffset + _playerData.VaultClearancePadding,
            topHit.point.z);
        return HasStandingClearance(targetPosition);
    }

    private bool HasStandingClearance(Vector3 targetPosition)
    {
        float heightScale = Mathf.Abs(transform.lossyScale.y);
        float radiusScale = Mathf.Max(
            Mathf.Abs(transform.lossyScale.x),
            Mathf.Abs(transform.lossyScale.z));
        float radius = _characterController.radius * radiusScale;
        radius = Mathf.Max(0.01f, radius - _playerData.VaultClearancePadding);
        float halfHeight = Mathf.Max(radius, _characterController.height * heightScale * 0.5f);
        Vector3 centerOffset = _characterController.bounds.center - transform.position;
        Vector3 center = targetPosition + centerOffset;
        Vector3 capsuleOffset = Vector3.up * (halfHeight - radius);

        int count = Physics.OverlapCapsuleNonAlloc(
            center - capsuleOffset,
            center + capsuleOffset,
            radius,
            _clearanceBuffer,
            _playerData.VaultMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider hitCollider = _clearanceBuffer[i];
            _clearanceBuffer[i] = null;
            if (!hitCollider || IsOwnCollider(hitCollider))
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private bool IsOwnCollider(Collider targetCollider)
    {
        return targetCollider && targetCollider.transform.IsChildOf(transform);
    }

    private Vector3 GetWorldMoveDirection()
    {
        if (!_cameraCtrl || !_cameraCtrl.IsThirdPerson)
        {
            return transform.right * _moveInput.x + transform.forward * _moveInput.y;
        }

        Vector3 forward = _cameraCtrl.GetPlanarLookDirection();
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        return right * _moveInput.x + forward * _moveInput.y;
    }

    private void UpdateThirdPersonFacing(Vector3 moveDirection)
    {
        if (!_cameraCtrl || !_cameraCtrl.IsThirdPerson ||
            _cameraCtrl.IsFacingAimDirection() || moveDirection.sqrMagnitude < 0.01f)
        {
            return;
        }

        Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            _thirdPersonTurnSpeed * Time.deltaTime);
        _cameraCtrl.KeepThirdPersonLookDirection();
    }

    private void RefreshSprintState()
    {
        bool wasSprinting = _isSprinting;
        _isSprinting = !_isVaulting && !_isSliding &&
            _sprintRequested &&
            (!_aimCtrl || !_aimCtrl.IsAimRequested) &&
            (_moveInput.y >= -InputThreshold ||
             (_cameraCtrl && _cameraCtrl.IsThirdPerson)) &&
            _moveInput.sqrMagnitude > InputThreshold * InputThreshold;
        if (wasSprinting && !_isSprinting &&
            (!_characterController.isGrounded || _verticalSpeed > 0f))
        {
            _airSprintCarryVelocity = _horizontalVelocity;
            _airSprintCarryRemaining = _airSprintCarryDuration;
        }
    }

    private Vector3 ConsumeQueuedDisplacement()
    {
        Vector3 displacement = _queuedDisplacement;
        _queuedDisplacement = Vector3.zero;
        return displacement;
    }

    private void MoveCharacter(Vector3 worldDisplacement)
    {
        CollisionFlags collisionFlags = _characterController.Move(worldDisplacement);
        _localVelocity = transform.InverseTransformDirection(_characterController.velocity);
        _horizontalVelocity = Vector3.ProjectOnPlane(
            _characterController.velocity,
            Vector3.up);
        if ((collisionFlags & CollisionFlags.Above) != 0 && _verticalSpeed > 0f)
        {
            _verticalSpeed = 0f;
        }

        if ((collisionFlags & CollisionFlags.Below) != 0 && _verticalSpeed < 0f)
        {
            _verticalSpeed = _playerData.GroundedForce;
        }
    }

    private void FinishVault()
    {
        _isVaulting = false;
        if (_isSliding && _characterController)
        {
            _characterController.height = _standingHeight;
            _characterController.center = _standingCenter;
        }
        _isSliding = false;
        _isGrounded = _characterController.isGrounded;
        _verticalSpeed = _playerData.GroundedForce;
        VaultFinished?.Invoke();
    }

    private void ResetMotion()
    {
        ResetInput();
        _queuedDisplacement = Vector3.zero;
        _localVelocity = Vector3.zero;
        _horizontalVelocity = Vector3.zero;
        _airSprintCarryVelocity = Vector3.zero;
        _airSprintCarryRemaining = 0f;
        _isVaulting = false;
        _isGrounded = false;
        _verticalSpeed = 0f;
        _vaultElapsed = 0f;
        _slideElapsed = 0f;
        _externalSpeedMultiplier = 1f;
    }
}
