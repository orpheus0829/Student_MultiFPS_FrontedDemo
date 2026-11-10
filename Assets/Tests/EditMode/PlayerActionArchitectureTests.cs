using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public sealed class PlayerActionArchitectureTests
{
    private const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
    private const string PlayerHudPrefabPath = "Assets/Prefabs/UI/PlayerHUD/PlayerHUD.prefab";
    private const string UpperBodyMaskPath =
        "Assets/Resource/Animations/ThirdPerson/UpperBodyWeapon.mask";
    private const string ThirdPersonControllerPath =
        "Assets/Resource/Animations/ThirdPerson/PlayerThirdPerson.controller";
    private const string SlideAnimationPath =
        "Assets/Resource/Animations/ThirdPerson/Slide/ParkourHookSlide.fbx";

    [Test]
    public void ThirdPersonWeaponMaskKeepsLegsOnLocomotionLayer()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(UpperBodyMaskPath);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            ThirdPersonControllerPath);
        Assert.That(mask, Is.Not.Null);
        Assert.That(controller, Is.Not.Null);
        Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Root), Is.False);
        Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False);
        Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightLeg), Is.False);
        Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.Body), Is.True);
        Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm), Is.True);
        Assert.That(mask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), Is.True);

        Assert.That(controller.layers.Length, Is.EqualTo(2));
        Assert.That(controller.layers[1].name, Is.EqualTo("UpperBodyWeapon"));
        Assert.That(controller.layers[1].avatarMask, Is.EqualTo(mask));
        Assert.That(controller.layers[1].blendingMode,
            Is.EqualTo(AnimatorLayerBlendingMode.Override));

        string[] weaponStates =
        {
            "Fire", "Reload_Tac", "Reload_Empty", "Equip_Override", "Holster", "Inspect"
        };
        AnimatorStateMachine weaponLayer = controller.layers[1].stateMachine;
        for (int i = 0; i < weaponStates.Length; i++)
        {
            Assert.That(HasState(weaponLayer, weaponStates[i]), Is.True,
                "上半身层缺少武器动作 " + weaponStates[i]);
        }

        AnimatorStateMachine locomotionLayer = controller.layers[0].stateMachine;
        Assert.That(HasState(locomotionLayer, "Jump"), Is.True);
        Assert.That(HasState(locomotionLayer, "Slide"), Is.True);
        Assert.That(HasState(locomotionLayer, "Vault"), Is.True);
        AnimatorStateTransition[] anyStateTransitions = locomotionLayer.anyStateTransitions;
        bool jumpExcludesOtherFullBodyActions = false;
        for (int i = 0; i < anyStateTransitions.Length; i++)
        {
            AnimatorStateTransition transition = anyStateTransitions[i];
            if (!transition.destinationState || transition.destinationState.name != "Jump")
            {
                continue;
            }

            jumpExcludesOtherFullBodyActions =
                HasCondition(transition, "Vaulting", AnimatorConditionMode.IfNot) &&
                HasCondition(transition, "Sliding", AnimatorConditionMode.IfNot);
        }

        Assert.That(jumpExcludesOtherFullBodyActions, Is.True,
            "腾空跳跃不能盖掉正在播放的翻越或滑铲");
    }

    [Test]
    public void ThirdPersonSlideUsesGroundedHumanoidAnimationWithoutRootMotion()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            ThirdPersonControllerPath);
        Assert.That(controller, Is.Not.Null);

        AnimationClip slide = null;
        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        for (int index = 0; index < states.Length; index++)
        {
            if (states[index].state && states[index].state.name == "Slide")
            {
                slide = states[index].state.motion as AnimationClip;
                Assert.That(states[index].state.speed, Is.EqualTo(2.166667f).Within(0.01f));
                break;
            }
        }

        Assert.That(slide, Is.Not.Null);
        Assert.That(AssetDatabase.GetAssetPath(slide), Is.EqualTo(SlideAnimationPath));
        Assert.That(slide.isHumanMotion, Is.True);

        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(prefab, Is.Not.Null);
        MonoBehaviour[] components = prefab.GetComponentsInChildren<MonoBehaviour>(true);
        for (int index = 0; index < components.Length; index++)
        {
            MonoBehaviour component = components[index];
            if (!component || component.GetType().Name != "PlayerAnimationView")
            {
                continue;
            }

            SerializedObject view = new SerializedObject(component);
            if (view.FindProperty("_viewType").enumValueIndex != 1)
            {
                continue;
            }

            Animator animator = view.FindProperty("_characterAnimator").objectReferenceValue
                as Animator;
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False,
                "角色移动由 PlayerMoveCtrl 执行，动画不得重复驱动根节点");
            return;
        }

        Assert.Fail("玩家 Prefab 缺少第三人称动画表现组件");
    }

    [Test]
    public void ThirdPersonLateralSprintUsesRunClips()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            ThirdPersonControllerPath);
        Assert.That(controller, Is.Not.Null);

        ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (!states[i].state || states[i].state.name != "Sprint")
            {
                continue;
            }

            BlendTree sprint = states[i].state.motion as BlendTree;
            Assert.That(sprint, Is.Not.Null);
            ChildMotion[] motions = sprint.children;
            Assert.That(motions.Length, Is.EqualTo(5));
            Assert.That(motions[1].motion.name, Is.EqualTo("Rifle_StrafeRunLeftLoop"));
            Assert.That(motions[2].motion.name, Is.EqualTo("Rifle_StrafeRunRightLoop"));
            Assert.That(motions[3].motion.name, Is.EqualTo("Rifle_StrafeRun135LeftLoop"));
            Assert.That(motions[4].motion.name, Is.EqualTo("Rifle_StrafeRun135LeftLoop"));
            Assert.That(motions[4].mirror, Is.True);
            return;
        }

        Assert.Fail("第三人称基础层缺少疾跑状态");
    }

    [Test]
    public void PlayerPrefabHasUnifiedActionComponents()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
        Assert.That(prefab, Is.Not.Null, "找不到玩家 Prefab");

        string[] requiredRootComponents =
        {
            "PlayerInputCtrl",
            "PlayerActionCtrl",
            "PlayerMoveCtrl",
            "PlayerAimCtrl",
            "PlayerWeaponInventoryCtrl",
            "WeaponActionCtrl",
            "WeaponCombatCtrl",
            "BulletImpactCtrl",
            "PlayerAnimationCtrl",
            "PlayerWeaponViewCtrl"
        };

        for (int i = 0; i < requiredRootComponents.Length; i++)
        {
            Assert.That(
                HasComponent(prefab, requiredRootComponents[i]),
                Is.True,
                "玩家根节点缺少 " + requiredRootComponents[i]);
        }

        Component inventory = prefab.GetComponent("PlayerWeaponInventoryCtrl");
        Assert.That(inventory, Is.Not.Null);
        SerializedObject serializedInventory = new SerializedObject(inventory);
        SerializedProperty slotCount = serializedInventory.FindProperty("_slotCount");
        SerializedProperty weaponIds = serializedInventory.FindProperty("_weaponResourceIds");
        Assert.That(slotCount.intValue, Is.GreaterThanOrEqualTo(2));
        Assert.That(weaponIds.arraySize, Is.GreaterThanOrEqualTo(2));
        Assert.That(weaponIds.arraySize, Is.EqualTo(slotCount.intValue));

        Assert.That(
            HasComponentInChildren(prefab, "FirstPersonCameraCtrl"),
            Is.True,
            "玩家 Prefab 缺少第一人称相机控制");
        Assert.That(
            HasComponentInChildren(prefab, "FirstPersonCameraMotionCtrl"),
            Is.True,
            "玩家 Prefab 缺少独立镜头运动反馈");
    }

    [Test]
    public void InputCatalogContainsMouseWheelWeaponCycle()
    {
        Object catalog = AssetDatabase.LoadMainAssetAtPath(
            "Assets/SO/Input/InputActionCatalog.asset");
        Assert.That(catalog, Is.Not.Null);

        SerializedProperty actions = new SerializedObject(catalog).FindProperty("_actions");
        for (int i = 0; i < actions.arraySize; i++)
        {
            SerializedProperty action = actions.GetArrayElementAtIndex(i);
            if (action.FindPropertyRelative("_id").intValue != 10)
            {
                continue;
            }

            Assert.That(
                action.FindPropertyRelative("_expectedControlType").stringValue,
                Is.EqualTo("Vector2"));
            SerializedProperty bindings = action.FindPropertyRelative("_bindings");
            Assert.That(bindings.arraySize, Is.GreaterThan(0));
            Assert.That(
                bindings.GetArrayElementAtIndex(0)
                    .FindPropertyRelative("_path").stringValue,
                Is.EqualTo("<Mouse>/scroll"));
            return;
        }

        Assert.Fail("输入目录缺少鼠标滚轮切枪动作");
    }

    [Test]
    public void InputCatalogUsesTabForViewAndF1ForScoreboard()
    {
        Object catalog = AssetDatabase.LoadMainAssetAtPath(
            "Assets/SO/Input/InputActionCatalog.asset");
        Assert.That(catalog, Is.Not.Null);
        SerializedProperty actions = new SerializedObject(catalog).FindProperty("_actions");
        string viewBinding = null;
        string scoreboardBinding = null;
        for (int index = 0; index < actions.arraySize; index++)
        {
            SerializedProperty action = actions.GetArrayElementAtIndex(index);
            int actionId = action.FindPropertyRelative("_id").intValue;
            if (actionId != 8 && actionId != 12)
            {
                continue;
            }

            string path = action.FindPropertyRelative("_bindings")
                .GetArrayElementAtIndex(0)
                .FindPropertyRelative("_path").stringValue;
            if (actionId == 8)
            {
                scoreboardBinding = path;
            }
            else
            {
                viewBinding = path;
            }
        }

        Assert.That(viewBinding, Is.EqualTo("<Keyboard>/tab"));
        Assert.That(scoreboardBinding, Is.EqualTo("<Keyboard>/f1"));
    }

    [Test]
    public void PlayerHudSeparatesBaseCrosshairAndHitFeedback()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerHudPrefabPath);
        Assert.That(prefab, Is.Not.Null, "找不到玩家 HUD Prefab");

        Component playerCrosshair = FindComponentInChildren(prefab, "PlayerCrosshairUI");
        Component hitFeedback = FindComponentInChildren(prefab, "CrosshairHitFeedbackUI");
        Assert.That(playerCrosshair, Is.Not.Null, "HUD 缺少基础准心控制");
        Assert.That(hitFeedback, Is.Not.Null, "HUD 缺少命中叉反馈");

        SerializedObject serializedCrosshair = new SerializedObject(playerCrosshair);
        SerializedProperty baseGroup = serializedCrosshair.FindProperty("_baseCrosshairGroup");
        Assert.That(baseGroup, Is.Not.Null);
        Assert.That(baseGroup.objectReferenceValue, Is.Not.Null, "基础准心没有独立 CanvasGroup");
        foreach (string field in new[] { "_topLine", "_bottomLine", "_leftLine", "_rightLine" })
        {
            Assert.That(serializedCrosshair.FindProperty(field).objectReferenceValue,
                Is.Not.Null, field + " 未连接到准心线");
        }

        SerializedObject serializedHitFeedback = new SerializedObject(hitFeedback);
        SerializedProperty feedbackGroup = serializedHitFeedback.FindProperty("_feedbackGroup");
        Assert.That(feedbackGroup, Is.Not.Null);
        Assert.That(feedbackGroup.objectReferenceValue, Is.Not.Null);
        Assert.That(
            feedbackGroup.objectReferenceValue,
            Is.Not.EqualTo(baseGroup.objectReferenceValue),
            "ADS 淡出组不应与命中叉反馈共用");
    }

    private static bool HasComponent(GameObject gameObject, string typeName)
    {
        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component && component.GetType().Name == typeName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasState(AnimatorStateMachine stateMachine, string stateName)
    {
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (states[i].state && states[i].state.name == stateName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasCondition(
        AnimatorStateTransition transition,
        string parameterName,
        AnimatorConditionMode conditionMode)
    {
        AnimatorCondition[] conditions = transition.conditions;
        for (int i = 0; i < conditions.Length; i++)
        {
            if (conditions[i].parameter == parameterName &&
                conditions[i].mode == conditionMode)
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasComponentInChildren(GameObject gameObject, string typeName)
    {
        return FindComponentInChildren(gameObject, typeName);
    }

    private static Component FindComponentInChildren(GameObject gameObject, string typeName)
    {
        Component[] components = gameObject.GetComponentsInChildren<Component>(true);
        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component && component.GetType().Name == typeName)
            {
                return component;
            }
        }

        return null;
    }
}
