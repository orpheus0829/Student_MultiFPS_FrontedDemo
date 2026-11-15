using UnityEngine;

public enum BombEquipmentKind
{
    [InspectorName("胸甲")] Armor = 0,
    [InspectorName("头盔")] Helmet = 1
}

[CreateAssetMenu(fileName = "BombEquipment", menuName = "GunDemo/爆破/防护装备数据")]
public sealed class BombEquipmentDefinitionSO : ScriptableObject
{
    [Header("身份")]
    [SerializeField, InspectorName("稳定装备 ID")] private string _equipmentId;
    [SerializeField, InspectorName("显示名称")] private string _displayName;
    [SerializeField, InspectorName("装备类型")] private BombEquipmentKind _kind;

    [Header("经济")]
    [SerializeField, Min(0), InspectorName("购买价格")] private int _price;

    [Header("防护")]
    [SerializeField, Min(0f), InspectorName("护甲耐久")] private float _armorCapacity;
    [SerializeField, Range(0f, 1f), InspectorName("身体伤害吸收比例")]
    private float _bodyAbsorption;
    [SerializeField, Range(0f, 1f), InspectorName("头部伤害吸收比例")]
    private float _headAbsorption;

    [Header("表现资源")]
    [SerializeField, ResourceId(typeof(Sprite)), InspectorName("剪影资源 ID")]
    private string _silhouetteResourceId;
    [SerializeField, ResourceId(typeof(GameObject)), InspectorName("装备箱模型 ID")]
    private string _crateResourceId;

    public string EquipmentId { get { return _equipmentId; } }
    public string DisplayName { get { return _displayName; } }
    public BombEquipmentKind Kind { get { return _kind; } }
    public int Price { get { return _price; } }
    public float ArmorCapacity { get { return _armorCapacity; } }
    public float BodyAbsorption { get { return _bodyAbsorption; } }
    public float HeadAbsorption { get { return _headAbsorption; } }
    public string SilhouetteResourceId { get { return _silhouetteResourceId; } }
    public string CrateResourceId { get { return _crateResourceId; } }

    public void Configure(
        string equipmentId,
        string displayName,
        BombEquipmentKind kind,
        int price,
        float armorCapacity,
        float bodyAbsorption,
        float headAbsorption,
        string silhouetteResourceId,
        string crateResourceId)
    {
        _equipmentId = equipmentId ?? string.Empty;
        _displayName = displayName ?? string.Empty;
        _kind = kind;
        _price = Mathf.Max(0, price);
        _armorCapacity = Mathf.Max(0f, armorCapacity);
        _bodyAbsorption = Mathf.Clamp01(bodyAbsorption);
        _headAbsorption = Mathf.Clamp01(headAbsorption);
        _silhouetteResourceId = silhouetteResourceId ?? string.Empty;
        _crateResourceId = crateResourceId ?? string.Empty;
    }
}
