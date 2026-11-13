using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BombEconomyService : MonoBehaviour
{
    public readonly struct ShopItem
    {
        public readonly string Name;
        public readonly string ResourceId;
        public readonly int Price;
        public ShopItem(string name, string resourceId, int price)
        {
            Name = name;
            ResourceId = resourceId;
            Price = Mathf.Max(0, price);
        }
    }

    public sealed class Account
    {
        public int Money { get; internal set; }
        public event Action<int> Changed;
        internal void Notify() { Changed?.Invoke(Money); }
    }

    private readonly Dictionary<PlayerInfo, Account> _accounts =
        new Dictionary<PlayerInfo, Account>();
    private MatchManager _match;
    private ModeConfigRegistry _registry;
    private float _buyDeadline;
    private readonly List<ShopItem> _shopItems = new List<ShopItem>();
    private readonly Dictionary<string, int> _killRewardOverrides =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<ShopItem> ShopItems { get { return _shopItems; } }
    public int ArmorPrice { get { return GetInt("economy.armorPrice", 650); } }
    public int HelmetPrice { get { return GetInt("economy.helmetPrice", 350); } }
    public float PickupDistance
    {
        get { return _registry.GetFloat("economy.pickupDistance", 3.2f); }
    }
    public float DropForwardSpeed
    {
        get { return _registry.GetFloat("economy.dropForwardSpeed", 2f); }
    }
    public float DropUpSpeed
    {
        get { return _registry.GetFloat("economy.dropUpSpeed", 0.6f); }
    }

    public bool IsBuyWindowOpen
    {
        get
        {
            return _match && _match.State == MatchState.InRound &&
                Time.unscaledTime <= _buyDeadline;
        }
    }

    public void Initialize(MatchManager match, ModeConfigRegistry registry)
    {
        _match = match;
        _registry = registry;
        _match.KillAwarded += HandleKillAwarded;
        _match.RoundStateChanged += HandleRoundStateChanged;
        ParseShopItems(_registry.GetString("economy.shopItems",
            "AK|weapon.ak.data|2700;AS Val|weapon.as-val.data|2900;" +
            "MPS5|weapon.mps5.data|1500;PDW90|weapon.pdw90.data|2350;" +
            "KXG12|weapon.kxg-12.data|2000;Kar98k|weapon.kar98k.data|3200"));
        ParseKillRewards(_registry.GetString("economy.killRewardOverrides",
            "mps5=600;pdw90=600;kxg-12=900;melee-fist=1500"));
    }

    private void OnDestroy()
    {
        if (!_match) return;
        _match.KillAwarded -= HandleKillAwarded;
        _match.RoundStateChanged -= HandleRoundStateChanged;
    }

    public Account Bind(MatchParticipant participant)
    {
        if (participant == null || !participant.Info) return null;
        if (!_accounts.TryGetValue(participant.Info, out Account account))
        {
            account = new Account
            {
                Money = GetInt("economy.startMoney", 800)
            };
            _accounts.Add(participant.Info, account);
        }
        return account;
    }

    public bool TrySpend(PlayerInfo player, int amount)
    {
        if (!IsBuyWindowOpen || !player || amount < 0 ||
            !_accounts.TryGetValue(player, out Account account) ||
            account.Money < amount)
        {
            return false;
        }

        account.Money -= amount;
        account.Notify();
        return true;
    }

    public void Refund(PlayerInfo player, int amount)
    {
        Award(player, Mathf.Max(0, amount));
    }

    public void AwardRound(int winningTeam)
    {
        if (!_match) return;
        IReadOnlyList<MatchParticipant> roster = _match.Roster;
        for (int i = 0; i < roster.Count; i++)
        {
            MatchParticipant participant = roster[i];
            Award(participant.Info, participant.Team == winningTeam
                ? GetInt("economy.roundWinReward", 3250)
                : GetInt("economy.roundLossReward", 1900));
        }
    }

    private void HandleKillAwarded(KillAwardedEvent data)
    {
        if (data == null || data.Killer == null) return;
        int reward = GetKillReward(data.WeaponId);
        if (data.WasHeadshot)
        {
            reward += GetInt("economy.headshotBonus", 100);
        }
        Award(data.Killer.Info, reward);
    }

    private int GetKillReward(string weaponId)
    {
        if (!string.IsNullOrWhiteSpace(weaponId) &&
            _killRewardOverrides.TryGetValue(weaponId, out int reward))
        {
            return reward;
        }
        return GetInt("economy.killReward", 300);
    }

    private void HandleRoundStateChanged(MatchState state)
    {
        if (state == MatchState.InRound)
        {
            DroppedWeaponEntity.ClearAll();
            _buyDeadline = Time.unscaledTime +
                Mathf.Max(1f, _registry.GetFloat("economy.buyDuration", 25f));
            IReadOnlyList<MatchParticipant> roster = _match.Roster;
            for (int i = 0; i < roster.Count; i++)
            {
                if (roster[i].Info && roster[i].Info.TryGetComponent(
                    out BombEconomyPlayerCtrl economyPlayer))
                {
                    economyPlayer.AutoBuyForRound();
                }
            }
        }
    }

    private void Award(PlayerInfo player, int amount)
    {
        if (!player || amount <= 0 || !_accounts.TryGetValue(player, out Account account))
            return;
        int maximum = GetInt("economy.maxMoney", 16000);
        account.Money = Mathf.Clamp(account.Money + amount, 0, maximum);
        account.Notify();
    }

    private int GetInt(string key, int fallback)
    {
        return _registry != null ? _registry.GetInt(key, fallback) : fallback;
    }

    private void ParseShopItems(string serialized)
    {
        _shopItems.Clear();
        string[] entries = (serialized ?? string.Empty).Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string[] fields = entries[i].Split('|');
            if (fields.Length != 3 || string.IsNullOrWhiteSpace(fields[1]) ||
                !int.TryParse(fields[2], out int price))
            {
                continue;
            }
            _shopItems.Add(new ShopItem(fields[0].Trim(), fields[1].Trim(), price));
        }
    }

    private void ParseKillRewards(string serialized)
    {
        _killRewardOverrides.Clear();
        string[] entries = (serialized ?? string.Empty).Split(';');
        for (int i = 0; i < entries.Length; i++)
        {
            string[] fields = entries[i].Split('=');
            if (fields.Length == 2 && !string.IsNullOrWhiteSpace(fields[0]) &&
                int.TryParse(fields[1], out int reward))
            {
                _killRewardOverrides[fields[0].Trim()] = Mathf.Max(0, reward);
            }
        }
    }
}
