using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MatchContentConfigurator
{
    public const string BombPrefabPath = "Assets/Prefabs/Props/Bomb.prefab";
    public const string BombModelPath =
        "Assets/Resource/Props/Bomb/C4_Keypad_Textured.fbx";
    public const string BombSitePrefabPath = "Assets/Prefabs/Match/BombSite.prefab";
    public const string SoloModePath = "Assets/SO/Modes/Solo.asset";
    public const string TdmModePath = "Assets/SO/Modes/TeamDeathmatch.asset";
    public const string BombModePath = "Assets/SO/Modes/Bomb.asset";

    [MenuItem("工具/Gun Demo/对局/配置内置模式与爆破资源")]
    public static MatchContentConfigureReport Configure()
    {
        EnsureFolder("Assets/Prefabs", "Props");
        EnsureFolder("Assets/Prefabs", "Match");
        EnsureFolder("Assets/SO", "Modes");
        CreateBombPrefab();
        CreateBombSitePrefab();
        ConfigureModes();
        EnsureSitesInOpenScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return new MatchContentConfigureReport
        {
            Passed = AssetDatabase.LoadAssetAtPath<GameObject>(BombPrefabPath) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(BombSitePrefabPath) &&
                AssetDatabase.LoadAssetAtPath<ModeDefinitionSO>(SoloModePath) &&
                AssetDatabase.LoadAssetAtPath<ModeDefinitionSO>(TdmModePath) &&
                AssetDatabase.LoadAssetAtPath<ModeDefinitionSO>(BombModePath)
        };
    }

    private static void CreateBombPrefab()
    {
        GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(BombModelPath);
        if (!modelAsset)
        {
            Debug.LogError("缺少正式炸弹模型：" + BombModelPath);
            return;
        }

        GameObject root = new GameObject("Bomb");
        GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        model.name = "C4_Keypad";
        model.transform.SetParent(root.transform, false);

        Bounds bounds = CalculateRendererBounds(model);
        BoxCollider collider = root.AddComponent<BoxCollider>();
        collider.center = bounds.center;
        collider.size = bounds.size;

        GameObject lightObject = new GameObject("ArmedLight");
        lightObject.transform.SetParent(root.transform, false);
        lightObject.transform.localPosition = new Vector3(
            bounds.center.x,
            bounds.max.y + 0.008f,
            bounds.center.z);
        Light armedLight = lightObject.AddComponent<Light>();
        armedLight.type = LightType.Point;
        armedLight.color = new Color(1f, 0.015f, 0.01f);
        armedLight.range = 2.2f;
        armedLight.intensity = 0f;
        armedLight.shadows = LightShadows.None;
        armedLight.enabled = false;

        root.name = "Bomb";
        BombEntity bomb = root.AddComponent<BombEntity>();
        bomb.ConfigureArmedLight(armedLight);
        PrefabUtility.SaveAsPrefabAsset(root, BombPrefabPath);
        Object.DestroyImmediate(root);
    }

    private static Bounds CalculateRendererBounds(GameObject model)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            return new Bounds(Vector3.zero, Vector3.one * 0.5f);
        }

        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static void CreateBombSitePrefab()
    {
        GameObject root = new GameObject("BombSite");
        SphereCollider area = root.AddComponent<SphereCollider>();
        area.isTrigger = true;
        area.radius = 3f;
        root.AddComponent<BombSite>();
        PrefabUtility.SaveAsPrefabAsset(root, BombSitePrefabPath);
        Object.DestroyImmediate(root);
    }

    private static void ConfigureModes()
    {
        List<ModeConfigItem> score = new List<ModeConfigItem>
        {
            ModeConfigItem.CreateNumber("score.kill", 100, ModeValueType.Int, false),
            ModeConfigItem.CreateNumber("score.assist", 25, ModeValueType.Int, false),
            ModeConfigItem.CreateNumber("score.headshotBonus", 25, ModeValueType.Int, false),
            ModeConfigItem.CreateNumber("score.roundWin", 100, ModeValueType.Int, false),
            ModeConfigItem.CreateNumber("score.matchWin", 500, ModeValueType.Int, false)
        };
        ConfigureMode(
            SoloModePath, ModeKind.Solo, "mode.solo", "个人竞技",
            8, 1, true, 5f, WinConditionType.Score, 30, 300f,
            "ui.match.mode.solo.logo",
            "solo.self_preservation",
            Combine(score,
                ModeConfigItem.CreateString("rules.type", "solo", false),
                ModeConfigItem.CreateBool("match.usesRounds", false, false),
                ModeConfigItem.CreateBool("match.respawnEnabled", true, false),
                ModeConfigItem.CreateNumber("match.respawnDelay", 5f, ModeValueType.Float, false),
                ModeConfigItem.CreateBool("solo.respawnEnabled", true, false),
                ModeConfigItem.CreateNumber("solo.roundTimeLimit", 300f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("solo.scoreToWin", 30, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("ai.selfPreservation", 1f, ModeValueType.Float, false)));
        ConfigureMode(
            TdmModePath, ModeKind.TeamDeathmatch, "mode.tdm.short", "短团队竞技",
            2, 5, true, 5f, WinConditionType.TeamScore, 30, 300f,
            "ui.match.mode.tdm.logo",
            "tdm.teamwork",
            Combine(score,
                ModeConfigItem.CreateString("rules.type", "tdm", false),
                ModeConfigItem.CreateBool("match.usesRounds", false, false),
                ModeConfigItem.CreateBool("match.respawnEnabled", true, false),
                ModeConfigItem.CreateNumber("match.respawnDelay", 5f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("tdm.teamCount", 2, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("tdm.teamSize", 5, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("tdm.respawnDelay", 5f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("tdm.scoreToWin", 30, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("tdm.roundTimeLimit", 300f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("ai.teamwork", 1f, ModeValueType.Float, false)));
        ConfigureMode(
            BombModePath, ModeKind.Bomb, "mode.bomb", "爆破模式",
            2, 5, false, 0f, WinConditionType.RoundWins, 6, 120f,
            "ui.match.mode.bomb.logo",
            "bomb.team_strategy",
            Combine(score,
                ModeConfigItem.CreateString("rules.type", "bomb", false),
                ModeConfigItem.CreateBool("features.bombObjective", true, false),
                ModeConfigItem.CreateBool("features.economy", true, false),
                ModeConfigItem.CreateBool("features.eliminationRounds", true, false),
                ModeConfigItem.CreateBool("match.usesRounds", true, false),
                ModeConfigItem.CreateBool("match.respawnEnabled", false, false),
                ModeConfigItem.CreateNumber("match.respawnDelay", 0f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("match.roundsToWin", 6, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("match.roundRestartDelay", 4f, ModeValueType.Float, false),
                ModeConfigItem.CreateString("bomb.resourceId", BombRules.DefaultBombResourceId, false),
                ModeConfigItem.CreateNumber("bomb.plantDuration", 3f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("bomb.defuseDuration", 5f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("bomb.fuseDuration", 40f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("bomb.pickupRadius", 1.8f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("bomb.siteRadius", 3f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("bomb.roundTimeLimit", 120f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("bomb.roundRestartDelay", 4f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("bomb.roundsToWin", 6, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("bomb.attackerTeam", 0, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("bomb.defenderTeam", 1, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("bomb.carrierSpeedScale", 0.92f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("economy.startMoney", 800, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("economy.maxMoney", 16000, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("economy.buyDuration", 25f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("economy.killReward", 300, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("economy.smgKillReward", 600, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("economy.shotgunKillReward", 900, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("economy.meleeKillReward", 1500, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("economy.headshotBonus", 100, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("economy.roundWinReward", 3250, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("economy.roundLossReward", 1900, ModeValueType.Int, false),
                ModeConfigItem.CreateString("economy.defaultPistol", "weapon.m1911.data", false),
                ModeConfigItem.CreateString("economy.shopItems", "AK|weapon.ak.data|2700;AS Val|weapon.as-val.data|2900;MPS5|weapon.mps5.data|1500;PDW90|weapon.pdw90.data|2350;KXG12|weapon.kxg-12.data|2000;Kar98k|weapon.kar98k.data|3200", false),
                ModeConfigItem.CreateString("economy.killRewardOverrides", "mps5=600;pdw90=600;kxg-12=900;melee-fist=1500", false),
                ModeConfigItem.CreateNumber("economy.armorPrice", 650, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("economy.helmetPrice", 350, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("economy.pickupDistance", 3.2f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("economy.dropForwardSpeed", 2f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("economy.dropUpSpeed", 0.6f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("ai.attackBias", 1f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("ai.defendBias", 1f, ModeValueType.Float, false)));
    }

    private static void ConfigureMode(
        string path,
        ModeKind kind,
        string id,
        string displayName,
        int teamCount,
        int teamSize,
        bool respawn,
        float respawnDelay,
        WinConditionType condition,
        int scoreToWin,
        float timeLimit,
        string logoResourceId,
        string patchId,
        IEnumerable<ModeConfigItem> items)
    {
        ModeDefinitionSO mode = AssetDatabase.LoadAssetAtPath<ModeDefinitionSO>(path);
        if (!mode)
        {
            mode = ScriptableObject.CreateInstance<ModeDefinitionSO>();
            AssetDatabase.CreateAsset(mode, path);
        }
        mode.ConfigureBuiltIn(
            kind, id, displayName, teamCount, teamSize, respawn, respawnDelay,
            condition, scoreToWin, timeLimit, logoResourceId, patchId, items);
        EditorUtility.SetDirty(mode);
    }

    private static IEnumerable<ModeConfigItem> Combine(
        List<ModeConfigItem> common,
        params ModeConfigItem[] specific)
    {
        List<ModeConfigItem> result = new List<ModeConfigItem>(common);
        result.AddRange(specific);
        return result;
    }

    private static void EnsureSitesInOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != "Assets/Scenes/example.unity")
        {
            return;
        }
        BombSite[] sites = Object.FindObjectsByType<BombSite>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (sites.Length >= 2)
        {
            return;
        }
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BombSitePrefabPath);
        for (int i = sites.Length; i < 2; i++)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = i == 0 ? "BombSite_A" : "BombSite_B";
            instance.transform.position = new Vector3(i == 0 ? -12f : 12f, 0.15f, 10f);
            instance.GetComponent<BombSite>().Configure(
                i == 0 ? "site.a" : "site.b",
                i == 0 ? "A 点" : "B 点",
                3f);
        }
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
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

public sealed class MatchContentConfigureReport
{
    public bool Passed;
}
