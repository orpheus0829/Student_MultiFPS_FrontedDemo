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
    private const int PreviewBuildVersion = 3;
    private const string RootFolder = "Assets/Timelines/MeleePreview";
    private const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
    private const string BladePrefabPath = "Assets/Prefabs/Weapons/Melee/AnbyBlackStick.prefab";
    private const string BladeMaterialPath =
        "Assets/Resource/Weapons/Melee/AnbyBlade/Materials/AnbyBlackStick.mat";
    private const string FirstPersonControllerPath =
        "Assets/Resource/Animations/FirstPerson/PlayerFirstPerson.controller";
    private const string HitEffectPrefabPath = "Assets/Prefabs/Effects/Melee/MeleeHitBurst.prefab";
    private const string FirstPersonClipPath =
        "Assets/Resource/Animations/FirstPerson/Melee/FP_Anby_Attack_Normal_02.anim";
    private const string ThirdPersonClipPath =
        "Assets/Resource/Animations/ThirdPerson/Melee/Anby_Attack_Normal_02.fbx";

    // RPG ATK2 在 0.1 秒激活的真实对象：安比武器_L_2，直接挂在左手骨下。
    private static readonly Vector3 BladePosition = new Vector3(-0.0803f, -0.158f, 0.09f);
    private static readonly Quaternion BladeRotation =
        new Quaternion(0.009287184f, 0.70543915f, -0.31225657f, 0.63621175f);
    private static readonly Vector3 BladeScale = new Vector3(1f, 0.6f, 0.1f);

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
        AnimationClip clip = firstPerson
            ? AssetDatabase.LoadAssetAtPath<AnimationClip>(FirstPersonClipPath)
            : AssetDatabase.LoadAllAssetsAtPath(ThirdPersonClipPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(item => !item.name.StartsWith("__preview__", StringComparison.Ordinal));
        if (!playerPrefab || !bladePrefab || !effectPrefab || !clip)
        {
            Debug.LogError("无法生成近战 Timeline 预览：玩家、刀、特效或动画资源缺失。");
            return;
        }

        if (overwrite)
        {
            AssetDatabase.DeleteAsset(scenePath);
            AssetDatabase.DeleteAsset(timelinePath);
        }

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
        GameObject player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab, scene);
        player.name = firstPerson ? "第一人称玩家预览" : "第三人称玩家预览";
        player.transform.SetParent(root.transform, false);

        PlayerAnimationView[] views = player.GetComponentsInChildren<PlayerAnimationView>(true);
        PlayerAnimationView selected = views.FirstOrDefault(view =>
            firstPerson ? view.ViewType == PlayerViewType.FirstPerson : view.ViewType == PlayerViewType.ThirdPerson);
        foreach (PlayerAnimationView view in views)
        {
            view.gameObject.SetActive(view == selected);
        }
        // 预览场景只由 Timeline 驱动。禁用玩家玩法脚本，避免它们在 Play Mode
        // 覆盖双手骨骼、重新生成枪械，或对未运行 Controller 的 Animator 持续写参数。
        foreach (MonoBehaviour behaviour in player.GetComponentsInChildren<MonoBehaviour>(true))
        {
            behaviour.enabled = false;
        }
        Animator animator = selected ? selected.CharacterAnimator : null;
        if (!animator)
        {
            Debug.LogError($"{prefix} 近战预览找不到 Animator。");
            EditorSceneManager.CloseScene(scene, true);
            SceneManager.SetActiveScene(previous);
            return;
        }
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        if (firstPerson)
        {
            animator.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(FirstPersonControllerPath);
        }
        HideAllWeaponModels(player, selected);

        Transform hand = FindHand(animator, !firstPerson, false);
        GameObject adjust = new GameObject("BLACK_BLADE_ADJUST_HERE");
        adjust.transform.SetParent(hand, false);
        adjust.transform.localPosition = BladePosition;
        adjust.transform.localRotation = BladeRotation;
        adjust.transform.localScale = BladeScale;
        GameObject blade = (GameObject)PrefabUtility.InstantiatePrefab(bladePrefab, scene);
        blade.name = "黑色棍状刀_资源实例";
        blade.transform.SetParent(adjust.transform, false);

        GameObject effectAdjust = new GameObject("HIT_EFFECT_ADJUST_HERE");
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
            firstPerson ? "第一人称双手_0.5倍速" : "第三人称上半身_0.5倍速");
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
