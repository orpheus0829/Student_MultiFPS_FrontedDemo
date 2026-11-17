using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class BombCarryAnimationConfigurator
{
    public const string CarryAnimationPath =
        "Assets/Resource/Animations/ThirdPerson/Bomb/Carry.fbx";
    public const string CarryMaskPath =
        "Assets/Resource/Animations/ThirdPerson/Bomb/BombCarryArms.mask";

    private const string ControllerPath =
        "Assets/Resource/Animations/ThirdPerson/PlayerThirdPerson.controller";
    private const string CrouchAnimationPath =
        "Assets/Resource/Animations/ThirdPerson/Rifle/Clips/RifleAnimsetPro_Additionals.fbx";
    private const string CarryLayerName = "BombCarryUpperBody";
    private const string CarryStateName = "Carry";
    private const string EmptyStateName = "Empty";
    private const string PlantStateName = "BombPlantCrouch";
    private const string CrouchClipName = "Rifle_CrouchLoop";

    public static List<string> Configure()
    {
        List<string> problems = new List<string>();
        ConfigureImporter(problems);
        AnimationClip carryClip = FindClip(CarryAnimationPath, string.Empty);
        AnimationClip crouchClip = FindClip(CrouchAnimationPath, CrouchClipName);
        if (!carryClip)
        {
            problems.Add(CarryAnimationPath + " 没有可用的 Carry 动画片段");
        }
        if (!crouchClip)
        {
            problems.Add(CrouchAnimationPath + " 缺少 " + CrouchClipName);
        }

        AvatarMask mask = ConfigureMask();
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            ControllerPath);
        if (!controller)
        {
            problems.Add(ControllerPath + " 不存在");
            return problems;
        }

        EnsureBoolParameter(controller, PlayerAnimatorContract.BombCarryName);
        EnsureBoolParameter(controller, PlayerAnimatorContract.BombPlantingName);
        if (carryClip && mask)
        {
            ConfigureCarryLayer(controller, carryClip, mask);
        }
        if (crouchClip)
        {
            ConfigurePlantState(controller, crouchClip);
        }

        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return problems;
    }

    private static void ConfigureImporter(List<string> problems)
    {
        ModelImporter importer = AssetImporter.GetAtPath(CarryAnimationPath) as ModelImporter;
        if (!importer)
        {
            problems.Add(CarryAnimationPath + " 不存在或不是 FBX");
            return;
        }

        bool changed = false;
        if (importer.animationType != ModelImporterAnimationType.Human)
        {
            importer.animationType = ModelImporterAnimationType.Human;
            changed = true;
        }
        if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
        {
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            changed = true;
        }

        ModelImporterClipAnimation[] clips = importer.clipAnimations;
        if (clips == null || clips.Length == 0)
        {
            clips = importer.defaultClipAnimations;
            changed = true;
        }
        for (int i = 0; i < clips.Length; i++)
        {
            if (!clips[i].loopTime)
            {
                clips[i].loopTime = true;
                changed = true;
            }
            clips[i].lockRootHeightY = true;
            clips[i].lockRootRotation = true;
            clips[i].lockRootPositionXZ = true;
        }
        importer.clipAnimations = clips;
        if (changed)
        {
            importer.SaveAndReimport();
        }
    }

    private static AvatarMask ConfigureMask()
    {
        AvatarMask mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(CarryMaskPath);
        if (!mask)
        {
            mask = new AvatarMask();
            mask.name = "BombCarryArms";
            AssetDatabase.CreateAsset(mask, CarryMaskPath);
        }

        Array parts = Enum.GetValues(typeof(AvatarMaskBodyPart));
        for (int i = 0; i < parts.Length; i++)
        {
            AvatarMaskBodyPart part = (AvatarMaskBodyPart)parts.GetValue(i);
            if (part == AvatarMaskBodyPart.LastBodyPart)
            {
                continue;
            }
            mask.SetHumanoidBodyPartActive(part, false);
        }
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
        mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
        EditorUtility.SetDirty(mask);
        return mask;
    }

    private static void ConfigureCarryLayer(
        AnimatorController controller,
        AnimationClip carryClip,
        AvatarMask mask)
    {
        AnimatorControllerLayer layer = FindLayer(controller, CarryLayerName);
        if (layer == null)
        {
            AnimatorStateMachine stateMachine = new AnimatorStateMachine();
            stateMachine.name = CarryLayerName;
            AssetDatabase.AddObjectToAsset(stateMachine, controller);
            layer = new AnimatorControllerLayer
            {
                name = CarryLayerName,
                defaultWeight = 1f,
                blendingMode = AnimatorLayerBlendingMode.Override,
                avatarMask = mask,
                stateMachine = stateMachine
            };
            controller.AddLayer(layer);
        }

        layer.defaultWeight = 1f;
        layer.blendingMode = AnimatorLayerBlendingMode.Override;
        layer.avatarMask = mask;
        AnimatorState emptyState = FindOrAddState(layer.stateMachine, EmptyStateName);
        AnimatorState carryState = FindOrAddState(layer.stateMachine, CarryStateName);
        emptyState.motion = null;
        carryState.motion = carryClip;
        carryState.speed = 1f;
        layer.stateMachine.defaultState = emptyState;
        RemoveTransitions(emptyState);
        RemoveTransitions(carryState);
        AnimatorStateTransition enter = emptyState.AddTransition(carryState);
        ConfigureTransition(enter, PlayerAnimatorContract.BombCarryName, true);
        AnimatorStateTransition exit = carryState.AddTransition(emptyState);
        ConfigureTransition(exit, PlayerAnimatorContract.BombCarryName, false);
    }

    private static void ConfigurePlantState(
        AnimatorController controller,
        AnimationClip crouchClip)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        if (layers.Length == 0 || layers[0].stateMachine == null)
        {
            return;
        }

        AnimatorStateMachine stateMachine = layers[0].stateMachine;
        AnimatorState plantState = FindOrAddState(stateMachine, PlantStateName);
        plantState.motion = crouchClip;
        plantState.speed = 1f;
        plantState.iKOnFeet = true;

        AnimatorStateTransition[] anyTransitions = stateMachine.anyStateTransitions;
        for (int i = anyTransitions.Length - 1; i >= 0; i--)
        {
            if (anyTransitions[i].destinationState == plantState)
            {
                stateMachine.RemoveAnyStateTransition(anyTransitions[i]);
            }
        }
        RemoveTransitions(plantState);
        AnimatorStateTransition enter = stateMachine.AddAnyStateTransition(plantState);
        ConfigureTransition(enter, PlayerAnimatorContract.BombPlantingName, true);
        enter.canTransitionToSelf = false;
        AnimatorStateTransition exit = plantState.AddTransition(stateMachine.defaultState);
        ConfigureTransition(exit, PlayerAnimatorContract.BombPlantingName, false);
    }

    private static void ConfigureTransition(
        AnimatorStateTransition transition,
        string parameter,
        bool value)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.08f;
        transition.offset = 0f;
        transition.AddCondition(
            value ? AnimatorConditionMode.If : AnimatorConditionMode.IfNot,
            0f,
            parameter);
    }

    private static AnimatorControllerLayer FindLayer(
        AnimatorController controller,
        string layerName)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        for (int i = 0; i < layers.Length; i++)
        {
            if (string.Equals(layers[i].name, layerName, StringComparison.Ordinal))
            {
                return layers[i];
            }
        }
        return null;
    }

    private static void EnsureBoolParameter(
        AnimatorController controller,
        string parameterName)
    {
        AnimatorControllerParameter[] parameters = controller.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (string.Equals(
                    parameters[i].name,
                    parameterName,
                    StringComparison.Ordinal))
            {
                return;
            }
        }

        controller.AddParameter(parameterName, AnimatorControllerParameterType.Bool);
    }

    private static AnimatorState FindOrAddState(
        AnimatorStateMachine stateMachine,
        string stateName)
    {
        ChildAnimatorState[] states = stateMachine.states;
        for (int i = 0; i < states.Length; i++)
        {
            if (string.Equals(states[i].state.name, stateName, StringComparison.Ordinal))
            {
                return states[i].state;
            }
        }
        return stateMachine.AddState(stateName);
    }

    private static void RemoveTransitions(AnimatorState state)
    {
        AnimatorStateTransition[] transitions = state.transitions;
        for (int i = transitions.Length - 1; i >= 0; i--)
        {
            state.RemoveTransition(transitions[i]);
        }
    }

    private static AnimationClip FindClip(string assetPath, string clipName)
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        for (int i = 0; i < assets.Length; i++)
        {
            AnimationClip clip = assets[i] as AnimationClip;
            if (!clip || clip.name.StartsWith("__preview__", StringComparison.Ordinal))
            {
                continue;
            }
            if (string.IsNullOrWhiteSpace(clipName) ||
                string.Equals(clip.name, clipName, StringComparison.Ordinal))
            {
                return clip;
            }
        }
        return null;
    }
}
