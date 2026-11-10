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

        private static int[] _fingerMuscleIndices;
        private static string[] _fingerMuscleLabels;

        [UnityTest]
        public IEnumerator EveryCharacterWeaponAndStateMeetsThirdPersonGrip()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("example", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

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
            Assert.That(forwardError, Is.LessThanOrEqualTo(MaxPalmForwardError),
                context + " 掌框前向误差 " + forwardError.ToString("F2") + "°");

            float twistAngle = Mathf.Abs(Vector3.SignedAngle(
                Vector3.ProjectOnPlane(actualUp, anchor.forward),
                anchor.up,
                anchor.forward));
            Assert.That(twistAngle, Is.LessThanOrEqualTo(MaxWristTwist),
                context + " 腕扭转 " + twistAngle.ToString("F2") + "°");
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
            object rightPose = poseData.GetType()
                .GetProperty(isFiring ? "RightFirePose" : "RightIdlePose")
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
                    Assert.That(Mathf.Abs(actual - expected), Is.LessThanOrEqualTo(MaxMuscleError),
                        context + " " + side + " " + FingerNames[finger] + " " +
                        FingerComponents[component] + " muscle 误差 " +
                        Mathf.Abs(actual - expected).ToString("F3"));
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

        private static object GetField(Component component, string fieldName)
        {
            FieldInfo field = component.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, component.GetType().Name + " 缺少字段 " + fieldName);
            return field.GetValue(component);
        }
    }
}
