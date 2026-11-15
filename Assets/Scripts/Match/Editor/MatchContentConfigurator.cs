using System.Collections.Generic;
using Unity.AI.Navigation;
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
    public const string BattleField1MapPath = "Assets/SO/Maps/BattleField1.asset";
    public const string ExampleMapPath = "Assets/SO/Maps/Example.asset";
    public const string MapCatalogPath = "Assets/SO/Maps/MapCatalog.asset";
    public const string SoloSpawnPointPath =
        "Assets/Prefabs/Gameplay/单人复活点.prefab";
    public const string TeamSpawnZonePath =
        "Assets/Prefabs/Gameplay/正式团队复活区.prefab";
    public const string BattleField1ScenePath = "Assets/Scenes/BattleField1.unity";
    private const string BattleField1NavigationDataPath =
        "Assets/Scenes/example/NavMesh-AI 复活区域.asset";
    private const string MapNavigationName = "地图导航";
    private static readonly string[] AdditionalBattlefieldNames =
    {
        "BattleField2", "BattleField3", "BattleField4", "BattleField5"
    };

    [MenuItem("工具/Gun Demo/对局/配置内置模式与爆破资源")]
    public static MatchContentConfigureReport Configure()
    {
        EnsureFolder("Assets/Prefabs", "Props");
        EnsureFolder("Assets/Prefabs", "Match");
        EnsureFolder("Assets/Prefabs", "Gameplay");
        EnsureFolder("Assets/SO", "Modes");
        EnsureFolder("Assets/SO", "Maps");
        CreateBombPrefab();
        CreateBombSitePrefab();
        CreateSoloSpawnPointPrefab();
        CreateTeamSpawnZonePrefab();
        ConfigureModes();
        ConfigureMaps();
        BombShopContentConfigurator.Configure();
        EnsureMapScenesInBuildSettings();
        EnsureSpawnZonesInOpenScene();
        EnsureMapNavigationInOpenScene();
        EnsureSitesInOpenScene();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return new MatchContentConfigureReport
        {
            Passed = AssetDatabase.LoadAssetAtPath<GameObject>(BombPrefabPath) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(BombSitePrefabPath) &&
                AssetDatabase.LoadAssetAtPath<ModeDefinitionSO>(SoloModePath) &&
                AssetDatabase.LoadAssetAtPath<ModeDefinitionSO>(TdmModePath) &&
                AssetDatabase.LoadAssetAtPath<ModeDefinitionSO>(BombModePath) &&
                AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(BattleField1MapPath) &&
                AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(ExampleMapPath) &&
                AssetDatabase.LoadAssetAtPath<MapCatalogSO>(MapCatalogPath) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(SoloSpawnPointPath) &&
                AssetDatabase.LoadAssetAtPath<GameObject>(TeamSpawnZonePath)
        };
    }

    public static void ConfigureFromCommandLine()
    {
        Configure();
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

    private static void CreateSoloSpawnPointPrefab()
    {
        GameObject root = new GameObject("单人复活点");
        PlayerSpawnZone spawnPoint = root.AddComponent<PlayerSpawnZone>();
        spawnPoint.ConfigureSoloPoint();
        PrefabUtility.SaveAsPrefabAsset(root, SoloSpawnPointPath);
        Object.DestroyImmediate(root);
    }

    private static void CreateTeamSpawnZonePrefab()
    {
        GameObject root = new GameObject("正式团队复活区");
        PlayerSpawnZone spawnZone = root.AddComponent<PlayerSpawnZone>();
        spawnZone.ConfigureMatchZone(
            PlayerSpawnZoneMode.Team,
            -1,
            0,
            new Vector3(9f, 3f, 9f));
        PrefabUtility.SaveAsPrefabAsset(root, TeamSpawnZonePath);
        Object.DestroyImmediate(root);
    }

    private static void ConfigureModes()
    {
        List<ModeConfigItem> score = new List<ModeConfigItem>
        {
            ModeConfigItem.CreateNumber(
                "match.startCountdown", 5f, ModeValueType.Float, false),
            ModeConfigItem.CreateNumber("score.kill", 100, ModeValueType.Int, false),
            ModeConfigItem.CreateNumber("score.assist", 25, ModeValueType.Int, false),
            ModeConfigItem.CreateNumber("score.headshotBonus", 25, ModeValueType.Int, false),
            ModeConfigItem.CreateNumber("score.roundWin", 100, ModeValueType.Int, false),
            ModeConfigItem.CreateNumber("score.matchWin", 500, ModeValueType.Int, false)
        };
        ConfigureMode(
            SoloModePath, ModeKind.Solo, "mode.solo", "个人战",
            8, 1, true, 5f, WinConditionType.Score, 30, 300f,
            "ui.match.mode.solo.logo",
            "solo.self_preservation",
            Combine(score,
                ModeConfigItem.CreateString("rules.type", "solo", false),
                ModeConfigItem.CreateString("hud.scoreStyle", "solo", false),
                ModeConfigItem.CreateBool("match.usesRounds", false, false),
                ModeConfigItem.CreateBool("match.respawnEnabled", true, false),
                ModeConfigItem.CreateNumber("match.respawnDelay", 5f, ModeValueType.Float, false),
                ModeConfigItem.CreateBool("solo.respawnEnabled", true, false),
                ModeConfigItem.CreateNumber("solo.roundTimeLimit", 300f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("solo.scoreToWin", 30, ModeValueType.Int, false),
                ModeConfigItem.CreateNumber("ai.selfPreservation", 1f, ModeValueType.Float, false)));
        ConfigureMode(
            TdmModePath, ModeKind.TeamDeathmatch, "mode.tdm.short", "团队竞技",
            2, 5, true, 5f, WinConditionType.TeamScore, 30, 300f,
            "ui.match.mode.tdm.logo",
            "tdm.teamwork",
            Combine(score,
                ModeConfigItem.CreateString("rules.type", "tdm", false),
                ModeConfigItem.CreateString("hud.scoreStyle", "team", false),
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
            BombModePath, ModeKind.Bomb, "mode.bomb", "赏金爆破",
            2, 5, false, 0f, WinConditionType.RoundWins, 6, 120f,
            "ui.match.mode.bomb.logo",
            "bomb.team_strategy",
            Combine(score,
                ModeConfigItem.CreateString("rules.type", "bomb", false),
                ModeConfigItem.CreateString("hud.scoreStyle", "rounds", false),
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
                ModeConfigItem.CreateNumber("bomb.explosionKillRadius", 18f, ModeValueType.Float, false),
                ModeConfigItem.CreateNumber("bomb.explosionVisualScale", 1f, ModeValueType.Float, false),
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
                ModeConfigItem.CreateNumber(
                    "economy.buyAfterCountdownDuration",
                    5f,
                    ModeValueType.Float,
                    false),
                ModeConfigItem.CreateString(
                    "economy.shopCatalogResourceId",
                    "economy.bomb.shop_catalog",
                    false),
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

    private static void ConfigureMaps()
    {
        MapDefinitionSO battleField1 =
            AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(BattleField1MapPath);
        if (!battleField1)
        {
            battleField1 = ScriptableObject.CreateInstance<MapDefinitionSO>();
            AssetDatabase.CreateAsset(battleField1, BattleField1MapPath);
        }
        battleField1.Configure(
            "map.battlefield1",
            "BattleField1",
            "BattleField1",
            new[] { ModeKind.TeamDeathmatch, ModeKind.Bomb });
        EditorUtility.SetDirty(battleField1);

        MapDefinitionSO example =
            AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(ExampleMapPath);
        if (!example)
        {
            example = ScriptableObject.CreateInstance<MapDefinitionSO>();
            AssetDatabase.CreateAsset(example, ExampleMapPath);
        }
        example.Configure(
            "map.example",
            "测试靶场",
            "example",
            new[] { ModeKind.Solo, ModeKind.Bomb });
        EditorUtility.SetDirty(example);

        MapCatalogSO catalog = AssetDatabase.LoadAssetAtPath<MapCatalogSO>(MapCatalogPath);
        if (!catalog)
        {
            catalog = ScriptableObject.CreateInstance<MapCatalogSO>();
            AssetDatabase.CreateAsset(catalog, MapCatalogPath);
        }
        List<MapDefinitionSO> maps = new List<MapDefinitionSO> { battleField1, example };
        for (int i = 0; i < AdditionalBattlefieldNames.Length; i++)
        {
            string mapName = AdditionalBattlefieldNames[i];
            string path = $"Assets/SO/Maps/{mapName}.asset";
            MapDefinitionSO map = AssetDatabase.LoadAssetAtPath<MapDefinitionSO>(path);
            if (!map)
            {
                map = ScriptableObject.CreateInstance<MapDefinitionSO>();
                AssetDatabase.CreateAsset(map, path);
            }
            map.Configure(
                "map." + mapName.ToLowerInvariant(),
                mapName,
                mapName,
                new[] { ModeKind.TeamDeathmatch, ModeKind.Solo, ModeKind.Bomb });
            EditorUtility.SetDirty(map);
            maps.Add(map);
        }
        catalog.Configure(maps);
        EditorUtility.SetDirty(catalog);
    }

    private static void EnsureMapScenesInBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes =
            new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        List<string> requiredScenes = new List<string> { BattleField1ScenePath };
        for (int i = 0; i < AdditionalBattlefieldNames.Length; i++)
        {
            requiredScenes.Add($"Assets/Scenes/{AdditionalBattlefieldNames[i]}.unity");
        }
        for (int requiredIndex = 0; requiredIndex < requiredScenes.Count; requiredIndex++)
        {
            string requiredScene = requiredScenes[requiredIndex];
            bool found = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != requiredScene)
                {
                    continue;
                }
                scenes[i].enabled = true;
                found = true;
                break;
            }
            if (!found && AssetDatabase.LoadAssetAtPath<SceneAsset>(requiredScene))
            {
                scenes.Add(new EditorBuildSettingsScene(requiredScene, true));
            }
        }
        EditorBuildSettings.scenes = scenes.ToArray();
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

    [MenuItem("工具/Gun Demo/对局/修复当前地图正式复活区")]
    public static void EnsureSpawnZonesInOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid())
        {
            return;
        }

        string scenePath = EditorSceneManager.GetActiveScene().path;
        if (scenePath != "Assets/Scenes/example.unity" &&
            scenePath != BattleField1ScenePath)
        {
            return;
        }

        PlayerSpawnZone[] zones = Object.FindObjectsByType<PlayerSpawnZone>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        PlayerSpawnZone testZone = null;
        PlayerSpawnZone teamZero = null;
        PlayerSpawnZone teamOne = null;
        PlayerSpawnZone unassignedTeam = null;
        PlayerSpawnZone legacyPlayerZone = null;
        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i].Mode == PlayerSpawnZoneMode.Team)
            {
                if (zones[i].Team == 0)
                {
                    teamZero = zones[i];
                }
                else if (zones[i].Team == 1)
                {
                    teamOne = zones[i];
                }
                else if (!unassignedTeam)
                {
                    unassignedTeam = zones[i];
                }
            }
            if (!testZone && zones[i].ControlMode == PlayerSpawnControlMode.TestLocal)
            {
                testZone = zones[i];
            }
            if (!legacyPlayerZone &&
                zones[i].ControlMode == PlayerSpawnControlMode.MatchManaged &&
                zones[i].Mode == PlayerSpawnZoneMode.Player)
            {
                legacyPlayerZone = zones[i];
            }
        }

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TeamSpawnZonePath);
        if (!prefab)
        {
            return;
        }

        if (!teamZero)
        {
            teamZero = unassignedTeam;
        }
        Vector3 firstPosition = testZone
            ? testZone.transform.position
            : Vector3.zero;
        Quaternion firstRotation = testZone
            ? testZone.transform.rotation
            : Quaternion.identity;
        teamZero = EnsureTeamZone(
            teamZero,
            prefab,
            0,
            0,
            firstPosition,
            firstRotation);

        if (!teamOne)
        {
            teamOne = legacyPlayerZone;
        }
        Vector3 secondPosition = teamOne
            ? teamOne.transform.position
            : teamZero.transform.position + teamZero.transform.forward * 60f;
        Quaternion secondRotation = teamOne
            ? teamOne.transform.rotation
            : Quaternion.Euler(0f, 180f, 0f) * teamZero.transform.rotation;
        EnsureTeamZone(
            teamOne,
            prefab,
            1,
            1,
            secondPosition,
            secondRotation);

        EnsureMapNavigationInOpenScene();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    [MenuItem("工具/Gun Demo/对局/修复当前地图共享导航")]
    public static void EnsureMapNavigationInOpenScene()
    {
        if (!EditorSceneManager.GetActiveScene().IsValid() ||
            EditorSceneManager.GetActiveScene().path != BattleField1ScenePath)
        {
            return;
        }

        NavMeshSurface[] surfaces = Object.FindObjectsByType<NavMeshSurface>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        NavMeshSurface source = null;
        NavMeshSurface mapSurface = null;
        for (int i = 0; i < surfaces.Length; i++)
        {
            if (surfaces[i].gameObject.name == MapNavigationName)
            {
                mapSurface = surfaces[i];
            }
            if (!source && surfaces[i].navMeshData)
            {
                source = surfaces[i];
            }
        }

        if (!mapSurface)
        {
            GameObject navigationObject = new GameObject(MapNavigationName);
            if (source)
            {
                navigationObject.transform.SetPositionAndRotation(
                    source.transform.position,
                    source.transform.rotation);
                navigationObject.transform.localScale = source.transform.lossyScale;
            }
            else
            {
                PlayerSpawnZone[] zones = Object.FindObjectsByType<PlayerSpawnZone>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None);
                for (int i = 0; i < zones.Length; i++)
                {
                    if (zones[i].ControlMode != PlayerSpawnControlMode.TestLocal)
                    {
                        continue;
                    }

                    navigationObject.transform.SetPositionAndRotation(
                        zones[i].transform.position,
                        zones[i].transform.rotation);
                    break;
                }
            }
            mapSurface = navigationObject.AddComponent<NavMeshSurface>();
        }
        if (source && source != mapSurface)
        {
            EditorUtility.CopySerialized(source, mapSurface);
        }
        if (!mapSurface.navMeshData)
        {
            mapSurface.navMeshData = AssetDatabase.LoadAssetAtPath<UnityEngine.AI.NavMeshData>(
                BattleField1NavigationDataPath);
        }
        if (!mapSurface.TryGetComponent(out AINavigationSurfaceCtrl _))
        {
            mapSurface.gameObject.AddComponent<AINavigationSurfaceCtrl>();
        }

        for (int i = 0; i < surfaces.Length; i++)
        {
            NavMeshSurface surface = surfaces[i];
            if (!surface || surface == mapSurface)
            {
                continue;
            }

            AINavigationSurfaceCtrl navigationCtrl =
                surface.GetComponent<AINavigationSurfaceCtrl>();
            if (navigationCtrl)
            {
                Object.DestroyImmediate(navigationCtrl);
            }
            Object.DestroyImmediate(surface);
        }

        EditorUtility.SetDirty(mapSurface);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
    }

    private static PlayerSpawnZone EnsureTeamZone(
        PlayerSpawnZone zone,
        GameObject prefab,
        int team,
        int slotOffset,
        Vector3 position,
        Quaternion rotation)
    {
        if (!zone)
        {
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetPositionAndRotation(position, rotation);
            zone = instance.GetComponent<PlayerSpawnZone>();
        }

        zone.name = $"队伍 {team + 1} 正式复活区";
        zone.ConfigureMatchZone(
            PlayerSpawnZoneMode.Team,
            team,
            slotOffset,
            new Vector3(12f, 3f, 12f));
        EditorUtility.SetDirty(zone);
        PrefabUtility.RecordPrefabInstancePropertyModifications(zone);
        return zone;
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
