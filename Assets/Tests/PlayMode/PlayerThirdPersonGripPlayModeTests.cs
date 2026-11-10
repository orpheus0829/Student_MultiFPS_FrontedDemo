using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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

        private static readonly string[] CharacterIds =
        {
            "character.anbi", "character.ellen", "character.zhuyuan", "character.belle"
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
            MethodInfo tryCycle = inventoryType.GetMethod("TryCycle");
            MethodInfo switchModel = modelType.GetMethod("SwitchModelAsync");
            Assert.That(firstGunProperty, Is.Not.Null);
            Assert.That(thirdGunProperty, Is.Not.Null);
            Assert.That(currentActionProperty, Is.Not.Null);
            Assert.That(currentWeaponProperty, Is.Not.Null);
            Assert.That(isSwitchingProperty, Is.Not.Null);
            Assert.That(tryCycle, Is.Not.Null);
            Assert.That(switchModel, Is.Not.Null);

            yield return WaitForFirstWeapon(weaponView, firstGunProperty, 30f);

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

                for (int slot = 0; slot < 7; slot++)
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

                    GameObject thirdWeapon = thirdGunProperty.GetValue(weaponView) as GameObject;
                    Assert.That(thirdWeapon, Is.Not.Null, characterId + " 缺少第三人称武器实例");
                    Transform targetWeapon = thirdWeapon.transform;
                    Transform anchor = targetWeapon.Find(LeftAnchorPath);
                    string weaponName = GetWeaponName(action, currentWeaponProperty);
                    string context = characterId + "/" + weaponName;
                    Assert.That(anchor, Is.Not.Null,
                        context + " 缺少 " + LeftAnchorPath + "，七把正式枪必须显式锚点");

                    if (slot > 0)
                    {
                        VerifyTargetResolved(grip, targetType, anchor, context + "/切枪");
                    }

                    object weaponData = currentWeaponProperty.GetValue(action);
                    AssertFingerPoseEnabled(weaponData, context);

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

                    yield return VerifyAnimationTakeover(action, actionType, actionEnumType,
                        cancelEnumType, grip, lateUpdate, body, "Reload", context + "/换弹");
                    yield return VerifyAnimationTakeover(action, actionType, actionEnumType,
                        cancelEnumType, grip, lateUpdate, body, "EmptyReload", context + "/空仓换弹");
                    yield return VerifyAnimationTakeover(action, actionType, actionEnumType,
                        cancelEnumType, grip, lateUpdate, body, "Equip", context + "/切出");
                    yield return VerifyAnimationTakeover(action, actionType, actionEnumType,
                        cancelEnumType, grip, lateUpdate, body, "Holster", context + "/收起");
                    yield return VerifyAnimationTakeover(action, actionType, actionEnumType,
                        cancelEnumType, grip, lateUpdate, body, "Inspect", context + "/检视");
                }
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
            float swing = Vector3.Angle(animatedForward, correction * animatedForward);
            Vector3 correctedUp = correction * animatedUp;
            float correctionTwist = Mathf.Abs(Vector3.SignedAngle(
                Vector3.ProjectOnPlane(animatedUp, animatedForward),
                correctedUp,
                animatedForward));
            Assert.That(swing, Is.LessThanOrEqualTo(MaxCorrectionSwing),
                context + " 程序修正 swing " + swing.ToString("F2") + "°");
            Assert.That(correctionTwist, Is.LessThanOrEqualTo(MaxCorrectionTwist),
                context + " 程序修正 twist " + correctionTwist.ToString("F2") + "°");

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
            if (!started)
            {
                yield break;
            }

            try
            {
                body.Play("Idle", 0, 0f);
                body.Update(0f);
                Transform hand = body.GetBoneTransform(HumanBodyBones.LeftHand);
                Assert.That(hand, Is.Not.Null, context + " 缺少左手骨");
                Vector3 positionBefore = hand.position;
                Quaternion rotationBefore = hand.rotation;
                float[] musclesBefore = ReadFingerMuscles(body);

                InvokeGrip(lateUpdate, grip);

                Assert.That(Vector3.Distance(positionBefore, hand.position),
                    Is.LessThanOrEqualTo(MaxTakeoverPositionDelta),
                    context + " 动画接管帧左腕位置被程序写入");
                Assert.That(Quaternion.Angle(rotationBefore, hand.rotation),
                    Is.LessThanOrEqualTo(MaxTakeoverRotationDelta),
                    context + " 动画接管帧左腕旋转被程序写入");
                float[] musclesAfter = ReadFingerMuscles(body);
                for (int i = 0; i < musclesBefore.Length; i++)
                {
                    Assert.That(Mathf.Abs(musclesBefore[i] - musclesAfter[i]),
                        Is.LessThanOrEqualTo(MaxTakeoverMuscleDelta),
                        context + " 动画接管帧指骨 muscle 被程序写入");
                }
                yield return null;
            }
            finally
            {
                CancelAction(action, actionType, cancelEnumType);
            }
        }

        private static void InvokeGrip(MethodInfo lateUpdate, Component grip)
        {
            lateUpdate.Invoke(grip, null);
        }

        private static void CancelAction(Component action, Type actionType, Type cancelEnumType)
        {
            actionType.GetMethod("CancelCurrentAction").Invoke(
                action,
                new object[] { Enum.Parse(cancelEnumType, "Manual"), true });
        }

        private static void VerifyTargetResolved(
            Component grip, Type targetType, Transform anchor, string context)
        {
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

        private static string GetWeaponName(Component action, PropertyInfo currentWeaponProperty)
        {
            object weaponData = currentWeaponProperty.GetValue(action);
            if (weaponData == null)
            {
                return "unknown";
            }
            object basic = weaponData.GetType().GetProperty("Basic").GetValue(weaponData);
            if (basic == null)
            {
                return "unknown";
            }
            object name = basic.GetType().GetProperty("WeaponName").GetValue(basic);
            return name as string ?? "unknown";
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
