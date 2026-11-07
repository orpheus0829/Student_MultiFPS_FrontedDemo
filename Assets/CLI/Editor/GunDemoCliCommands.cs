using System.Collections.Generic;
using System.IO;
using Unity.Pipeline.Commands;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class GunDemoCliCommands
{
    private const string Group = "gundemo";

    [CliGroupCommand(Group, "一次返回项目全部专属检查，供 Agent 工作流生成验证证据", Name = "verification_bundle", Tags = new[] { "gundemo/verification" })]
    public static CliVerificationBundle VerificationBundle()
    {
        CliSceneReport scene = InspectScene();
        CliPlayerReport player = InspectPlayer();
        CliAnimationReport animation = InspectAnimation();
        CliUiReport ui = InspectUi();
        CliDataReport data = InspectData();
        CliPrefabReport prefabs = InspectPrefabs();
        CliAssetReport assets = InspectAssets();
        int blockingProblemCount = scene.Problems.Count +
            player.Problems.Count +
            ui.Problems.Count +
            data.Problems.Count +
            prefabs.Problems.Count +
            assets.MissingReferenceCount +
            assets.CandidateReferenceCount;
        int warningCount = animation.Problems.Count;

        return new CliVerificationBundle
        {
            CreatedAtUtc = System.DateTime.UtcNow,
            Project = ProjectSnapshot(),
            Scene = scene,
            Player = player,
            Animation = animation,
            Ui = ui,
            Data = data,
            Prefabs = prefabs,
            Assets = assets,
            ProblemCount = blockingProblemCount + warningCount,
            BlockingProblemCount = blockingProblemCount,
            WarningCount = warningCount,
            Passed = blockingProblemCount == 0
        };
    }

    [CliGroupCommand(Group, "获取当前项目、场景、资源和控制台的简明状态", Name = "project_snapshot", Tags = new[] { "gundemo/project" })]
    public static CliProjectSnapshot ProjectSnapshot()
    {
        CliSceneReport scene = InspectScene();
        CliDataReport data = InspectData();
        CliPrefabReport prefabs = InspectPrefabs();
        int warnings = 0;
        int errors = 0;
        GetConsoleCounts(out warnings, out errors);

        List<CliCheckResult> checks = new List<CliCheckResult>
        {
            CreateCheck("场景已保存", !string.IsNullOrEmpty(scene.ScenePath) && !scene.IsDirty, scene.ScenePath),
            CreateCheck("场景没有丢失脚本", scene.MissingScriptCount == 0, scene.MissingScriptCount + " 个"),
            CreateCheck("Prefab 没有丢失脚本", prefabs.MissingScriptCount == 0, prefabs.MissingScriptCount + " 个"),
            CreateCheck("Prefab 没有丢失材质", prefabs.MissingMaterialCount == 0, prefabs.MissingMaterialCount + " 个"),
            CreateCheck("控制台没有错误", errors == 0, errors + " 个")
        };

        return new CliProjectSnapshot
        {
            ProjectName = Application.productName,
            UnityVersion = Application.unityVersion,
            ActiveScene = scene.SceneName,
            IsPlaying = EditorApplication.isPlaying,
            IsCompiling = EditorApplication.isCompiling,
            SceneObjectCount = scene.ObjectCount,
            PrefabCount = prefabs.PrefabCount,
            ScriptableObjectCount = data.ScriptableObjectCount,
            WarningCount = warnings,
            ErrorCount = errors,
            Checks = checks
        };
    }

    [CliGroupCommand(Group, "检查当前场景的结构、构建设置和常见缺失项", Name = "inspect_scene", Tags = new[] { "gundemo/scene" })]
    public static CliSceneReport InspectScene()
    {
        Scene scene = SceneManager.GetActiveScene();
        List<GameObject> objects = GunDemoCliUtility.GetSceneObjects();
        int activeCount = 0;
        int cameraCount = 0;
        int lightCount = 0;
        int listenerCount = 0;
        int missingScripts = 0;
        List<string> problems = new List<string>();

        foreach (GameObject gameObject in objects)
        {
            if (gameObject.activeInHierarchy)
            {
                activeCount++;
            }

            cameraCount += gameObject.GetComponents<Camera>().Length;
            lightCount += gameObject.GetComponents<Light>().Length;
            listenerCount += gameObject.GetComponents<AudioListener>().Length;
            int missing = GunDemoCliUtility.CountMissingScripts(gameObject);
            missingScripts += missing;
            if (missing > 0)
            {
                problems.Add(GunDemoCliUtility.GetPath(gameObject) + " 丢失 " + missing + " 个脚本");
            }
        }

        if (cameraCount == 0)
        {
            problems.Add("当前场景没有 Camera");
        }

        if (listenerCount > 1)
        {
            problems.Add("当前场景存在多个 AudioListener");
        }

        List<string> buildScenes = new List<string>();
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (buildScene.enabled)
            {
                buildScenes.Add(buildScene.path);
            }
        }

        return new CliSceneReport
        {
            SceneName = scene.name,
            ScenePath = scene.path,
            IsDirty = scene.isDirty,
            RootCount = scene.IsValid() && scene.isLoaded ? scene.rootCount : 0,
            ObjectCount = objects.Count,
            ActiveObjectCount = activeCount,
            CameraCount = cameraCount,
            LightCount = lightCount,
            AudioListenerCount = listenerCount,
            MissingScriptCount = missingScripts,
            BuildScenes = buildScenes,
            Problems = problems
        };
    }

    [CliGroupCommand(Group, "检查场景内玩家输入、动作、动画和双视角配置", Name = "inspect_player", Tags = new[] { "gundemo/player" })]
    public static CliPlayerReport InspectPlayer()
    {
        PlayerActionCtrl[] actions = Object.FindObjectsByType<PlayerActionCtrl>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<CliPlayerItem> players = new List<CliPlayerItem>();
        List<string> problems = new List<string>();

        foreach (PlayerActionCtrl action in actions)
        {
            GameObject gameObject = action.gameObject;
            PlayerInputCtrl input = gameObject.GetComponent<PlayerInputCtrl>();
            CharacterController characterController = gameObject.GetComponent<CharacterController>();
            PlayerMoveCtrl move = gameObject.GetComponent<PlayerMoveCtrl>();
            PlayerAimCtrl aim = gameObject.GetComponent<PlayerAimCtrl>();
            PlayerAnimationCtrl animation = gameObject.GetComponent<PlayerAnimationCtrl>();
            WeaponActionCtrl weaponActions = gameObject.GetComponent<WeaponActionCtrl>();
            PlayerWeaponInventoryCtrl weaponInventory =
                gameObject.GetComponent<PlayerWeaponInventoryCtrl>();
            WeaponCombatCtrl weaponCombat = gameObject.GetComponent<WeaponCombatCtrl>();
            PlayerWeaponViewCtrl weaponView = gameObject.GetComponent<PlayerWeaponViewCtrl>();
            FirstPersonCameraMotionCtrl cameraMotion =
                gameObject.GetComponentInChildren<FirstPersonCameraMotionCtrl>(true);
            PlayerInfo playerInfo = gameObject.GetComponent<PlayerInfo>();
            CharacterHealth health = gameObject.GetComponent<CharacterHealth>();
            CombatScore score = gameObject.GetComponent<CombatScore>();
            BodyHitbox[] hitboxes = gameObject.GetComponentsInChildren<BodyHitbox>(true);
            CliPlayerItem item = new CliPlayerItem
            {
                Path = GunDemoCliUtility.GetPath(gameObject),
                IsActive = gameObject.activeInHierarchy,
                HasInput = input,
                HasAction = action,
                HasCharacterController = characterController,
                HasMove = move,
                HasAim = aim,
                HasWeaponAction = weaponActions,
                HasWeaponInventory = weaponInventory,
                HasWeaponCombat = weaponCombat,
                HasWeaponView = weaponView,
                HasCameraMotion = cameraMotion,
                HasPlayerData = action.PlayerData,
                HasAnimationController = animation,
                HasFirstPersonView = animation && animation.FirstPersonView,
                HasThirdPersonView = animation && animation.ThirdPersonView,
                HasPlayerInfo = playerInfo,
                HasHealth = health,
                HasCombatScore = score,
                BodyHitboxCount = hitboxes.Length,
                WeaponSlotCount = weaponInventory ? weaponInventory.SlotCount : 0,
                ConfiguredWeaponCount = weaponInventory
                    ? weaponInventory.ConfiguredWeaponCount
                    : 0,
                PlayerName = playerInfo ? playerInfo.PlayerName : string.Empty,
                WeaponData = GetCurrentWeaponResourceId(weaponActions)
            };
            players.Add(item);

            if (!item.HasInput || !item.HasCharacterController || !item.HasPlayerData)
            {
                problems.Add(item.Path + " 的输入、CharacterController 或玩家数据不完整");
            }

            if (!item.HasMove || !item.HasAim || !item.HasWeaponInventory ||
                !item.HasWeaponAction ||
                !item.HasWeaponCombat || !item.HasWeaponView || !item.HasCameraMotion)
            {
                problems.Add(item.Path + " 的移动、瞄准、枪械栏、枪械动作、枪械表现或镜头反馈组件不完整");
            }

            if (item.HasWeaponInventory &&
                (item.WeaponSlotCount < 1 ||
                 item.ConfiguredWeaponCount > item.WeaponSlotCount))
            {
                problems.Add(item.Path + " 的枪械栏容量或已配置枪械数量无效");
            }

            if (!item.HasAnimationController || !item.HasFirstPersonView || !item.HasThirdPersonView)
            {
                problems.Add(item.Path + " 的第一/第三人称动画表现配置不完整");
            }

            if (!item.HasPlayerInfo || !item.HasHealth || !item.HasCombatScore)
            {
                problems.Add(item.Path + " 的玩家身份、生命或计分组件不完整");
            }

            if (item.BodyHitboxCount < 3)
            {
                problems.Add(item.Path + " 缺少头部、上半身或下半身命中区域");
            }
        }

        if (players.Count == 0)
        {
            problems.Add("当前场景没有 PlayerActionCtrl");
        }

        return new CliPlayerReport
        {
            PlayerCount = players.Count,
            Players = players,
            Problems = problems
        };
    }

    private static string GetCurrentWeaponResourceId(WeaponActionCtrl weaponActions)
    {
        if (!weaponActions)
        {
            return string.Empty;
        }

        WeaponDefinitionSO current = weaponActions.CurrentWeaponData;
        if (current && current.Basic != null && !string.IsNullOrWhiteSpace(current.Basic.WeaponId))
        {
            return "weapon." + current.Basic.WeaponId.Trim().ToLowerInvariant() + ".data";
        }

        return weaponActions.StartingWeaponResourceId;
    }

    [CliGroupCommand(Group, "检查当前场景 Animator、Avatar、控制器和玩家动画视角", Name = "inspect_animation", Tags = new[] { "gundemo/animation" })]
    public static CliAnimationReport InspectAnimation()
    {
        Animator[] animators = Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PlayerAnimationView[] views = Object.FindObjectsByType<PlayerAnimationView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<CliAnimatorItem> items = new List<CliAnimatorItem>();
        List<string> problems = new List<string>();

        foreach (Animator animator in animators)
        {
            CliAnimatorItem item = new CliAnimatorItem
            {
                Path = GunDemoCliUtility.GetPath(animator.gameObject),
                IsActive = animator.gameObject.activeInHierarchy,
                Controller = GunDemoCliUtility.GetAssetName(animator.runtimeAnimatorController),
                Avatar = GunDemoCliUtility.GetAssetName(animator.avatar),
                ParameterCount = animator.parameters.Length
            };
            items.Add(item);

            if (!animator.runtimeAnimatorController)
            {
                problems.Add(item.Path + " 没有 Animator Controller");
            }
        }

        foreach (PlayerAnimationView view in views)
        {
            if (!view.CharacterAnimator || !view.WeaponRoot)
            {
                problems.Add(GunDemoCliUtility.GetPath(view.gameObject) + " 的角色 Animator 或武器挂点未配置");
            }
        }

        return new CliAnimationReport
        {
            AnimatorCount = animators.Length,
            ViewCount = views.Length,
            Animators = items,
            Problems = problems
        };
    }

    [CliGroupCommand(Group, "检查 Canvas、EventSystem 和交互控件的基础配置", Name = "inspect_ui", Tags = new[] { "gundemo/ui" })]
    public static CliUiReport InspectUi()
    {
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Selectable[] selectables = Object.FindObjectsByType<Selectable>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        WordNoteUI[] wordNotes = Object.FindObjectsByType<WordNoteUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        WordNoteStackRegionUI[] stackRegions = Object.FindObjectsByType<WordNoteStackRegionUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        KillFeedUI[] killFeeds = Object.FindObjectsByType<KillFeedUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        CombatScoreFeedbackUI[] scoreFeedbacks = Object.FindObjectsByType<CombatScoreFeedbackUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        CombatFeedbackCtrl[] combatFeedbackControllers = Object.FindObjectsByType<CombatFeedbackCtrl>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        CrosshairHitFeedbackUI[] crosshairFeedbacks = Object.FindObjectsByType<CrosshairHitFeedbackUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        PlayerCrosshairUI[] playerCrosshairs = Object.FindObjectsByType<PlayerCrosshairUI>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        List<string> canvasPaths = new List<string>();
        List<string> problems = new List<string>();
        int topRightRegionCount = 0;
        int playerHudCount = 0;
        int crosshairFeedbackLineCount = 0;

        foreach (Canvas canvas in canvases)
        {
            canvasPaths.Add(GunDemoCliUtility.GetPath(canvas.gameObject) + " [" + canvas.renderMode + "]");
            if (canvas.gameObject.name == "PlayerHUD")
            {
                playerHudCount++;
            }
        }

        if (canvases.Length > 0 && eventSystems.Length == 0 && selectables.Length > 0)
        {
            problems.Add("场景存在可交互 UI，但没有 EventSystem");
        }

        if (eventSystems.Length > 1)
        {
            problems.Add("场景存在多个 EventSystem");
        }

        for (int i = 0; i < stackRegions.Length; i++)
        {
            if (stackRegions[i].Region == WordNoteRegion.TopRight && stackRegions[i].IsReady)
            {
                topRightRegionCount++;
            }
        }

        if (wordNotes.Length != 1 || topRightRegionCount != 1)
        {
            problems.Add("场景需要且只能有一套 WordNote 与可用的右上角堆叠区域");
        }

        if (killFeeds.Length != 1 || scoreFeedbacks.Length != 1 || combatFeedbackControllers.Length != 1)
        {
            problems.Add("场景中的击杀播报、分数反馈或战斗反馈控制器配置不完整");
        }

        for (int i = 0; i < crosshairFeedbacks.Length; i++)
        {
            crosshairFeedbackLineCount += crosshairFeedbacks[i].FeedbackLineCount;
            if (!crosshairFeedbacks[i].IsReady)
            {
                problems.Add(GunDemoCliUtility.GetPath(crosshairFeedbacks[i].gameObject) +
                    " 的准心命中反馈引用不完整");
            }
        }

        if (playerHudCount != 1 || crosshairFeedbacks.Length != 1)
        {
            problems.Add("场景需要且只能有一套 PlayerHUD 与准心命中反馈");
        }

        if (playerCrosshairs.Length != 1 || !playerCrosshairs[0].IsReady)
        {
            problems.Add("场景需要且只能有一套已配置的玩家基础准心");
        }

        return new CliUiReport
        {
            CanvasCount = canvases.Length,
            EventSystemCount = eventSystems.Length,
            SelectableCount = selectables.Length,
            WordNoteCount = wordNotes.Length,
            TopRightRegionCount = topRightRegionCount,
            KillFeedCount = killFeeds.Length,
            ScoreFeedbackCount = scoreFeedbacks.Length,
            CombatFeedbackControllerCount = combatFeedbackControllers.Length,
            PlayerHudCount = playerHudCount,
            PlayerCrosshairCount = playerCrosshairs.Length,
            CrosshairFeedbackCount = crosshairFeedbacks.Length,
            CrosshairFeedbackLineCount = crosshairFeedbackLineCount,
            Canvases = canvasPaths,
            Problems = problems
        };
    }

    [CliGroupCommand(Group, "汇总项目 ScriptableObject 数据与枪械数据/Prefab 对应关系", Name = "inspect_data", Tags = new[] { "gundemo/data" })]
    public static CliDataReport InspectData()
    {
        string[] allData = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets" });
        string[] weaponData = AssetDatabase.FindAssets("t:WeaponDefinitionSO", new[] { "Assets/SO/Weapons" });
        string[] weaponActionSets = AssetDatabase.FindAssets(
            "t:WeaponActionSetSO",
            new[] { WeaponActionSetConfigurator.WeaponActionFolder });
        string[] playerData = AssetDatabase.FindAssets("t:PlayerDataSO", new[] { "Assets/SO" });
        string[] inputCatalogs = AssetDatabase.FindAssets("t:InputActionCatalogSO", new[] { "Assets/SO" });
        string[] weaponPrefabs = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Weapons" });
        HashSet<string> prefabNames = new HashSet<string>();
        List<string> weaponIds = new List<string>();
        List<string> unused = new List<string>();
        List<string> missingResourceIds = new List<string>();
        List<string> duplicateAddresses = new List<string>();
        List<string> problems = new List<string>();
        AddressableAssetSettings addressableSettings = AddressableAssetSettingsDefaultObject.GetSettings(false);
        HashSet<string> knownAddresses = new HashSet<string>();
        int addressableEntryCount = 0;

        if (addressableSettings)
        {
            foreach (AddressableAssetGroup group in addressableSettings.groups)
            {
                if (!group)
                {
                    continue;
                }

                foreach (AddressableAssetEntry entry in group.entries)
                {
                    addressableEntryCount++;
                    if (!knownAddresses.Add(entry.address))
                    {
                        duplicateAddresses.Add(entry.address);
                    }
                }
            }
        }
        else
        {
            problems.Add("项目缺少 Addressables Settings");
        }

        foreach (string guid in weaponPrefabs)
        {
            prefabNames.Add(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(guid)));
        }

        foreach (string guid in weaponData)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            WeaponDefinitionSO data = AssetDatabase.LoadAssetAtPath<WeaponDefinitionSO>(path);
            string id = data && data.Basic != null && !string.IsNullOrWhiteSpace(data.Basic.WeaponId)
                ? data.Basic.WeaponId
                : Path.GetFileNameWithoutExtension(path);
            weaponIds.Add(id);
            if (!prefabNames.Contains(Path.GetFileNameWithoutExtension(path)))
            {
                unused.Add(path);
            }


            SerializedObject serializedData = new SerializedObject(data);
            CheckResourceId(serializedData, "_basic._silhouetteResourceId", path, knownAddresses, missingResourceIds);
            CheckResourceId(serializedData, "_resources._firstPersonPrefabResourceId", path, knownAddresses, missingResourceIds);
            CheckResourceId(serializedData, "_animation._weaponAnimatorControllerResourceId", path, knownAddresses, missingResourceIds);
            CheckResourceId(serializedData, "_actionSetResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_basic._iconResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_resources._worldModelPrefabResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_animation._characterAnimatorControllerResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_animation._thirdPersonAnimatorControllerResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_animation._thirdPersonWeaponAnimatorControllerResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_audio._dryFireSoundResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_audio._equipSoundResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_audio._unequipSoundResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_effects._muzzleFlashResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_effects._muzzleSmokeResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_effects._shellResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_effects._tracerResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_effects._impactEffectResourceId", path, knownAddresses, missingResourceIds);
            CheckOptionalResourceId(serializedData, "_special._specialAnimationResourceId", path, knownAddresses, missingResourceIds);
            CheckResourceIdArray(serializedData, "_audio._fireSoundResourceIds", path, knownAddresses, missingResourceIds);
            CheckResourceIdArray(serializedData, "_audio._weaponEventSoundResourceIds", path, knownAddresses, missingResourceIds);
            CheckResourceIdArray(serializedData, "_audio._reloadSoundResourceIds", path, knownAddresses, missingResourceIds);
            CheckResourceIdArray(serializedData, "_audio._mechanicalSoundResourceIds", path, knownAddresses, missingResourceIds);

            string prefabPath = "Assets/Prefabs/Weapons/" + Path.GetFileNameWithoutExtension(path) + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            WeaponView weaponView = prefab ? prefab.GetComponent<WeaponView>() : null;
            if (!weaponView ||
                string.IsNullOrWhiteSpace(weaponView.WeaponDataResourceId) ||
                !knownAddresses.Contains(weaponView.WeaponDataResourceId))
            {
                missingResourceIds.Add(prefabPath + " -> WeaponView.WeaponDataResourceId");
            }

            string actionPath = WeaponActionSetConfigurator.GetActionSetPath(data);
            WeaponActionSetSO actionSet = AssetDatabase.LoadAssetAtPath<WeaponActionSetSO>(actionPath);
            if (!actionSet)
            {
                problems.Add(path + " 缺少枪械动作集");
            }
            else
            {
                ValidateWeaponActionSet(actionSet, actionPath, problems);
            }
        }

        if (weaponData.Length != weaponPrefabs.Length)
        {
            problems.Add("枪械数据数量与正式枪械 Prefab 数量不同");
        }

        if (weaponData.Length != weaponActionSets.Length)
        {
            problems.Add("枪械数据数量与枪械动作集数量不同");
        }

        if (missingResourceIds.Count > 0)
        {
            problems.Add("枪械数据存在空资源 ID 或未登记 Addressables 的资源 ID");
        }

        if (duplicateAddresses.Count > 0)
        {
            problems.Add("Addressables 存在重复 Address");
        }

        return new CliDataReport
        {
            ScriptableObjectCount = allData.Length,
            WeaponDataCount = weaponData.Length,
            WeaponActionSetCount = weaponActionSets.Length,
            WeaponPrefabCount = weaponPrefabs.Length,
            PlayerDataCount = playerData.Length,
            InputCatalogCount = inputCatalogs.Length,
            HasAddressableSettings = addressableSettings,
            AddressableEntryCount = addressableEntryCount,
            WeaponIds = weaponIds,
            UnusedWeaponData = unused,
            MissingResourceIds = missingResourceIds,
            DuplicateAddresses = duplicateAddresses,
            Problems = problems
        };
    }

    private static void ValidateWeaponActionSet(
        WeaponActionSetSO actionSet,
        string assetPath,
        List<string> problems)
    {
        if (actionSet.DataVersion < WeaponActionSetConfigurator.CurrentDataVersion)
        {
            problems.Add(assetPath + " 的动作数据版本过旧");
        }

        ValidateActionMarker(
            actionSet.Fire,
            WeaponActionMarkerType.FireShot,
            assetPath,
            problems);
        ValidateActionMarker(
            actionSet.Reload,
            WeaponActionMarkerType.AmmoCommit,
            assetPath,
            problems);
        ValidateActionMarker(
            actionSet.EmptyReload,
            WeaponActionMarkerType.AmmoCommit,
            assetPath,
            problems);
        ValidateActionMarker(
            actionSet.PerRoundInsert,
            WeaponActionMarkerType.InsertRound,
            assetPath,
            problems);
    }

    private static void ValidateActionMarker(
        WeaponActionDefinition action,
        WeaponActionMarkerType requiredMarker,
        string assetPath,
        List<string> problems)
    {
        if (action == null || !action.Enabled)
        {
            return;
        }

        for (int i = 0; i < action.Markers.Count; i++)
        {
            if (action.Markers[i].MarkerType == requiredMarker)
            {
                return;
            }
        }

        problems.Add(assetPath + " 的" + action.ActionType + "缺少" + requiredMarker + "节点");
    }

    [CliGroupCommand(Group, "检查正式 Prefab 的丢失脚本和丢失材质", Name = "inspect_prefabs", Tags = new[] { "gundemo/prefabs" })]
    public static CliPrefabReport InspectPrefabs()
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        int missingScripts = 0;
        int missingMaterials = 0;
        List<string> problems = new List<string>();

        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
                foreach (Transform transform in transforms)
                {
                    int missing = GunDemoCliUtility.CountMissingScripts(transform.gameObject);
                    if (missing > 0)
                    {
                        missingScripts += missing;
                        problems.Add(path + ": " + GunDemoCliUtility.GetPath(transform.gameObject) + " 丢失脚本");
                    }
                }

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer renderer in renderers)
                {
                    Material[] materials = renderer.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++)
                    {
                        if (!materials[i])
                        {
                            missingMaterials++;
                            problems.Add(path + ": " + GunDemoCliUtility.GetPath(renderer.gameObject) + " 丢失材质");
                        }
                    }
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return new CliPrefabReport
        {
            PrefabCount = prefabGuids.Length,
            MissingScriptCount = missingScripts,
            MissingMaterialCount = missingMaterials,
            Problems = problems
        };
    }

    [CliGroupCommand(Group, "检查正式资源的缺失依赖和 source_unselected 候选资源引用", Name = "inspect_assets", Tags = new[] { "gundemo/assets" })]
    public static CliAssetReport InspectAssets()
    {
        string[] assetPaths = AssetDatabase.GetAllAssetPaths();
        List<string> missing = new List<string>();
        List<string> candidates = new List<string>();
        int assetCount = 0;

        foreach (string path in assetPaths)
        {
            if (!path.StartsWith("Assets/") || path.StartsWith("Assets/source_unselected/"))
            {
                continue;
            }

            assetCount++;
            string[] dependencies = AssetDatabase.GetDependencies(path, false);
            foreach (string dependency in dependencies)
            {
                if (dependency.StartsWith("Assets/source_unselected/"))
                {
                    candidates.Add(path + " -> " + dependency);
                }

                if (dependency.StartsWith("Assets/") && !File.Exists(dependency) && !Directory.Exists(dependency))
                {
                    missing.Add(path + " -> " + dependency);
                }
            }
        }

        return new CliAssetReport
        {
            AssetCount = assetCount,
            MissingReferenceCount = missing.Count,
            CandidateReferenceCount = candidates.Count,
            MissingReferences = missing,
            CandidateReferences = candidates
        };
    }

    private static CliCheckResult CreateCheck(string name, bool passed, string detail)
    {
        return new CliCheckResult
        {
            Name = name,
            Passed = passed,
            Detail = detail
        };
    }

    private static void CheckResourceId(
        SerializedObject owner,
        string propertyPath,
        string assetPath,
        HashSet<string> knownAddresses,
        List<string> missingResourceIds)
    {
        SerializedProperty property = owner.FindProperty(propertyPath);
        string resourceId = property != null ? property.stringValue : string.Empty;
        if (string.IsNullOrWhiteSpace(resourceId) || !knownAddresses.Contains(resourceId))
        {
            missingResourceIds.Add(assetPath + " -> " + propertyPath + " = " + resourceId);
        }
    }

    private static void CheckOptionalResourceId(
        SerializedObject owner,
        string propertyPath,
        string assetPath,
        HashSet<string> knownAddresses,
        List<string> missingResourceIds)
    {
        SerializedProperty property = owner.FindProperty(propertyPath);
        if (property == null || string.IsNullOrWhiteSpace(property.stringValue))
        {
            return;
        }

        if (!knownAddresses.Contains(property.stringValue))
        {
            missingResourceIds.Add(assetPath + " -> " + propertyPath + " = " + property.stringValue);
        }
    }

    private static void CheckResourceIdArray(
        SerializedObject owner,
        string propertyPath,
        string assetPath,
        HashSet<string> knownAddresses,
        List<string> missingResourceIds)
    {
        SerializedProperty property = owner.FindProperty(propertyPath);
        if (property == null || !property.isArray)
        {
            return;
        }

        for (int i = 0; i < property.arraySize; i++)
        {
            string resourceId = property.GetArrayElementAtIndex(i).stringValue;
            if (!string.IsNullOrWhiteSpace(resourceId) && !knownAddresses.Contains(resourceId))
            {
                missingResourceIds.Add(assetPath + " -> " + propertyPath + "[" + i + "] = " + resourceId);
            }
        }
    }

    private static void GetConsoleCounts(out int warnings, out int errors)
    {
        warnings = 0;
        errors = 0;
        System.Type logEntries = System.Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
        if (logEntries == null)
        {
            return;
        }

        System.Reflection.MethodInfo method = logEntries.GetMethod("GetCountsByType", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
        if (method == null)
        {
            return;
        }

        object[] values = { 0, 0, 0 };
        method.Invoke(null, values);
        warnings = (int)values[1];
        errors = (int)values[2];
    }
}
