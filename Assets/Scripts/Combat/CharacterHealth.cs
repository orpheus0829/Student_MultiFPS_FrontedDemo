using System;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterHealth : MonoBehaviour
{
    [Header("生命值")]
    [SerializeField, Min(1f), InspectorName("生命上限")] private float _maxHealth = 100f;

    private PlayerInfo _playerInfo;
    private float _currentHealth;
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
        ResetHealth();
    }

    public void ResetHealth()
    {
        if (_isResolvingDamage)
        {
            return;
        }

        _currentHealth = Mathf.Max(1f, _maxHealth);
        _isDead = false;
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
}
