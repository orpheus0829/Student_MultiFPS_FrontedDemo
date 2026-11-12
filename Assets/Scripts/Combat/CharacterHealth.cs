using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterHealth : MonoBehaviour
{
    [Header("生命值")]
    [SerializeField, Min(1f), InspectorName("生命上限")] private float _maxHealth = 100f;

    private PlayerInfo _playerInfo;
    private PlayerLifeCtrl _lifeCtrl;
    private FirstPersonCameraMotionCtrl _cameraMotion;
    private float _currentHealth;
    private float _nextRecoveryTime = float.PositiveInfinity;
    private bool _isDead;
    private bool _isResolvingDamage;

    public float MaxHealth
    {
        get
        {
            return _maxHealth;
        }
    }

    public float CurrentHealth
    {
        get
        {
            return _currentHealth;
        }
    }

    public bool IsDead
    {
        get
        {
            return _isDead;
        }
    }

    public PlayerInfo PlayerInfo
    {
        get
        {
            return _playerInfo;
        }
    }

    public event Action<float, float> HealthChanged;
    public event Action Died;

    private void Awake()
    {
        TryGetComponent(out _playerInfo);
        TryGetComponent(out _lifeCtrl);
        _cameraMotion = GetComponentInChildren<FirstPersonCameraMotionCtrl>(true);
        ResetHealth();
    }

    private void Update()
    {
        if (_isDead || _isResolvingDamage || _currentHealth >= _maxHealth ||
            Time.time < _nextRecoveryTime)
        {
            return;
        }

        float recoveryPerSecond = _maxHealth / RecoveryDuration;
        float recoveredHealth = Mathf.MoveTowards(
            _currentHealth,
            _maxHealth,
            recoveryPerSecond * Time.deltaTime);
        if (Mathf.Approximately(recoveredHealth, _currentHealth))
        {
            return;
        }

        _currentHealth = recoveredHealth;
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    public void ResetHealth()
    {
        if (_isResolvingDamage)
        {
            return;
        }

        _currentHealth = Mathf.Max(1f, _maxHealth);
        _isDead = false;
        _nextRecoveryTime = float.PositiveInfinity;
        HealthChanged?.Invoke(_currentHealth, _maxHealth);
    }

    internal float ApplyDamage(
        float damage,
        out bool wasKilled,
        out float remainingHealth)
    {
        wasKilled = false;
        remainingHealth = _currentHealth;
        if (_isDead || _isResolvingDamage || damage <= 0f)
        {
            return 0f;
        }

        _isResolvingDamage = true;
        float oldHealth = _currentHealth;
        _currentHealth = Mathf.Max(0f, _currentHealth - damage);
        float appliedDamage = oldHealth - _currentHealth;
        if (appliedDamage > 0f && _cameraMotion)
        {
            _cameraMotion.PlayDamageFeedback(appliedDamage / Mathf.Max(1f, _maxHealth));
        }
        _nextRecoveryTime = Time.time + RecoveryDelay;
        if (_currentHealth <= 0f)
        {
            _isDead = true;
            wasKilled = true;
        }

        remainingHealth = _currentHealth;
        try
        {
            HealthChanged?.Invoke(_currentHealth, _maxHealth);
            if (wasKilled)
            {
                Died?.Invoke();
            }
        }
        finally
        {
            _isResolvingDamage = false;
        }

        return appliedDamage;
    }

    private void OnValidate()
    {
        _maxHealth = Mathf.Max(1f, _maxHealth);
    }

    private float RecoveryDelay
    {
        get
        {
            PlayerDataSO data = _lifeCtrl ? _lifeCtrl.PlayerData : null;
            return data ? Mathf.Max(0f, data.HealthRecoveryDelay) : 5f;
        }
    }

    private float RecoveryDuration
    {
        get
        {
            PlayerDataSO data = _lifeCtrl ? _lifeCtrl.PlayerData : null;
            return data ? Mathf.Max(0.1f, data.HealthRecoveryDuration) : 6f;
        }
    }
}
