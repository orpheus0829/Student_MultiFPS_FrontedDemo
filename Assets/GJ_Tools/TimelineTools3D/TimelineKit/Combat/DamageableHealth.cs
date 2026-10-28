using System;
using UnityEngine;

[AddComponentMenu("TimelineKit/DamageableHealth")]
public class DamageableHealth : MonoBehaviour, IDamageable
{
    [Header("生命值")]
    [Tooltip("生命上限，用于 Heal/ResetHp 的封顶")]
    public float maxHp = 100f;
    [SerializeField] private float _hp = 100f;

    public event Action<float, float> onDamaged;
    public event Action onDeath;

    public float Hp => _hp;

    public bool IsDead => _hp <= 0f;

    private void Awake()
    {
        if (_hp <= 0f)
        {
            _hp = maxHp;
        }
    }

    public void TakeDamage(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }
        _hp -= amount;
        if (_hp < 0f)
        {
            _hp = 0f;
        }
        onDamaged?.Invoke(amount, _hp);
        if (_hp <= 0f)
        {
            onDeath?.Invoke();
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || IsDead)
        {
            return;
        }
        _hp = Mathf.Min(maxHp, _hp + amount);
        onDamaged?.Invoke(-amount, _hp);
    }

    public void ResetHp()
    {
        _hp = maxHp;
    }

    public void Kill()
    {
        TakeDamage(_hp);
    }
}
