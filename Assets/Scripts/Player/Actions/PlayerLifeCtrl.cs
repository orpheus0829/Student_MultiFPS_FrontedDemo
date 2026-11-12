using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerLifeCtrl : MonoBehaviour
{
    [Header("复活")]
    [SerializeField, InspectorName("玩家数据")] private PlayerDataSO _playerData;
    [SerializeField, InspectorName("自动复活")] private bool _autoRespawn = true;
    [SerializeField, InspectorName("绑定复活区域（可暂留空）")]
    private PlayerSpawnZone _spawnZone;

    [Header("死亡镜头")]
    [SerializeField, Min(0.1f), InspectorName("镜头后退距离")] private float _cameraBackDistance = 2.7f;
    [SerializeField, Min(0f), InspectorName("镜头抬高距离")] private float _cameraRise = 1.2f;
    [SerializeField, Min(0.1f), InspectorName("镜头跟随速度")] private float _cameraFollowSpeed = 5f;
    [SerializeField, Min(0f), InspectorName("死亡镜头碰撞半径")]
    private float _deathCameraCollisionRadius = 0.2f;
    [SerializeField, Min(0f), InspectorName("死亡镜头离墙余量")]
    private float _deathCameraCollisionPadding = 0.08f;
    [SerializeField, Min(0.1f), InspectorName("追踪击杀者转向速度")]
    private float _killerLookSpeed = 4.5f;
    [SerializeField, Min(0f), InspectorName("击杀者注视高度")]
    private float _killerLookHeight = 1.25f;

    [Header("死亡表现")]
    [SerializeField, InspectorName("布娃娃")] private PlayerRagdollCtrl _ragdoll;
    [SerializeField, InspectorName("双视角动画")] private PlayerAnimationCtrl _animationCtrl;
    [SerializeField, InspectorName("枪械表现")] private PlayerWeaponViewCtrl _weaponViewCtrl;

    private CharacterHealth _health;
    private CharacterController _characterController;
    private PlayerInputCtrl _inputCtrl;
    private PlayerActionCtrl _actionCtrl;
    private PlayerMoveCtrl _moveCtrl;
    private PlayerAimCtrl _aimCtrl;
    private WeaponCombatCtrl _combatCtrl;
    private WeaponActionCtrl _weaponActionCtrl;
    private FirstPersonCameraCtrl _cameraCtrl;
    private FirstPersonCameraMotionCtrl _cameraMotionCtrl;
    private Camera _camera;
    private Transform _cameraParent;
    private Vector3 _cameraLocalPosition;
    private Quaternion _cameraLocalRotation;
    private int _cameraMask;
    private bool _hasCameraPose;
    private bool _isDead;
    private bool _inputCtrlWasEnabled;
    private bool _actionCtrlWasEnabled;
    private bool _moveCtrlWasEnabled;
    private bool _aimCtrlWasEnabled;
    private bool _combatCtrlWasEnabled;
    private bool _weaponActionCtrlWasEnabled;
    private bool _weaponViewCtrlWasEnabled;
    private bool _animationCtrlWasEnabled;
    private bool _cameraCtrlWasEnabled;
    private bool _cameraMotionCtrlWasEnabled;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private GameObject _droppedWeapon;
    private Collider[] _hitboxColliders;
    private bool[] _hitboxEnabled;
    private SmartTimer.TimerTask _respawnTask;
    private bool _isClosing;
    private Transform _killerLookTarget;
    private DeathKillerOutline _killerOutline;
    private readonly RaycastHit[] _deathCameraHits = new RaycastHit[24];

    public event Action Respawned;

    public float RespawnDelay
    {
        get { return _playerData ? _playerData.RespawnDelay : 5f; }
    }

    public bool IsDead
    {
        get { return _isDead; }
    }

    public PlayerDataSO PlayerData
    {
        get { return _playerData; }
    }

    private void Awake()
    {
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
        TryGetComponent(out _health);
        TryGetComponent(out _characterController);
        TryGetComponent(out _inputCtrl);
        TryGetComponent(out _actionCtrl);
        TryGetComponent(out _moveCtrl);
        TryGetComponent(out _aimCtrl);
        TryGetComponent(out _combatCtrl);
        TryGetComponent(out _weaponActionCtrl);
        if (!_ragdoll)
        {
            TryGetComponent(out _ragdoll);
        }
        if (!_animationCtrl)
        {
            TryGetComponent(out _animationCtrl);
        }
        if (!_weaponViewCtrl)
        {
            TryGetComponent(out _weaponViewCtrl);
        }
        _cameraCtrl = GetComponentInChildren<FirstPersonCameraCtrl>(true);
        _cameraMotionCtrl = GetComponentInChildren<FirstPersonCameraMotionCtrl>(true);
        _camera = _cameraCtrl ? _cameraCtrl.GetComponentInChildren<Camera>(true) : null;
        BodyHitbox[] hitboxes = GetComponentsInChildren<BodyHitbox>(true);
        _hitboxColliders = new Collider[hitboxes.Length];
        _hitboxEnabled = new bool[hitboxes.Length];
        for (int i = 0; i < hitboxes.Length; i++)
        {
            _hitboxColliders[i] = hitboxes[i].GetComponent<Collider>();
        }
    }

    private void OnEnable()
    {
        _isClosing = false;
        CombatDamage.DamageResolved += HandleDamageResolved;
    }

    private void LateUpdate()
    {
        if (!_isDead || !_hasCameraPose || !_camera)
        {
            return;
        }

        Vector3 focus = _ragdoll ? _ragdoll.FocusPoint :
            transform.position + Vector3.up * 0.95f;
        Vector3 desired = focus - transform.forward * _cameraBackDistance +
            Vector3.up * _cameraRise;
        desired = ResolveDeathCameraPosition(focus, desired);
        float blend = 1f - Mathf.Exp(-_cameraFollowSpeed * Time.deltaTime);
        _camera.transform.position = Vector3.Lerp(_camera.transform.position, desired, blend);
        Vector3 lookPoint = _killerLookTarget
            ? _killerLookTarget.position + Vector3.up * _killerLookHeight
            : focus;
        Vector3 lookDirection = lookPoint - _camera.transform.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion look = Quaternion.LookRotation(lookDirection, Vector3.up);
            float lookBlend = 1f - Mathf.Exp(-_killerLookSpeed * Time.deltaTime);
            _camera.transform.rotation = Quaternion.Slerp(
                _camera.transform.rotation, look, lookBlend);
        }
    }

    private Vector3 ResolveDeathCameraPosition(Vector3 focus, Vector3 desired)
    {
        Vector3 offset = desired - focus;
        float distance = offset.magnitude;
        if (distance <= 0.001f || _deathCameraCollisionRadius <= 0f)
        {
            return desired;
        }

        int hitCount = Physics.SphereCastNonAlloc(
            focus,
            _deathCameraCollisionRadius,
            offset / distance,
            _deathCameraHits,
            distance,
            ~0,
            QueryTriggerInteraction.Ignore);
        float allowedDistance = distance;
        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _deathCameraHits[i];
            if (!hit.collider || hit.transform.IsChildOf(transform))
            {
                continue;
            }
            allowedDistance = Mathf.Min(allowedDistance,
                Mathf.Max(0f, hit.distance - _deathCameraCollisionPadding));
        }
        return focus + offset.normalized * allowedDistance;
    }

    private void OnDisable()
    {
        _isClosing = true;
        CombatDamage.DamageResolved -= HandleDamageResolved;
        if (_respawnTask != null && _respawnTask.IsRunning)
        {
            _respawnTask.Stop();
        }
    }

    private void OnDestroy()
    {
        RestoreCamera();
        ClearKillerTracking();
        if (_droppedWeapon)
        {
            Destroy(_droppedWeapon);
        }
    }

    public void BindSpawnZone(PlayerSpawnZone zone)
    {
        _spawnZone = zone;
    }

    public void Respawn()
    {
        TryRespawn();
    }

    public bool TryRespawn()
    {
        if (_isClosing || !_isDead || !_health)
        {
            return false;
        }

        if (_characterController)
        {
            _characterController.enabled = false;
        }
        _ragdoll?.StandUp();
        if (_spawnZone && _spawnZone.TryGetRandomPosition(out Vector3 position))
        {
            transform.position = position;
            transform.rotation = _spawnZone.transform.rotation;
        }
        else
        {
            transform.position = _initialPosition;
            transform.rotation = _initialRotation;
        }

        if (_droppedWeapon)
        {
            Destroy(_droppedWeapon);
            _droppedWeapon = null;
        }
        RestoreCamera();
        ClearKillerTracking();
        SetHitboxesEnabled(true);
        _health.ResetHealth();
        _combatCtrl?.ResetAllAmmoToFull();
        if (_characterController)
        {
            _characterController.enabled = true;
        }
        RestoreControls();
        if (_animationCtrl && _animationCtrl.FirstPersonView)
        {
            _animationCtrl.FirstPersonView.gameObject.SetActive(true);
        }
        _isDead = false;
        Respawned?.Invoke();
        return true;
    }

    private void HandleDamageResolved(DamageResult result)
    {
        if (_isDead || result == null || !result.WasKill || result.Target != _health)
        {
            return;
        }

        _isDead = true;
        BeginKillerTracking(result.Instigator);
        DropWeapon();
        PrepareDeathCamera();
        CaptureAndDisableControls();
        if (_hasCameraPose && _camera)
        {
            _camera.cullingMask = (_camera.cullingMask | (1 << 8)) & ~(1 << 9);
        }
        SetHitboxesEnabled(false);
        if (_characterController)
        {
            _characterController.enabled = false;
        }
        if (_animationCtrl && _animationCtrl.FirstPersonView)
        {
            _animationCtrl.FirstPersonView.gameObject.SetActive(false);
        }
        _ragdoll?.Fall(result.HitPoint, result.HitDirection, result.AppliedDamage);
        if (_autoRespawn)
        {
            _respawnTask = SmartTimer.instance.SetTimer(this, RespawnDelay, Respawn);
        }
    }

    private void CaptureAndDisableControls()
    {
        _inputCtrlWasEnabled = _inputCtrl && _inputCtrl.enabled;
        _actionCtrlWasEnabled = _actionCtrl && _actionCtrl.enabled;
        _moveCtrlWasEnabled = _moveCtrl && _moveCtrl.enabled;
        _aimCtrlWasEnabled = _aimCtrl && _aimCtrl.enabled;
        _combatCtrlWasEnabled = _combatCtrl && _combatCtrl.enabled;
        _weaponActionCtrlWasEnabled = _weaponActionCtrl && _weaponActionCtrl.enabled;
        _weaponViewCtrlWasEnabled = _weaponViewCtrl && _weaponViewCtrl.enabled;
        _animationCtrlWasEnabled = _animationCtrl && _animationCtrl.enabled;
        _cameraCtrlWasEnabled = _cameraCtrl && _cameraCtrl.enabled;
        _cameraMotionCtrlWasEnabled = _cameraMotionCtrl && _cameraMotionCtrl.enabled;

        if (_inputCtrl) _inputCtrl.enabled = false;
        if (_actionCtrl) _actionCtrl.enabled = false;
        if (_moveCtrl) _moveCtrl.enabled = false;
        if (_aimCtrl) _aimCtrl.enabled = false;
        if (_combatCtrl) _combatCtrl.enabled = false;
        if (_weaponActionCtrl) _weaponActionCtrl.enabled = false;
        if (_weaponViewCtrl) _weaponViewCtrl.enabled = false;
        if (_animationCtrl) _animationCtrl.enabled = false;
        if (_cameraCtrl) _cameraCtrl.enabled = false;
        if (_cameraMotionCtrl) _cameraMotionCtrl.enabled = false;
    }

    private void RestoreControls()
    {
        if (_inputCtrl) _inputCtrl.enabled = _inputCtrlWasEnabled;
        if (_actionCtrl) _actionCtrl.enabled = _actionCtrlWasEnabled;
        if (_moveCtrl) _moveCtrl.enabled = _moveCtrlWasEnabled;
        if (_aimCtrl) _aimCtrl.enabled = _aimCtrlWasEnabled;
        if (_combatCtrl) _combatCtrl.enabled = _combatCtrlWasEnabled;
        if (_weaponActionCtrl) _weaponActionCtrl.enabled = _weaponActionCtrlWasEnabled;
        if (_weaponViewCtrl) _weaponViewCtrl.enabled = _weaponViewCtrlWasEnabled;
        if (_animationCtrl) _animationCtrl.enabled = _animationCtrlWasEnabled;
        if (_cameraCtrl) _cameraCtrl.enabled = _cameraCtrlWasEnabled;
        if (_cameraMotionCtrl) _cameraMotionCtrl.enabled = _cameraMotionCtrlWasEnabled;
    }

    private void SetHitboxesEnabled(bool isEnabled)
    {
        for (int i = 0; i < _hitboxColliders.Length; i++)
        {
            if (!_hitboxColliders[i])
            {
                continue;
            }
            if (!isEnabled)
            {
                _hitboxEnabled[i] = _hitboxColliders[i].enabled;
            }
            _hitboxColliders[i].enabled = isEnabled && _hitboxEnabled[i];
        }
    }

    private void PrepareDeathCamera()
    {
        if (!_camera || !_cameraCtrl || !_cameraCtrl.enabled)
        {
            return;
        }

        _cameraParent = _camera.transform.parent;
        _cameraLocalPosition = _camera.transform.localPosition;
        _cameraLocalRotation = _camera.transform.localRotation;
        _cameraMask = _camera.cullingMask;
        _camera.transform.SetParent(null, true);
        _hasCameraPose = true;
    }

    private void RestoreCamera()
    {
        if (!_hasCameraPose || !_camera)
        {
            return;
        }

        _camera.transform.SetParent(_cameraParent, false);
        _camera.transform.localPosition = _cameraLocalPosition;
        _camera.transform.localRotation = _cameraLocalRotation;
        _camera.cullingMask = _cameraMask;
        _hasCameraPose = false;
    }

    private void BeginKillerTracking(PlayerInfo instigator)
    {
        ClearKillerTracking();
        if (!instigator || instigator == _health.PlayerInfo)
        {
            return;
        }

        _killerLookTarget = instigator.transform;
        // 只有本地玩家的死亡视角需要创建可见描边。
        if (_inputCtrl && _inputCtrl.enabled)
        {
            _killerOutline = DeathKillerOutline.Show(instigator.gameObject, _camera);
        }
    }

    private void ClearKillerTracking()
    {
        _killerLookTarget = null;
        if (_killerOutline)
        {
            _killerOutline.Hide();
            _killerOutline = null;
        }
    }

    private void DropWeapon()
    {
        if (!_weaponViewCtrl || !_weaponViewCtrl.ThirdPersonWeapon)
        {
            return;
        }

        _droppedWeapon = Instantiate(_weaponViewCtrl.ThirdPersonWeapon);
        _droppedWeapon.name = "掉落枪械";
        _droppedWeapon.transform.SetParent(null, true);
        Animator[] animators = _droppedWeapon.GetComponentsInChildren<Animator>(true);
        for (int i = 0; i < animators.Length; i++)
        {
            animators[i].enabled = false;
        }
        Collider[] sourceColliders = _droppedWeapon.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < sourceColliders.Length; i++)
        {
            sourceColliders[i].enabled = false;
        }
        MonoBehaviour[] behaviours = _droppedWeapon.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            behaviours[i].enabled = false;
        }

        Renderer[] renderers = _droppedWeapon.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Destroy(_droppedWeapon);
            _droppedWeapon = null;
            return;
        }
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].forceRenderingOff = false;
        }
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }
        BoxCollider collider = _droppedWeapon.AddComponent<BoxCollider>();
        collider.center = _droppedWeapon.transform.InverseTransformPoint(bounds.center);
        Vector3 scale = _droppedWeapon.transform.lossyScale;
        collider.size = new Vector3(
            bounds.size.x / Mathf.Max(0.01f, Mathf.Abs(scale.x)),
            bounds.size.y / Mathf.Max(0.01f, Mathf.Abs(scale.y)),
            bounds.size.z / Mathf.Max(0.01f, Mathf.Abs(scale.z)));
        Rigidbody body = _droppedWeapon.AddComponent<Rigidbody>();
        body.mass = 2f;
    }
}
