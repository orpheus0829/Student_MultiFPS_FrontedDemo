using UnityEngine;

[DisallowMultipleComponent]
public sealed class FirstPersonCameraMotionCtrl : MonoBehaviour
{
    private const float FullCircle = Mathf.PI * 2f;

    [Header("表现节点")]
    [SerializeField, InspectorName("镜头运动节点")]
    [Tooltip("只填写相机层级中的表现子节点，不要填写玩家根节点或视角权威节点。")]
    private Transform _motionRoot;

    [Header("正常移动")]
    [SerializeField, InspectorName("位置摆动幅度")]
    private Vector3 _walkPositionAmplitude = new Vector3(0.018f, 0.025f, 0f);
    [SerializeField, InspectorName("旋转摆动幅度")]
    private Vector3 _walkRotationAmplitude = new Vector3(0.35f, 0.25f, 0.5f);
    [SerializeField, Min(0f), InspectorName("摆动频率")]
    private float _walkFrequency = 1.75f;

    [Header("疾跑")]
    [SerializeField, InspectorName("位置摆动幅度")]
    private Vector3 _sprintPositionAmplitude = new Vector3(0.035f, 0.05f, 0.008f);
    [SerializeField, InspectorName("旋转摆动幅度")]
    private Vector3 _sprintRotationAmplitude = new Vector3(0.7f, 0.5f, 1.1f);
    [SerializeField, Min(0f), InspectorName("摆动频率")]
    private float _sprintFrequency = 2.8f;

    [Header("摆动过渡")]
    [SerializeField, Min(0f), InspectorName("进入速度")]
    private float _bobEnterSpeed = 8f;
    [SerializeField, Min(0f), InspectorName("停止速度")]
    private float _bobExitSpeed = 11f;
    [SerializeField, Min(0f), InspectorName("姿态跟随速度")]
    private float _poseFollowSpeed = 24f;

    [Header("默认开火反馈")]
    [SerializeField, InspectorName("位置冲击")]
    private Vector3 _defaultFirePositionKick = new Vector3(0f, 0.004f, -0.025f);
    [SerializeField, InspectorName("旋转冲击")]
    private Vector3 _defaultFireRotationKick = new Vector3(-1.1f, 0.25f, 0.3f);
    [SerializeField, Min(0f), InspectorName("冲击时间")]
    private float _defaultFireKickDuration = 0.045f;
    [SerializeField, Min(0f), InspectorName("回正时间")]
    private float _defaultFireReturnDuration = 0.11f;
    [SerializeField, Range(0f, 0.1f), InspectorName("开火镜头滞后")]
    private float _fireCameraDelay = 0.02f;
    [SerializeField, Range(0f, 1f), InspectorName("水平随机程度")]
    private float _fireHorizontalRandomness = 0.65f;

    [Header("动作反馈")]
    [SerializeField, InspectorName("落地位置冲击")]
    private Vector3 _landingPositionKick = new Vector3(0f, -0.055f, 0.012f);
    [SerializeField, InspectorName("落地旋转冲击")]
    private Vector3 _landingRotationKick = new Vector3(1.6f, 0f, 0f);
    [SerializeField, InspectorName("翻越位置冲击")]
    private Vector3 _vaultPositionKick = new Vector3(0f, 0.025f, 0.02f);
    [SerializeField, InspectorName("翻越旋转冲击")]
    private Vector3 _vaultRotationKick = new Vector3(-0.8f, 0f, 0f);
    [SerializeField, InspectorName("滑铲位置冲击")]
    private Vector3 _slidePositionKick = new Vector3(0f, -0.16f, 0.08f);
    [SerializeField, InspectorName("滑铲旋转冲击")]
    private Vector3 _slideRotationKick = new Vector3(8f, 0f, -2.5f);
    [SerializeField, InspectorName("插匣位置冲击")]
    private Vector3 _magazineSeatPositionKick = new Vector3(0f, -0.027f, -0.025f);
    [SerializeField, InspectorName("插匣旋转冲击")]
    private Vector3 _magazineSeatRotationKick = new Vector3(2f, 0f, 0.65f);
    [SerializeField, InspectorName("逐发装填位置冲击")]
    private Vector3 _roundInsertPositionKick = new Vector3(0f, -0.014f, -0.012f);
    [SerializeField, InspectorName("逐发装填旋转冲击")]
    private Vector3 _roundInsertRotationKick = new Vector3(0.95f, 0f, 0.25f);
    [SerializeField, InspectorName("检视位置冲击")]
    private Vector3 _inspectPositionKick = new Vector3(0f, 0.008f, -0.012f);
    [SerializeField, InspectorName("检视旋转冲击")]
    private Vector3 _inspectRotationKick = new Vector3(-0.85f, 0.25f, 0.35f);
    [SerializeField, Min(0f), InspectorName("动作冲击时间")]
    private float _actionKickDuration = 0.08f;
    [SerializeField, Min(0f), InspectorName("动作回正时间")]
    private float _actionReturnDuration = 0.18f;

    private Vector3 _baseLocalPosition;
    private FirstPersonCameraCtrl _cameraCtrl;
    private Vector3 _viewOffset;
    private Quaternion _baseLocalRotation;
    private Vector3 _currentPositionOffset;
    private Vector3 _currentRotationOffset;
    private Vector3 _bobPositionOffset;
    private Vector3 _bobRotationOffset;
    private Vector3 _impulseStartPosition;
    private Vector3 _impulseStartRotation;
    private Vector3 _impulsePosition;
    private Vector3 _impulseRotation;
    private Vector3 _impulseTargetPosition;
    private Vector3 _impulseTargetRotation;
    private Vector2 _moveInput;
    private bool _isGrounded;
    private bool _isSprinting;
    private bool _isReturning;
    private bool _hasImpulse;
    private float _bobPhase;
    private float _bobWeight;
    private float _impulseElapsed;
    private float _kickDuration;
    private float _returnDuration;
    private float _pendingFireDelay;
    private float _pendingFireKickDuration;
    private float _pendingFireReturnDuration;
    private Vector3 _pendingFirePosition;
    private Vector3 _pendingFireRotation;
    private bool _hasPendingFire;

    public Vector3 CurrentPositionOffset
    {
        get
        {
            return _currentPositionOffset;
        }
    }

    public Vector3 CurrentRotationOffset
    {
        get
        {
            return _currentRotationOffset;
        }
    }

    private void Awake()
    {
        TryGetComponent(out _cameraCtrl);
        if (!_motionRoot)
        {
            Debug.LogError($"{name} 未配置镜头运动表现节点。", this);
            enabled = false;
            return;
        }

        _baseLocalPosition = _motionRoot.localPosition;
        _baseLocalRotation = _motionRoot.localRotation;
    }

    private void OnEnable()
    {
        if (_cameraCtrl)
        {
            _cameraCtrl.ViewChanged += HandleViewChanged;
        }
    }

    private void LateUpdate()
    {
        float deltaTime = Time.deltaTime;
        EvaluateMovementBob(deltaTime, out Vector3 bobPosition, out Vector3 bobRotation);
        EvaluatePendingFire(deltaTime);
        EvaluateImpulse(deltaTime);

        float follow = 1f - Mathf.Exp(-_poseFollowSpeed * deltaTime);
        _bobPositionOffset = Vector3.Lerp(_bobPositionOffset, bobPosition, follow);
        _bobRotationOffset = Vector3.Lerp(_bobRotationOffset, bobRotation, follow);
        _currentPositionOffset = _bobPositionOffset + _impulsePosition;
        _currentRotationOffset = _bobRotationOffset + _impulseRotation;

        _motionRoot.localPosition = _baseLocalPosition + _viewOffset +
            _currentPositionOffset;
        _motionRoot.localRotation = _baseLocalRotation *
            Quaternion.Euler(_currentRotationOffset);
    }

    private void OnDisable()
    {
        if (_cameraCtrl)
        {
            _cameraCtrl.ViewChanged -= HandleViewChanged;
        }
        ClearMotion();
    }

    public void SetMovementState(Vector2 moveInput, bool isGrounded, bool isSprinting)
    {
        _moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        _isGrounded = isGrounded;
        _isSprinting = isSprinting;
    }

    public void StopMovementFeedback()
    {
        _moveInput = Vector2.zero;
        _isSprinting = false;
    }

    public void SetViewOffset(Vector3 offset)
    {
        _viewOffset = offset;
    }

    public void PlayFireFeedback()
    {
        QueueFireImpulse(
            _defaultFirePositionKick,
            RandomizeHorizontalKick(_defaultFireRotationKick),
            _defaultFireKickDuration,
            _defaultFireReturnDuration);
    }

    public void PlayFireFeedback(WeaponCameraFeedbackData feedback, float aimProgress)
    {
        if (feedback == null)
        {
            PlayFireFeedback();
            return;
        }

        Vector3 positionKick = feedback.CameraKickPosition;
        Vector3 rotationKick = feedback.CameraKickRotation;
        if (positionKick == Vector3.zero && rotationKick == Vector3.zero)
        {
            positionKick = _defaultFirePositionKick;
            rotationKick = _defaultFireRotationKick;
        }

        float aimMultiplier = Mathf.Lerp(
            1f,
            Mathf.Max(0f, feedback.AimCameraKickMultiplier),
            Mathf.Clamp01(aimProgress));
        float kickDuration = feedback.CameraKickDuration > 0f
            ? feedback.CameraKickDuration
            : _defaultFireKickDuration;
        float returnDuration = feedback.CameraReturnDuration > 0f
            ? feedback.CameraReturnDuration
            : _defaultFireReturnDuration;

        QueueFireImpulse(
            positionKick * aimMultiplier,
            RandomizeHorizontalKick(rotationKick) * aimMultiplier,
            kickDuration,
            returnDuration);
    }

    public void PlayLandingFeedback(float strength = 1f)
    {
        float safeStrength = Mathf.Max(0f, strength);
        AddImpulse(
            _landingPositionKick * safeStrength,
            _landingRotationKick * safeStrength,
            _actionKickDuration,
            _actionReturnDuration);
    }

    public void PlayVaultFeedback(float strength = 1f)
    {
        float safeStrength = Mathf.Max(0f, strength);
        AddImpulse(
            _vaultPositionKick * safeStrength,
            _vaultRotationKick * safeStrength,
            _actionKickDuration,
            _actionReturnDuration);
    }

    public void PlaySlideFeedback(float strength = 1f)
    {
        float safeStrength = Mathf.Max(0f, strength);
        AddImpulse(
            _slidePositionKick * safeStrength,
            _slideRotationKick * safeStrength,
            0.08f,
            0.75f);
    }

    public void PlayMagazineSeatFeedback()
    {
        AddImpulse(_magazineSeatPositionKick, _magazineSeatRotationKick, 0.025f, 0.13f);
    }

    public void PlayRoundInsertFeedback()
    {
        AddImpulse(_roundInsertPositionKick, _roundInsertRotationKick, 0.025f, 0.11f);
    }

    public void PlayInspectFeedback()
    {
        AddImpulse(_inspectPositionKick, _inspectRotationKick, 0.05f, 0.18f);
    }

    public void CancelInspectFeedback()
    {
        ClearImpulse();
    }

    public void AddImpulse(
        Vector3 positionKick,
        Vector3 rotationKick,
        float kickDuration,
        float returnDuration)
    {
        if (_cameraCtrl && _cameraCtrl.IsThirdPerson)
        {
            return;
        }

        StartImpulse(positionKick, rotationKick, kickDuration, returnDuration);
    }

    private void StartImpulse(
        Vector3 positionKick,
        Vector3 rotationKick,
        float kickDuration,
        float returnDuration)
    {
        _impulseStartPosition = _impulsePosition;
        _impulseStartRotation = _impulseRotation;
        _impulseTargetPosition = _impulsePosition + positionKick;
        _impulseTargetRotation = _impulseRotation + rotationKick;
        _kickDuration = Mathf.Max(0f, kickDuration);
        _returnDuration = Mathf.Max(0f, returnDuration);
        _impulseElapsed = 0f;
        _isReturning = false;
        _hasImpulse = true;
    }

    public void ClearMotion()
    {
        _moveInput = Vector2.zero;
        _isGrounded = false;
        _isSprinting = false;
        ClearImpulse();
        _bobPhase = 0f;
        _bobWeight = 0f;
        _currentPositionOffset = Vector3.zero;
        _currentRotationOffset = Vector3.zero;
        _bobPositionOffset = Vector3.zero;
        _bobRotationOffset = Vector3.zero;

        if (_motionRoot)
        {
            _motionRoot.localPosition = _baseLocalPosition + _viewOffset;
            _motionRoot.localRotation = _baseLocalRotation;
        }
    }

    private void HandleViewChanged(bool isThirdPerson)
    {
        ClearImpulse();
    }

    private void ClearImpulse()
    {
        _isReturning = false;
        _hasImpulse = false;
        _hasPendingFire = false;
        _impulseElapsed = 0f;
        _impulsePosition = Vector3.zero;
        _impulseRotation = Vector3.zero;
    }

    private void EvaluateMovementBob(
        float deltaTime,
        out Vector3 positionOffset,
        out Vector3 rotationOffset)
    {
        float moveStrength = _isGrounded ? _moveInput.magnitude : 0f;
        float targetWeight = Mathf.Clamp01(moveStrength);
        float weightSpeed = targetWeight > _bobWeight ? _bobEnterSpeed : _bobExitSpeed;
        _bobWeight = Mathf.MoveTowards(_bobWeight, targetWeight, weightSpeed * deltaTime);

        float frequency = _isSprinting ? _sprintFrequency : _walkFrequency;
        if (targetWeight > 0f)
        {
            _bobPhase = Mathf.Repeat(_bobPhase + frequency * FullCircle * deltaTime, FullCircle);
        }

        Vector3 positionAmplitude = _isSprinting
            ? _sprintPositionAmplitude
            : _walkPositionAmplitude;
        Vector3 rotationAmplitude = _isSprinting
            ? _sprintRotationAmplitude
            : _walkRotationAmplitude;
        float horizontalWave = Mathf.Cos(_bobPhase);
        float verticalWave = _isSprinting
            ? -Mathf.Pow(Mathf.Max(0f, Mathf.Sin(_bobPhase * 2f)), 3f)
            : Mathf.Sin(_bobPhase * 2f);
        float forwardWave = Mathf.Sin(_bobPhase);

        positionOffset = new Vector3(
            horizontalWave * positionAmplitude.x,
            verticalWave * positionAmplitude.y,
            forwardWave * positionAmplitude.z) * _bobWeight;
        rotationOffset = new Vector3(
            (_isSprinting ? -verticalWave : verticalWave) * rotationAmplitude.x,
            forwardWave * rotationAmplitude.y,
            -horizontalWave * rotationAmplitude.z) * _bobWeight;
    }

    private void QueueFireImpulse(
        Vector3 positionKick,
        Vector3 rotationKick,
        float kickDuration,
        float returnDuration)
    {
        _pendingFirePosition = _hasPendingFire
            ? _pendingFirePosition + positionKick
            : positionKick;
        _pendingFireRotation = _hasPendingFire
            ? _pendingFireRotation + rotationKick
            : rotationKick;
        _pendingFireKickDuration = kickDuration;
        _pendingFireReturnDuration = returnDuration;
        _pendingFireDelay = _fireCameraDelay;
        _hasPendingFire = true;
    }

    private void EvaluatePendingFire(float deltaTime)
    {
        if (!_hasPendingFire)
        {
            return;
        }

        _pendingFireDelay -= deltaTime;
        if (_pendingFireDelay > 0f)
        {
            return;
        }

        _hasPendingFire = false;
        StartImpulse(
            _pendingFirePosition,
            _pendingFireRotation,
            _pendingFireKickDuration,
            _pendingFireReturnDuration);
    }

    private void EvaluateImpulse(float deltaTime)
    {
        if (!_hasImpulse)
        {
            return;
        }

        _impulseElapsed += deltaTime;
        if (!_isReturning)
        {
            float progress = _kickDuration <= 0f
                ? 1f
                : Mathf.Clamp01(_impulseElapsed / _kickDuration);
            float easedProgress = 1f - (1f - progress) * (1f - progress);
            _impulsePosition = Vector3.Lerp(
                _impulseStartPosition,
                _impulseTargetPosition,
                easedProgress);
            _impulseRotation = Vector3.Lerp(
                _impulseStartRotation,
                _impulseTargetRotation,
                easedProgress);

            if (progress < 1f)
            {
                return;
            }

            _isReturning = true;
            _impulseElapsed = 0f;
        }

        float returnProgress = _returnDuration <= 0f
            ? 1f
            : Mathf.Clamp01(_impulseElapsed / _returnDuration);
        float returnWeight = 1f - Mathf.SmoothStep(0f, 1f, returnProgress);
        _impulsePosition = _impulseTargetPosition * returnWeight;
        _impulseRotation = _impulseTargetRotation * returnWeight;
        if (returnProgress < 1f)
        {
            return;
        }

        _impulsePosition = Vector3.zero;
        _impulseRotation = Vector3.zero;
        _hasImpulse = false;
    }

    private Vector3 RandomizeHorizontalKick(Vector3 rotationKick)
    {
        rotationKick.y *= Mathf.Lerp(
            1f,
            Random.Range(-1f, 1f),
            _fireHorizontalRandomness);
        rotationKick.z *= Mathf.Lerp(
            1f,
            Random.Range(-1f, 1f),
            _fireHorizontalRandomness);
        return rotationKick;
    }

    private void OnValidate()
    {
        _walkFrequency = Mathf.Max(0f, _walkFrequency);
        _sprintFrequency = Mathf.Max(0f, _sprintFrequency);
        _bobEnterSpeed = Mathf.Max(0f, _bobEnterSpeed);
        _bobExitSpeed = Mathf.Max(0f, _bobExitSpeed);
        _poseFollowSpeed = Mathf.Max(0f, _poseFollowSpeed);
        _defaultFireKickDuration = Mathf.Max(0f, _defaultFireKickDuration);
        _defaultFireReturnDuration = Mathf.Max(0f, _defaultFireReturnDuration);
        _fireCameraDelay = Mathf.Clamp(_fireCameraDelay, 0f, 0.1f);
        _actionKickDuration = Mathf.Max(0f, _actionKickDuration);
        _actionReturnDuration = Mathf.Max(0f, _actionReturnDuration);
    }
}
