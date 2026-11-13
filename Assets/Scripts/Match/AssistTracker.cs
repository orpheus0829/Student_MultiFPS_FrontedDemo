using System.Collections.Generic;
using UnityEngine;

public sealed class AssistTracker
{
    private const float AssistThreshold = 0.25f;

    private readonly Dictionary<PlayerInfo, VictimDamageLedger> _damageByVictim =
        new Dictionary<PlayerInfo, VictimDamageLedger>();
    private readonly List<PlayerInfo> _resolved = new List<PlayerInfo>();

    public void RecordDamage(
        PlayerInfo attacker,
        PlayerInfo victim,
        float appliedDamage,
        float victimMaxHealth)
    {
        if (!attacker || !victim || attacker == victim || appliedDamage <= 0f ||
            victimMaxHealth <= 0f)
        {
            return;
        }

        if (!_damageByVictim.TryGetValue(victim, out VictimDamageLedger ledger))
        {
            ledger = new VictimDamageLedger(victimMaxHealth);
            _damageByVictim.Add(victim, ledger);
        }

        ledger.MaxHealth = Mathf.Max(ledger.MaxHealth, victimMaxHealth);
        ledger.Attackers.TryGetValue(attacker, out float totalDamage);
        ledger.Attackers[attacker] = totalDamage + Mathf.Min(appliedDamage, victimMaxHealth);
    }

    public IReadOnlyList<PlayerInfo> ResolveAssists(
        PlayerInfo killer,
        PlayerInfo victim)
    {
        _resolved.Clear();
        if (!victim || !_damageByVictim.TryGetValue(victim, out VictimDamageLedger ledger))
        {
            return _resolved;
        }

        float threshold = ledger.MaxHealth * AssistThreshold;
        foreach (KeyValuePair<PlayerInfo, float> entry in ledger.Attackers)
        {
            if (!entry.Key || entry.Key == killer || entry.Value < threshold)
            {
                continue;
            }

            _resolved.Add(entry.Key);
        }

        _damageByVictim.Remove(victim);
        return _resolved;
    }

    private sealed class VictimDamageLedger
    {
        public readonly Dictionary<PlayerInfo, float> Attackers =
            new Dictionary<PlayerInfo, float>();
        public float MaxHealth;

        public VictimDamageLedger(float maxHealth)
        {
            MaxHealth = maxHealth;
        }
    }

    public void ClearVictim(PlayerInfo victim)
    {
        if (victim)
        {
            _damageByVictim.Remove(victim);
        }
    }

    public void Clear()
    {
        _damageByVictim.Clear();
        _resolved.Clear();
    }
}
