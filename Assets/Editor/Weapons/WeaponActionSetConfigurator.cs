using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class WeaponActionSetConfigurator
{
    public const string WeaponDataFolder = "Assets/SO/Weapons/Data";
    public const string WeaponActionFolder = "Assets/SO/Weapons/Actions";
    public const int CurrentDataVersion = 1;

    [MenuItem("工具/Gun Demo/数据/配置枪械动作集")]
    public static void ConfigureFromMenu()
    {
        WeaponActionConfigureReport report = Configure();
        EditorUtility.DisplayDialog(
            "枪械动作集配置完成",
            $"已处理 {report.WeaponCount} 把枪，创建 {report.CreatedCount} 个动作集，发现 {report.Problems.Count} 个问题。",
            "确定");
    }

    public static WeaponActionConfigureReport Configure()
    {
        WeaponActionConfigureReport report = new WeaponActionConfigureReport();
        EnsureFolder(WeaponActionFolder);

        string[] guids = AssetDatabase.FindAssets(
            "t:WeaponDefinitionSO",
            new[] { WeaponDataFolder });
        Array.Sort(guids, CompareAssetGuidsByPath);

        for (int i = 0; i < guids.Length; i++)
        {
            string weaponPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            WeaponDefinitionSO weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinitionSO>(weaponPath);
            if (!weapon || weapon.Basic == null || string.IsNullOrWhiteSpace(weapon.Basic.WeaponId))
            {
                report.Problems.Add(weaponPath + " 缺少枪械 ID");
                continue;
            }

            ConfigureWeapon(weapon, weaponPath, report);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        report.WeaponCount = guids.Length;
        report.Passed = report.Problems.Count == 0;
        return report;
    }

    public static void ConfigureFromBatchMode()
    {
        WeaponActionConfigureReport report = Configure();
        if (!report.Passed)
        {
            throw new InvalidOperationException(
                "枪械动作集配置失败：" + string.Join("；", report.Problems));
        }

        Debug.Log(
            $"枪械动作集配置完成：{report.WeaponCount} 把枪，创建 {report.CreatedCount} 个，更新 {report.UpdatedCount} 个。");
    }

    public static string GetActionSetPath(WeaponDefinitionSO weapon)
    {
        string weaponPath = AssetDatabase.GetAssetPath(weapon);
        string fileName = Path.GetFileNameWithoutExtension(weaponPath);
        return WeaponActionFolder + "/" + fileName + ".asset";
    }

    private static void ConfigureWeapon(
        WeaponDefinitionSO weapon,
        string weaponPath,
        WeaponActionConfigureReport report)
    {
        string weaponId = Normalize(weapon.Basic.WeaponId);
        string resourceId = "weapon." + weaponId + ".actions";
        string actionPath = GetActionSetPath(weapon);
        WeaponActionSetSO actionSet = AssetDatabase.LoadAssetAtPath<WeaponActionSetSO>(actionPath);
        bool created = !actionSet;

        if (created)
        {
            actionSet = ScriptableObject.CreateInstance<WeaponActionSetSO>();
            AssetDatabase.CreateAsset(actionSet, actionPath);
            report.CreatedCount++;
        }
        else
        {
            report.UpdatedCount++;
        }

        SerializedObject weaponObject = new SerializedObject(weapon);
        SerializedProperty actionId = weaponObject.FindProperty("_actionSetResourceId");
        if (actionId == null)
        {
            report.Problems.Add(weaponPath + " 找不到动作集资源 ID 字段");
            return;
        }

        if (!string.Equals(actionId.stringValue, resourceId, StringComparison.Ordinal))
        {
            actionId.stringValue = resourceId;
            weaponObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(weapon);
            report.MigratedFieldCount++;
        }

        SerializedObject actionObject = new SerializedObject(actionSet);
        SerializedProperty version = actionObject.FindProperty("_dataVersion");
        if (version == null)
        {
            report.Problems.Add(actionPath + " 找不到数据版本字段");
            return;
        }

        if (version.intValue < CurrentDataVersion)
        {
            MigrateLegacyWeaponData(weapon, weaponPath, actionObject);
            version.intValue = CurrentDataVersion;
            actionObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(actionSet);
            report.MigratedFieldCount++;
        }
    }

    private static void MigrateLegacyWeaponData(
        WeaponDefinitionSO weapon,
        string weaponPath,
        SerializedObject actionObject)
    {
        Dictionary<string, string> legacy = ReadLegacyValues(weaponPath);

        float reloadDuration = GetFloat(legacy, "_tacticalReloadDuration", 2f);
        float emptyReloadDuration = GetFloat(legacy, "_emptyReloadDuration", 2.5f);
        float ammoCommitTime = GetFloat(legacy, "_ammoCommitTime", reloadDuration * 0.7f);
        float insertDuration = GetFloat(legacy, "_perRoundInsertDuration", 0.65f);
        float equipDuration = GetFloat(legacy, "_equipDuration", 0.45f);
        float holsterDuration = GetFloat(legacy, "_unequipDuration", 0.3f);
        bool canInterruptReload = GetBool(legacy, "_canInterruptReload", true);
        bool hasEmptyReload = GetBool(legacy, "_hasEmptyReloadAnimation", true);
        bool usesPerRoundReload = weapon.Ammo.ReloadStyle == WeaponReloadStyle.PerRound ||
            weapon.Ammo.ReloadStyle == WeaponReloadStyle.Revolver;

        ConfigureAction(
            actionObject.FindProperty("_fire"),
            WeaponActionType.Fire,
            true,
            GetFireDuration(weapon),
            true,
            true,
            true);
        ConfigureAction(
            actionObject.FindProperty("_reload"),
            WeaponActionType.Reload,
            !usesPerRoundReload,
            reloadDuration,
            canInterruptReload,
            false,
            false);
        ConfigureAction(
            actionObject.FindProperty("_emptyReload"),
            WeaponActionType.EmptyReload,
            !usesPerRoundReload && hasEmptyReload,
            emptyReloadDuration,
            canInterruptReload,
            false,
            false);
        ConfigureAction(
            actionObject.FindProperty("_perRoundStart"),
            WeaponActionType.PerRoundStart,
            usesPerRoundReload,
            insertDuration,
            canInterruptReload,
            false,
            false);
        ConfigureAction(
            actionObject.FindProperty("_perRoundInsert"),
            WeaponActionType.PerRoundInsert,
            usesPerRoundReload,
            insertDuration,
            canInterruptReload,
            false,
            false);
        ConfigureAction(
            actionObject.FindProperty("_perRoundEnd"),
            WeaponActionType.PerRoundEnd,
            usesPerRoundReload,
            insertDuration,
            canInterruptReload,
            false,
            false);
        ConfigureAction(
            actionObject.FindProperty("_equip"),
            WeaponActionType.Equip,
            true,
            equipDuration,
            true,
            false,
            false);
        ConfigureAction(
            actionObject.FindProperty("_holster"),
            WeaponActionType.Holster,
            true,
            holsterDuration,
            true,
            false,
            false);

        SetMarker(
            actionObject.FindProperty("_fire"),
            WeaponActionMarkerType.FireShot,
            0f);
        SetMarker(
            actionObject.FindProperty("_reload"),
            WeaponActionMarkerType.AmmoCommit,
            GetProgress(ammoCommitTime, reloadDuration));
        SetMarker(
            actionObject.FindProperty("_emptyReload"),
            WeaponActionMarkerType.AmmoCommit,
            GetProgress(ammoCommitTime, emptyReloadDuration));
        SetMarker(
            actionObject.FindProperty("_perRoundInsert"),
            WeaponActionMarkerType.InsertRound,
            0.5f);
    }

    private static void ConfigureAction(
        SerializedProperty action,
        WeaponActionType actionType,
        bool enabled,
        float duration,
        bool canInterrupt,
        bool allowsAim,
        bool allowsFire)
    {
        action.FindPropertyRelative("_actionType").enumValueIndex = (int)actionType;
        action.FindPropertyRelative("_enabled").boolValue = enabled;
        action.FindPropertyRelative("_duration").floatValue = Mathf.Max(0.01f, duration);
        action.FindPropertyRelative("_canInterrupt").boolValue = canInterrupt;
        action.FindPropertyRelative("_interruptStartProgress").floatValue = 0f;
        action.FindPropertyRelative("_allowsMovement").boolValue = true;
        action.FindPropertyRelative("_movementSpeedMultiplier").floatValue = 1f;
        action.FindPropertyRelative("_allowsAim").boolValue = allowsAim;
        action.FindPropertyRelative("_allowsFire").boolValue = allowsFire;
    }

    private static void SetMarker(
        SerializedProperty action,
        WeaponActionMarkerType markerType,
        float progress)
    {
        SerializedProperty markers = action.FindPropertyRelative("_markers");
        markers.arraySize = 1;
        SerializedProperty marker = markers.GetArrayElementAtIndex(0);
        marker.FindPropertyRelative("_markerType").enumValueIndex = (int)markerType;
        marker.FindPropertyRelative("_progress").floatValue = Mathf.Clamp01(progress);
    }

    private static float GetFireDuration(WeaponDefinitionSO weapon)
    {
        float rpm;
        switch (weapon.Fire.DefaultMode)
        {
            case WeaponFireMode.Automatic:
                rpm = weapon.Fire.AutomaticRpm;
                break;
            case WeaponFireMode.Burst:
                rpm = weapon.Fire.BurstRpm;
                break;
            default:
                rpm = weapon.Fire.SemiAutomaticRpm;
                break;
        }

        return Mathf.Clamp(60f / Mathf.Max(1f, rpm), 0.05f, 0.5f);
    }

    private static float GetProgress(float time, float duration)
    {
        if (duration <= 0f)
        {
            return 1f;
        }

        return Mathf.Clamp01(time / duration);
    }

    private static Dictionary<string, string> ReadLegacyValues(string assetPath)
    {
        Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
        string[] lines = File.ReadAllLines(assetPath);
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            int separator = line.IndexOf(':');
            if (separator <= 0 || line[0] != '_')
            {
                continue;
            }

            string key = line.Substring(0, separator);
            string value = line.Substring(separator + 1).Trim();
            if (!values.ContainsKey(key))
            {
                values.Add(key, value);
            }
        }
        return values;
    }

    private static float GetFloat(
        Dictionary<string, string> values,
        string key,
        float fallback)
    {
        if (values.TryGetValue(key, out string value) &&
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float result))
        {
            return result;
        }

        return fallback;
    }

    private static bool GetBool(
        Dictionary<string, string> values,
        string key,
        bool fallback)
    {
        if (!values.TryGetValue(key, out string value))
        {
            return fallback;
        }

        return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static void EnsureFolder(string path)
    {
        string[] parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }
            current = next;
        }
    }

    private static string Normalize(string value)
    {
        return value.Trim().ToLowerInvariant();
    }

    private static int CompareAssetGuidsByPath(string left, string right)
    {
        return string.CompareOrdinal(
            AssetDatabase.GUIDToAssetPath(left),
            AssetDatabase.GUIDToAssetPath(right));
    }
}

[Serializable]
public sealed class WeaponActionConfigureReport
{
    public bool Passed { get; set; }
    public int WeaponCount { get; set; }
    public int CreatedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int MigratedFieldCount { get; set; }
    public List<string> Problems { get; set; } = new List<string>();
}
