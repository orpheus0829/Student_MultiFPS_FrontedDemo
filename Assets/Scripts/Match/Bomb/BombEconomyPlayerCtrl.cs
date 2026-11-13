using System;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BombEconomyPlayerCtrl : MonoBehaviour
{
    private BombEconomyService _service;
    private BombEconomyService.Account _account;
    private MatchParticipant _participant;
    private PlayerInfo _playerInfo;
    private PlayerActionCtrl _actionCtrl;
    private PlayerWeaponInventoryCtrl _inventory;
    private WeaponActionCtrl _weaponAction;
    private WeaponCombatCtrl _combat;
    private PlayerWeaponViewCtrl _view;
    private PlayerArmorCtrl _armor;
    private Camera _camera;
    private string _pistolResourceId = "weapon.m1911.data";
    private bool _shopOpen;
    private bool _transactionBusy;
    private bool _initialized;
    private bool _isLocal;

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
        TryGetComponent(out _actionCtrl);
        TryGetComponent(out _inventory);
        TryGetComponent(out _weaponAction);
        TryGetComponent(out _combat);
        TryGetComponent(out _view);
        TryGetComponent(out _armor);
        if (!_armor) _armor = gameObject.AddComponent<PlayerArmorCtrl>();
        _camera = GetComponentInChildren<Camera>(true);
        _account = _service.Bind(participant);
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
        if (!_inventory) return;
        await _inventory.ConfigureBombLoadoutAsync(_pistolResourceId);
        if (this && _participant != null && _participant.IsAI)
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
        if (_actionCtrl) _actionCtrl.CommandReceived -= HandleCommand;
        CloseShop();
    }

    public void AutoBuyForRound()
    {
        if (!_participant.IsAI || !_service.IsBuyWindowOpen || _account == null) return;
        if (_armor.Armor <= 0f && _account.Money >= 650) TryPurchaseArmor();
        if (!_armor.HasHelmet && _account.Money >= 350) TryPurchaseHelmet();
        if (!string.IsNullOrWhiteSpace(_inventory.PrimaryWeaponResourceId)) return;
        for (int i = _service.ShopItems.Count - 1; i >= 0; i--)
        {
            if (_account.Money >= _service.ShopItems[i].Price)
            {
                _ = TryPurchaseAsync(_service.ShopItems[i]);
                break;
            }
        }
    }

    public bool TryOpenShop()
    {
        if (!_initialized || !_isLocal || !_service || !_service.IsBuyWindowOpen)
        {
            return false;
        }

        _shopOpen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
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
            case GJInputAction.DropWeapon:
                _ = TryDropCurrentAsync();
                break;
            case GJInputAction.PickupWeapon:
                _ = TryPickupAsync();
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
        if (_isLocal)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private async Task TryPurchaseAsync(BombEconomyService.ShopItem item)
    {
        if (_transactionBusy || !_service.TrySpend(_playerInfo, item.Price)) return;
        _transactionBusy = true;
        bool success = await _inventory.AddOrReplaceWeaponAsync(item.ResourceId, false);
        if (!success) _service.Refund(_playerInfo, item.Price);
        _transactionBusy = false;
    }

    private void TryPurchaseArmor()
    {
        int price = _service.ArmorPrice;
        if (_transactionBusy || _armor.Armor >= _armor.MaximumArmor ||
            !_service.TrySpend(_playerInfo, price)) return;
        _armor.Refill(_armor.MaximumArmor);
    }

    private void TryPurchaseHelmet()
    {
        int price = _service.HelmetPrice;
        if (_transactionBusy || _armor.HasHelmet ||
            !_service.TrySpend(_playerInfo, price)) return;
        _armor.EquipHelmet();
    }

    private async Task TryDropCurrentAsync()
    {
        if (_transactionBusy || !_weaponAction || !_inventory || !_combat || !_view ||
            _weaponAction.IsBusy || !_weaponAction.CurrentWeaponData) return;
        WeaponCategory category = _weaponAction.CurrentWeaponData.Basic.Category;
        if (category == WeaponCategory.Pistol || category == WeaponCategory.Revolver ||
            category == WeaponCategory.Melee) return;

        string resourceId = _inventory.CurrentWeaponResourceId;
        Vector3 position = transform.position + Vector3.up * 1.1f + transform.forward * 0.7f;
        DroppedWeaponEntity dropped = DroppedWeaponEntity.Create(
            _view, resourceId, _combat.MagazineAmmo, _combat.ReserveAmmo,
            position, transform.forward * _service.DropForwardSpeed +
            Vector3.up * _service.DropUpSpeed);
        if (!dropped) return;

        _transactionBusy = true;
        bool removed = await _inventory.RemoveCurrentPrimaryAsync(_pistolResourceId);
        _transactionBusy = false;
        if (!removed && dropped) Destroy(dropped.gameObject);
    }

    public void DropWeaponsOnDeath()
    {
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
        _inventory.ClearPrimaryOwnership();
        Vector3 position = transform.position + Vector3.up * 0.8f;
        if (_inventory.CurrentWeaponResourceId == primaryId && _view &&
            _view.ThirdPersonWeapon)
        {
            DroppedWeaponEntity.Create(_view, primaryId,
                _combat ? _combat.MagazineAmmo : 0,
                _combat ? _combat.ReserveAmmo : 0,
                position, transform.forward * 1.2f + Vector3.up * 0.5f);
            return;
        }

        ResourceMgr resources = ResourceMgr.instance;
        WeaponDefinitionSO data = await resources.LoadAsync<WeaponDefinitionSO>(primaryId);
        if (!this || !data) return;
        string modelId = data.Resources.WorldModelPrefabResourceId;
        GameObject prefab = await resources.LoadAsync<GameObject>(modelId);
        if (prefab)
        {
            DroppedWeaponEntity.CreateFromModel(prefab, primaryId,
                data.Ammo.MagazineCapacity, data.Ammo.StartingReserveAmmo,
                position, transform.rotation,
                transform.forward * 1.2f + Vector3.up * 0.5f);
            resources.Release<GameObject>(modelId);
        }
        resources.Release<WeaponDefinitionSO>(primaryId);
    }

    private async Task TryPickupAsync()
    {
        if (_transactionBusy || !_camera || !_inventory) return;
        Ray ray = new Ray(_camera.transform.position, _camera.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, _service.PickupDistance, ~0,
            QueryTriggerInteraction.Collide)) return;
        DroppedWeaponEntity dropped = hit.collider.GetComponentInParent<DroppedWeaponEntity>();
        if (!dropped) return;

        _transactionBusy = true;
        bool loaded = await _inventory.AddOrReplaceWeaponAsync(
            dropped.WeaponResourceId, false);
        if (loaded)
        {
            _combat.SetCurrentAmmo(dropped.MagazineAmmo, dropped.ReserveAmmo);
            if (dropped) Destroy(dropped.gameObject);
        }
        _transactionBusy = false;
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

        Rect panel = new Rect(Screen.width * 0.5f - 220f,
            Screen.height * 0.5f - 210f, 440f, 420f);
        GUI.Box(panel, $"爆破商城   余额 ${_account.Money}");
        GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 42f,
            panel.width - 48f, panel.height - 58f));
        GUILayout.Label("主武器");
        for (int i = 0; i < _service.ShopItems.Count; i++)
        {
            BombEconomyService.ShopItem item = _service.ShopItems[i];
            if (GUILayout.Button($"{item.Name}    ${item.Price}", GUILayout.Height(38f)))
                _ = TryPurchaseAsync(item);
        }
        GUILayout.Space(10f);
        if (GUILayout.Button($"护甲 100    ${_service.ArmorPrice}", GUILayout.Height(38f)))
            TryPurchaseArmor();
        if (GUILayout.Button($"头盔    ${_service.HelmetPrice}", GUILayout.Height(38f)))
            TryPurchaseHelmet();
        GUILayout.Label("B 关闭商城　G 丢弃主武器　F 拾取");
        GUILayout.EndArea();
    }
}
