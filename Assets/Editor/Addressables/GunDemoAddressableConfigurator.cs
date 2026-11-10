using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.AddressableAssets.Settings.GroupSchemas;
using UnityEngine;

public static class GunDemoAddressableConfigurator
{
    private const string LocalCommonGroup = "Local_Common";
    private const string WeaponsGroup = "Weapons";
    private const string WeaponUiGroup = "Weapon_UI";
    private const string WeaponAudioGroup = "Weapon_Audio";
    private const string CharactersGroup = "Characters";
    private const string EffectsGroup = "Effects";

    private const string WeaponDataFolder = "Assets/SO/Weapons/Data";
    private const string WeaponPrefabFolder = "Assets/Prefabs/Weapons";
    private const string WeaponSilhouetteFolder = "Assets/Resource/UI/WeaponSilhouettes";
    private const string WeaponResourceFolder = "Assets/Resource/Weapons";
    private const string FirstPersonCharacterFolder =
        "Assets/Resource/Animations/FirstPerson/KINEMATION/Weapons";
    private const string KinemationRecoilFolder =
        "Assets/Resource/Animations/FirstPerson/KINEMATION/Weapons";
    private const string MuzzleFlashPath =
        "Assets/Prefabs/Effects/Weapons/MuzzleFlash.prefab";
    private const string MuzzleFlashAddress = "effect.weapon.muzzle_flash";
    private const string BulletTracerPath =
        "Assets/Prefabs/Effects/Weapons/HovlBulletTracer.prefab";
    private const string BulletTracerAddress = "effect.weapon.bullet_tracer";
    private const string BulletImpactPrefabPath =
        "Assets/Prefabs/Effects/BulletImpacts/BulletImpactDecal.prefab";
    private const string BulletImpactTextureFolder =
        "Assets/Resource/Effects/BulletImpacts/Textures";

    [MenuItem("工具/Gun Demo/资源/配置 Addressables")]
    public static void ConfigureFromMenu()
    {
        GunDemoAddressableConfigureReport report = Configure();
        EditorUtility.DisplayDialog(
            "Addressables 配置完成",
            $"已登记 {report.EntryCount} 个资源，发现 {report.Problems.Count} 个问题。",
            "确定");
    }

    public static GunDemoAddressableConfigureReport Configure()
    {
        WeaponActionConfigureReport actionReport = WeaponActionSetConfigurator.Configure();
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.GetSettings(true);
        Dictionary<string, AddressableAssetGroup> groups = CreateGroups(settings);
        GunDemoAddressableConfigureReport report = new GunDemoAddressableConfigureReport();
        report.MigratedFieldCount += actionReport.MigratedFieldCount;
        report.Problems.AddRange(actionReport.Problems);
        Dictionary<string, string> configuredGuids = new Dictionary<string, string>();
        ConfigureSharedEffects(settings, groups[EffectsGroup], configuredGuids, report);
        ConfigureKinemationRecoil(
            settings,
            groups[CharactersGroup],
            configuredGuids,
            report);
        Register(
            settings,
            groups[CharactersGroup],
            "Assets/Prefabs/Characters/IdleAIPlayer.prefab",
            "character.ai_player.prefab",
            new[] { "type.prefab", "type.character" },
            configuredGuids,
            report);
        Register(
            settings,
            groups[CharactersGroup],
            "Assets/SO/Player/Voice/AnbiCombatVoice.asset",
            "character.anbi.voice",
            new[] { "type.character", "type.voice" },
            configuredGuids,
            report);
        string[] voiceCharacters = { "Ellen", "ZhuYuan", "Belle" };
        string[] voiceIds = { "ellen", "zhuyuan", "belle" };
        for (int i = 0; i < voiceCharacters.Length; i++)
        {
            Register(
                settings,
                groups[CharactersGroup],
                "Assets/Prefabs/Characters/Models/" + voiceCharacters[i] + ".prefab",
                "character." + voiceIds[i] + ".prefab",
                new[] { "type.prefab", "type.character" },
                configuredGuids,
                report);
            Register(
                settings,
                groups[CharactersGroup],
                "Assets/Prefabs/Characters/Arms/" + voiceCharacters[i] + ".prefab",
                "character." + voiceIds[i] + ".arms",
                new[] { "type.prefab", "type.character" },
                configuredGuids,
                report);
            Register(
                settings,
                groups[CharactersGroup],
                "Assets/SO/Player/Voice/" + voiceCharacters[i] + "CombatVoice.asset",
                "character." + voiceIds[i] + ".voice",
                new[] { "type.character", "type.voice" },
                configuredGuids,
                report);
        }

        string[] weaponGuids = AssetDatabase.FindAssets(
            "t:WeaponDefinitionSO",
            new[] { WeaponDataFolder });
        Array.Sort(weaponGuids, CompareAssetGuidsByPath);

        for (int i = 0; i < weaponGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(weaponGuids[i]);
            WeaponDefinitionSO weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinitionSO>(path);
            if (!weapon || weapon.Basic == null || string.IsNullOrWhiteSpace(weapon.Basic.WeaponId))
            {
                report.Problems.Add(path + " 缺少枪械 ID");
                continue;
            }

            MigrateSharedTracerId(weapon, report);
            ConfigureWeapon(settings, groups, weapon, configuredGuids, report);
        }

        ConfigureCatalog(settings, groups[LocalCommonGroup], configuredGuids, report);
        ConfigureKillIcons(settings, groups[WeaponUiGroup], configuredGuids, report);
        ReserializeWeaponData();

        settings.SetDirty(
            AddressableAssetSettings.ModificationEvent.BatchModification,
            null,
            true,
            true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        report.EntryCount = configuredGuids.Count;
        report.WeaponCount = weaponGuids.Length;
        report.Passed = report.Problems.Count == 0;
        return report;
    }

    public static void ConfigureFromBatchMode()
    {
        GunDemoAddressableConfigureReport report = Configure();
        if (!report.Passed)
        {
            throw new InvalidOperationException(
                "Addressables 配置失败：" + string.Join("；", report.Problems));
        }

        Debug.Log(
            $"Addressables 配置完成：{report.WeaponCount} 把枪，登记 {report.EntryCount} 个资源。");
    }

    private static void ReserializeWeaponData()
    {
        List<string> paths = new List<string>();
        string[] weaponGuids = AssetDatabase.FindAssets("t:WeaponDefinitionSO", new[] { WeaponDataFolder });
        for (int i = 0; i < weaponGuids.Length; i++)
        {
            paths.Add(AssetDatabase.GUIDToAssetPath(weaponGuids[i]));
        }

        string[] catalogGuids = AssetDatabase.FindAssets("t:WeaponCatalogSO", new[] { "Assets/SO/Weapons/Catalog" });
        for (int i = 0; i < catalogGuids.Length; i++)
        {
            paths.Add(AssetDatabase.GUIDToAssetPath(catalogGuids[i]));
        }

        string[] actionGuids = AssetDatabase.FindAssets(
            "t:WeaponActionSetSO",
            new[] { WeaponActionSetConfigurator.WeaponActionFolder });
        for (int i = 0; i < actionGuids.Length; i++)
        {
            paths.Add(AssetDatabase.GUIDToAssetPath(actionGuids[i]));
        }

        AssetDatabase.ForceReserializeAssets(paths, ForceReserializeAssetsOptions.ReserializeAssets);
    }

    private static Dictionary<string, AddressableAssetGroup> CreateGroups(AddressableAssetSettings settings)
    {
        Dictionary<string, AddressableAssetGroup> groups = new Dictionary<string, AddressableAssetGroup>();
        string[] names =
        {
            LocalCommonGroup,
            WeaponsGroup,
            WeaponUiGroup,
            WeaponAudioGroup,
            CharactersGroup,
            EffectsGroup
        };

        for (int i = 0; i < names.Length; i++)
        {
            string name = names[i];
            AddressableAssetGroup group = settings.FindGroup(name);
            if (!group)
            {
                group = settings.CreateGroup(
                    name,
                    false,
                    false,
                    false,
                    null,
                    typeof(ContentUpdateGroupSchema),
                    typeof(BundledAssetGroupSchema));
            }

            BundledAssetGroupSchema schema = group.GetSchema<BundledAssetGroupSchema>();
            if (schema != null)
            {
                schema.BuildPath.SetVariableByName(settings, AddressableAssetSettings.kLocalBuildPath);
                schema.LoadPath.SetVariableByName(settings, AddressableAssetSettings.kLocalLoadPath);
            }

            groups.Add(name, group);
        }

        return groups;
    }

    private static void ConfigureWeapon(
        AddressableAssetSettings settings,
        Dictionary<string, AddressableAssetGroup> groups,
        WeaponDefinitionSO weapon,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        string weaponId = weapon.Basic.WeaponId.Trim().ToLowerInvariant();
        string weaponLabel = "weapon." + weaponId;
        string dataPath = AssetDatabase.GetAssetPath(weapon);
        string fileName = Path.GetFileNameWithoutExtension(dataPath);

        Register(settings, groups[WeaponsGroup], dataPath, "weapon." + weaponId + ".data", new[] { weaponLabel, "type.data" }, configuredGuids, report);
        RegisterResourceId(
            settings,
            groups[WeaponsGroup],
            WeaponActionSetConfigurator.GetActionSetPath(weapon),
            weapon.ActionSetResourceId,
            new[] { weaponLabel, "type.data", "type.weapon_action" },
            configuredGuids,
            report);
        ConfigureUi(settings, groups[WeaponUiGroup], weapon, fileName, weaponLabel, configuredGuids, report);
        ConfigurePrefabs(settings, groups[WeaponsGroup], weapon, fileName, weaponLabel, configuredGuids, report);
        ConfigureControllers(settings, groups, weapon, fileName, weaponLabel, configuredGuids, report);
        ConfigureAudio(settings, groups[WeaponAudioGroup], weapon, weaponLabel, configuredGuids, report);
        ConfigureOptionalResources(settings, groups, weapon, weaponLabel, configuredGuids, report);
    }

    private static void ConfigureUi(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        WeaponDefinitionSO weapon,
        string fileName,
        string weaponLabel,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        string silhouettePath = WeaponSilhouetteFolder + "/" + fileName + ".png";
        RegisterResourceId(settings, group, silhouettePath, weapon.Basic.SilhouetteResourceId, new[] { weaponLabel, "type.ui" }, configuredGuids, report);

        if (string.Equals(weapon.Basic.IconResourceId, weapon.Basic.SilhouetteResourceId, StringComparison.Ordinal))
        {
            return;
        }

        string iconPath = "Assets/Resource/UI/WeaponIcons/" + fileName + ".png";
        RegisterResourceId(settings, group, iconPath, weapon.Basic.IconResourceId, new[] { weaponLabel, "type.ui" }, configuredGuids, report);
    }

    private static void ConfigurePrefabs(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        WeaponDefinitionSO weapon,
        string fileName,
        string weaponLabel,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        string firstPersonPath = WeaponPrefabFolder + "/" + fileName + ".prefab";
        RegisterResourceId(settings, group, firstPersonPath, weapon.Resources.FirstPersonPrefabResourceId, new[] { weaponLabel, "type.prefab" }, configuredGuids, report);

        if (string.Equals(weapon.Resources.WorldModelPrefabResourceId, weapon.Resources.FirstPersonPrefabResourceId, StringComparison.Ordinal))
        {
            return;
        }

        string worldPath = WeaponPrefabFolder + "/World/" + fileName + ".prefab";
        RegisterResourceId(settings, group, worldPath, weapon.Resources.WorldModelPrefabResourceId, new[] { weaponLabel, "type.prefab" }, configuredGuids, report);
    }

    private static void ConfigureControllers(
        AddressableAssetSettings settings,
        Dictionary<string, AddressableAssetGroup> groups,
        WeaponDefinitionSO weapon,
        string fileName,
        string weaponLabel,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        string weaponFolder = WeaponResourceFolder + "/" + fileName + "/Weapon";
        string weaponControllerPath = FindFirstAssetWithExtension(weaponFolder, ".overrideController");
        RegisterResourceId(settings, groups[WeaponsGroup], weaponControllerPath, weapon.Animation.WeaponAnimatorControllerResourceId, new[] { weaponLabel, "type.animator" }, configuredGuids, report);

        string characterFolder = FirstPersonCharacterFolder + "/" + fileName + "/Character";
        string characterControllerPath = FindFirstAssetWithExtension(
            characterFolder,
            ".overrideController");
        RegisterResourceId(settings, groups[CharactersGroup], characterControllerPath, weapon.Animation.CharacterAnimatorControllerResourceId, new[] { weaponLabel, "type.animator" }, configuredGuids, report);
        RegisterExistingResourceId(settings, groups[CharactersGroup], weapon.Animation.ThirdPersonAnimatorControllerResourceId, new[] { weaponLabel, "type.animator" }, configuredGuids, report);
        RegisterExistingResourceId(settings, groups[WeaponsGroup], weapon.Animation.ThirdPersonWeaponAnimatorControllerResourceId, new[] { weaponLabel, "type.animator" }, configuredGuids, report);
    }

    private static void ConfigureKinemationRecoil(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        string[] recoilGuids = AssetDatabase.FindAssets(
            "t:RecoilAnimData",
            new[] { KinemationRecoilFolder });
        for (int i = 0; i < recoilGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(recoilGuids[i]);
            string address = "weapon.kinemation.recoil." +
                Normalize(Path.GetFileNameWithoutExtension(path));
            Register(
                settings,
                group,
                path,
                address,
                new[] { "type.animation", "type.recoil" },
                configuredGuids,
                report);
        }
    }

    private static void ConfigureAudio(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        WeaponDefinitionSO weapon,
        string weaponLabel,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        HashSet<string> requestedIds = new HashSet<string>(StringComparer.Ordinal);
        AddResourceIds(requestedIds, weapon.Audio.FireSoundResourceIds);
        AddResourceIds(requestedIds, weapon.Audio.WeaponEventSoundResourceIds);
        AddResourceId(requestedIds, weapon.Audio.DryFireSoundResourceId);
        AddResourceId(requestedIds, weapon.Audio.EquipSoundResourceId);
        AddResourceId(requestedIds, weapon.Audio.UnequipSoundResourceId);
        AddResourceIds(requestedIds, weapon.Audio.ReloadSoundResourceIds);
        AddResourceIds(requestedIds, weapon.Audio.MechanicalSoundResourceIds);

        if (requestedIds.Count == 0)
        {
            return;
        }

        HashSet<string> foundIds = new HashSet<string>(StringComparer.Ordinal);
        string[] audioGuids = AssetDatabase.FindAssets("t:AudioClip", new[] { WeaponResourceFolder });
        for (int i = 0; i < audioGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(audioGuids[i]);
            string address = GetAudioAddress(path);
            if (!requestedIds.Contains(address))
            {
                continue;
            }

            string label = address.StartsWith("weapon.common.", StringComparison.Ordinal) ? "weapon.common" : weaponLabel;
            Register(settings, group, path, address, new[] { label, "type.audio" }, configuredGuids, report);
            foundIds.Add(address);
        }

        foreach (string requestedId in requestedIds)
        {
            if (!foundIds.Contains(requestedId))
            {
                report.Problems.Add(weapon.name + " 的音频 ID 找不到资源：" + requestedId);
            }
        }
    }

    private static void ConfigureOptionalResources(
        AddressableAssetSettings settings,
        Dictionary<string, AddressableAssetGroup> groups,
        WeaponDefinitionSO weapon,
        string weaponLabel,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        RegisterExistingResourceId(settings, groups[EffectsGroup], weapon.Effects.MuzzleFlashResourceId, new[] { weaponLabel, "type.effect" }, configuredGuids, report);
        RegisterExistingResourceId(settings, groups[EffectsGroup], weapon.Effects.MuzzleSmokeResourceId, new[] { weaponLabel, "type.effect" }, configuredGuids, report);
        RegisterExistingResourceId(settings, groups[EffectsGroup], weapon.Effects.ShellResourceId, new[] { weaponLabel, "type.effect" }, configuredGuids, report);
        RegisterExistingResourceId(settings, groups[EffectsGroup], weapon.Effects.TracerResourceId, new[] { weaponLabel, "type.effect" }, configuredGuids, report);
        RegisterExistingResourceId(settings, groups[EffectsGroup], weapon.Effects.ImpactEffectResourceId, new[] { weaponLabel, "type.effect" }, configuredGuids, report);
        RegisterExistingResourceId(settings, groups[WeaponsGroup], weapon.Special.SpecialAnimationResourceId, new[] { weaponLabel, "type.animation" }, configuredGuids, report);
    }

    private static void ConfigureCatalog(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        string[] guids = AssetDatabase.FindAssets("t:WeaponCatalogSO", new[] { "Assets/SO/Weapons/Catalog" });
        for (int i = 0; i < guids.Length; i++)
        {
            Register(settings, group, AssetDatabase.GUIDToAssetPath(guids[i]), "weapon.catalog", new[] { "type.data" }, configuredGuids, report);
        }
    }

    private static void ConfigureKillIcons(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        Register(settings, group, "Assets/Resource/UI/KillIcons/KillSkull_White.png", "ui.kill.skull.white", new[] { "type.ui" }, configuredGuids, report);
        Register(settings, group, "Assets/Resource/UI/KillIcons/KillSkull_Red.png", "ui.kill.skull.red", new[] { "type.ui" }, configuredGuids, report);

        string spriteAssetPath = "Assets/Resource/UI/KillFeed/KillFeedSpriteAsset.asset";
        if (!string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(spriteAssetPath)))
        {
            Register(settings, group, spriteAssetPath, "ui.killfeed.sprite_asset", new[] { "type.ui" }, configuredGuids, report);
        }
    }

    private static void ConfigureSharedEffects(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        Register(
            settings,
            group,
            MuzzleFlashPath,
            MuzzleFlashAddress,
            new[] { "type.effect", "effect.weapon" },
            configuredGuids,
            report);

        Register(
            settings,
            group,
            BulletTracerPath,
            BulletTracerAddress,
            new[] { "type.effect", "effect.weapon", "effect.projectile" },
            configuredGuids,
            report);

        Register(
            settings,
            group,
            BulletImpactPrefabPath,
            "effect.impact.decal",
            new[] { "type.effect", "effect.impact" },
            configuredGuids,
            report);

        string[] textureFiles =
        {
            "DirtBulletDecal2.png",
            "MetalBulletDecal.png",
            "WoodDecal2.png",
            "DirtBulletDecal3.png",
            "BrickBulletDecal3.png",
            "BrokenGlassDecal.png"
        };
        string[] surfaceNames =
        {
            "concrete", "metal", "wood", "dirt", "brick", "glass"
        };
        for (int i = 0; i < textureFiles.Length; i++)
        {
            Register(
                settings,
                group,
                BulletImpactTextureFolder + "/" + textureFiles[i],
                "effect.impact.texture." + surfaceNames[i],
                new[] { "type.effect", "effect.impact" },
                configuredGuids,
                report);
        }
    }

    private static void MigrateSharedTracerId(
        WeaponDefinitionSO weapon,
        GunDemoAddressableConfigureReport report)
    {
        SerializedObject serializedWeapon = new SerializedObject(weapon);
        SerializedProperty tracerId = serializedWeapon.FindProperty(
            "_effects._tracerResourceId");
        if (tracerId == null || !string.IsNullOrWhiteSpace(tracerId.stringValue))
        {
            return;
        }

        tracerId.stringValue = BulletTracerAddress;
        serializedWeapon.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(weapon);
        report.MigratedFieldCount++;
    }

    private static void RegisterResourceId(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        string expectedPath,
        string address,
        string[] labels,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(expectedPath) && !string.IsNullOrWhiteSpace(AssetDatabase.AssetPathToGUID(expectedPath)))
        {
            Register(settings, group, expectedPath, address, labels, configuredGuids, report);
            return;
        }

        RegisterExistingResourceId(settings, group, address, labels, configuredGuids, report);
    }

    private static void RegisterExistingResourceId(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        string address,
        string[] labels,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        AddressableAssetEntry entry = FindEntryByAddress(settings, address);
        if (entry == null)
        {
            report.Problems.Add("资源 ID 找不到已登记资源：" + address);
            return;
        }

        Register(settings, group, AssetDatabase.GUIDToAssetPath(entry.guid), address, labels, configuredGuids, report);
    }

    private static AddressableAssetEntry FindEntryByAddress(AddressableAssetSettings settings, string address)
    {
        for (int groupIndex = 0; groupIndex < settings.groups.Count; groupIndex++)
        {
            AddressableAssetGroup group = settings.groups[groupIndex];
            if (!group)
            {
                continue;
            }

            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (string.Equals(entry.address, address, StringComparison.Ordinal))
                {
                    return entry;
                }
            }
        }

        return null;
    }

    private static string FindFirstAssetWithExtension(string folder, string extension)
    {
        if (!AssetDatabase.IsValidFolder(folder))
        {
            return string.Empty;
        }

        string[] guids = AssetDatabase.FindAssets(string.Empty, new[] { folder });
        Array.Sort(guids, CompareAssetGuidsByPath);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }

        return string.Empty;
    }

    private static void AddResourceIds(HashSet<string> result, IReadOnlyList<string> values)
    {
        for (int i = 0; i < values.Count; i++)
        {
            AddResourceId(result, values[i]);
        }
    }

    private static void AddResourceId(HashSet<string> result, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            result.Add(value);
        }
    }

    private static string GetAudioAddress(string assetPath)
    {
        string path = assetPath.Replace('\\', '/');
        string scope;
        if (path.Contains("/Shared/Audio/"))
        {
            scope = "weapon.common.audio.";
        }
        else
        {
            string prefix = WeaponResourceFolder + "/";
            int start = path.IndexOf(prefix, StringComparison.Ordinal);
            int end = start >= 0 ? path.IndexOf('/', start + prefix.Length) : -1;
            string folder = start >= 0 && end > start ? path.Substring(start + prefix.Length, end - start - prefix.Length) : "unknown";
            scope = "weapon." + Normalize(folder) + ".audio.";
        }

        return scope + Normalize(Path.GetFileNameWithoutExtension(path));
    }

    private static string Register(
        AddressableAssetSettings settings,
        AddressableAssetGroup group,
        string assetPath,
        string address,
        string[] labels,
        Dictionary<string, string> configuredGuids,
        GunDemoAddressableConfigureReport report)
    {
        string guid = AssetDatabase.AssetPathToGUID(assetPath);
        if (string.IsNullOrWhiteSpace(guid))
        {
            report.Problems.Add(assetPath + " 不存在，无法登记 Addressables");
            return string.Empty;
        }

        if (configuredGuids.TryGetValue(guid, out string existingAddress))
        {
            AddressableAssetEntry existingEntry = settings.FindAssetEntry(guid);
            for (int i = 0; i < labels.Length; i++)
            {
                existingEntry.SetLabel(labels[i], true, true, false);
            }

            if (!string.Equals(existingAddress, address, StringComparison.Ordinal))
            {
                report.Problems.Add(assetPath + " 同时配置了两个资源 ID：" + existingAddress + " / " + address);
            }
            return existingAddress;
        }

        AddressableAssetEntry entry = settings.CreateOrMoveEntry(guid, group, false, false);
        entry.SetAddress(address, false);
        configuredGuids.Add(guid, address);

        for (int i = 0; i < labels.Length; i++)
        {
            entry.SetLabel(labels[i], true, true, false);
        }
        return address;
    }

    private static string Normalize(string value)
    {
        value = value.Trim().ToLowerInvariant();
        char[] result = new char[value.Length];
        int count = 0;
        bool lastWasSeparator = false;
        for (int i = 0; i < value.Length; i++)
        {
            char character = value[i];
            if (char.IsLetterOrDigit(character))
            {
                result[count++] = character;
                lastWasSeparator = false;
            }
            else if (!lastWasSeparator && count > 0)
            {
                result[count++] = '_';
                lastWasSeparator = true;
            }
        }

        if (count > 0 && result[count - 1] == '_')
        {
            count--;
        }
        return new string(result, 0, count);
    }

    private static int CompareAssetGuidsByPath(string left, string right)
    {
        return string.CompareOrdinal(AssetDatabase.GUIDToAssetPath(left), AssetDatabase.GUIDToAssetPath(right));
    }
}

[Serializable]
public sealed class GunDemoAddressableConfigureReport
{
    public bool Passed { get; set; }
    public int WeaponCount { get; set; }
    public int EntryCount { get; set; }
    public int MigratedFieldCount { get; set; }
    public List<string> Problems { get; set; } = new List<string>();
}
