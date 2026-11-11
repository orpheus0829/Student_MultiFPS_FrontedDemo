using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GunDemo.Tests
{
    public sealed class PlayerThirdPersonGripPlayModeTests
    {
        private const string LeftAnchorPath = "ThirdPersonGrip/LeftHand";
        private const float MaxPositionError = 0.015f;
        private const float MaxPalmForwardError = 12f;
        private const float MaxWristTwist = 15f;
        private const float MinPalmUpDot = 0.90f;
        private const float MaxCorrectionSwing = 35.5f;
        private const float MaxCorrectionTwist = 25.5f;
        private const float MaxMuscleError = 0.05f;
        private const float MaxTakeoverPositionDelta = 0.001f;
        private const float MaxTakeoverRotationDelta = 1f;
        private const float MaxTakeoverMuscleDelta = 0.01f;
        private const float MaxCorrectionAppliedError = 0.5f;
        private const float SpecMaxWristSwing = 35f;
        private const float SpecMaxWristTwist = 25f;
        private const float MaxAutomaticRelativePositionStep = 0.004f;
        private const float MaxAutomaticRelativeRotationStep = 4f;
        private const float MaxFingerScaleDelta = 0.0001f;
        private const float MaxAutomaticRightHandStep = 0.01f;
        private const float MaxAutomaticRightHandRotationStep = 3f;
        private const float MaxAutomaticFingerRotationStep = 0.5f;

        private static readonly string[] CharacterIds =
        {
            "character.anbi", "character.ellen", "character.zhuyuan", "character.belle"
        };

        // 枪栏顺序取自 PlayerWeaponInventoryCtrl 的 _weaponResourceIds，
        // ID 与名称取自七份 SO 的 _weaponId/_weaponName，三组必须一一对应。
        private static readonly string[] ExpectedWeaponResourceIds =
        {
            "weapon.ak.data", "weapon.as-val.data", "weapon.m1911.data",
            "weapon.kar98k.data", "weapon.mps5.data", "weapon.kxg-12.data",
            "weapon.pdw90.data"
        };

        private static readonly string[] ExpectedWeaponIds =
        {
            "ak", "as-val", "m1911", "kar98k", "mps5", "kxg-12", "pdw90"
        };

        private static readonly string[] ExpectedWeaponNames =
        {
            "AK", "AS Val", "M1911", "Kar98k", "MPS5", "KXG12", "PDW90"
        };

        private static readonly string[] TakeoverActionNames =
        {
            "Reload", "EmptyReload", "PerRoundStart", "PerRoundInsert", "PerRoundEnd",
            "Equip", "Holster", "Inspect"
        };

        private static readonly string[] FingerNames =
        {
            "Thumb", "Index", "Middle", "Ring", "Little"
        };

        private static readonly string[] FingerComponents =
        {
            "Proximal", "Intermediate", "Distal", "Spread"
        };

        private static readonly string[] FingerSegments =
        {
            "1", "2", "3", "Spread"
        };

        private static readonly HumanBodyBones[] FingerBones =
        {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate,
            HumanBodyBones.LeftThumbDistal, HumanBodyBones.LeftIndexProximal,
            HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate,
            HumanBodyBones.LeftMiddleDistal, HumanBodyBones.LeftRingProximal,
            HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate,
            HumanBodyBones.LeftLittleDistal, HumanBodyBones.RightThumbProximal,
            HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate,
            HumanBodyBones.RightIndexDistal, HumanBodyBones.RightMiddleProximal,
            HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate,
            HumanBodyBones.RightRingDistal, HumanBodyBones.RightLittleProximal,
            HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal
        };

        private static readonly float[] MaxNaturalFingerAngles = { 45f, 55f, 40f };

        private static int[] _fingerMuscleIndices;
        private static string[] _fingerMuscleLabels;

        [UnityTest]
        public IEnumerator EveryCharacterWeaponAndStateMeetsThirdPersonGrip()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("example", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            DisableSceneAI();

            Type inputType = RequireType("PlayerInputCtrl");
            Type modelType = RequireType("PlayerCharacterModelCtrl");
            Type weaponViewType = RequireType("PlayerWeaponViewCtrl");
            Type actionType = RequireType("WeaponActionCtrl");
            Type inventoryType = RequireType("PlayerWeaponInventoryCtrl");
            Type gripType = RequireType("PlayerThirdPersonGripCtrl");
            Type actionEnumType = RequireType("WeaponActionType");
            Type cancelEnumType = RequireType("WeaponActionCancelReason");
            Type targetType = RequireType("ThirdPersonGripTarget");

            Component player = FindEnabledPlayer(inputType);
            Assert.That(player, Is.Not.Null, "example 场景缺少启用的玩家输入组件");
            Component model = player.GetComponent(modelType);
            Component weaponView = player.GetComponent(weaponViewType);
            Component action = player.GetComponent(actionType);
            Component inventory = player.GetComponent(inventoryType);
            Component grip = player.GetComponent(gripType);
            Assert.That(model, Is.Not.Null);
            Assert.That(weaponView, Is.Not.Null);
            Assert.That(action, Is.Not.Null);
            Assert.That(inventory, Is.Not.Null);
            Assert.That(grip, Is.Not.Null, "缺少 PlayerThirdPersonGripCtrl，无法验收第三人称握持");

            MethodInfo lateUpdate = gripType.GetMethod(
                "LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lateUpdate, Is.Not.Null, "PlayerThirdPersonGripCtrl 必须保留 LateUpdate");

            PropertyInfo firstGunProperty = weaponViewType.GetProperty("FirstPersonWeapon");
            PropertyInfo thirdGunProperty = weaponViewType.GetProperty("ThirdPersonWeapon");
            PropertyInfo currentActionProperty = actionType.GetProperty("CurrentAction");
            PropertyInfo currentWeaponProperty = actionType.GetProperty("CurrentWeaponData");
            PropertyInfo isSwitchingProperty = inventoryType.GetProperty("IsSwitching");
            PropertyInfo slotCountProperty = inventoryType.GetProperty("SlotCount");
            PropertyInfo currentIndexProperty = inventoryType.GetProperty("CurrentIndex");
            PropertyInfo resourceIdsProperty = inventoryType.GetProperty("WeaponResourceIds");
            MethodInfo tryCycle = inventoryType.GetMethod("TryCycle");
            MethodInfo switchModel = modelType.GetMethod("SwitchModelAsync");
            Assert.That(firstGunProperty, Is.Not.Null);
            Assert.That(thirdGunProperty, Is.Not.Null);
            Assert.That(currentActionProperty, Is.Not.Null);
            Assert.That(currentWeaponProperty, Is.Not.Null);
            Assert.That(isSwitchingProperty, Is.Not.Null);
            Assert.That(slotCountProperty, Is.Not.Null);
            Assert.That(currentIndexProperty, Is.Not.Null);
            Assert.That(resourceIdsProperty, Is.Not.Null);
            Assert.That(tryCycle, Is.Not.Null);
            Assert.That(switchModel, Is.Not.Null);

            yield return WaitForFirstWeapon(weaponView, firstGunProperty, 30f);
            VerifyWeaponOrder(inventory, slotCountProperty, resourceIdsProperty);

            foreach (string characterId in CharacterIds)
            {
                Task<bool> switchTask = switchModel.Invoke(
                    model, new object[] { characterId }) as Task<bool>;
                Assert.That(switchTask, Is.Not.Null, characterId);
                yield return WaitForTask(switchTask, 30f, characterId + " 切换模型超时");
                Assert.That(switchTask.Result, Is.True, characterId + " 切换模型失败");
                yield return WaitForNoAction(action, currentActionProperty, 15f);
                yield return new WaitForEndOfFrame();

                Component thirdView = GetField(model, "_thirdPersonView") as Component;
                Assert.That(thirdView, Is.Not.Null, characterId + " 缺少第三人称表现");
                Animator body = thirdView.GetType().GetProperty("CharacterAnimator")
                    .GetValue(thirdView) as Animator;
                Assert.That(body, Is.Not.Null, characterId + " 缺少第三人称 Humanoid Animator");

                // 每个角色进入循环时的起始槽位不一定是 0，按实际 CurrentIndex 推算顺序。
                int startIndex = (int)currentIndexProperty.GetValue(inventory);
                GameObject previousWeapon = null;
                for (int slot = 0; slot < ExpectedWeaponResourceIds.Length; slot++)
                {
                    if (slot > 0)
                    {
                        Assert.That((bool)tryCycle.Invoke(inventory, new object[] { 1 }), Is.True,
                            characterId + " 第 " + slot + " 次切枪未启动");
                        yield return WaitForSwitch(inventory, isSwitchingProperty, action,
                            currentActionProperty, 30f);
                        // 规格十：切枪结束两个渲染帧内必须重新解析新枪锚点。
                        yield return new WaitForEndOfFrame();
                        yield return new WaitForEndOfFrame();
                    }

                    int expectedIndex = (startIndex + slot) % ExpectedWeaponResourceIds.Length;
                    Assert.That((int)currentIndexProperty.GetValue(inventory),
                        Is.EqualTo(expectedIndex),
                        characterId + " 第 " + slot + " 次切枪后枪栏索引不符，期望 " +
                        expectedIndex);
                    object weaponData = currentWeaponProperty.GetValue(action);
                    Assert.That(weaponData, Is.Not.Null,
                        characterId + " 第 " + slot + " 次切枪后没有武器数据");
                    string weaponName = GetWeaponName(weaponData);
                    string context = characterId + "/" + weaponName + "/槽" + expectedIndex;
                    Assert.That(GetWeaponId(weaponData), Is.EqualTo(ExpectedWeaponIds[expectedIndex]),
                        context + " 枪械 ID 与枪栏顺序不符，实际 " + GetWeaponId(weaponData));
                    Assert.That(weaponName, Is.EqualTo(ExpectedWeaponNames[expectedIndex]),
                        context + " 枪械名称与枪栏顺序不符，实际 " + weaponName);

                    GameObject thirdWeapon = thirdGunProperty.GetValue(weaponView) as GameObject;
                    Assert.That(thirdWeapon, Is.Not.Null, characterId + " 缺少第三人称武器实例");
                    Transform targetWeapon = thirdWeapon.transform;
                    Transform anchor = targetWeapon.Find(LeftAnchorPath);
                    Assert.That(anchor, Is.Not.Null,
                        context + " 缺少 " + LeftAnchorPath + "，七把正式枪必须显式锚点");

                    if (slot > 0)
                    {
                        VerifyTargetResolved(grip, targetType, anchor, targetWeapon, weaponData,
                            previousWeapon, context + "/切枪");
                    }

                    AssertFingerPoseEnabled(weaponData, context);
                    AssertLeftHandIkPath(weaponData, context);

                    body.Play("Idle", 0, 0f);
                    body.Update(0f);
                    VerifyGripAndFingers(body, grip, lateUpdate, anchor, weaponData,
                        false, context + "/待机");

                    body.Play("Sprint", 0, 0.35f);
                    body.Update(0f);
                    VerifyGripAndFingers(body, grip, lateUpdate, anchor, weaponData,
                        false, context + "/疾跑");

                    Assert.That((bool)actionType.GetMethod("TryStartAction").Invoke(
                        action, new object[] { Enum.Parse(actionEnumType, "Fire"), false }),
                        Is.True, context + " 无法进入 Fire 动作");
                    yield return null;
                    body.Update(0f);
                    VerifyGripAndFingers(body, grip, lateUpdate, anchor, weaponData,
                        true, context + "/开火");
                    CancelAction(action, actionType, cancelEnumType);
                    yield return null;

                    // SPEC-001 五-9：这些动作状态下必须完全由动画接管。是否进入由动作集的
                    // 启用状态决定（KXG12 关闭了 Reload/EmptyReload，改用逐发装填），
                    // 启用的动作启动失败必须判失败，未启用的动作必须启动不了。
                    for (int takeover = 0; takeover < TakeoverActionNames.Length; takeover++)
                    {
                        string takeoverName = TakeoverActionNames[takeover];
                        if (IsActionEnabled(action, actionEnumType, takeoverName))
                        {
                            yield return VerifyAnimationTakeover(action, actionType, actionEnumType,
                                cancelEnumType, grip, lateUpdate, body, takeoverName,
                                context + "/" + takeoverName);
                        }
                        else
                        {
                            Assert.That((bool)actionType.GetMethod("TryStartAction").Invoke(
                                action,
                                new object[] { Enum.Parse(actionEnumType, takeoverName), false }),
                                Is.False,
                                context + " 未启用的 " + takeoverName + " 不应能启动");
                        }
                    }

                    previousWeapon = thirdWeapon;
                }
            }
        }

        /// <summary>
        /// 走 PlayerActionCtrl -> WeaponCombatCtrl 的真实按住开火链，覆盖 AK 与 PDW90
        /// 连续至少五发。每发都不得重启人物 UpperBodyWeapon 层；手腕相对枪锚点不得
        /// 逐帧跳变；指骨只能在自然姿势附近旋转，且绝不允许改缩放来强行贴枪。
        /// </summary>
        [UnityTest]
        public IEnumerator AutomaticFireKeepsBodyAndFingersStable()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("example", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            DisableSceneAI();

            Type inputType = RequireType("PlayerInputCtrl");
            Type modelType = RequireType("PlayerCharacterModelCtrl");
            Type weaponViewType = RequireType("PlayerWeaponViewCtrl");
            Type actionType = RequireType("WeaponActionCtrl");
            Type inventoryType = RequireType("PlayerWeaponInventoryCtrl");
            Type playerActionType = RequireType("PlayerActionCtrl");
            Type combatType = RequireType("WeaponCombatCtrl");
            Type gripType = RequireType("PlayerThirdPersonGripCtrl");
            Type kinemationType = RequireType("PlayerKinemationViewCtrl");

            Component player = FindEnabledPlayer(inputType);
            Assert.That(player, Is.Not.Null, "example 场景缺少启用的玩家");
            Component model = player.GetComponent(modelType);
            Component weaponView = player.GetComponent(weaponViewType);
            Component action = player.GetComponent(actionType);
            Component inventory = player.GetComponent(inventoryType);
            Component playerAction = player.GetComponent(playerActionType);
            Component combat = player.GetComponent(combatType);
            Component grip = player.GetComponent(gripType);
            Component kinemation = player.GetComponent(kinemationType);
            Assert.That(playerAction && combat && grip && kinemation, Is.True,
                "真实开火链组件不完整");

            PropertyInfo firstGunProperty = weaponViewType.GetProperty("FirstPersonWeapon");
            PropertyInfo thirdGunProperty = weaponViewType.GetProperty("ThirdPersonWeapon");
            PropertyInfo currentActionProperty = actionType.GetProperty("CurrentAction");
            PropertyInfo currentWeaponProperty = actionType.GetProperty("CurrentWeaponData");
            PropertyInfo isSwitchingProperty = inventoryType.GetProperty("IsSwitching");
            MethodInfo tryCycle = inventoryType.GetMethod("TryCycle");
            yield return WaitForFirstWeapon(weaponView, firstGunProperty, 30f);
            yield return WaitForActionIdle(action, currentActionProperty, "AK 连发前");
            yield return WaitForProjectile(combat, 10f);

            Component thirdView = GetField(model, "_thirdPersonView") as Component;
            Component firstView = GetField(model, "_firstPersonView") as Component;
            Animator body = thirdView.GetType().GetProperty("CharacterAnimator")
                .GetValue(thirdView) as Animator;
            Animator firstPersonBody = firstView.GetType().GetProperty("CharacterAnimator")
                .GetValue(firstView) as Animator;
            Assert.That(body, Is.Not.Null, "缺少第三人称 Animator");
            Assert.That(firstPersonBody, Is.Not.Null, "缺少第一人称 Animator");

            yield return VerifyAutomaticFireStability(
                playerAction, combat, grip, body, firstPersonBody, thirdView,
                thirdGunProperty.GetValue(weaponView) as GameObject,
                firstGunProperty.GetValue(weaponView) as GameObject,
                kinemation, "AK");

            Assert.That((bool)tryCycle.Invoke(inventory, new object[] { -1 }), Is.True,
                "无法从 AK 切换到 PDW90");
            yield return WaitForSwitch(inventory, isSwitchingProperty, action,
                currentActionProperty, 30f);
            yield return WaitForProjectile(combat, 10f);
            Assert.That(GetWeaponName(currentWeaponProperty.GetValue(action)), Is.EqualTo("PDW90"),
                "向前回绕一次后应为 PDW90");

            yield return VerifyAutomaticFireStability(
                playerAction, combat, grip, body, firstPersonBody, thirdView,
                thirdGunProperty.GetValue(weaponView) as GameObject,
                firstGunProperty.GetValue(weaponView) as GameObject,
                kinemation, "PDW90");
        }

        private static IEnumerator VerifyAutomaticFireStability(
            Component playerAction,
            Component combat,
            Component grip,
            Animator body,
            Animator firstPersonBody,
            Component thirdView,
            GameObject thirdWeapon,
            GameObject firstWeapon,
            Component kinemation,
            string context)
        {
            object fireMode = combat.GetType().GetProperty("FireMode").GetValue(combat);
            Assert.That(fireMode.ToString(), Is.EqualTo("Automatic"),
                context + " 必须以全自动模式测试");
            Assert.That(thirdWeapon, Is.Not.Null, context + " 缺少第三人称枪");
            Assert.That(firstWeapon, Is.Not.Null, context + " 缺少第一人称枪");
            Transform firstPersonWeaponBone = FindChild(firstPersonBody.transform, "ik_hand_gun");
            Transform firstPersonRightHand = FindChild(firstPersonBody.transform, "hand_r");
            Assert.That(firstPersonWeaponBone && firstPersonRightHand, Is.True,
                context + " 缺少第一人称枪骨或右手");
            Transform anchor = thirdWeapon.transform.Find(LeftAnchorPath);
            Transform hand = body.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform rightHand = body.GetBoneTransform(HumanBodyBones.RightHand);
            Transform bodyReference = body.GetBoneTransform(HumanBodyBones.UpperChest);
            if (!bodyReference)
            {
                bodyReference = body.GetBoneTransform(HumanBodyBones.Chest);
            }
            Assert.That(anchor && hand && rightHand && bodyReference, Is.True,
                context + " 缺少手腕、胸骨或锚点");

            // 先让待机握姿完整写入一帧；后续以它作为右手和指骨的稳定基准。
            yield return new WaitForEndOfFrame();
            float firstPersonSettleDeadline = Time.realtimeSinceStartup + 1f;
            while (firstPersonBody.IsInTransition(0) &&
                Time.realtimeSinceStartup < firstPersonSettleDeadline)
            {
                yield return new WaitForEndOfFrame();
            }
            int firstPersonIdleState = firstPersonBody.GetCurrentAnimatorStateInfo(0).fullPathHash;
            Assert.That(firstPersonBody.IsInTransition(0), Is.False,
                context + " 连发前第一人称手臂仍在过渡");

            int layerIndex = (int)GetField(thirdView, "_upperBodyWeaponLayerIndex");
            Assert.That(layerIndex, Is.GreaterThanOrEqualTo(0),
                context + " 缺少 UpperBodyWeapon 层");
            Quaternion[] neutralRotations =
                (Quaternion[])GetField(grip, "_fingerNeutralLocalRotations");
            Transform[] fingerTransforms = new Transform[FingerBones.Length];
            Vector3[] initialScales = new Vector3[FingerBones.Length];
            Quaternion[] initialFingerRotations = new Quaternion[FingerBones.Length];
            for (int i = 0; i < FingerBones.Length; i++)
            {
                fingerTransforms[i] = body.GetBoneTransform(FingerBones[i]);
                Assert.That(fingerTransforms[i], Is.Not.Null,
                    context + " 缺少指骨 " + FingerBones[i]);
                initialScales[i] = fingerTransforms[i].localScale;
                initialFingerRotations[i] = fingerTransforms[i].localRotation;
            }

            PropertyInfo magazineAmmoProperty = combat.GetType().GetProperty("MagazineAmmo");
            int ammoBefore = (int)magazineAmmoProperty.GetValue(combat);
            float idleMaxRightHandRotationStep = 0f;
            Vector3 idlePreviousPosition =
                bodyReference.InverseTransformPoint(rightHand.position);
            Quaternion idlePreviousRotation =
                Quaternion.Inverse(bodyReference.rotation) * rightHand.rotation;
            float idleDeadline = Time.realtimeSinceStartup + 1.2f;
            while (Time.realtimeSinceStartup < idleDeadline)
            {
                yield return new WaitForEndOfFrame();
                Vector3 idlePosition =
                    bodyReference.InverseTransformPoint(rightHand.position);
                Quaternion idleRotation =
                    Quaternion.Inverse(bodyReference.rotation) * rightHand.rotation;
                idleMaxRightHandRotationStep = Mathf.Max(
                    idleMaxRightHandRotationStep,
                    Quaternion.Angle(idlePreviousRotation, idleRotation));
                idlePreviousPosition = idlePosition;
                idlePreviousRotation = idleRotation;
            }
            Assert.That(idleMaxRightHandRotationStep,
                Is.LessThanOrEqualTo(MaxAutomaticRightHandRotationStep),
                context + " 不开火时右手腕已有待机循环跳变 " +
                idleMaxRightHandRotationStep.ToString("F2") + "°");
            Vector3 previousRelativePosition = Vector3.zero;
            Quaternion previousRelativeRotation = Quaternion.identity;
            bool hasPreviousPose = false;
            Vector3 previousRightHandPosition = Vector3.zero;
            Quaternion previousRightHandRotation = Quaternion.identity;
            Vector3 previousWeaponPosition = Vector3.zero;
            Quaternion previousWeaponRotation = Quaternion.identity;
            Vector3 previousFirstPersonHandRelativePosition = Vector3.zero;
            Quaternion previousFirstPersonHandRelativeRotation = Quaternion.identity;
            bool hasPreviousFirstPersonPose = false;
            float deadline = Time.realtimeSinceStartup + 6f;
            SendFireInput(playerAction, true);
            try
            {
                while (ammoBefore - (int)magazineAmmoProperty.GetValue(combat) < 12 &&
                    Time.realtimeSinceStartup < deadline)
                {
                    yield return new WaitForEndOfFrame();

                    Assert.That(firstPersonBody.IsInTransition(0), Is.False,
                        context + " 连发时第一人称手臂被反复切换状态");
                    Assert.That(
                        firstPersonBody.GetCurrentAnimatorStateInfo(0).fullPathHash,
                        Is.EqualTo(firstPersonIdleState),
                        context + " 连发时第一人称手臂离开待机基准；应只由程序后坐驱动");
                    Assert.That(body.GetLayerWeight(layerIndex), Is.LessThanOrEqualTo(0.001f),
                        context + " 连发时人物 UpperBodyWeapon 层被反复抬起");
                    Vector3 relativePosition = anchor.InverseTransformPoint(hand.position);
                    Quaternion relativeRotation =
                        Quaternion.Inverse(anchor.rotation) * hand.rotation;
                    Vector3 rightHandPosition =
                        bodyReference.InverseTransformPoint(rightHand.position);
                    Quaternion rightHandRotation =
                        Quaternion.Inverse(bodyReference.rotation) * rightHand.rotation;
                    Vector3 weaponPosition =
                        bodyReference.InverseTransformPoint(thirdWeapon.transform.position);
                    Quaternion weaponRotation = Quaternion.Inverse(bodyReference.rotation) *
                        thirdWeapon.transform.rotation;
                    if (hasPreviousPose)
                    {
                        Assert.That(Vector3.Distance(previousRelativePosition, relativePosition),
                            Is.LessThanOrEqualTo(MaxAutomaticRelativePositionStep),
                            context + " 连发时手腕相对枪发生位置跳变");
                        Assert.That(Quaternion.Angle(previousRelativeRotation, relativeRotation),
                            Is.LessThanOrEqualTo(MaxAutomaticRelativeRotationStep),
                            context + " 连发时手腕相对枪发生旋转跳变");
                        Assert.That(Vector3.Distance(
                                previousRightHandPosition, rightHandPosition),
                            Is.LessThanOrEqualTo(MaxAutomaticRightHandStep),
                            context + " 连发时右手腕相对身体抽动");
                        Assert.That(Quaternion.Angle(
                                previousRightHandRotation, rightHandRotation),
                            Is.LessThanOrEqualTo(MaxAutomaticRightHandRotationStep),
                            context + " 连发时右手腕相对身体旋转抽动；不开火对照最大单帧=" +
                            idleMaxRightHandRotationStep.ToString("F2") + "°；已射击=" +
                            (ammoBefore - (int)magazineAmmoProperty.GetValue(combat)));
                        Assert.That(Vector3.Distance(previousWeaponPosition, weaponPosition),
                            Is.LessThanOrEqualTo(MaxAutomaticRightHandStep),
                            context + " 连发时枪身相对身体抽动");
                        Assert.That(Quaternion.Angle(previousWeaponRotation, weaponRotation),
                            Is.LessThanOrEqualTo(MaxAutomaticRightHandRotationStep),
                            context + " 连发时枪身相对身体旋转抽动");
                    }
                    previousRelativePosition = relativePosition;
                    previousRelativeRotation = relativeRotation;
                    hasPreviousPose = true;
                    previousRightHandPosition = rightHandPosition;
                    previousRightHandRotation = rightHandRotation;
                    previousWeaponPosition = weaponPosition;
                    previousWeaponRotation = weaponRotation;

                    Vector3 firstPersonHandRelativePosition =
                        firstPersonWeaponBone.InverseTransformPoint(firstPersonRightHand.position);
                    Quaternion firstPersonHandRelativeRotation =
                        Quaternion.Inverse(firstPersonWeaponBone.rotation) *
                        firstPersonRightHand.rotation;
                    if (hasPreviousFirstPersonPose)
                    {
                        Assert.That(Vector3.Distance(
                                previousFirstPersonHandRelativePosition,
                                firstPersonHandRelativePosition),
                            Is.LessThanOrEqualTo(0.001f),
                            context + " 连发时第一人称右手相对枪骨抽动");
                        Assert.That(Quaternion.Angle(
                                previousFirstPersonHandRelativeRotation,
                                firstPersonHandRelativeRotation),
                            Is.LessThanOrEqualTo(1f),
                            context + " 连发时第一人称右手相对枪骨旋转抽动");
                    }
                    previousFirstPersonHandRelativePosition = firstPersonHandRelativePosition;
                    previousFirstPersonHandRelativeRotation = firstPersonHandRelativeRotation;
                    hasPreviousFirstPersonPose = true;

                    for (int i = 0; i < fingerTransforms.Length; i++)
                    {
                        float naturalAngle = Quaternion.Angle(
                            neutralRotations[i], fingerTransforms[i].localRotation);
                        Assert.That(naturalAngle,
                            Is.LessThanOrEqualTo(MaxNaturalFingerAngles[i % 3] + 0.5f),
                            context + " " + FingerBones[i] + " 偏离自然姿势 " +
                            naturalAngle.ToString("F2") + "°");
                        Assert.That(Vector3.Distance(
                                initialScales[i], fingerTransforms[i].localScale),
                            Is.LessThanOrEqualTo(MaxFingerScaleDelta),
                            context + " " + FingerBones[i] + " 被拉伸或压缩");
                        Assert.That(Quaternion.Angle(
                                initialFingerRotations[i],
                                fingerTransforms[i].localRotation),
                            Is.LessThanOrEqualTo(MaxAutomaticFingerRotationStep),
                            context + " " + FingerBones[i] + " 连发时发生指姿硬切");
                    }
                }
            }
            finally
            {
                SendFireInput(playerAction, false);
            }

            Assert.That(ammoBefore - (int)magazineAmmoProperty.GetValue(combat),
                Is.GreaterThanOrEqualTo(12),
                context + " 未完成至少十二发真实连续射击");

            yield return new WaitForEndOfFrame();
            object recoil = GetField(kinemation, "_recoilAnimation");
            Assert.That(recoil, Is.Not.Null, context + " 缺少第一人称程序后坐");
            Assert.That((bool)GetField(recoil, "_isFiring"), Is.False,
                context + " 松开扳机后程序后坐仍标记为开火");
            Assert.That((bool)GetField(recoil, "_isLooping"), Is.False,
                context + " 松开扳机后全自动后坐仍在自循环");

            float recoilDeadline = Time.realtimeSinceStartup + 1.75f;
            while ((bool)GetField(recoil, "_isPlaying") &&
                Time.realtimeSinceStartup < recoilDeadline)
            {
                yield return new WaitForEndOfFrame();
            }
            Assert.That((bool)GetField(recoil, "_isPlaying"), Is.False,
                context + " 后坐尾段未在 1.75 秒内结束");
            Vector3 outLoc = (Vector3)recoil.GetType().GetProperty("OutLoc").GetValue(recoil);
            Quaternion outRot = (Quaternion)recoil.GetType().GetProperty("OutRot").GetValue(recoil);
            Assert.That(outLoc.magnitude, Is.LessThanOrEqualTo(0.001f),
                context + " 后坐结束后仍有位置输出");
            Assert.That(Quaternion.Angle(Quaternion.identity, outRot),
                Is.LessThanOrEqualTo(0.5f), context + " 后坐结束后仍有旋转输出");

            Vector3 previousGunPosition = firstPersonBody.transform.InverseTransformPoint(
                firstPersonWeaponBone.position);
            Quaternion previousGunRotation = Quaternion.Inverse(firstPersonBody.transform.rotation) *
                firstPersonWeaponBone.rotation;
            for (int frame = 0; frame < 30; frame++)
            {
                yield return new WaitForEndOfFrame();
                Vector3 gunPosition = firstPersonBody.transform.InverseTransformPoint(
                    firstPersonWeaponBone.position);
                Quaternion gunRotation = Quaternion.Inverse(firstPersonBody.transform.rotation) *
                    firstPersonWeaponBone.rotation;
                Assert.That(Vector3.Distance(previousGunPosition, gunPosition),
                    Is.LessThanOrEqualTo(0.00025f),
                    context + " 停火回正后第一人称枪骨仍在位置抖动");
                Assert.That(Quaternion.Angle(previousGunRotation, gunRotation),
                    Is.LessThanOrEqualTo(0.15f),
                    context + " 停火回正后第一人称枪骨仍在旋转抖动");
                previousGunPosition = gunPosition;
                previousGunRotation = gunRotation;
            }
        }

        /// <summary>
        /// SPEC-001 五-9：死亡后左臂 IK 与程序指骨都不许再写入。
        /// 单独一个用例加一次场景加载，避免击杀触发 PlayerLifeCtrl 掉枪/布娃娃污染主矩阵。
        /// </summary>
        [UnityTest]
        public IEnumerator DeathStopsThirdPersonGripWrite()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("example", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Type inputType = RequireType("PlayerInputCtrl");
            Type modelType = RequireType("PlayerCharacterModelCtrl");
            Type weaponViewType = RequireType("PlayerWeaponViewCtrl");
            Type gripType = RequireType("PlayerThirdPersonGripCtrl");
            Type healthType = RequireType("CharacterHealth");
            Type lifeType = RequireType("PlayerLifeCtrl");
            Type damageType = RequireType("CombatDamage");
            Type infoType = RequireType("PlayerInfo");

            Component player = FindEnabledPlayer(inputType);
            Assert.That(player, Is.Not.Null, "example 场景缺少启用的玩家输入组件");
            Component model = player.GetComponent(modelType);
            Component weaponView = player.GetComponent(weaponViewType);
            Component grip = player.GetComponent(gripType);
            Component health = player.GetComponent(healthType);
            Component life = player.GetComponent(lifeType);
            Assert.That(model, Is.Not.Null);
            Assert.That(weaponView, Is.Not.Null);
            Assert.That(grip, Is.Not.Null, "缺少 PlayerThirdPersonGripCtrl");
            Assert.That(health, Is.Not.Null, "缺少 CharacterHealth");
            Assert.That(life, Is.Not.Null, "缺少 PlayerLifeCtrl，死亡后无法还原");

            PropertyInfo firstGunProperty = weaponViewType.GetProperty("FirstPersonWeapon");
            PropertyInfo thirdGunProperty = weaponViewType.GetProperty("ThirdPersonWeapon");
            yield return WaitForFirstWeapon(weaponView, firstGunProperty, 30f);
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            MethodInfo lateUpdate = gripType.GetMethod(
                "LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lateUpdate, Is.Not.Null, "PlayerThirdPersonGripCtrl 必须保留 LateUpdate");

            Component thirdView = GetField(model, "_thirdPersonView") as Component;
            Assert.That(thirdView, Is.Not.Null, "缺少第三人称表现");
            Animator body = thirdView.GetType().GetProperty("CharacterAnimator")
                .GetValue(thirdView) as Animator;
            Assert.That(body, Is.Not.Null, "缺少第三人称 Humanoid Animator");

            GameObject thirdWeapon = thirdGunProperty.GetValue(weaponView) as GameObject;
            Assert.That(thirdWeapon, Is.Not.Null, "死亡用例缺少第三人称武器实例");
            Transform anchor = thirdWeapon.transform.Find(LeftAnchorPath);
            Assert.That(anchor, Is.Not.Null, "死亡用例缺少 " + LeftAnchorPath);

            Type actionType = RequireType("WeaponActionCtrl");
            Component action = player.GetComponent(actionType);
            PropertyInfo currentActionProperty = actionType.GetProperty("CurrentAction");
            Assert.That(action, Is.Not.Null, "缺少 WeaponActionCtrl");
            Assert.That(currentActionProperty, Is.Not.Null, "WeaponActionCtrl 缺少 CurrentAction");
            // 场景加载后 Equip 可能仍在执行，控制器按规格在非 Fire 动作期间不写入；
            // 必须等动作结束再做"活着时握持生效"的正控制，否则会把正控制误判成失败。
            yield return WaitForActionIdle(action, currentActionProperty, "死亡前正控制");
            yield return new WaitForEndOfFrame();

            // 正控制：活着时握持必须真的生效，否则"死亡不写"是假通过。
            AssertGripActive(body, grip, lateUpdate, anchor, "死亡前正控制");

            PropertyInfo isDeadProperty = healthType.GetProperty("IsDead");
            Assert.That(isDeadProperty, Is.Not.Null, "CharacterHealth 缺少 IsDead");
            MethodInfo kill = damageType.GetMethod("Kill", new Type[] { healthType, infoType });
            Assert.That(kill, Is.Not.Null, "CombatDamage.Kill(CharacterHealth, PlayerInfo) 签名不符");
            kill.Invoke(null, new object[] { health, null });
            Assert.That((bool)isDeadProperty.GetValue(health), Is.True, "击杀没有生效，死亡用例失效");

            AssertNoGripWrite(body, grip, lateUpdate, "死亡");

            MethodInfo respawn = lifeType.GetMethod("Respawn");
            Assert.That(respawn, Is.Not.Null, "PlayerLifeCtrl 缺少 Respawn");
            respawn.Invoke(life, null);
            Assert.That((bool)isDeadProperty.GetValue(health), Is.False, "复活后仍处于死亡状态");
        }

        /// <summary>
        /// SPEC-001 五-9：Animator 禁用、武器缺失、锚点配置错误三种状态下控制器都不得写入。
        /// 每个状态结束后都要求握持重新生效，防止"本来就没在写"造成的假通过。
        /// </summary>
        [UnityTest]
        public IEnumerator DisabledAnimatorMissingWeaponAndBadAnchorStopGripWrite()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("example", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Type inputType = RequireType("PlayerInputCtrl");
            Type modelType = RequireType("PlayerCharacterModelCtrl");
            Type weaponViewType = RequireType("PlayerWeaponViewCtrl");
            Type gripType = RequireType("PlayerThirdPersonGripCtrl");

            Component player = FindEnabledPlayer(inputType);
            Assert.That(player, Is.Not.Null, "example 场景缺少启用的玩家输入组件");
            Component model = player.GetComponent(modelType);
            Component weaponView = player.GetComponent(weaponViewType);
            Component grip = player.GetComponent(gripType);
            Assert.That(model, Is.Not.Null);
            Assert.That(weaponView, Is.Not.Null);
            Assert.That(grip, Is.Not.Null, "缺少 PlayerThirdPersonGripCtrl");

            PropertyInfo firstGunProperty = weaponViewType.GetProperty("FirstPersonWeapon");
            PropertyInfo thirdGunProperty = weaponViewType.GetProperty("ThirdPersonWeapon");
            yield return WaitForFirstWeapon(weaponView, firstGunProperty, 30f);
            yield return new WaitForEndOfFrame();
            yield return new WaitForEndOfFrame();

            MethodInfo lateUpdate = gripType.GetMethod(
                "LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(lateUpdate, Is.Not.Null, "PlayerThirdPersonGripCtrl 必须保留 LateUpdate");

            Component thirdView = GetField(model, "_thirdPersonView") as Component;
            Assert.That(thirdView, Is.Not.Null, "缺少第三人称表现");
            Animator body = thirdView.GetType().GetProperty("CharacterAnimator")
                .GetValue(thirdView) as Animator;
            Assert.That(body, Is.Not.Null, "缺少第三人称 Humanoid Animator");

            GameObject thirdWeapon = thirdGunProperty.GetValue(weaponView) as GameObject;
            Assert.That(thirdWeapon, Is.Not.Null, "缺少第三人称武器实例");
            Transform anchor = thirdWeapon.transform.Find(LeftAnchorPath);
            Assert.That(anchor, Is.Not.Null, "缺少 " + LeftAnchorPath);

            Type actionType = RequireType("WeaponActionCtrl");
            Component action = player.GetComponent(actionType);
            PropertyInfo currentActionProperty = actionType.GetProperty("CurrentAction");
            Assert.That(action, Is.Not.Null, "缺少 WeaponActionCtrl");
            Assert.That(currentActionProperty, Is.Not.Null, "WeaponActionCtrl 缺少 CurrentAction");
            yield return WaitForActionIdle(action, currentActionProperty, "正控制");
            yield return new WaitForEndOfFrame();

            AssertGripActive(body, grip, lateUpdate, anchor, "正控制");

            body.enabled = false;
            try
            {
                AssertNoGripWrite(body, grip, lateUpdate, "Animator 禁用");
            }
            finally
            {
                body.enabled = true;
            }
            yield return WaitForActionIdle(action, currentActionProperty, "Animator 恢复后");
            yield return new WaitForEndOfFrame();
            AssertGripActive(body, grip, lateUpdate, anchor, "Animator 恢复后");

            FieldInfo weaponField = weaponViewType.GetField(
                "_thirdPersonWeapon", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(weaponField, Is.Not.Null, "PlayerWeaponViewCtrl 缺少 _thirdPersonWeapon");
            object savedWeapon = weaponField.GetValue(weaponView);
            Assert.That(savedWeapon, Is.Not.Null, "第三人称武器实例为空，武器缺失用例前提不成立");
            weaponField.SetValue(weaponView, null);
            try
            {
                AssertNoGripWrite(body, grip, lateUpdate, "武器缺失");
            }
            finally
            {
                weaponField.SetValue(weaponView, savedWeapon);
                CacheNeutralGrip(grip);
            }
            yield return WaitForActionIdle(action, currentActionProperty, "武器恢复后");
            yield return new WaitForEndOfFrame();
            AssertGripActive(body, grip, lateUpdate, anchor, "武器恢复后");

            string originalName = anchor.name;
            anchor.name = "LeftHand_Missing";
            SetField(grip, "_loggedMissingGripPath", null);
            LogAssert.Expect(LogType.Error, new Regex("锚点解析失败"));
            try
            {
                CacheNeutralGrip(grip);
                AssertNoGripWrite(body, grip, lateUpdate, "锚点配置错误");
            }
            finally
            {
                anchor.name = originalName;
                CacheNeutralGrip(grip);
            }
            yield return WaitForActionIdle(action, currentActionProperty, "锚点恢复后");
            yield return new WaitForEndOfFrame();
            AssertGripActive(body, grip, lateUpdate, anchor, "锚点恢复后");
        }

        /// <summary>
        /// 等到 WeaponActionCtrl 当前动作为空：控制器在非 Fire 动作期间按规格不写入，
        /// 抢在动作结束前做正控制会把"规格正确"误判成"握持失效"。
        /// 不固定睡眠，按状态等待并带超时，超时直接失败并打印卡住的动作名。
        /// </summary>
        private static IEnumerator WaitForActionIdle(
            Component action, PropertyInfo currentActionProperty, string context, float timeout = 3f)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (currentActionProperty.GetValue(action) != null)
            {
                if (Time.realtimeSinceStartup >= deadline)
                {
                    object stuck = currentActionProperty.GetValue(action);
                    object type = stuck.GetType().GetProperty("ActionType").GetValue(stuck);
                    Assert.Fail(context + " 等待动作结束超时，当前动作 " + type);
                }
                yield return null;
            }
        }

        private static IEnumerator WaitForFirstWeapon(
            Component weaponView, PropertyInfo firstGunProperty, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (firstGunProperty.GetValue(weaponView) as GameObject == null &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(firstGunProperty.GetValue(weaponView) as GameObject, Is.Not.Null,
                "等待第一人称武器加载超时");
        }

        private static IEnumerator WaitForProjectile(Component combat, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (GetField(combat, "_projectilePrefab") == null &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(GetField(combat, "_projectilePrefab"), Is.Not.Null,
                "等待射击弹丸资源加载超时");
        }

        private static void SendFireInput(Component playerAction, bool started)
        {
            Type commandType = RequireType("PlayerInputCommand");
            Type inputActionType = RequireType("GJInputAction");
            Type phaseType = RequireType("PlayerInputPhase");
            object command = Activator.CreateInstance(commandType, new object[]
            {
                Enum.Parse(inputActionType, "Fire"),
                Enum.Parse(phaseType, started ? "Started" : "Canceled"),
                Vector2.zero
            });
            MethodInfo receive = playerAction.GetType().GetMethod("Receive");
            Assert.That(receive, Is.Not.Null, "PlayerActionCtrl 缺少 Receive");
            receive.Invoke(playerAction, new object[] { command });
        }

        private static IEnumerator WaitForTask(Task task, float timeout, string message)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That(task.IsCompleted, Is.True, message);
        }

        private static IEnumerator WaitForNoAction(
            Component action, PropertyInfo currentActionProperty, float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (currentActionProperty.GetValue(action) != null &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private static IEnumerator WaitForSwitch(
            Component inventory,
            PropertyInfo isSwitchingProperty,
            Component action,
            PropertyInfo currentActionProperty,
            float timeout)
        {
            float deadline = Time.realtimeSinceStartup + timeout;
            while (((bool)isSwitchingProperty.GetValue(inventory) ||
                currentActionProperty.GetValue(action) != null) &&
                Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
            Assert.That((bool)isSwitchingProperty.GetValue(inventory), Is.False, "切枪超时");
            Assert.That(currentActionProperty.GetValue(action), Is.Null, "切枪动作未结束");
        }

        private static void VerifyGripAndFingers(
            Animator body,
            Component grip,
            MethodInfo lateUpdate,
            Transform anchor,
            object weaponData,
            bool isFiring,
            string context)
        {
            Transform hand = body.GetBoneTransform(HumanBodyBones.LeftHand);
            Transform middle = body.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            Transform index = body.GetBoneTransform(HumanBodyBones.LeftIndexProximal);
            Transform little = body.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
            Assert.That(hand && middle && index && little, Is.True,
                context + " 缺少左手 Humanoid 指骨");

            Vector3 animatedForward = (middle.position - hand.position).normalized;
            Vector3 animatedUp = Vector3.Cross(
                animatedForward, index.position - little.position).normalized;
            Assert.That(animatedForward.sqrMagnitude, Is.GreaterThan(0.5f),
                context + " 动画掌框前向退化");
            Assert.That(animatedUp.sqrMagnitude, Is.GreaterThan(0.5f),
                context + " 动画掌框法线退化");
            Quaternion animatedRotation = hand.rotation;

            InvokeGrip(lateUpdate, grip);

            float distance = Vector3.Distance(hand.position, anchor.position);
            Assert.That(distance, Is.LessThanOrEqualTo(MaxPositionError),
                context + " 左腕位置误差 " + distance.ToString("F4") + "m");

            Vector3 actualForward = (middle.position - hand.position).normalized;
            Vector3 actualUp = Vector3.Cross(
                actualForward, index.position - little.position).normalized;
            float forwardError = Vector3.Angle(actualForward, anchor.forward);
            Quaternion cachedGripLocalRotation =
                (Quaternion)GetField(grip, "_gripLocalRotation");
            Assert.That(forwardError, Is.LessThanOrEqualTo(MaxPalmForwardError),
                context + " 掌框前向误差 " + forwardError.ToString("F2") + "°" +
                "; 握持前误差=" + Vector3.Angle(animatedForward, anchor.forward).ToString("F2") +
                "; anchorLocal=" + FormatQuaternion(anchor.localRotation) +
                "; cachedGripLocal=" + FormatQuaternion(cachedGripLocalRotation));

            float twistAngle = Mathf.Abs(Vector3.SignedAngle(
                Vector3.ProjectOnPlane(actualUp, anchor.forward),
                anchor.up,
                anchor.forward));
            float animatedTwistAngle = Mathf.Abs(Vector3.SignedAngle(
                Vector3.ProjectOnPlane(animatedUp, anchor.forward),
                anchor.up,
                anchor.forward));
            Assert.That(twistAngle, Is.LessThanOrEqualTo(MaxWristTwist),
                context + " 腕扭转 " + twistAngle.ToString("F2") + "°" +
                "; 握持前扭转=" + animatedTwistAngle.ToString("F2") +
                "; anchorLocal=" + FormatQuaternion(anchor.localRotation));
            Assert.That(Vector3.Dot(actualUp, anchor.up), Is.GreaterThanOrEqualTo(MinPalmUpDot),
                context + " 翻掌保护 Dot(up)=" + Vector3.Dot(actualUp, anchor.up).ToString("F3"));

            Quaternion correction = hand.rotation * Quaternion.Inverse(animatedRotation);
            // SPEC-001 第三、五节：twist 轴取动画掌框 +Z，swing = correction * inverse(twist)，
            // twist ≤ 25°、swing ≤ 35°，再按 swing * twist 合成。测试端必须复算同一套定义，
            // 不能用别的角度拆分冒充。
            Quaternion correctionSwing;
            Quaternion correctionTwist;
            DecomposeCorrection(correction, animatedForward,
                out correctionSwing, out correctionTwist);
            float swing = Quaternion.Angle(Quaternion.identity, correctionSwing);
            float twist = Quaternion.Angle(Quaternion.identity, correctionTwist);
            Assert.That(swing, Is.LessThanOrEqualTo(MaxCorrectionSwing),
                context + " 程序修正 swing " + swing.ToString("F2") + "°");
            Assert.That(twist, Is.LessThanOrEqualTo(MaxCorrectionTwist),
                context + " 程序修正 twist " + twist.ToString("F2") + "°");
            Quaternion expectedRotation = ClampCorrection(correctionSwing, correctionTwist) *
                animatedRotation;
            // KTwoBoneIK 以 Slerp(原腕姿, 修正后腕姿, rotWeight) 落地，rotWeight=_gripWeight；
            // 按同一公式复算，才能在权重不是 1 时也严格等价。
            float gripWeight = (float)GetField(grip, "_gripWeight");
            Quaternion blendedRotation = Quaternion.Slerp(
                animatedRotation, expectedRotation, Mathf.Clamp01(gripWeight));
            float appliedError = Quaternion.Angle(hand.rotation, blendedRotation);
            Assert.That(appliedError, Is.LessThanOrEqualTo(MaxCorrectionAppliedError),
                context + " 实际腕部修正与 SPEC-001 定义不等价，误差 " +
                appliedError.ToString("F2") + "°");

            VerifyFingerPose(body, weaponData, isFiring, context);
        }

        private static void VerifyFingerPose(
            Animator body, object weaponData, bool isFiring, string context)
        {
            object ikData = weaponData.GetType().GetProperty("Ik").GetValue(weaponData);
            object poseData = ikData.GetType().GetProperty("ThirdPersonFingerPose")
                .GetValue(ikData);
            object leftPose = poseData.GetType().GetProperty("LeftGripPose").GetValue(poseData);
            // 第三人称开火也保持自然待机握姿，避免后握把附近每发硬切指骨。
            object rightPose = poseData.GetType()
                .GetProperty("RightIdlePose")
                .GetValue(poseData);

            EnsureFingerMuscleIndices();
            using (HumanPoseHandler handler = new HumanPoseHandler(body.avatar, body.transform))
            {
                HumanPose pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                VerifyFingerSet("Left", leftPose, pose.muscles, context);
                VerifyFingerSet("Right", rightPose, pose.muscles, context);
            }
        }

        private static void VerifyFingerSet(
            string side, object fingerPose, float[] muscles, string context)
        {
            int muscleCursor = side == "Left" ? 0 : 20;
            for (int finger = 0; finger < FingerNames.Length; finger++)
            {
                object fingerData = fingerPose.GetType().GetProperty(FingerNames[finger])
                    .GetValue(fingerPose);
                Type fingerType = fingerData.GetType();
                for (int component = 0; component < FingerComponents.Length; component++)
                {
                    float expected = (float)fingerType.GetProperty(FingerComponents[component])
                        .GetValue(fingerData);
                    int muscleIndex = _fingerMuscleIndices[muscleCursor + component];
                    Assert.That(muscleIndex, Is.GreaterThanOrEqualTo(0),
                        context + " " + side + " " + FingerNames[finger] + " " +
                        FingerComponents[component] + " 缺少 muscle 映射");
                    float actual = muscles[muscleIndex];
                    Assert.That(Mathf.Abs(actual - expected),
                        Is.LessThanOrEqualTo(MaxMuscleError + 0.000001f),
                        context + " " + side + " " + FingerNames[finger] + " " +
                        FingerComponents[component] + " muscle 误差 " +
                        Mathf.Abs(actual - expected).ToString("F3") +
                        "; expected=" + expected.ToString("F6") +
                        "; actual=" + actual.ToString("F6"));
                }
                muscleCursor += 4;
            }
        }

        private static void AssertFingerPoseEnabled(object weaponData, string context)
        {
            object ikData = weaponData.GetType().GetProperty("Ik").GetValue(weaponData);
            object poseData = ikData.GetType().GetProperty("ThirdPersonFingerPose")
                .GetValue(ikData);
            bool useLeft = (bool)poseData.GetType().GetProperty("UseLeftGripPose")
                .GetValue(poseData);
            bool useRight = (bool)poseData.GetType().GetProperty("UseRightGripPose")
                .GetValue(poseData);
            Assert.That(useLeft, Is.True, context + " 七把正式枪必须启用左手程序指骨");
            Assert.That(useRight, Is.True, context + " 七把正式枪必须启用右手程序指骨");
        }

        private static IEnumerator VerifyAnimationTakeover(
            Component action,
            Type actionType,
            Type actionEnumType,
            Type cancelEnumType,
            Component grip,
            MethodInfo lateUpdate,
            Animator body,
            string actionName,
            string context)
        {
            object actionValue = Enum.Parse(actionEnumType, actionName);
            CancelAction(action, actionType, cancelEnumType);
            bool started = (bool)actionType.GetMethod("TryStartAction").Invoke(
                action, new object[] { actionValue, false });
            // 启动失败必须直接判失败；此前 yield break 会把"没验证"伪装成通过。
            Assert.That(started, Is.True,
                context + " 无法进入 " + actionName + " 动作，动画接管未被验证");

            try
            {
                body.Play("Idle", 0, 0f);
                body.Update(0f);
                AssertNoGripWrite(body, grip, lateUpdate, context + "/" + actionName);
                yield return null;
            }
            finally
            {
                CancelAction(action, actionType, cancelEnumType);
            }
        }

        /// <summary>
        /// 断言该状态下控制器完全不写入：左腕位置、旋转、指骨 muscle 都没有被程序改动。
        /// 动画接管、死亡、Animator 禁用、武器缺失、锚点配置错误共用这一条判据。
        /// </summary>
        private static void AssertNoGripWrite(
            Animator body, Component grip, MethodInfo lateUpdate, string context)
        {
            Transform hand = body.GetBoneTransform(HumanBodyBones.LeftHand);
            Assert.That(hand, Is.Not.Null, context + " 缺少左手骨");
            Vector3 positionBefore = hand.position;
            Quaternion rotationBefore = hand.rotation;
            float[] musclesBefore = ReadFingerMuscles(body);

            InvokeGrip(lateUpdate, grip);

            Assert.That(Vector3.Distance(positionBefore, hand.position),
                Is.LessThanOrEqualTo(MaxTakeoverPositionDelta),
                context + " 左腕位置被程序写入");
            Assert.That(Quaternion.Angle(rotationBefore, hand.rotation),
                Is.LessThanOrEqualTo(MaxTakeoverRotationDelta),
                context + " 左腕旋转被程序写入");
            float[] musclesAfter = ReadFingerMuscles(body);
            for (int i = 0; i < musclesBefore.Length; i++)
            {
                Assert.That(Mathf.Abs(musclesBefore[i] - musclesAfter[i]),
                    Is.LessThanOrEqualTo(MaxTakeoverMuscleDelta),
                    context + " 指骨 muscle 被程序写入");
            }
        }

        private static void InvokeGrip(MethodInfo lateUpdate, Component grip)
        {
            lateUpdate.Invoke(grip, null);
        }

        private static void CacheNeutralGrip(Component grip)
        {
            MethodInfo method = grip.GetType().GetMethod("CacheNeutralGrip");
            Assert.That(method, Is.Not.Null, "PlayerThirdPersonGripCtrl 缺少 CacheNeutralGrip");
            method.Invoke(grip, null);
        }

        /// <summary>
        /// 正向判据：控制器已缓存握持目标，且左腕真的贴到锚点。用于确认上一个
        /// 状态结束后握持重新生效，避免后续"不写入"断言在早已失效的状态上空转。
        /// </summary>
        private static void AssertGripActive(
            Animator body, Component grip, MethodInfo lateUpdate, Transform anchor, string context)
        {
            body.Play("Idle", 0, 0f);
            body.Update(0f);
            InvokeGrip(lateUpdate, grip);
            Assert.That((bool)GetField(grip, "_hasGripTarget"), Is.True,
                context + " 控制器没有缓存握持目标");
            Transform hand = body.GetBoneTransform(HumanBodyBones.LeftHand);
            Assert.That(hand, Is.Not.Null, context + " 缺少左手骨");
            float distance = Vector3.Distance(hand.position, anchor.position);
            Assert.That(distance, Is.LessThanOrEqualTo(MaxPositionError),
                context + " 握持未生效，左腕位置误差 " + distance.ToString("F4") + "m");
        }

        private static void SetField(Component component, string fieldName, object value)
        {
            FieldInfo field = component.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, component.GetType().Name + " 缺少字段 " + fieldName);
            field.SetValue(component, value);
        }

        private static void CancelAction(Component action, Type actionType, Type cancelEnumType)
        {
            actionType.GetMethod("CancelCurrentAction").Invoke(
                action,
                new object[] { Enum.Parse(cancelEnumType, "Manual"), true });
        }

        // 与 PlayerThirdPersonGripCtrl.ClampWristCorrection 同一套拆解：
        // twist 取 correction 在动画掌框 +Z 上的投影，swing 为 correction * inverse(twist)。
        private static void DecomposeCorrection(
            Quaternion correction,
            Vector3 twistAxisWorld,
            out Quaternion swing,
            out Quaternion twist)
        {
            if (correction.w < 0f)
            {
                correction = new Quaternion(
                    -correction.x, -correction.y, -correction.z, -correction.w);
            }

            Vector3 axis = twistAxisWorld.sqrMagnitude > 0.000001f
                ? twistAxisWorld.normalized
                : Vector3.forward;
            float projected = correction.x * axis.x + correction.y * axis.y +
                correction.z * axis.z;
            twist = new Quaternion(
                axis.x * projected, axis.y * projected, axis.z * projected, correction.w);
            float twistSqr = twist.x * twist.x + twist.y * twist.y +
                twist.z * twist.z + twist.w * twist.w;
            if (twistSqr < 0.00000001f)
            {
                twist = Quaternion.identity;
            }
            else
            {
                float inverseLength = 1f / Mathf.Sqrt(twistSqr);
                twist = new Quaternion(
                    twist.x * inverseLength, twist.y * inverseLength,
                    twist.z * inverseLength, twist.w * inverseLength);
            }

            swing = correction * Quaternion.Inverse(twist);
        }

        private static Quaternion ClampCorrection(Quaternion swing, Quaternion twist)
        {
            float swingAngle = Quaternion.Angle(Quaternion.identity, swing);
            if (swingAngle > SpecMaxWristSwing)
            {
                swing = Quaternion.Slerp(
                    Quaternion.identity, swing, SpecMaxWristSwing / swingAngle);
            }

            float twistAngle = Quaternion.Angle(Quaternion.identity, twist);
            if (twistAngle > SpecMaxWristTwist)
            {
                twist = Quaternion.Slerp(
                    Quaternion.identity, twist, SpecMaxWristTwist / twistAngle);
            }

            return swing * twist;
        }

        private static void VerifyWeaponOrder(
            Component inventory,
            PropertyInfo slotCountProperty,
            PropertyInfo resourceIdsProperty)
        {
            int slotCount = (int)slotCountProperty.GetValue(inventory);
            Assert.That(slotCount, Is.EqualTo(ExpectedWeaponResourceIds.Length),
                "枪械栏容量必须等于七把正式枪，实际 " + slotCount);
            object resourceIdsValue = resourceIdsProperty.GetValue(inventory);
            IReadOnlyList<string> resourceIds = resourceIdsValue as IReadOnlyList<string>;
            Assert.That(resourceIds, Is.Not.Null, "枪械栏资源 ID 列表不可读");
            Assert.That(resourceIds.Count, Is.EqualTo(ExpectedWeaponResourceIds.Length),
                "枪械栏资源 ID 数量不符，实际 " + resourceIds.Count);
            for (int i = 0; i < ExpectedWeaponResourceIds.Length; i++)
            {
                Assert.That(resourceIds[i], Is.EqualTo(ExpectedWeaponResourceIds[i]),
                    "枪械栏第 " + i + " 把资源 ID 不符，实际 " + resourceIds[i]);
            }
        }

        private static void AssertLeftHandIkPath(object weaponData, string context)
        {
            object ikData = weaponData.GetType().GetProperty("Ik").GetValue(weaponData);
            Assert.That(ikData, Is.Not.Null, context + " 缺少 Ik 数据");
            object path = ikData.GetType().GetProperty("LeftHandIkPointPath").GetValue(ikData);
            Assert.That(path as string, Is.EqualTo(LeftAnchorPath),
                context + " SO 的 LeftHandIkPointPath 必须等于 " + LeftAnchorPath);
        }

        private static void VerifyTargetResolved(
            Component grip,
            Type targetType,
            Transform anchor,
            Transform newWeapon,
            object weaponData,
            GameObject previousWeapon,
            string context)
        {
            // SPEC-001 第十节：切枪后必须证明锚点属于新枪实例，不能只比位置——
            // 两把枪锚点同位置也会蒙过去，所以用实例引用与 instanceID 判。
            Assert.That(previousWeapon, Is.Not.Null,
                context + " 缺少上一把枪实例，无法证明切枪换了实例");
            Assert.That(newWeapon.gameObject, Is.Not.SameAs(previousWeapon),
                context + " 切枪后第三人称武器实例未更换");
            Assert.That(newWeapon.gameObject.GetInstanceID(),
                Is.Not.EqualTo(previousWeapon.GetInstanceID()),
                context + " 切枪后第三人称武器 instanceID 未变");
            Assert.That(anchor.IsChildOf(newWeapon), Is.True,
                context + " 锚点不属于新枪实例层级");
            object resolvedWeapon = GetField(grip, "_resolvedWeapon");
            Assert.That(resolvedWeapon, Is.SameAs(newWeapon),
                context + " 控制器缓存的武器实例不是新枪");
            object ikData = weaponData.GetType().GetProperty("Ik").GetValue(weaponData);
            object resolvedIkData = GetField(grip, "_resolvedIkData");
            Assert.That(resolvedIkData, Is.SameAs(ikData),
                context + " 控制器缓存的 Ik 数据不是新枪的数据");

            object target = ReadCachedTarget(grip, targetType);
            Assert.That(target, Is.Not.Null,
                context + " 控制器未缓存 ThirdPersonGripTarget，无法满足两帧重解析判据");
            object source = targetType.GetProperty("Source").GetValue(target);
            Assert.That(source.ToString(), Is.EqualTo("ExplicitAnchor"),
                context + " TargetSource=" + source);
            Vector3 position = (Vector3)targetType.GetProperty("Position").GetValue(target);
            float distance = Vector3.Distance(position, anchor.position);
            Assert.That(distance, Is.LessThanOrEqualTo(0.001f),
                context + " 缓存锚点仍属于旧枪，距离新锚点 " + distance.ToString("F4") + "m");
        }

        private static bool IsActionEnabled(Component action, Type actionEnumType, string actionName)
        {
            PropertyInfo actionSetProperty = action.GetType().GetProperty("CurrentActionSet");
            Assert.That(actionSetProperty, Is.Not.Null,
                "WeaponActionCtrl 缺少 CurrentActionSet，无法判定动作是否启用");
            object actionSet = actionSetProperty.GetValue(action);
            Assert.That(actionSet, Is.Not.Null, "当前没有动作集，无法判定动作是否启用");
            MethodInfo tryGetAction = actionSet.GetType().GetMethod("TryGetAction");
            Assert.That(tryGetAction, Is.Not.Null, "WeaponActionSetSO 缺少 TryGetAction");
            object[] arguments = { Enum.Parse(actionEnumType, actionName), null };
            bool found = (bool)tryGetAction.Invoke(actionSet, arguments);
            if (!found || arguments[1] == null)
            {
                return false;
            }
            object enabled = arguments[1].GetType().GetProperty("Enabled").GetValue(arguments[1]);
            return (bool)enabled;
        }

        private static object ReadCachedTarget(Component grip, Type targetType)
        {
            FieldInfo[] fields = grip.GetType().GetFields(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].FieldType == targetType)
                {
                    return fields[i].GetValue(grip);
                }
            }

            PropertyInfo[] properties = grip.GetType().GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < properties.Length; i++)
            {
                if (properties[i].PropertyType == targetType)
                {
                    return properties[i].GetValue(grip);
                }
            }

            return null;
        }

        private static float[] ReadFingerMuscles(Animator body)
        {
            EnsureFingerMuscleIndices();
            float[] values = new float[_fingerMuscleIndices.Length];
            using (HumanPoseHandler handler = new HumanPoseHandler(body.avatar, body.transform))
            {
                HumanPose pose = new HumanPose();
                handler.GetHumanPose(ref pose);
                for (int i = 0; i < values.Length; i++)
                {
                    values[i] = pose.muscles[_fingerMuscleIndices[i]];
                }
            }
            return values;
        }

        private static void EnsureFingerMuscleIndices()
        {
            if (_fingerMuscleIndices != null)
            {
                return;
            }

            _fingerMuscleIndices = new int[40];
            _fingerMuscleLabels = new string[40];
            int cursor = 0;
            for (int side = 0; side < 2; side++)
            {
                string sideName = side == 0 ? "Left" : "Right";
                for (int finger = 0; finger < FingerNames.Length; finger++)
                {
                    for (int component = 0; component < FingerComponents.Length; component++)
                    {
                        string muscleName = FingerSegments[component] == "Spread"
                            ? sideName + " " + FingerNames[finger] + " Spread"
                            : sideName + " " + FingerNames[finger] + " " +
                                FingerSegments[component] + " Stretched";
                        _fingerMuscleLabels[cursor] = muscleName;
                        _fingerMuscleIndices[cursor] = FindMuscle(muscleName);
                        cursor++;
                    }
                }
            }
        }

        private static int FindMuscle(string muscleName)
        {
            string[] names = HumanTrait.MuscleName;
            for (int i = 0; i < names.Length; i++)
            {
                if (string.Equals(names[i], muscleName, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            return -1;
        }

        private static string GetWeaponName(object weaponData)
        {
            return ReadWeaponBasicString(weaponData, "WeaponName");
        }

        private static string GetWeaponId(object weaponData)
        {
            return ReadWeaponBasicString(weaponData, "WeaponId");
        }

        private static string FormatQuaternion(Quaternion value)
        {
            return "(" + value.x.ToString("F6") + "," + value.y.ToString("F6") + "," +
                value.z.ToString("F6") + "," + value.w.ToString("F6") + ")";
        }

        private static string ReadWeaponBasicString(object weaponData, string propertyName)
        {
            if (weaponData == null)
            {
                return "unknown";
            }
            object basic = weaponData.GetType().GetProperty("Basic").GetValue(weaponData);
            if (basic == null)
            {
                return "unknown";
            }
            object value = basic.GetType().GetProperty(propertyName).GetValue(basic);
            return value as string ?? "unknown";
        }

        private static Component FindEnabledPlayer(Type inputType)
        {
            UnityEngine.Object[] inputs = UnityEngine.Object.FindObjectsByType(
                inputType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] is Behaviour behaviour && behaviour.enabled)
                {
                    return inputs[i] as Component;
                }
            }
            return null;
        }

        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, "缺少类型 " + typeName);
            return type;
        }

        private static Transform FindChild(Transform root, string childName)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == childName)
                {
                    return child;
                }
            }
            return null;
        }

        private static void DisableSceneAI()
        {
            Type aiType = Type.GetType("AIPlayerCtrl, Assembly-CSharp");
            if (aiType == null)
            {
                return;
            }
            UnityEngine.Object[] agents = UnityEngine.Object.FindObjectsByType(
                aiType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < agents.Length; i++)
            {
                if (agents[i] is Behaviour behaviour)
                {
                    behaviour.enabled = false;
                }
            }
        }

        private static object GetField(object component, string fieldName)
        {
            FieldInfo field = component.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, component.GetType().Name + " 缺少字段 " + fieldName);
            return field.GetValue(component);
        }
    }
}
