using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BombEconomyService : MonoBehaviour
{
    public readonly struct ShopItem
    {
        public readonly string Name;
        public readonly string ResourceId;
        public readonly int Price;
        public readonly BombShopItemKind Kind;
        public readonly BombShopColumn Column;
        public readonly string ItemId;

        public ShopItem(
            string itemId,
            string name,
            string resourceId,
            int price,
            BombShopItemKind kind,
            BombShopColumn column)
        {
            ItemId = itemId ?? string.Empty;
            Name = name;
            ResourceId = resourceId;
            Price = Mathf.Max(0, price);
            Kind = kind;
            Column = column;
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
    private ResourceMgr _resources;
    private BombShopCatalogSO _catalog;
    private string _catalogResourceId;
    private int _buyWindowRoundIndex = -1;

    public IReadOnlyList<ShopItem> ShopItems { get { return _shopItems; } }
    public string ArmorCrateResourceId
    {
        get { return _catalog ? _catalog.ArmorCrateResourceId : string.Empty; }
    }
    public string HelmetCrateResourceId
    {
        get { return _catalog ? _catalog.HelmetCrateResourceId : string.Empty; }
    }
    public int ArmorPrice
    {
        get
        {
            return TryGetShopItem(BombShopItemKind.Armor, out ShopItem item)
                ? item.Price
                : GetInt("economy.armorPrice", 650);
        }
    }
    public int HelmetPrice
    {
        get
        {
            return TryGetShopItem(BombShopItemKind.Helmet, out ShopItem item)
                ? item.Price
                : GetInt("economy.helmetPrice", 350);
        }
    }
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
    public float BuyWindowRemaining
    {
        get
        {
            return IsBuyWindowState()
                ? Mathf.Max(0f, _buyDeadline - Time.unscaledTime)
                : 0f;
        }
    }
    public int BuyWindowRoundIndex
    {
        get { return _buyWindowRoundIndex; }
    }
    public int AttackerTeam
    {
        get
        {
            return _registry != null
                ? _registry.GetInt("bomb.attackerTeam", BombRules.DefaultAttackerTeam)
                : BombRules.DefaultAttackerTeam;
        }
    }

    public bool IsBuyWindowOpen
    {
        get
        {
            return IsBuyWindowState() &&
                Time.unscaledTime <= _buyDeadline;
        }
    }

    public event Action CatalogChanged;

    public void Initialize(MatchManager match, ModeConfigRegistry registry)
    {
        _match = match;
        _registry = registry;
        _match.KillAwarded += HandleKillAwarded;
        _match.RoundStateChanged += HandleRoundStateChanged;
        BuildFallbackShopItems();
        _catalogResourceId = _registry.GetString(
            "economy.shopCatalogResourceId",
            "economy.bomb.shop_catalog");
        _ = LoadCatalogAsync();
        ParseKillRewards(_registry.GetString("economy.killRewardOverrides",
            "mps5=600;pdw90=600;kxg-12=900;melee-fist=1500"));
    }

    private void OnDestroy()
    {
        if (_match)
        {
            _match.KillAwarded -= HandleKillAwarded;
            _match.RoundStateChanged -= HandleRoundStateChanged;
        }
        if (_resources && _catalog && !string.IsNullOrWhiteSpace(_catalogResourceId))
        {
            _resources.Release<BombShopCatalogSO>(_catalogResourceId);
        }
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

    public float GetTeamMoneyRatio(int team)
    {
        if (!_match)
        {
            return 1f;
        }

        IReadOnlyList<MatchParticipant> participants = _match.GetParticipantsByTeam(team);
        float totalMoney = 0f;
        int accountCount = 0;
        for (int i = 0; i < participants.Count; i++)
        {
            PlayerInfo player = participants[i].Info;
            if (!player || !_accounts.TryGetValue(player, out Account account))
            {
                continue;
            }

            totalMoney += account.Money;
            accountCount++;
        }

        if (accountCount == 0)
        {
            return 1f;
        }

        float averageMoney = totalMoney / accountCount;
        return Mathf.Clamp01(averageMoney / 4500f);
    }

    public bool CanOpenShop(MatchParticipant participant)
    {
        if (!IsBuyWindowOpen || participant == null || !participant.Info || !_match)
        {
            return false;
        }

        // 倒计时从 5 开始就是全员购买阶段，玩家尚未完全落入复活区包围盒时也应可开店。
        // 倒计时结束后的追加购买时间才要求玩家仍留在分配到的正式复活区内。
        return _match.State == MatchState.Countdown ||
            _match.IsInsideAssignedSpawnZone(participant.Info);
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

    public void BeginRoundBuyWindow()
    {
        if (!_match || _match.RoundIndex == _buyWindowRoundIndex)
        {
            return;
        }

        _buyWindowRoundIndex = _match.RoundIndex;
        DroppedWeaponEntity.ClearAll();
        float afterCountdownDuration = Mathf.Max(0f, _registry.GetFloat(
            "economy.buyAfterCountdownDuration",
            5f));
        _buyDeadline = Time.unscaledTime +
            Mathf.Max(0f, _match.CountdownRemaining) +
            afterCountdownDuration;
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
        if (state == MatchState.Countdown)
        {
            BeginRoundBuyWindow();
        }
    }

    private bool IsBuyWindowState()
    {
        return _match &&
            (_match.State == MatchState.Countdown ||
            _match.State == MatchState.InRound);
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

    public bool TryGetShopItem(BombShopItemKind kind, out ShopItem item)
    {
        for (int i = 0; i < _shopItems.Count; i++)
        {
            if (_shopItems[i].Kind == kind)
            {
                item = _shopItems[i];
                return true;
            }
        }

        item = default;
        return false;
    }

    private async Task LoadCatalogAsync()
    {
        if (string.IsNullOrWhiteSpace(_catalogResourceId))
        {
            return;
        }

        try
        {
            _resources = ResourceMgr.instance;
            BombShopCatalogSO catalog =
                await _resources.LoadAsync<BombShopCatalogSO>(_catalogResourceId);
            if (!this || !catalog)
            {
                if (catalog)
                {
                    _resources.Release<BombShopCatalogSO>(_catalogResourceId);
                }
                return;
            }

            _catalog = catalog;
            _shopItems.Clear();
            for (int i = 0; i < catalog.Items.Count; i++)
            {
                BombShopItemConfig configured = catalog.Items[i];
                _shopItems.Add(new ShopItem(
                    configured.ItemId,
                    configured.DisplayName,
                    configured.ResourceId,
                    configured.Price,
                    configured.Kind,
                    configured.Column));
            }
            CatalogChanged?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"爆破商店目录加载失败，继续使用内置兜底：{exception.Message}", this);
        }
    }

    private void BuildFallbackShopItems()
    {
        _shopItems.Clear();
        _shopItems.Add(new ShopItem("x18", "X18", "weapon.x18.data", 400,
            BombShopItemKind.Sidearm, BombShopColumn.Pistol));
        _shopItems.Add(new ShopItem("m1911", "M1911", "weapon.m1911.data", 500,
            BombShopItemKind.Sidearm, BombShopColumn.Pistol));
        _shopItems.Add(new ShopItem("dgl-50", "DGL50", "weapon.dgl-50.data", 750,
            BombShopItemKind.Sidearm, BombShopColumn.Pistol));
        _shopItems.Add(new ShopItem("viper-357", "Viper-357", "weapon.viper-357.data", 800,
            BombShopItemKind.Sidearm, BombShopColumn.Pistol));
        _shopItems.Add(new ShopItem("mps5", "MPS5", "weapon.mps5.data", 1500,
            BombShopItemKind.Primary, BombShopColumn.Light));
        _shopItems.Add(new ShopItem("striker-v", "Striker-V", "weapon.striker-v.data", 1750,
            BombShopItemKind.Primary, BombShopColumn.Light));
        _shopItems.Add(new ShopItem("kxg12", "KXG12", "weapon.kxg-12.data", 2000,
            BombShopItemKind.Primary, BombShopColumn.Light));
        _shopItems.Add(new ShopItem("drake-12", "Drake-12", "weapon.drake-12.data", 2100,
            BombShopItemKind.Primary, BombShopColumn.Light));
        _shopItems.Add(new ShopItem("pdw90", "PDW90", "weapon.pdw90.data", 2350,
            BombShopItemKind.Primary, BombShopColumn.Medium));
        _shopItems.Add(new ShopItem("mx16a4", "MX16A4", "weapon.mx16a4.data", 2450,
            BombShopItemKind.Primary, BombShopColumn.Medium));
        _shopItems.Add(new ShopItem("ak", "AK", "weapon.ak.data", 2700,
            BombShopItemKind.Primary, BombShopColumn.Medium));
        _shopItems.Add(new ShopItem("g3", "G3", "weapon.g3.data", 2800,
            BombShopItemKind.Primary, BombShopColumn.Medium));
        _shopItems.Add(new ShopItem("as-val", "AS Val", "weapon.as-val.data", 2900,
            BombShopItemKind.Primary, BombShopColumn.Heavy));
        _shopItems.Add(new ShopItem("mk14-ebr", "Mk14 EBR", "weapon.mk14-ebr.data", 3100,
            BombShopItemKind.Primary, BombShopColumn.Heavy));
        _shopItems.Add(new ShopItem("mgx5", "MGX5", "weapon.mgx5.data", 3200,
            BombShopItemKind.Primary, BombShopColumn.Heavy));
        _shopItems.Add(new ShopItem("svd", "SVD", "weapon.svd.data", 3300,
            BombShopItemKind.Primary, BombShopColumn.Heavy));
        _shopItems.Add(new ShopItem("kar98k", "Kar98k", "weapon.kar98k.data", 3400,
            BombShopItemKind.Primary, BombShopColumn.Heavy));
        _shopItems.Add(new ShopItem("l96x", "L96X", "weapon.l96x.data", 3900,
            BombShopItemKind.Primary, BombShopColumn.Heavy));
        _shopItems.Add(new ShopItem("armor", "胸甲", string.Empty, 650,
            BombShopItemKind.Armor, BombShopColumn.Protection));
        _shopItems.Add(new ShopItem("helmet", "头盔", string.Empty, 350,
            BombShopItemKind.Helmet, BombShopColumn.Protection));
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
