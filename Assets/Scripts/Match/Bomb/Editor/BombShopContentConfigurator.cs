using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BombShopContentConfigurator
{
    public const string CatalogPath = "Assets/SO/Equipment/BombShopCatalog.asset";
    public const string ArmorDataPath = "Assets/SO/Equipment/Armor.asset";
    public const string HelmetDataPath = "Assets/SO/Equipment/Helmet.asset";
    public const string ArmorCratePrefabPath =
        "Assets/Prefabs/Props/Equipment/ArmorCrate.prefab";
    public const string HelmetCratePrefabPath =
        "Assets/Prefabs/Props/Equipment/HelmetCrate.prefab";
    public const string ArmorSilhouettePath =
        "Assets/Resource/UI/Equipment/Sil_PlateCarrier.png";
    public const string HelmetSilhouettePath =
        "Assets/Resource/UI/Equipment/Sil_Helmet.png";

    private const string ArmorModelPath =
        "Assets/Resource/Props/ArmorCrates/Crate_Armor.fbx";
    private const string HelmetModelPath =
        "Assets/Resource/Props/ArmorCrates/Crate_Helmet.fbx";

    [MenuItem("工具/Gun Demo/爆破/配置商店目录与装备资源")]
    public static BombShopContentConfigureReport Configure()
    {
        EnsureFolder("Assets/Prefabs/Props", "Equipment");
        EnsureFolder("Assets/SO", "Equipment");
        ConfigureStaticModelImporter(ArmorModelPath);
        ConfigureStaticModelImporter(HelmetModelPath);
        ConfigureSilhouetteImporter(ArmorSilhouettePath);
        ConfigureSilhouetteImporter(HelmetSilhouettePath);
        CreateCratePrefab(ArmorModelPath, ArmorCratePrefabPath, "胸甲装备箱");
        CreateCratePrefab(HelmetModelPath, HelmetCratePrefabPath, "头盔装备箱");
        CreateEquipmentData();
        CreateCatalog();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return new BombShopContentConfigureReport
        {
            Passed = AssetDatabase.LoadAssetAtPath<GameObject>(ArmorCratePrefabPath) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(HelmetCratePrefabPath) &&
                AssetDatabase.LoadAssetAtPath<BombEquipmentDefinitionSO>(ArmorDataPath) &&
                AssetDatabase.LoadAssetAtPath<BombEquipmentDefinitionSO>(HelmetDataPath) &&
                AssetDatabase.LoadAssetAtPath<BombShopCatalogSO>(CatalogPath)
        };
    }

    private static void CreateEquipmentData()
    {
        BombEquipmentDefinitionSO armor = LoadOrCreate<BombEquipmentDefinitionSO>(
            ArmorDataPath);
        armor.Configure(
            "equipment.armor",
            "胸甲",
            BombEquipmentKind.Armor,
            650,
            100f,
            0.5f,
            0f,
            "ui.equipment.armor.silhouette",
            "prop.equipment.armor_crate");
        EditorUtility.SetDirty(armor);

        BombEquipmentDefinitionSO helmet = LoadOrCreate<BombEquipmentDefinitionSO>(
            HelmetDataPath);
        helmet.Configure(
            "equipment.helmet",
            "头盔",
            BombEquipmentKind.Helmet,
            350,
            0f,
            0f,
            0.5f,
            "ui.equipment.helmet.silhouette",
            "prop.equipment.helmet_crate");
        EditorUtility.SetDirty(helmet);
    }

    private static void CreateCatalog()
    {
        BombShopCatalogSO catalog = LoadOrCreate<BombShopCatalogSO>(CatalogPath);
        List<BombShopItemConfig> items = new List<BombShopItemConfig>
        {
            CreateWeapon("m1911", "M1911", BombShopItemKind.Sidearm,
                BombShopColumn.Pistol, "weapon.m1911.data", 500, 0),
            CreateWeapon("mps5", "MPS5", BombShopItemKind.Primary,
                BombShopColumn.Light, "weapon.mps5.data", 1500, 0),
            CreateWeapon("kxg12", "KXG12", BombShopItemKind.Primary,
                BombShopColumn.Light, "weapon.kxg-12.data", 2000, 1),
            CreateWeapon("pdw90", "PDW90", BombShopItemKind.Primary,
                BombShopColumn.Medium, "weapon.pdw90.data", 2350, 0),
            CreateWeapon("ak", "AK", BombShopItemKind.Primary,
                BombShopColumn.Medium, "weapon.ak.data", 2700, 1),
            CreateWeapon("as-val", "AS Val", BombShopItemKind.Primary,
                BombShopColumn.Heavy, "weapon.as-val.data", 2900, 0),
            CreateWeapon("kar98k", "Kar98k", BombShopItemKind.Primary,
                BombShopColumn.Heavy, "weapon.kar98k.data", 3400, 1),
            new BombShopItemConfig(
                "armor", "胸甲", BombShopItemKind.Armor,
                BombShopColumn.Protection, "equipment.armor.data", 650, 0),
            new BombShopItemConfig(
                "helmet", "头盔", BombShopItemKind.Helmet,
                BombShopColumn.Protection, "equipment.helmet.data", 350, 1)
        };
        catalog.Configure(
            items,
            "prop.equipment.armor_crate",
            "prop.equipment.helmet_crate");
        EditorUtility.SetDirty(catalog);
    }

    private static BombShopItemConfig CreateWeapon(
        string itemId,
        string displayName,
        BombShopItemKind kind,
        BombShopColumn column,
        string resourceId,
        int price,
        int sortOrder)
    {
        return new BombShopItemConfig(
            itemId,
            displayName,
            kind,
            column,
            resourceId,
            price,
            sortOrder);
    }

    private static void CreateCratePrefab(
        string modelPath,
        string prefabPath,
        string objectName)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (!model)
        {
            Debug.LogError("缺少装备箱模型：" + modelPath);
            return;
        }

        GameObject root = new GameObject(objectName);
        GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(model);
        visual.name = "Visual";
        visual.transform.SetParent(root.transform, false);
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        Object.DestroyImmediate(root);
    }

    private static void ConfigureStaticModelImporter(string path)
    {
        ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (!importer)
        {
            return;
        }

        importer.animationType = ModelImporterAnimationType.None;
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.importBlendShapes = false;
        importer.importVisibility = false;
        importer.SaveAndReimport();
    }

    private static void ConfigureSilhouetteImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (!importer)
        {
            return;
        }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }

    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset)
        {
            return asset;
        }

        asset = ScriptableObject.CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private static void EnsureFolder(string parent, string child)
    {
        string path = parent + "/" + child;
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, child);
        }
    }
}

public sealed class BombShopContentConfigureReport
{
    public bool Passed;
}
