using System;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerWeaponWorldInteractionCtrl : MonoBehaviour
{
    [Header("交互")]
    [SerializeField, Min(0f), InspectorName("丢出前向速度")] private float _dropForwardSpeed = 2f;
    [SerializeField, Min(0f), InspectorName("丢出向上速度")] private float _dropUpSpeed = 0.6f;
    [SerializeField, InspectorName("禁止丢弃手枪")] private bool _preventSidearmDrop = true;

    private PlayerActionCtrl _actionCtrl;
    private PlayerWeaponInventoryCtrl _inventory;
    private WeaponActionCtrl _weaponAction;
    private WeaponCombatCtrl _combat;
    private PlayerWeaponViewCtrl _view;
    private bool _transactionBusy;

    public bool IsBusy { get { return _transactionBusy; } }

    private void Awake()
    {
        TryGetComponent(out _actionCtrl);
        TryGetComponent(out _inventory);
        TryGetComponent(out _weaponAction);
        TryGetComponent(out _combat);
        TryGetComponent(out _view);
        if (!_actionCtrl || !_inventory || !_weaponAction || !_combat || !_view)
        {
            Debug.LogError($"{name} 的武器丢拾组件缺少玩家武器依赖。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_actionCtrl)
        {
            _actionCtrl.CommandReceived += HandleCommand;
        }
    }

    private void OnDisable()
    {
        if (_actionCtrl)
        {
            _actionCtrl.CommandReceived -= HandleCommand;
        }
    }

    public void Configure(
        bool preventSidearmDrop,
        float pickupDistance,
        float dropForwardSpeed,
        float dropUpSpeed)
    {
        // 手枪和左轮是所有模式的保底副武器，模式配置不能解除这条保护。
        _preventSidearmDrop = true;
        _dropForwardSpeed = Mathf.Max(0f, dropForwardSpeed);
        _dropUpSpeed = Mathf.Max(0f, dropUpSpeed);
    }

    public async Task<bool> TryAcquireWeaponAsync(
        string weaponResourceId,
        int magazineAmmo = -1,
        int reserveAmmo = -1)
    {
        if (_transactionBusy || string.IsNullOrWhiteSpace(weaponResourceId))
        {
            return false;
        }

        _transactionBusy = true;
        ResourceMgr resources = ResourceMgr.instance;
        WeaponDefinitionSO newWeapon = null;
        DroppedWeaponEntity replacedDrop = null;
        try
        {
            newWeapon = await resources.LoadAsync<WeaponDefinitionSO>(weaponResourceId);
            if (!this || !newWeapon)
            {
                return false;
            }

            bool isSidearm = IsSidearm(newWeapon.Basic.Category);
            int slot;
            if (_preventSidearmDrop)
            {
                slot = _inventory.GetSlotForCategory(newWeapon.Basic.Category);
            }
            else
            {
                int emptySlot = _inventory.FindFirstEmptySlot();
                slot = emptySlot >= 0 ? emptySlot : _inventory.CurrentWeaponIndex;
            }
            string replacedResourceId = _inventory.GetWeaponResourceId(slot);
            bool shouldDropReplaced = !string.IsNullOrWhiteSpace(replacedResourceId) &&
                !string.Equals(replacedResourceId, weaponResourceId, StringComparison.Ordinal) &&
                !(isSidearm && _preventSidearmDrop);
            if (shouldDropReplaced)
            {
                replacedDrop = await CreateDropFromResourceAsync(replacedResourceId);
                if (!replacedDrop)
                {
                    return false;
                }
            }

            bool loaded = await _inventory.AddOrReplaceWeaponAtAsync(
                weaponResourceId,
                slot);
            if (!this || !loaded)
            {
                if (replacedDrop)
                {
                    Destroy(replacedDrop.gameObject);
                }
                return false;
            }

            if (magazineAmmo >= 0 || reserveAmmo >= 0)
            {
                _combat.SetCurrentAmmo(
                    magazineAmmo >= 0 ? magazineAmmo : newWeapon.Ammo.MagazineCapacity,
                    reserveAmmo >= 0 ? reserveAmmo : newWeapon.Ammo.StartingReserveAmmo);
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"取得武器 {weaponResourceId} 失败：{exception.Message}", this);
            if (replacedDrop)
            {
                Destroy(replacedDrop.gameObject);
            }
            return false;
        }
        finally
        {
            if (newWeapon)
            {
                resources.Release<WeaponDefinitionSO>(weaponResourceId);
            }
            _transactionBusy = false;
        }
    }

    private void HandleCommand(PlayerInputCommand command)
    {
        if (command.Phase != PlayerInputPhase.Started || _transactionBusy)
        {
            return;
        }

        if (command.Action == GJInputAction.DropWeapon)
        {
            _ = TryDropCurrentAsync();
        }
    }

    private async Task TryDropCurrentAsync()
    {
        if (_inventory.IsBombSelected || _weaponAction.IsBusy ||
            !_weaponAction.CurrentWeaponData ||
            !_view.ThirdPersonWeapon)
        {
            return;
        }

        WeaponCategory category = _weaponAction.CurrentWeaponData.Basic.Category;
        if (category == WeaponCategory.Melee ||
            IsSidearm(category))
        {
            return;
        }

        string resourceId = _inventory.CurrentWeaponResourceId;
        Vector3 position = GetDropPosition();
        DroppedWeaponEntity dropped = DroppedWeaponEntity.Create(
            _view,
            resourceId,
            _combat.MagazineAmmo,
            _combat.ReserveAmmo,
            position,
            GetDropImpulse());
        if (!dropped)
        {
            return;
        }

        _transactionBusy = true;
        bool removed = await _inventory.RemoveCurrentWeaponAsync();
        _transactionBusy = false;
        if (!removed && dropped)
        {
            Destroy(dropped.gameObject);
        }
    }

    private async Task<DroppedWeaponEntity> CreateDropFromResourceAsync(
        string weaponResourceId)
    {
        ResourceMgr resources = ResourceMgr.instance;
        WeaponDefinitionSO weaponData = null;
        try
        {
            weaponData = await resources.LoadAsync<WeaponDefinitionSO>(weaponResourceId);
            if (!weaponData)
            {
                return null;
            }

            _combat.TryGetAmmo(weaponData, out int magazineAmmo, out int reserveAmmo);
            return await DroppedWeaponEntity.CreateFromResourceAsync(
                weaponResourceId,
                magazineAmmo,
                reserveAmmo,
                GetDropPosition(),
                transform.rotation,
                GetDropImpulse());
        }
        finally
        {
            if (weaponData)
            {
                resources.Release<WeaponDefinitionSO>(weaponResourceId);
            }
        }
    }

    private Vector3 GetDropPosition()
    {
        return transform.position + Vector3.up * 1.1f + transform.forward * 0.7f;
    }

    private Vector3 GetDropImpulse()
    {
        return transform.forward * _dropForwardSpeed + Vector3.up * _dropUpSpeed;
    }

    private static bool IsSidearm(WeaponCategory category)
    {
        return category == WeaponCategory.Pistol || category == WeaponCategory.Revolver;
    }
}
