using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public sealed class BombEconomyPlayerCtrl : MonoBehaviour
{
    private const int EcoReserve = 1400;
    private const int RushReserve = 650;
    private const int RushProtectionReserve = 250;
    private const int DefaultReserve = 800;
    private const int DefaultProtectionReserve = 450;

    private BombEconomyService _service;
    private BombEconomyService.Account _account;
    private MatchParticipant _participant;
    private PlayerInfo _playerInfo;
    private PlayerInputCtrl _inputCtrl;
    private PlayerActionCtrl _actionCtrl;
    private PlayerWeaponInventoryCtrl _inventory;
    private WeaponCombatCtrl _combat;
    private PlayerWeaponViewCtrl _view;
    private PlayerArmorCtrl _armor;
    private PlayerWeaponWorldInteractionCtrl _worldInteraction;
    private PlayerCharacterModelCtrl _models;
    private PlayerMoveCtrl _moveCtrl;
    private readonly Dictionary<string, Sprite> _shopSprites =
        new Dictionary<string, Sprite>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _shopSpriteResourceIds =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private BombShopPreviewStage _previewStage;
    private string _hoveredItemId;
    private bool _shopArtLoading;
    private string _pistolResourceId = "weapon.m1911.data";
    private bool _shopOpen;
    private bool _transactionBusy;
    private bool _autoBuyBusy;
    private bool _autoBuyRequested;
    private bool _loadoutReady;
    private bool _initialized;
    private bool _isLocal;
    private int _lastAutoBuyRound = int.MinValue;

    public bool IsActive { get { return _initialized && _service; } }

    public void Initialize(
        BombEconomyService service,
        MatchParticipant participant,
        string pistolResourceId)
    {
        _service = service;
        _participant = participant;
        _playerInfo = participant.Info;
        _isLocal = participant.IsLocalPlayer;
        _pistolResourceId = string.IsNullOrWhiteSpace(pistolResourceId)
            ? _pistolResourceId : pistolResourceId;
        TryGetComponent(out _inputCtrl);
        TryGetComponent(out _actionCtrl);
        TryGetComponent(out _inventory);
        TryGetComponent(out _combat);
        TryGetComponent(out _view);
        TryGetComponent(out _armor);
        TryGetComponent(out _worldInteraction);
        TryGetComponent(out _models);
        TryGetComponent(out _moveCtrl);
        if (!_armor) _armor = gameObject.AddComponent<PlayerArmorCtrl>();
        _account = _service.Bind(participant);
        _service.CatalogChanged -= HandleCatalogChanged;
        _service.CatalogChanged += HandleCatalogChanged;
        _loadoutReady = false;
        _autoBuyRequested = false;
        _lastAutoBuyRound = int.MinValue;
        _initialized = true;
        if (_actionCtrl)
        {
            _actionCtrl.CommandReceived -= HandleCommand;
            _actionCtrl.CommandReceived += HandleCommand;
        }
        _ = InitializeLoadoutAsync();
    }

    private async Task InitializeLoadoutAsync()
    {
        if (!_inventory)
        {
            return;
        }

        _loadoutReady = await _inventory.ConfigureBombLoadoutAsync(
            _pistolResourceId);
        if (this && _loadoutReady && _participant != null &&
            _participant.IsAI)
        {
            AutoBuyForRound();
        }
    }

    private void OnDisable()
    {
        if (_actionCtrl) _actionCtrl.CommandReceived -= HandleCommand;
        CloseShop();
    }

    public void Shutdown()
    {
        _initialized = false;
        _autoBuyRequested = false;
        _loadoutReady = false;
        if (_service)
        {
            _service.CatalogChanged -= HandleCatalogChanged;
        }
        if (_actionCtrl) _actionCtrl.CommandReceived -= HandleCommand;
        CloseShop();
        ReleaseShopArt();
    }

    public void AutoBuyForRound()
    {
        if (!_initialized || _participant == null || !_participant.IsAI ||
            !_service || !_service.IsBuyWindowOpen || _account == null)
        {
            return;
        }

        if (!_loadoutReady || _autoBuyBusy)
        {
            _autoBuyRequested = true;
            return;
        }

        if (_lastAutoBuyRound == _service.BuyWindowRoundIndex)
        {
            return;
        }

        _ = AutoBuyForRoundAsync();
    }

    private async Task AutoBuyForRoundAsync()
    {
        _autoBuyBusy = true;
        _autoBuyRequested = false;
        int round = _service.BuyWindowRoundIndex;
        try
        {
            AITacticalRole role = AITacticalCoordinator.GetRole(
                _participant.Slot);
            AIBombRoundPlan plan = AITacticalCoordinator.EnsureBombPlan(
                _participant.Team,
                round,
                _service.GetTeamMoneyRatio(_participant.Team),
                _participant.Team == _service.AttackerTeam);

            if (plan == AIBombRoundPlan.Eco)
            {
                await BuyEcoLoadoutAsync(role, round);
            }
            else if (plan == AIBombRoundPlan.RushA ||
                plan == AIBombRoundPlan.RushB)
            {
                await BuyRushLoadoutAsync(role, round);
            }
            else
            {
                await BuyDefaultLoadoutAsync(role, round);
            }

            if (this)
            {
                _lastAutoBuyRound = round;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"AI 自动购买失败：{exception.Message}",
                this);
        }
        finally
        {
            _autoBuyBusy = false;
            if (this && _autoBuyRequested)
            {
                AutoBuyForRound();
            }
        }
    }

    private async Task BuyEcoLoadoutAsync(AITacticalRole role, int round)
    {
        int reserve = EcoReserve + Mathf.Abs(_participant.Slot % 3) * 150;
        bool hasPrimary = !string.IsNullOrWhiteSpace(
            _inventory.PrimaryWeaponResourceId);
        if (hasPrimary)
        {
            await TryBuyEquipmentWithinBudgetAsync(
                BombShopItemKind.Armor,
                reserve);
            return;
        }

        if (role == AITacticalRole.Entry || role == AITacticalRole.Scout ||
            role == AITacticalRole.Lurker)
        {
            await TryBuySidearmWithinBudgetAsync(reserve, role, round);
        }
        else if (role == AITacticalRole.Support)
        {
            await TryBuyEquipmentWithinBudgetAsync(
                BombShopItemKind.Armor,
                reserve);
        }
    }

    private async Task BuyRushLoadoutAsync(AITacticalRole role, int round)
    {
        await TryBuyPrimaryWithinBudgetAsync(
            BombShopColumn.Light,
            RushReserve,
            role,
            round);
        await TryBuyEquipmentWithinBudgetAsync(
            BombShopItemKind.Armor,
            RushProtectionReserve);
        await TryBuyEquipmentWithinBudgetAsync(
            BombShopItemKind.Helmet,
            RushProtectionReserve);
    }

    private async Task BuyDefaultLoadoutAsync(AITacticalRole role, int round)
    {
        BombShopColumn preferredColumn = GetPreferredColumn(role);
        int reserve = DefaultReserve;
        int protectionReserve = DefaultProtectionReserve;
        if (role == AITacticalRole.Anchor || role == AITacticalRole.Lurker)
        {
            reserve += 200;
            protectionReserve += 200;
        }

        await TryBuyPrimaryWithinBudgetAsync(
            preferredColumn,
            reserve,
            role,
            round);
        await TryBuyEquipmentWithinBudgetAsync(
            BombShopItemKind.Armor,
            protectionReserve);
        await TryBuyEquipmentWithinBudgetAsync(
            BombShopItemKind.Helmet,
            protectionReserve);
    }

    private async Task<bool> TryBuyPrimaryWithinBudgetAsync(
        BombShopColumn preferredColumn,
        int reserve,
        AITacticalRole role,
        int round)
    {
        if (!string.IsNullOrWhiteSpace(_inventory.PrimaryWeaponResourceId))
        {
            return true;
        }

        int spendingBudget = _account.Money - reserve;
        int columnIndex = (int)preferredColumn;
        while (columnIndex >= (int)BombShopColumn.Light)
        {
            BombShopColumn column = (BombShopColumn)columnIndex;
            if (TrySelectShopItem(
                    BombShopItemKind.Primary,
                    column,
                    spendingBudget,
                    role,
                    round,
                    string.Empty,
                    out BombEconomyService.ShopItem item))
            {
                return await TryPurchaseItemAsync(item);
            }

            columnIndex--;
        }

        return false;
    }

    private async Task<bool> TryBuySidearmWithinBudgetAsync(
        int reserve,
        AITacticalRole role,
        int round)
    {
        string currentSidearm = _inventory.GetWeaponResourceId(1);
        if (!TrySelectShopItem(
                BombShopItemKind.Sidearm,
                BombShopColumn.Pistol,
                _account.Money - reserve,
                role,
                round,
                currentSidearm,
                out BombEconomyService.ShopItem item))
        {
            return false;
        }

        return await TryPurchaseItemAsync(item);
    }

    private async Task<bool> TryBuyEquipmentWithinBudgetAsync(
        BombShopItemKind kind,
        int reserve)
    {
        if (!_service.TryGetShopItem(
                kind,
                out BombEconomyService.ShopItem item) ||
            _account.Money - item.Price < reserve)
        {
            return false;
        }

        return await TryPurchaseEquipmentAsync(kind);
    }

    private bool TrySelectShopItem(
        BombShopItemKind kind,
        BombShopColumn column,
        int spendingBudget,
        AITacticalRole role,
        int round,
        string excludedResourceId,
        out BombEconomyService.ShopItem selected)
    {
        int candidateCount = 0;
        for (int i = 0; i < _service.ShopItems.Count; i++)
        {
            BombEconomyService.ShopItem item = _service.ShopItems[i];
            if (IsPurchaseCandidate(
                    item,
                    kind,
                    column,
                    spendingBudget,
                    excludedResourceId))
            {
                candidateCount++;
            }
        }

        if (candidateCount <= 0)
        {
            selected = default;
            return false;
        }

        int seed = _participant.Slot * 31 + round * 17 + (int)role * 13;
        int selectedIndex = Mathf.Abs(seed % candidateCount);
        for (int i = 0; i < _service.ShopItems.Count; i++)
        {
            BombEconomyService.ShopItem item = _service.ShopItems[i];
            if (!IsPurchaseCandidate(
                    item,
                    kind,
                    column,
                    spendingBudget,
                    excludedResourceId))
            {
                continue;
            }

            if (selectedIndex == 0)
            {
                selected = item;
                return true;
            }

            selectedIndex--;
        }

        selected = default;
        return false;
    }

    private static bool IsPurchaseCandidate(
        BombEconomyService.ShopItem item,
        BombShopItemKind kind,
        BombShopColumn column,
        int spendingBudget,
        string excludedResourceId)
    {
        return item.Kind == kind &&
            item.Column == column &&
            item.Price <= spendingBudget &&
            !string.Equals(
                item.ResourceId,
                excludedResourceId,
                StringComparison.OrdinalIgnoreCase);
    }

    private static BombShopColumn GetPreferredColumn(AITacticalRole role)
    {
        switch (role)
        {
            case AITacticalRole.Entry:
                return BombShopColumn.Light;
            case AITacticalRole.Anchor:
            case AITacticalRole.Scout:
                return BombShopColumn.Heavy;
            case AITacticalRole.Support:
            case AITacticalRole.Lurker:
            default:
                return BombShopColumn.Medium;
        }
    }

    public bool TryOpenShop()
    {
        if (_shopOpen)
        {
            return true;
        }

        if (!_initialized || !_isLocal || !_service ||
            !_service.CanOpenShop(_participant))
        {
            return false;
        }

        _shopOpen = true;
        _inputCtrl?.SetGameplayInputBlocked(this, true);
        _actionCtrl?.SetGameplayInputBlocked(this, true);
        _actionCtrl?.Receive(new PlayerInputCommand(
            GJInputAction.Fire,
            PlayerInputPhase.Canceled));
        _actionCtrl?.Receive(new PlayerInputCommand(
            GJInputAction.Aim,
            PlayerInputPhase.Canceled));
        _actionCtrl?.Receive(new PlayerInputCommand(
            GJInputAction.Sprint,
            PlayerInputPhase.Canceled));
        _moveCtrl?.SetExternalLock(true);
        CameraControlAuthority.Acquire(this);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        _previewStage = new BombShopPreviewStage(
            _models,
            _inventory,
            _view,
            _armor,
            _service);
        _previewStage.Open();
        _ = LoadShopArtAsync();
        return true;
    }

    private void HandleCommand(PlayerInputCommand command)
    {
        if (!_initialized || command.Phase != PlayerInputPhase.Started) return;
        switch (command.Action)
        {
            case GJInputAction.OpenShop:
                if (_isLocal) ToggleShop();
                break;
        }
    }

    private void ToggleShop()
    {
        if (_shopOpen)
        {
            CloseShop();
            return;
        }

        TryOpenShop();
    }

    private void CloseShop()
    {
        if (!_shopOpen) return;
        _shopOpen = false;
        _inputCtrl?.SetGameplayInputBlocked(this, false);
        _actionCtrl?.SetGameplayInputBlocked(this, false);
        _moveCtrl?.SetExternalLock(false);
        CameraControlAuthority.Release(this);
        _previewStage?.Close();
        _previewStage = null;
        _hoveredItemId = string.Empty;
        if (_isLocal)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public async Task<bool> TryPurchaseAsync(string itemId)
    {
        if (!_initialized || string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        for (int i = 0; i < _service.ShopItems.Count; i++)
        {
            BombEconomyService.ShopItem item = _service.ShopItems[i];
            if (string.Equals(item.ItemId, itemId, StringComparison.OrdinalIgnoreCase))
            {
                return await TryPurchaseItemAsync(item);
            }
        }

        return false;
    }

    private async Task<bool> TryPurchaseItemAsync(BombEconomyService.ShopItem item)
    {
        if (item.Kind == BombShopItemKind.Armor)
        {
            return await TryPurchaseEquipmentAsync(BombShopItemKind.Armor);
        }
        if (item.Kind == BombShopItemKind.Helmet)
        {
            return await TryPurchaseEquipmentAsync(BombShopItemKind.Helmet);
        }
        if (_transactionBusy || !_service.TrySpend(_playerInfo, item.Price))
        {
            return false;
        }

        _transactionBusy = true;
        try
        {
            bool success = _worldInteraction
                ? await _worldInteraction.TryAcquireWeaponAsync(item.ResourceId)
                : await _inventory.AddOrReplaceWeaponAsync(
                    item.ResourceId,
                    item.Kind == BombShopItemKind.Sidearm);
            if (!success)
            {
                _service.Refund(_playerInfo, item.Price);
                return false;
            }

            _previewStage?.RefreshDefault();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"购买武器 {item.Name} 失败：{exception.Message}", this);
            _service.Refund(_playerInfo, item.Price);
            return false;
        }
        finally
        {
            _transactionBusy = false;
        }
    }

    private async Task<bool> TryPurchaseEquipmentAsync(BombShopItemKind kind)
    {
        if (_transactionBusy || !_service.TryGetShopItem(kind, out BombEconomyService.ShopItem item))
        {
            return false;
        }
        if (kind == BombShopItemKind.Armor &&
            _armor.Armor >= _armor.MaximumArmor)
        {
            return false;
        }
        if (kind == BombShopItemKind.Helmet && _armor.HasHelmet)
        {
            return false;
        }
        if (!_service.TrySpend(_playerInfo, item.Price))
        {
            return false;
        }

        _transactionBusy = true;
        ResourceMgr resources = ResourceMgr.instance;
        BombEquipmentDefinitionSO equipment = null;
        try
        {
            equipment = await resources.LoadAsync<BombEquipmentDefinitionSO>(
                item.ResourceId);
            if (!this || !equipment)
            {
                _service.Refund(_playerInfo, item.Price);
                return false;
            }

            if (equipment.Kind == BombEquipmentKind.Armor)
            {
                _armor.EquipArmor(
                    equipment.ArmorCapacity,
                    equipment.BodyAbsorption);
            }
            else
            {
                _armor.EquipHelmet(equipment.HeadAbsorption);
            }
            _previewStage?.RefreshDefault();
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"购买装备 {item.Name} 失败：{exception.Message}", this);
            _service.Refund(_playerInfo, item.Price);
            return false;
        }
        finally
        {
            if (equipment)
            {
                resources.Release<BombEquipmentDefinitionSO>(item.ResourceId);
            }
            _transactionBusy = false;
        }
    }

    public void DropWeaponsOnDeath()
    {
        if (_armor)
        {
            _armor.ClearEquipment();
        }
        _ = DropPrimaryOnDeathAsync();
    }

    public void PrepareForRound()
    {
        if (_inventory && string.IsNullOrWhiteSpace(_inventory.PrimaryWeaponResourceId))
        {
            _ = _inventory.AddOrReplaceWeaponAsync(_pistolResourceId, true);
        }
    }

    private async Task DropPrimaryOnDeathAsync()
    {
        if (!_inventory || string.IsNullOrWhiteSpace(_inventory.PrimaryWeaponResourceId))
            return;
        string primaryId = _inventory.PrimaryWeaponResourceId;
        Vector3 position = transform.position + Vector3.up * 0.8f;
        if (_inventory.CurrentWeaponResourceId == primaryId && _view &&
            _view.ThirdPersonWeapon)
        {
            DroppedWeaponEntity dropped = DroppedWeaponEntity.Create(
                _view, primaryId,
                _combat ? _combat.MagazineAmmo : 0,
                _combat ? _combat.ReserveAmmo : 0,
                position, transform.forward * 1.2f + Vector3.up * 0.5f);
            if (dropped)
            {
                _inventory.ClearPrimaryOwnership();
            }
            return;
        }

        ResourceMgr resources = ResourceMgr.instance;
        WeaponDefinitionSO data = await resources.LoadAsync<WeaponDefinitionSO>(primaryId);
        if (!this || !data) return;
        int magazineAmmo = data.Ammo.MagazineCapacity;
        int reserveAmmo = data.Ammo.StartingReserveAmmo;
        if (_combat)
        {
            _combat.TryGetAmmo(data, out magazineAmmo, out reserveAmmo);
        }
        DroppedWeaponEntity resourceDrop =
            await DroppedWeaponEntity.CreateFromResourceAsync(
                primaryId,
                magazineAmmo,
                reserveAmmo,
                position,
                transform.rotation,
                transform.forward * 1.2f + Vector3.up * 0.5f);
        if (this && resourceDrop)
        {
            _inventory.ClearPrimaryOwnership();
        }
        resources.Release<WeaponDefinitionSO>(primaryId);
    }

    private void HandleCatalogChanged()
    {
        if (_shopOpen)
        {
            _ = LoadShopArtAsync();
        }
    }

    private async Task LoadShopArtAsync()
    {
        if (_shopArtLoading || !_service)
        {
            return;
        }

        _shopArtLoading = true;
        ResourceMgr resources = ResourceMgr.instance;
        try
        {
            for (int i = 0; i < _service.ShopItems.Count; i++)
            {
                BombEconomyService.ShopItem item = _service.ShopItems[i];
                if (_shopSprites.ContainsKey(item.ItemId))
                {
                    continue;
                }

                string silhouetteId = await ResolveSilhouetteIdAsync(resources, item);
                if (!this || string.IsNullOrWhiteSpace(silhouetteId))
                {
                    continue;
                }

                Sprite sprite = await resources.LoadAsync<Sprite>(silhouetteId);
                if (!this || !sprite)
                {
                    if (sprite)
                    {
                        resources.Release<Sprite>(silhouetteId);
                    }
                    continue;
                }

                _shopSprites[item.ItemId] = sprite;
                _shopSpriteResourceIds[item.ItemId] = silhouetteId;
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning("爆破商店剪影加载失败：" + exception.Message, this);
        }
        finally
        {
            _shopArtLoading = false;
        }
    }

    private static async Task<string> ResolveSilhouetteIdAsync(
        ResourceMgr resources,
        BombEconomyService.ShopItem item)
    {
        if (item.Kind == BombShopItemKind.Armor ||
            item.Kind == BombShopItemKind.Helmet)
        {
            BombEquipmentDefinitionSO equipment =
                await resources.LoadAsync<BombEquipmentDefinitionSO>(item.ResourceId);
            if (!equipment)
            {
                return string.Empty;
            }
            string silhouetteId = equipment.SilhouetteResourceId;
            resources.Release<BombEquipmentDefinitionSO>(item.ResourceId);
            return silhouetteId;
        }

        WeaponDefinitionSO weapon =
            await resources.LoadAsync<WeaponDefinitionSO>(item.ResourceId);
        if (!weapon)
        {
            return string.Empty;
        }
        string weaponSilhouetteId = weapon.Basic.SilhouetteResourceId;
        resources.Release<WeaponDefinitionSO>(item.ResourceId);
        return weaponSilhouetteId;
    }

    private void ReleaseShopArt()
    {
        ResourceMgr resources = ResourceMgr.instance;
        foreach (KeyValuePair<string, string> entry in _shopSpriteResourceIds)
        {
            resources.Release<Sprite>(entry.Value);
        }
        _shopSprites.Clear();
        _shopSpriteResourceIds.Clear();
    }

    private void OnGUI()
    {
        if (!_isLocal || !_initialized || _account == null) return;
        GUI.Label(new Rect(20f, Screen.height - 70f, 260f, 28f),
            $"金钱 ${_account.Money}   护甲 {Mathf.RoundToInt(_armor.Armor)}   头盔 {(_armor.HasHelmet ? "有" : "无")}");
        if (!_shopOpen) return;
        if (!_service.IsBuyWindowOpen)
        {
            CloseShop();
            return;
        }

        DrawShop();
    }

    private void DrawShop()
    {
        Rect panel = new Rect(36f, 32f, Screen.width - 72f, Screen.height - 64f);
        Color previousColor = GUI.color;
        GUI.color = new Color(0.025f, 0.03f, 0.038f, 0.98f);
        GUI.Box(panel, GUIContent.none);
        GUI.color = previousColor;

        GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 28,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft
        };
        GUIStyle balanceStyle = new GUIStyle(titleStyle)
        {
            alignment = TextAnchor.MiddleRight
        };
        GUI.Label(new Rect(panel.x + 24f, panel.y + 12f, 320f, 44f),
            "赏金爆破 · 装备商店", titleStyle);
        GUI.Label(new Rect(panel.xMax - 330f, panel.y + 12f, 300f, 44f),
            $"余额  ${_account.Money}", balanceStyle);

        float contentTop = panel.y + 68f;
        float contentHeight = panel.height - 116f;
        float previewWidth = Mathf.Clamp(panel.width * 0.31f, 360f, 560f);
        Rect previewRect = new Rect(
            panel.x + 18f,
            contentTop,
            previewWidth,
            contentHeight);
        GUI.Box(previewRect, GUIContent.none);
        if (_previewStage != null && _previewStage.Texture)
        {
            GUI.DrawTexture(previewRect, _previewStage.Texture, ScaleMode.ScaleToFit, true);
        }
        GUI.Label(new Rect(previewRect.x + 18f, previewRect.yMax - 42f,
            previewRect.width - 36f, 28f),
            "人物预览　　　　　　　　　装备箱预览");

        Rect gridRect = new Rect(
            previewRect.xMax + 18f,
            contentTop,
            panel.xMax - previewRect.xMax - 36f,
            contentHeight);
        string[] columnNames = { "手枪", "轻型", "中型", "重型", "防护装备" };
        BombShopColumn[] columns =
        {
            BombShopColumn.Pistol,
            BombShopColumn.Light,
            BombShopColumn.Medium,
            BombShopColumn.Heavy,
            BombShopColumn.Protection
        };
        float gap = 10f;
        float columnWidth = (gridRect.width - gap * (columns.Length - 1)) /
            columns.Length;
        BombEconomyService.ShopItem? hovered = null;
        Vector2 pointerPosition = Event.current.mousePosition;
        if (Mouse.current != null)
        {
            pointerPosition = Mouse.current.position.ReadValue();
            pointerPosition.y = Screen.height - pointerPosition.y;
        }
        for (int columnIndex = 0; columnIndex < columns.Length; columnIndex++)
        {
            float x = gridRect.x + columnIndex * (columnWidth + gap);
            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(x, gridRect.y, columnWidth, 34f),
                columnNames[columnIndex], headerStyle);

            int row = 0;
            for (int itemIndex = 0; itemIndex < _service.ShopItems.Count; itemIndex++)
            {
                BombEconomyService.ShopItem item = _service.ShopItems[itemIndex];
                if (item.Column != columns[columnIndex])
                {
                    continue;
                }

                Rect itemRect = new Rect(
                    x,
                    gridRect.y + 40f + row * 150f,
                    columnWidth,
                    138f);
                bool isHovered = itemRect.Contains(pointerPosition);
                if (isHovered)
                {
                    hovered = item;
                }
                DrawShopItem(itemRect, item, isHovered);
                row++;
            }
        }

        string hoveredId = hovered.HasValue ? hovered.Value.ItemId : string.Empty;
        if (!string.Equals(_hoveredItemId, hoveredId, StringComparison.Ordinal))
        {
            _hoveredItemId = hoveredId;
            if (hovered.HasValue)
            {
                _previewStage?.SetHoveredItem(hovered.Value);
            }
            else
            {
                _previewStage?.RefreshDefault();
            }
        }

        GUI.Label(new Rect(panel.x + 24f, panel.yMax - 40f,
            panel.width - 48f, 28f),
            "B 关闭商店　　G 丢弃当前武器　　F 拾取地面武器　　爆破模式手枪不可丢弃");
    }

    private void DrawShopItem(
        Rect rect,
        BombEconomyService.ShopItem item,
        bool isHovered)
    {
        Color previousBackground = GUI.backgroundColor;
        GUI.backgroundColor = isHovered
            ? new Color(0.95f, 0.66f, 0.18f, 1f)
            : new Color(0.18f, 0.22f, 0.28f, 1f);
        if (GUI.Button(rect, GUIContent.none))
        {
            _ = TryPurchaseItemAsync(item);
        }
        GUI.backgroundColor = previousBackground;

        GUIStyle nameStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.UpperCenter
        };
        GUIStyle priceStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.LowerCenter
        };
        GUI.Label(new Rect(rect.x + 5f, rect.y + 7f, rect.width - 10f, 24f),
            item.Name, nameStyle);
        if (_shopSprites.TryGetValue(item.ItemId, out Sprite sprite) && sprite)
        {
            GUI.DrawTexture(
                new Rect(rect.x + 12f, rect.y + 34f, rect.width - 24f, 70f),
                sprite.texture,
                ScaleMode.ScaleToFit,
                true);
        }
        GUI.Label(new Rect(rect.x + 5f, rect.yMax - 31f, rect.width - 10f, 24f),
            "$" + item.Price,
            priceStyle);
    }
}
