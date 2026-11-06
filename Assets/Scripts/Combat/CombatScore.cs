using System;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerInfo))]
public sealed class CombatScore : MonoBehaviour
{
    private PlayerInfo _playerInfo;
    private EventMgr.Subscription _damageSubscription;
    private int _totalScore;

    public int TotalScore
    {
        get
        {
            return _totalScore;
        }
    }

    public event Action<int, int> ScoreChanged;

    private void Awake()
    {
        if (!TryGetComponent(out _playerInfo))
        {
            Debug.LogError($"{name} 缺少玩家身份组件。", this);
            enabled = false;
            return;
        }

    }

    private void OnEnable()
    {
        _damageSubscription = EventMgr.Bind(this, StartListening, StopListening);
    }

    private void OnDisable()
    {
        if (_damageSubscription != null)
        {
            _damageSubscription.Dispose();
            _damageSubscription = null;
        }
    }

    public void AddScore(int score)
    {
        if (score <= 0)
        {
            return;
        }

        _totalScore += score;
        ScoreChanged?.Invoke(score, _totalScore);
    }

    public void ResetScore()
    {
        _totalScore = 0;
        ScoreChanged?.Invoke(0, _totalScore);
    }

    private void ReceiveDamageResult(DamageResult result)
    {
        if (result == null || !_playerInfo || result.Instigator != _playerInfo)
        {
            return;
        }

        AddScore(result.AwardedScore);
    }

    private void StopListening()
    {
        CombatDamage.DamageResolved -= ReceiveDamageResult;
    }

    private void StartListening()
    {
        CombatDamage.DamageResolved += ReceiveDamageResult;
    }
}
