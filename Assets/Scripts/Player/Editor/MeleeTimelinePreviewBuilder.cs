#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

public static class MeleeTimelinePreviewBuilder
{
    private const int PreviewBuildVersion = 4;
    private const string RootFolder = "Assets/Timelines/MeleePreview";
    private const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
    private const string BladePrefabPath = "Assets/Prefabs/Weapons/Melee/AnbyBlackStick.prefab";
    private const string FirstPersonBladePrefabPath =
        "Assets/Prefabs/Weapons/Melee/AnbyMeleeBlade.prefab";
    private const string BladeMaterialPath =
        "Assets/Resource/Weapons/Melee/AnbyBlade/Materials/AnbyBlackStick.mat";
    private const string HitEffectPrefabPath = "Assets/Prefabs/Effects/Melee/MeleeHitBurst.prefab";
    private const string ThirdPersonClipPath =
        "Assets/Resource/Animations/ThirdPerson/Melee/Anby_Attack_Normal_02.fbx";

    // RPG ATK2 在 0.1 秒激活的真实对象：安比武器_L_2，直接挂在左手骨下。
    private static readonly Vector3 BladePosition = new Vector3(-0.0803f, -0.158f, 0.09f);
    private static readonly Quaternion BladeRotation =
        new Quaternion(0.009287184f, 0.70543915f, -0.31225657f, 0.63621175f);
    private static readonly Vector3 BladeScale = new Vector3(1f, 0.6f, 0.1f);
    private static readonly Vector3 FirstPersonStartPosition =
        new Vector3(-0.46f, -0.34f, 0.78f);
    private static readonly Vector3 FirstPersonStartEuler =
        new Vector3(18f, -38f, 72f);
    private static readonly Vector3 FirstPersonEndPosition =
        new Vector3(0.42f, -0.02f, 0.64f);
    private static readonly Vector3 FirstPersonEndEuler =
        new Vector3(-22f, 42f, -58f);
    private static readonly Vector3 FirstPersonBladeScale = Vector3.one * 0.5f;

    [MenuItem("GunDemo/近战/生成或重建第一与第三人称 Timeline 预览")]
    public static void RebuildAll()
    {
        if (!EditorUtility.DisplayDialog(
            "重建近战 Timeline 预览",
            "这会覆盖两个预览场景及 Timeline。只应在尚未手动调整，或确认不要旧调整时执行。",
            "重建", "取消"))
        {
            return;
        }
        EnsureFolder();
        EnsureBlackStickPrefab();
        BuildPreview(true, true);
        BuildPreview(false, true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("GunDemo/近战/立即补建第一人称 Timeline 预览 %#m")]
    public static void BuildFirstPersonNow()
    {
        try
        {
            EnsureFolder();
            EnsureBlackStickPrefab();
            BuildPreview(true, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MeleePreview] 第一人称预览补建命令执行完毕。");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    [MenuItem("GunDemo/近战/返回正常游戏场景 %#g")]
    public static void ReturnToGameScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/example.unity", OpenSceneMode.Single);
    }

    private static void BuildMissingPreviews()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
        {
            return;
        }
        try
        {
            EnsureFolder();
            EnsureBlackStickPrefab();
            string versionKey = "GunDemo.MeleeTimelinePreview.Version." + Application.dataPath;
            bool needsUpgrade = EditorPrefs.GetInt(versionKey, 0) < PreviewBuildVersion;
            // 缺失的单个预览始终补建；版本升级时才覆盖已有预览。
            BuildPreview(true, needsUpgrade);
            BuildPreview(false, needsUpgrade);
            if (needsUpgrade) EditorPrefs.SetInt(versionKey, PreviewBuildVersion);
            AssetDatabase.SaveAssets();
            Debug.Log("[MeleePreview] 自动检查预览完成。");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private static void BuildPreview(bool firstPerson, bool overwrite)
    {
        if (firstPerson)
        {
            BuildFirstPersonPreview(overwrite);
            return;
        }

        BuildThirdPersonPreview(overwrite);
    }

    private static void BuildFirstPersonPreview(bool overwrite)
    {
        const string prefix = "FP";
        string scenePath = $"{RootFolder}/{prefix}_MeleePreview.unity";
        string timelinePath = $"{RootFolder}/{prefix}_MeleePreview.playable";
        if (!overwrite && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
        {
            return;
        }

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        GameObject bladePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            FirstPersonBladePrefabPath);
        GameObject effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitEffectPrefabPath);
        if (!playerPrefab || !bladePrefab || !effectPrefab)
        {
            Debug.LogError("无法生成第一人称近战校准：玩家、刀或命中特效资源缺失。");
            return;
        }

        DeletePreviewAssets(scenePath, timelinePath, overwrite);
        Scene previous = PreparePreviewScene(
            scenePath,
            prefix,
            out Scene scene,
            out bool reopenGeneratedScene);
        GameObject root = new GameObject("FP_MELEE_ARC_CALIBRATION");
        GameObject player = CreatePreviewPlayer(playerPrefab, scene, true, root.transform);
        PlayerAnimationView selected = FindSelectedView(player, true);
        if (!selected)
        {
            ClosePreviewScene(scene, previous, scenePath, reopenGeneratedScene);
            Debug.LogError("第一人称近战校准找不到第一人称表现对象。");
            return;
        }

        DisableGameplay(player);
        HideAllWeaponModels(player, selected);
        HideViewRenderers(selected);
        Camera camera = player.GetComponentInChildren<Camera>(true);
        Transform calibrationRoot = new GameObject("第一人称刀姿态_只调整下面两个节点").transform;
        calibrationRoot.SetParent(camera ? camera.transform : root.transform, false);

        GameObject startBlade = CreateCalibrationBlade(
            bladePrefab,
            scene,
            calibrationRoot,
            "01_挥砍起点刀_调整位置旋转大小",
            FirstPersonStartPosition,
            FirstPersonStartEuler,
            FirstPersonBladeScale);
        GameObject endBlade = CreateCalibrationBlade(
            bladePrefab,
            scene,
            calibrationRoot,
            "02_挥砍终点刀_调整位置旋转大小",
            FirstPersonEndPosition,
            FirstPersonEndEuler,
            FirstPersonBladeScale);
        GameObject previewBlade = CreateCalibrationBlade(
            bladePrefab,
            scene,
            calibrationRoot,
            "03_弧线运动预览刀_不要手调",
            FirstPersonStartPosition,
            FirstPersonStartEuler,
            FirstPersonBladeScale);

        GameObject effectAdjust = CreateHitEffect(effectPrefab, scene, calibrationRoot);
        TimelineAsset timeline = CreateTimeline(timelinePath, "FP_MeleePreview");
        const double duration = 1.0333333d;

        ActivationTrack startTrack = timeline.CreateTrack<ActivationTrack>(
            null,
            "起点刀_全程参考");
        startTrack.CreateDefaultClip().duration = duration;
        ActivationTrack endTrack = timeline.CreateTrack<ActivationTrack>(
            null,
            "终点刀_全程参考");
        endTrack.CreateDefaultClip().duration = duration;
        AnimationTrack arcTrack = timeline.CreateTrack<AnimationTrack>(
            null,
            "运动预览刀_自动弧线");
        AnimationClip arcClip = CreateFirstPersonArcClip(timeline);
        TimelineClip arcTimelineClip = arcTrack.CreateClip(arcClip);
        arcTimelineClip.displayName = "蓄力→爆发→过冲→回收";
        arcTimelineClip.duration = duration;
        ActivationTrack effectTrack = timeline.CreateTrack<ActivationTrack>(
            null,
            "伤害API响应点_拖动此片段");
        TimelineClip effectClip = effectTrack.CreateDefaultClip();
        effectClip.start = 0.34d;
        effectClip.duration = 0.04d;
        effectClip.displayName = "调用 ResolveImpactNow";

        PlayableDirector director = root.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.playOnAwake = false;
        director.timeUpdateMode = DirectorUpdateMode.GameTime;
        director.SetGenericBinding(startTrack, startBlade);
        director.SetGenericBinding(endTrack, endBlade);
        director.SetGenericBinding(arcTrack, previewBlade);
        director.SetGenericBinding(effectTrack, effectAdjust);

        SaveAndClosePreview(scene, previous, scenePath, reopenGeneratedScene);
        Debug.Log($"已生成第一人称无手臂弧线校准场景：{scenePath}");
    }

    private static void BuildThirdPersonPreview(bool overwrite)
    {
        const bool firstPerson = false;
        string prefix = firstPerson ? "FP" : "TP";
        string scenePath = $"{RootFolder}/{prefix}_MeleePreview.unity";
        string timelinePath = $"{RootFolder}/{prefix}_MeleePreview.playable";
        if (!overwrite && AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath))
        {
            return;
        }

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        GameObject bladePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BladePrefabPath);
        GameObject effectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(HitEffectPrefabPath);
        AnimationClip clip = AssetDatabase.LoadAllAssetsAtPath(ThirdPersonClipPath)
            .OfType<AnimationClip>()
            .FirstOrDefault(item => !item.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (!playerPrefab || !bladePrefab || !effectPrefab || !clip)
        {
            Debug.LogError("无法生成近战 Timeline 预览：玩家、刀、特效或动画资源缺失。");
            return;
        }

        DeletePreviewAssets(scenePath, timelinePath, overwrite);

        Scene previous = SceneManager.GetActiveScene();
        bool reopenGeneratedScene = previous.path == scenePath;
        if (reopenGeneratedScene)
        {
            previous = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        scene.name = prefix + "_MeleePreview";
        SceneManager.SetActiveScene(scene);

        GameObject root = new GameObject(prefix + "_MELEE_TIMELINE_PREVIEW");
        GameObject player = CreatePreviewPlayer(playerPrefab, scene, false, root.transform);
        PlayerAnimationView selected = FindSelectedView(player, false);
        // 预览场景只由 Timeline 驱动。禁用玩家玩法脚本，避免它们在 Play Mode
        // 覆盖双手骨骼、重新生成枪械，或对未运行 Controller 的 Animator 持续写参数。
        DisableGameplay(player);
        Animator animator = selected ? selected.CharacterAnimator : null;
        if (!animator)
        {
            Debug.LogError($"{prefix} 近战预览找不到 Animator。");
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(previous);
            return;
        }
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        HideAllWeaponModels(player, selected);

        Transform hand = FindHand(animator, !firstPerson, false);
        GameObject adjust = new GameObject("棍子绑定调整点_调整位置旋转大小");
        adjust.transform.SetParent(hand, false);
        adjust.transform.localPosition = BladePosition;
        adjust.transform.localRotation = BladeRotation;
        adjust.transform.localScale = BladeScale;
        GameObject blade = (GameObject)PrefabUtility.InstantiatePrefab(bladePrefab, scene);
        blade.name = "第三人称棍子_资源实例_不要单独调整";
        blade.transform.SetParent(adjust.transform, false);

        GameObject effectAdjust = new GameObject("命中特效位置_可选调整");
        effectAdjust.transform.SetParent(root.transform, false);
        effectAdjust.transform.localPosition = new Vector3(0f, 1.25f, 1.1f);
        effectAdjust.transform.localScale = Vector3.one * 0.08f;
        GameObject effect = (GameObject)PrefabUtility.InstantiatePrefab(effectPrefab, scene);
        effect.name = "命中特效_资源实例";
        effect.transform.SetParent(effectAdjust.transform, false);

        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = prefix + "_MeleePreview";
        AssetDatabase.CreateAsset(timeline, timelinePath);
        AnimationTrack animationTrack = timeline.CreateTrack<AnimationTrack>(null,
            "第三人称身体动作_0.5倍速");
        TimelineClip animationTimelineClip = animationTrack.CreateClip(clip);
        animationTimelineClip.displayName = "安比第二段普攻（0.5x）";
        animationTimelineClip.timeScale = 0.5d;
        animationTimelineClip.duration = Math.Max(0.01d, clip.length * 2d);
        ActivationTrack bladeTrack = timeline.CreateTrack<ActivationTrack>(null, "黑色刀_显示区间");
        bladeTrack.CreateDefaultClip().duration = animationTimelineClip.duration;
        ActivationTrack effectTrack = timeline.CreateTrack<ActivationTrack>(null, "命中特效_可视化");
        TimelineClip effectClip = effectTrack.CreateDefaultClip();
        effectClip.start = 0.3666d;
        effectClip.duration = 0.25d;

        PlayableDirector director = root.AddComponent<PlayableDirector>();
        director.playableAsset = timeline;
        director.playOnAwake = false;
        director.timeUpdateMode = DirectorUpdateMode.GameTime;
        director.SetGenericBinding(animationTrack, animator);
        director.SetGenericBinding(bladeTrack, adjust);
        director.SetGenericBinding(effectTrack, effectAdjust);

        EditorSceneManager.SaveScene(scene, scenePath);
        EditorSceneManager.CloseScene(scene, true);
        if (reopenGeneratedScene)
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        else if (previous.IsValid())
            SceneManager.SetActiveScene(previous);
        Debug.Log($"已生成 {prefix} 近战可视化场景：{scenePath}");
    }

    private static void DeletePreviewAssets(
        string scenePath,
        string timelinePath,
        bool overwrite)
    {
        if (!overwrite)
        {
            return;
        }

        AssetDatabase.DeleteAsset(scenePath);
        AssetDatabase.DeleteAsset(timelinePath);
    }

    private static Scene PreparePreviewScene(
        string scenePath,
        string prefix,
        out Scene scene,
        out bool reopenGeneratedScene)
    {
        Scene previous = SceneManager.GetActiveScene();
        reopenGeneratedScene = previous.path == scenePath;
        if (reopenGeneratedScene)
        {
            previous = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
        }

        scene = EditorSceneManager.NewScene(
            NewSceneSetup.EmptyScene,
            NewSceneMode.Additive);
        scene.name = prefix + "_MeleePreview";
        SceneManager.SetActiveScene(scene);
        return previous;
    }

    private static GameObject CreatePreviewPlayer(
        GameObject playerPrefab,
        Scene scene,
        bool firstPerson,
        Transform parent)
    {
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.name = firstPerson ? "第一人称相机参考_手和枪已隐藏" : "第三人称角色参考";
        player.transform.SetParent(parent, false);
        PlayerAnimationView[] views = player.GetComponentsInChildren<PlayerAnimationView>(true);
        PlayerAnimationView selected = views.FirstOrDefault(view =>
            firstPerson
                ? view.ViewType == PlayerViewType.FirstPerson
                : view.ViewType == PlayerViewType.ThirdPerson);
        foreach (PlayerAnimationView view in views)
        {
            view.gameObject.SetActive(view == selected);
        }
        return player;
    }

    private static PlayerAnimationView FindSelectedView(
        GameObject player,
        bool firstPerson)
    {
        return player.GetComponentsInChildren<PlayerAnimationView>(true)
            .FirstOrDefault(view => firstPerson
                ? view.ViewType == PlayerViewType.FirstPerson
                : view.ViewType == PlayerViewType.ThirdPerson);
    }

    private static void DisableGameplay(GameObject player)
    {
        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
        }
    }

    private static void HideViewRenderers(PlayerAnimationView view)
    {
        Animator animator = view ? view.CharacterAnimator : null;
        if (!animator)
        {
            return;
        }

        Renderer[] renderers = animator.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].enabled = false;
        }
    }

    private static GameObject CreateCalibrationBlade(
        GameObject bladePrefab,
        Scene scene,
        Transform parent,
        string name,
        Vector3 position,
        Vector3 euler,
        Vector3 scale)
    {
        GameObject blade = (GameObject)PrefabUtility.InstantiatePrefab(bladePrefab, scene);
        blade.name = name;
        blade.transform.SetParent(parent, false);
        blade.transform.localPosition = position;
        blade.transform.localEulerAngles = euler;
        blade.transform.localScale = scale;
        return blade;
    }

    private static GameObject CreateHitEffect(
        GameObject effectPrefab,
        Scene scene,
        Transform parent)
    {
        GameObject effectAdjust = new GameObject("伤害响应点_移动Timeline片段改时间");
        effectAdjust.transform.SetParent(parent, false);
        effectAdjust.transform.localPosition = new Vector3(0f, 0f, 1.1f);
        effectAdjust.transform.localScale = Vector3.one * 0.08f;
        GameObject effect = (GameObject)PrefabUtility.InstantiatePrefab(effectPrefab, scene);
        effect.name = "命中特效_仅作为响应点参考";
        effect.transform.SetParent(effectAdjust.transform, false);
        return effectAdjust;
    }

    private static TimelineAsset CreateTimeline(string timelinePath, string name)
    {
        TimelineAsset timeline = ScriptableObject.CreateInstance<TimelineAsset>();
        timeline.name = name;
        AssetDatabase.CreateAsset(timeline, timelinePath);
        return timeline;
    }

    private static AnimationClip CreateFirstPersonArcClip(TimelineAsset timeline)
    {
        AnimationClip clip = new AnimationClip
        {
            name = "FP_MeleeArc_DefaultPreview"
        };
        clip.frameRate = 60f;
        Vector3 middlePosition = Vector3.Lerp(
            FirstPersonStartPosition,
            FirstPersonEndPosition,
            0.5f) + new Vector3(0f, 0.28f, 0.18f);
        Vector3 middleEuler = Vector3.Lerp(
            FirstPersonStartEuler,
            FirstPersonEndEuler,
            0.5f) + new Vector3(-15f, 0f, -18f);
        Vector3 overshootPosition = FirstPersonEndPosition + new Vector3(0.05f, -0.025f, 0.04f);
        Vector3 overshootEuler = FirstPersonEndEuler + new Vector3(-5f, 7f, -8f);

        SetVector3Curves(
            clip,
            "m_LocalPosition",
            FirstPersonStartPosition,
            middlePosition,
            FirstPersonEndPosition,
            overshootPosition);
        SetVector3Curves(
            clip,
            "localEulerAnglesRaw",
            FirstPersonStartEuler,
            middleEuler,
            FirstPersonEndEuler,
            overshootEuler);
        AssetDatabase.AddObjectToAsset(clip, timeline);
        return clip;
    }

    private static void SetVector3Curves(
        AnimationClip clip,
        string propertyPrefix,
        Vector3 start,
        Vector3 middle,
        Vector3 end,
        Vector3 overshoot)
    {
        SetCurve(clip, propertyPrefix + ".x", start.x, middle.x, end.x, overshoot.x);
        SetCurve(clip, propertyPrefix + ".y", start.y, middle.y, end.y, overshoot.y);
        SetCurve(clip, propertyPrefix + ".z", start.z, middle.z, end.z, overshoot.z);
    }

    private static void SetCurve(
        AnimationClip clip,
        string propertyName,
        float start,
        float middle,
        float end,
        float overshoot)
    {
        AnimationCurve curve = new AnimationCurve(
            new Keyframe(0f, start),
            new Keyframe(0.08f, start),
            new Keyframe(0.31f, middle),
            new Keyframe(0.49f, end),
            new Keyframe(0.54f, overshoot),
            new Keyframe(0.76f, end));
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(
                curve,
                i,
                AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(
                curve,
                i,
                AnimationUtility.TangentMode.ClampedAuto);
        }
        clip.SetCurve(string.Empty, typeof(Transform), propertyName, curve);
    }

    private static void SaveAndClosePreview(
        Scene scene,
        Scene previous,
        string scenePath,
        bool reopenGeneratedScene)
    {
        EditorSceneManager.SaveScene(scene, scenePath);
        ClosePreviewScene(scene, previous, scenePath, reopenGeneratedScene);
    }

    private static void ClosePreviewScene(
        Scene scene,
        Scene previous,
        string scenePath,
        bool reopenGeneratedScene)
    {
        EditorSceneManager.CloseScene(scene, true);
        if (reopenGeneratedScene)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
        }
        else if (previous.IsValid())
        {
            SceneManager.SetActiveScene(previous);
        }
    }

    private static Transform FindHand(Animator animator, bool humanoid, bool right)
    {
        if (humanoid && animator.isHuman)
        {
            Transform bone = animator.GetBoneTransform(
                right ? HumanBodyBones.RightHand : HumanBodyBones.LeftHand);
            if (bone) return bone;
        }
        return animator.GetComponentsInChildren<Transform>(true)
            .First(child => child.name == (right ? "hand_r" : "hand_l"));
    }

    private static void HideAllWeaponModels(GameObject player, PlayerAnimationView selected)
    {
        if (selected && selected.WeaponRoot)
        {
            for (int i = 0; i < selected.WeaponRoot.childCount; i++)
            {
                selected.WeaponRoot.GetChild(i).gameObject.SetActive(false);
            }
        }
        foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
            if (child.name == "AK(Clone)") child.gameObject.SetActive(false);
    }

    private static void EnsureBlackStickPrefab()
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(BladePrefabPath)) return;
        Material material = AssetDatabase.LoadAssetAtPath<Material>(BladeMaterialPath);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.name = "AnbyBlackStick";
            material.color = new Color(0.012f, 0.015f, 0.02f, 1f);
            material.SetFloat("_Metallic", 0.55f);
            material.SetFloat("_Smoothness", 0.28f);
            AssetDatabase.CreateAsset(material, BladeMaterialPath);
        }

        GameObject root = new GameObject("AnbyBlackStick");
        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "BlackStickBody";
        body.transform.SetParent(root.transform, false);
        body.transform.localScale = new Vector3(0.045f, 0.62f, 0.045f);
        UnityEngine.Object.DestroyImmediate(body.GetComponent<Collider>());
        body.GetComponent<MeshRenderer>().sharedMaterial = material;
        PrefabUtility.SaveAsPrefabAsset(root, BladePrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
    }

    private static void EnsureFolder()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Timelines"))
            AssetDatabase.CreateFolder("Assets", "Timelines");
        if (!AssetDatabase.IsValidFolder(RootFolder))
            AssetDatabase.CreateFolder("Assets/Timelines", "MeleePreview");
    }
}
#endif
