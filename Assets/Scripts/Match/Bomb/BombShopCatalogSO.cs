using System;
using System.Collections.Generic;
using UnityEngine;

public enum BombShopItemKind
{
    [InspectorName("手枪")] Sidearm = 0,
    [InspectorName("长枪")] Primary = 1,
    [InspectorName("胸甲")] Armor = 2,
    [InspectorName("头盔")] Helmet = 3
}

public enum BombShopColumn
{
    [InspectorName("手枪")] Pistol = 0,
    [InspectorName("轻型武器")] Light = 1,
    [InspectorName("中型武器")] Medium = 2,
    [InspectorName("重型武器")] Heavy = 3,
    [InspectorName("防护装备")] Protection = 4
}

[Serializable]
public sealed class BombShopItemConfig
{
    [SerializeField, InspectorName("稳定条目 ID")] private string _itemId;
    [SerializeField, InspectorName("显示名称")] private string _displayName;
    [SerializeField, InspectorName("条目类型")] private BombShopItemKind _kind;
    [SerializeField, InspectorName("所在列")] private BombShopColumn _column;
    [SerializeField, ResourceId(typeof(UnityEngine.Object)), InspectorName("资源 ID")]
    private string _resourceId;
    [SerializeField, Min(0), InspectorName("价格")] private int _price;
    [SerializeField, InspectorName("显示顺序")] private int _sortOrder;

    public string ItemId { get { return _itemId; } }
    public string DisplayName { get { return _displayName; } }
    public BombShopItemKind Kind { get { return _kind; } }
    public BombShopColumn Column { get { return _column; } }
    public string ResourceId { get { return _resourceId; } }
    public int Price { get { return _price; } }
    public int SortOrder { get { return _sortOrder; } }

    public BombShopItemConfig(
        string itemId,
        string displayName,
        BombShopItemKind kind,
        BombShopColumn column,
        string resourceId,
        int price,
        int sortOrder)
    {
        _itemId = itemId ?? string.Empty;
        _displayName = displayName ?? string.Empty;
        _kind = kind;
        _column = column;
        _resourceId = resourceId ?? string.Empty;
        _price = Mathf.Max(0, price);
        _sortOrder = sortOrder;
    }
}

[CreateAssetMenu(fileName = "BombShopCatalog", menuName = "GunDemo/爆破/商店目录")]
public sealed class BombShopCatalogSO : ScriptableObject
{
    [SerializeField, InspectorName("商店条目")] private List<BombShopItemConfig> _items =
        new List<BombShopItemConfig>();
    [SerializeField, ResourceId(typeof(GameObject)), InspectorName("胸甲箱模型 ID")]
    private string _armorCrateResourceId = "prop.equipment.armor_crate";
    [SerializeField, ResourceId(typeof(GameObject)), InspectorName("头盔箱模型 ID")]
    private string _helmetCrateResourceId = "prop.equipment.helmet_crate";

    public IReadOnlyList<BombShopItemConfig> Items { get { return _items; } }
    public string ArmorCrateResourceId { get { return _armorCrateResourceId; } }
    public string HelmetCrateResourceId { get { return _helmetCrateResourceId; } }

    public void Configure(
        IEnumerable<BombShopItemConfig> items,
        string armorCrateResourceId,
        string helmetCrateResourceId)
    {
        _items = items != null
            ? new List<BombShopItemConfig>(items)
            : new List<BombShopItemConfig>();
        _items.Sort(CompareItems);
        _armorCrateResourceId = armorCrateResourceId ?? string.Empty;
        _helmetCrateResourceId = helmetCrateResourceId ?? string.Empty;
    }

    private static int CompareItems(BombShopItemConfig left, BombShopItemConfig right)
    {
        int column = left.Column.CompareTo(right.Column);
        return column != 0 ? column : left.SortOrder.CompareTo(right.SortOrder);
    }
}
