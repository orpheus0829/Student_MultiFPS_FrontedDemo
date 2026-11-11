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
    public sealed class PlayerCharacterModelPlayModeTests
    {
        [UnityTest]
        public IEnumerator AutomaticFireKeepsRightHandStableRelativeToWeapon()
        {
            // Keep motion sampling at a deterministic 60 Hz even when the Editor test
            // runner renders slowly. Raw per-frame pose derivatives are meaningless at
            // the 8-15 FPS occasionally seen while Addressables stream in the test.
            Time.captureFramerate = 60;
            AsyncOperation load = SceneManager.LoadSceneAsync("example", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;
            DisableSceneAI();

            Type inputType = Type.GetType("PlayerInputCtrl, Assembly-CSharp");
            Type actionType = Type.GetType("WeaponActionCtrl, Assembly-CSharp");
            Type combatType = Type.GetType("WeaponCombatCtrl, Assembly-CSharp");
            Type kinemationType = Type.GetType("PlayerKinemationViewCtrl, Assembly-CSharp");
            Type phaseType = Type.GetType("PlayerInputPhase, Assembly-CSharp");
            Type fireModeType = Type.GetType("WeaponFireMode, Assembly-CSharp");
            Assert.That(inputType, Is.Not.Null);
            Assert.That(actionType, Is.Not.Null);
            Assert.That(combatType, Is.Not.Null);
            Assert.That(kinemationType, Is.Not.Null);
            Assert.That(phaseType, Is.Not.Null);
            Assert.That(fireModeType, Is.Not.Null);

            Behaviour input = null;
            foreach (UnityEngine.Object candidate in UnityEngine.Object.FindObjectsByType(
                inputType, FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (candidate is Behaviour behaviour && behaviour.enabled)
                {
                    input = behaviour;
                    break;
                }
            }
            Assert.That(input, Is.Not.Null, "正式场景缺少本地玩家输入控制器");
            Component action = input.GetComponent(actionType);
            Component combat = input.GetComponent(combatType);
            Component kinemation = input.GetComponent(kinemationType);
            Assert.That(action, Is.Not.Null);
            Assert.That(combat, Is.Not.Null);
            Assert.That(kinemation, Is.Not.Null);
            input.enabled = false;

            MethodInfo loadWeapon = actionType.GetMethod("LoadWeaponAsync");
            MethodInfo receiveFire = combatType.GetMethod("ReceiveFireInput");
            MethodInfo setFireMode = combatType.GetMethod("TrySetFireMode");
            MethodInfo tryReload = combatType.GetMethod("TryReload");
            PropertyInfo currentAction = actionType.GetProperty("CurrentAction");
            PropertyInfo actionProgress = actionType.GetProperty("Progress");
            PropertyInfo magazineAmmo = combatType.GetProperty("MagazineAmmo");
            object started = Enum.Parse(phaseType, "Started");
            object canceled = Enum.Parse(phaseType, "Canceled");
            object automatic = Enum.Parse(fireModeType, "Automatic");
            FieldInfo weaponBoneField = kinemationType.GetField(
                "_weaponBone", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo rightArmField = kinemationType.GetField(
                "_rightArm", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo lockWeightField = kinemationType.GetField(
                "_rightHandLockWeight", BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo currentIkWeightField = kinemationType.GetField(
                "_currentIkWeight", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(weaponBoneField, Is.Not.Null);
            Assert.That(rightArmField, Is.Not.Null);
            Assert.That(lockWeightField, Is.Not.Null);
            Assert.That(currentIkWeightField, Is.Not.Null);

            string[] weapons = { "weapon.ak.data", "weapon.pdw90.data" };
            foreach (string weapon in weapons)
            {
                Task<bool> task = loadWeapon.Invoke(action, new object[] { weapon }) as Task<bool>;
                Assert.That(task, Is.Not.Null);
                float deadline = Time.realtimeSinceStartup + 12f;
                while (!task.IsCompleted && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
                Assert.That(task.IsCompletedSuccessfully && task.Result, Is.True,
                    weapon + " 加载失败");
                deadline = Time.realtimeSinceStartup + 6f;
                while (currentAction.GetValue(action) != null &&
                    Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
                for (int frame = 0; frame < 45; frame++)
                {
                    yield return null;
                }
                Assert.That((bool)setFireMode.Invoke(combat, new[] { automatic }), Is.True,
                    weapon + " 无法切换到全自动");

                Transform weaponBone = weaponBoneField.GetValue(kinemation) as Transform;
                object rightArm = rightArmField.GetValue(kinemation);
                Transform hand = rightArm.GetType().GetField("tip").GetValue(rightArm) as Transform;
                Assert.That(weaponBone && hand, Is.True, weapon + " 缺少右手或枪骨");
                GetRelativePose(weaponBone, hand, out Vector3 previousPosition,
                    out Quaternion previousRotation);
                int ammoBefore = (int)magazineAmmo.GetValue(combat);
                receiveFire.Invoke(combat, new[] { started });

                float maximumStepPosition = 0f;
                float maximumStepRotation = 0f;
                // Editor PlayMode test frame rate can be much lower during asset streaming.
                // Leave enough wall-clock-equivalent frames to observe at least 12 AK shots.
                for (int frame = 0; frame < 360; frame++)
                {
                    yield return new WaitForEndOfFrame();
                    GetRelativePose(weaponBone, hand, out Vector3 position,
                        out Quaternion rotation);
                    maximumStepPosition = Mathf.Max(maximumStepPosition,
                        Vector3.Distance(previousPosition, position));
                    maximumStepRotation = Mathf.Max(maximumStepRotation,
                        Quaternion.Angle(previousRotation, rotation));
                    previousPosition = position;
                    previousRotation = rotation;
                    if (frame >= 2)
                    {
                        Assert.That((float)lockWeightField.GetValue(kinemation),
                            Is.EqualTo(1f).Within(0.0001f),
                            weapon + " 连射期间右手锁定权重没有保持为 1");
                    }
                    if (ammoBefore - (int)magazineAmmo.GetValue(combat) >= 12)
                    {
                        break;
                    }
                }
                int shots = ammoBefore - (int)magazineAmmo.GetValue(combat);
                Assert.That(shots, Is.GreaterThanOrEqualTo(12),
                    weapon + " 连射不足 12 发，无法形成有效验收");
                Assert.That(maximumStepPosition, Is.LessThanOrEqualTo(0.001f),
                    weapon + " 右手相对枪骨单帧跳变 " +
                    (maximumStepPosition * 1000f).ToString("F3") + "mm");
                Assert.That(maximumStepRotation, Is.LessThanOrEqualTo(1f),
                    weapon + " 右手相对枪骨单帧跳变 " +
                    maximumStepRotation.ToString("F3") + "°");

                receiveFire.Invoke(combat, new[] { canceled });
                float previousWeight = (float)lockWeightField.GetValue(kinemation);
                deadline = Time.realtimeSinceStartup + 3f;
                while (previousWeight > 0.0001f &&
                    Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    float weight = (float)lockWeightField.GetValue(kinemation);
                    Assert.That(weight, Is.LessThanOrEqualTo(previousWeight + 0.0001f),
                        weapon + " 停火后右手锁定权重反向增加");
                    previousWeight = weight;
                }
                Assert.That(previousWeight, Is.EqualTo(0f).Within(0.0001f),
                    weapon + " 停火后仍未退出右手锁定");

                bool akReload = weapon == "weapon.ak.data";
                Animator handAnimator = hand.GetComponentInParent<Animator>();
                Assert.That(handAnimator, Is.Not.Null, weapon + " 右手缺少第一人称 Animator");
                List<Vector3> reloadHandPositions = new List<Vector3>();
                List<Quaternion> reloadHandRotations = new List<Quaternion>();
                List<float> reloadSampleProgress = new List<float>();
                reloadHandPositions.Add(
                    handAnimator.transform.InverseTransformPoint(hand.position));
                reloadHandRotations.Add(
                    Quaternion.Inverse(handAnimator.transform.rotation) * hand.rotation);
                reloadSampleProgress.Add(-1f);

                Assert.That((bool)tryReload.Invoke(combat, null), Is.True,
                    weapon + " 有空余弹位却无法开始换弹");
                object startedReloadAction = currentAction.GetValue(action);
                Assert.That(startedReloadAction, Is.Not.Null,
                    weapon + " TryReload 返回成功却没有进入换弹动作");
                Assert.That(startedReloadAction.GetType().GetProperty("ActionType")
                        .GetValue(startedReloadAction).ToString(),
                    Does.Contain("Reload"), weapon + " 没有进入 Reload 动作");
                float previousReloadIk =
                    (float)currentIkWeightField.GetValue(kinemation);
                int reloadFrames = 0;
                deadline = Time.realtimeSinceStartup + 45f;
                while (currentAction.GetValue(action) != null &&
                    Time.realtimeSinceStartup < deadline)
                {
                    yield return new WaitForEndOfFrame();
                    reloadFrames++;
                    reloadHandPositions.Add(
                        handAnimator.transform.InverseTransformPoint(hand.position));
                    reloadHandRotations.Add(
                        Quaternion.Inverse(handAnimator.transform.rotation) * hand.rotation);
                    object activeReloadAction = currentAction.GetValue(action);
                    reloadSampleProgress.Add(activeReloadAction != null
                        ? (float)actionProgress.GetValue(action)
                        : -2f);
                    if (activeReloadAction == null)
                    {
                        break;
                    }

                    float reloadIk =
                        (float)currentIkWeightField.GetValue(kinemation);
                    if (akReload)
                    {
                        Assert.That(reloadIk,
                            Is.LessThanOrEqualTo(previousReloadIk + 0.0001f),
                            weapon + " 换弹进入阶段 IK 权重反向增加");
                        if (reloadFrames >= 8)
                        {
                            Assert.That(reloadIk, Is.EqualTo(0f).Within(0.0001f),
                                weapon + " 换弹 CrossFade 后程序 IK 仍在抢写手腕");
                        }
                        Assert.That((float)lockWeightField.GetValue(kinemation),
                            Is.EqualTo(0f).Within(0.0001f),
                            weapon + " 换弹期间错误重启了连射右手锁定");
                    }
                    else
                    {
                        Assert.That(reloadIk, Is.EqualTo(1f).Within(0.0001f),
                            weapon + " 正常换弹不应被 AK 专项保护改变");
                    }
                    previousReloadIk = reloadIk;
                }
                Assert.That(reloadHandPositions.Count, Is.GreaterThan(10),
                    weapon + " 换弹动作没有形成有效采样");
                Assert.That(currentAction.GetValue(action), Is.Null,
                    weapon + " 换弹动作超时");
                float previousRestoreIk =
                    (float)currentIkWeightField.GetValue(kinemation);
                for (int frame = 0; frame < 20; frame++)
                {
                    yield return new WaitForEndOfFrame();
                    reloadHandPositions.Add(
                        handAnimator.transform.InverseTransformPoint(hand.position));
                    reloadHandRotations.Add(
                        Quaternion.Inverse(handAnimator.transform.rotation) * hand.rotation);
                    reloadSampleProgress.Add(-3f);
                    float restoreIk =
                        (float)currentIkWeightField.GetValue(kinemation);
                    Assert.That(restoreIk,
                        Is.GreaterThanOrEqualTo(previousRestoreIk - 0.0001f),
                        weapon + " 换弹结束后 IK 权重没有单调恢复");
                    previousRestoreIk = restoreIk;
                }
                Assert.That(previousRestoreIk, Is.EqualTo(1f).Within(0.0001f),
                    weapon + " 换弹完成后程序 IK 没有恢复");

                float maximumPositionSecondDifference = 0f;
                float maximumRotationStepChange = 0f;
                int maximumPositionFrame = -1;
                int maximumRotationFrame = -1;
                for (int frame = 2; frame < reloadHandPositions.Count; frame++)
                {
                    Vector3 previousStep = reloadHandPositions[frame - 1] -
                        reloadHandPositions[frame - 2];
                    Vector3 currentStep = reloadHandPositions[frame] -
                        reloadHandPositions[frame - 1];
                    float positionSecondDifference =
                        Vector3.Distance(previousStep, currentStep);
                    if (positionSecondDifference > maximumPositionSecondDifference)
                    {
                        maximumPositionSecondDifference = positionSecondDifference;
                        maximumPositionFrame = frame;
                    }
                    Quaternion previousRotationStep = Quaternion.Inverse(
                        reloadHandRotations[frame - 2]) * reloadHandRotations[frame - 1];
                    Quaternion currentRotationStep = Quaternion.Inverse(
                        reloadHandRotations[frame - 1]) * reloadHandRotations[frame];
                    float rotationStepChange =
                        Quaternion.Angle(previousRotationStep, currentRotationStep);
                    if (rotationStepChange > maximumRotationStepChange)
                    {
                        maximumRotationStepChange = rotationStepChange;
                        maximumRotationFrame = frame;
                    }
                }
                Assert.That(maximumPositionSecondDifference, Is.LessThanOrEqualTo(0.01f),
                    weapon + " 换弹右手出现高频位置抽动：" +
                    (maximumPositionSecondDifference * 1000f).ToString("F2") +
                    "mm/frame²，样本帧=" + maximumPositionFrame +
                    "，动作进度=" + reloadSampleProgress[maximumPositionFrame].ToString("F3") +
                    "，换弹采样帧=" + reloadFrames +
                    "，总采样帧=" + reloadHandPositions.Count +
                    "，P-2=" + reloadHandPositions[maximumPositionFrame - 2].ToString("F4") +
                    "，P-1=" + reloadHandPositions[maximumPositionFrame - 1].ToString("F4") +
                    "，P=" + reloadHandPositions[maximumPositionFrame].ToString("F4"));
                Assert.That(maximumRotationStepChange, Is.LessThanOrEqualTo(10f),
                    weapon + " 换弹右手出现高频旋转抽动：" +
                    maximumRotationStepChange.ToString("F2") +
                    "°/frame²，样本帧=" + maximumRotationFrame);
            }
            Time.captureFramerate = 0;
        }

        [UnityTest]
        public IEnumerator LocalPlayerCanSwitchEveryCharacterAndReturnToAnbi()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("example", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            DisableSceneAI();

            Type inputType = Type.GetType("PlayerInputCtrl, Assembly-CSharp");
            Type modelType = Type.GetType("PlayerCharacterModelCtrl, Assembly-CSharp");
            Assert.That(inputType, Is.Not.Null);
            Assert.That(modelType, Is.Not.Null);
            UnityEngine.Object[] inputs = UnityEngine.Object.FindObjectsByType(
                inputType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Component model = null;
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] is Behaviour input && input.enabled)
                {
                    model = input.GetComponent(modelType);
                    break;
                }
            }
            Assert.That(model, Is.Not.Null);

            Type kinemationType = Type.GetType("PlayerKinemationViewCtrl, Assembly-CSharp");
            Assert.That(kinemationType, Is.Not.Null);
            Component kinemation = model.GetComponent(kinemationType);
            Assert.That(kinemation, Is.Not.Null);

            Type weaponType = Type.GetType("PlayerWeaponViewCtrl, Assembly-CSharp");
            Assert.That(weaponType, Is.Not.Null);
            Component weaponView = model.GetComponent(weaponType);
            Assert.That(weaponView, Is.Not.Null);
            PropertyInfo firstGunProperty = weaponType.GetProperty("FirstPersonWeapon");
            PropertyInfo thirdGunProperty = weaponType.GetProperty("ThirdPersonWeapon");
            float readyDeadline = Time.realtimeSinceStartup + 20f;
            while ((!(firstGunProperty.GetValue(weaponView) as GameObject) ||
                (bool)modelType.GetProperty("IsSwitching").GetValue(model)) &&
                Time.realtimeSinceStartup < readyDeadline)
            {
                yield return null;
            }
            Assert.That(firstGunProperty.GetValue(weaponView) as GameObject, Is.Not.Null,
                "必须先等第一人称武器真正加载完，再测试模型切换。");

            SkinnedMeshRenderer arms = GetField(model, "_armsRenderer") as SkinnedMeshRenderer;
            Assert.That(arms, Is.Not.Null);
            Dictionary<string, Vector3> originalBoneScales = new Dictionary<string, Vector3>();
            Component firstView = GetField(model, "_firstPersonView") as Component;
            Animator firstAnimator = firstView.GetType().GetProperty("CharacterAnimator")
                .GetValue(firstView) as Animator;
            foreach (Transform bone in firstAnimator.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(bone, Is.Not.Null);
                originalBoneScales[bone.name] = bone.lossyScale;
            }
            Component thirdView = GetField(model, "_thirdPersonView") as Component;
            Assert.That(thirdView, Is.Not.Null);
            Vector3 playerScale = model.transform.lossyScale;

            MethodInfo switchMethod = modelType.GetMethod("SwitchModelAsync");
            PropertyInfo currentModel = modelType.GetProperty("CurrentModelId");
            Assert.That(modelType.GetProperty("ModelCount").GetValue(model), Is.EqualTo(4),
                "退位后的雅不能继续占据玩家可选角色位");
            Task<bool> retiredSwitch = switchMethod.Invoke(model,
                new object[] { "character.miyabi" }) as Task<bool>;
            Assert.That(retiredSwitch, Is.Not.Null);
            Assert.That(retiredSwitch.IsCompleted, Is.True);
            Assert.That(retiredSwitch.Result, Is.False, "退位角色不能通过公开切换入口重新启用");
            string[] ids = {
                "character.ellen", "character.zhuyuan",
                "character.belle", "character.anbi"
            };
            foreach (string id in ids)
            {
                Task<bool> switchTask = switchMethod.Invoke(model, new object[] { id }) as Task<bool>;
                Assert.That(switchTask, Is.Not.Null, id);
                float deadline = Time.realtimeSinceStartup + 15f;
                while (!switchTask.IsCompleted && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                }
                Assert.That(switchTask.IsCompleted, Is.True, id + " 切换超时");
                Assert.That(switchTask.IsFaulted, Is.False, id);
                Assert.That(switchTask.Result, Is.True, id);
                Assert.That(currentModel.GetValue(model) as string, Is.EqualTo(id));
                yield return null;

                GameObject firstGun = firstGunProperty.GetValue(weaponView) as GameObject;
                GameObject thirdGun = thirdGunProperty.GetValue(weaponView) as GameObject;
                Assert.That(firstGun, Is.Not.Null, id + " 缺少第一人称武器");
                Assert.That(firstGun.activeInHierarchy, Is.True, id + " 第一人称武器被隐藏");
                Assert.That(thirdGun, Is.Not.Null, id + " 缺少第三人称武器");
                Transform weaponRoot = thirdView.GetType().GetProperty("WeaponRoot")
                    .GetValue(thirdView) as Transform;
                AssertScaleNear(weaponRoot.lossyScale, playerScale, id + " 武器挂点缩放");
                Assert.That(thirdGun.transform.lossyScale.magnitude / playerScale.magnitude,
                    Is.InRange(0.1f, 5f), id + " 武器被骨骼的厘米单位放大");

                Animator body = thirdView.GetType().GetProperty("CharacterAnimator")
                    .GetValue(thirdView) as Animator;
                Assert.That(body, Is.Not.Null, id);
                Assert.That(body.isHuman && body.enabled && body.gameObject.activeInHierarchy,
                    Is.True, id + " 第三人称骨架没有正常参与动画");
                Assert.That(body.runtimeAnimatorController, Is.Not.Null, id);
                if (id == "character.anbi")
                {
                    // 安比是玩家本体上的 Animator，不是动态挂载的外观 Prefab；
                    // 不能把玩家重挂到自己的第三人称子节点，滑铲由原全身动画驱动。
                    Assert.That(body.transform, Is.SameAs(model.transform),
                        "默认安比必须保留玩家本体上的权威 Animator");
                }
                else
                {
                    Assert.That(body.transform.IsChildOf(thirdView.transform), Is.True,
                        id + " 新模型应跟随第三人称表现根节点的滑铲偏移");
                }
                Assert.That(body.applyRootMotion, Is.False,
                    id + " 角色外观不能独立于玩家移动组件累计 Root Motion");
                Assert.That(body.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate),
                    id + " 离屏时仍需更新武器、命中框及骨骼");
                if (id == "character.zhuyuan")
                {
                    foreach (Transform bone in body.GetComponentsInChildren<Transform>(true))
                    {
                        if (bone.name == "つま先.L" || bone.name == "つま先.R")
                        {
                            HumanBodyBones foot = bone.name.EndsWith(".L")
                                ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot;
                            Assert.That(bone.parent, Is.SameAs(body.GetBoneTransform(foot)),
                                "朱鸢脚趾蒙皮必须随D链脚踝，不能留在未驱动的普通腿链");
                            Assert.That(Vector3.Distance(bone.position, bone.parent.position),
                                Is.InRange(0.05f * Mathf.Abs(playerScale.y),
                                    0.25f * Mathf.Abs(playerScale.y)), "朱鸢鞋长异常");
                        }
                    }
                }
                Vector3 bodyRestPosition = body.transform.localPosition;
                Quaternion bodyRestRotation = body.transform.localRotation;
                body.Play("Sprint", 0, 0f);
                for (int frame = 0; frame < 120; frame++)
                {
                    body.Update(1f / 60f);
                }
                Assert.That(Vector3.Distance(body.transform.localPosition, bodyRestPosition),
                    Is.LessThan(0.001f), id + " 疾跑动画使外观根节点累计偏移");
                Assert.That(Quaternion.Angle(body.transform.localRotation, bodyRestRotation),
                    Is.LessThan(0.1f), id + " 疾跑动画使外观根节点独立旋转");
                body.Play("Idle", 0, 0f);
                body.Update(0f);
                Assert.That(body.humanScale, Is.InRange(0.5f, 1.3f),
                    id + " Avatar 的人形尺寸异常，可能在导入时破坏了骨架单位");
                Transform head = body.GetBoneTransform(HumanBodyBones.Head);
                Transform hips = body.GetBoneTransform(HumanBodyBones.Hips);
                Transform leftFoot = body.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = body.GetBoneTransform(HumanBodyBones.RightFoot);
                Assert.That(head && hips && leftFoot && rightFoot, Is.True, id + " 缺少人体关键骨骼");
                float heightScale = Mathf.Max(0.001f, Mathf.Abs(playerScale.y));
                float headAboveHips = Vector3.Dot(head.position - hips.position, model.transform.up) / heightScale;
                float hipsAboveFeet = Vector3.Dot(hips.position - (leftFoot.position + rightFoot.position) * 0.5f,
                    model.transform.up) / heightScale;
                Assert.That(headAboveHips, Is.InRange(0.2f, 1.3f), id + " 头部与胯部顺序或比例异常");
                Assert.That(hipsAboveFeet, Is.InRange(0.25f, 1.4f), id + " 腿部比例或落地朝向异常");
                Type gripType = Type.GetType("PlayerThirdPersonGripCtrl, Assembly-CSharp");
                Assert.That(gripType, Is.Not.Null);
                Component grip = model.GetComponent(gripType);
                Assert.That(grip, Is.Not.Null, id + " 缺少第三人称支撑手贴合");
                Type actionType = Type.GetType("WeaponActionCtrl, Assembly-CSharp");
                Component action = model.GetComponent(actionType);
                PropertyInfo currentAction = actionType.GetProperty("CurrentAction");
                float actionDeadline = Time.realtimeSinceStartup + 3f;
                while (currentAction.GetValue(action) != null &&
                    Time.realtimeSinceStartup < actionDeadline)
                {
                    yield return null;
                }
                Assert.That(currentAction.GetValue(action), Is.Null,
                    id + " 等待基础持枪姿势超时");
                // 支撑手在 LateUpdate 中贴合；Update 阶段读到的是 Animator 的原始姿势。
                yield return new WaitForEndOfFrame();
                if (id != "character.anbi")
                {
                    AssertFirstPersonFingerSafety(kinemation, id);
                }
                Transform sourceHand = GetField(grip, "_sourceHand") as Transform;
                Transform supportHand = body.GetBoneTransform(HumanBodyBones.LeftHand);
                Assert.That(sourceHand && supportHand, Is.True);
                // 中立抓握点判据已随机制变更删除；握持合格判据迁到
                // PlayerThirdPersonGripPlayModeTests 的位置、掌框前向、扭转与翻掌指标。
                // 这里仅保留粗略防脱枪检查。
                float gunDistance = GetRendererDistance(thirdGun, supportHand.position);
                Assert.That(gunDistance, Is.LessThan(0.35f * heightScale),
                    id + " 支撑手疑似完全脱离武器");
                body.Play("Idle", 0, 0f);
                body.Update(0f);
                Quaternion animatedWrist = supportHand.rotation;
                MethodInfo updateGrip = gripType.GetMethod("LateUpdate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                updateGrip.Invoke(grip, null);
                // SPEC-001 五-6：空路径旧回退枪 rotWeight=0，左腕不得被程序扭转；
                // 七把显式锚点枪允许 ≤35° swing / ≤25° twist 的修正，不能用 1° 判据。
                if (string.IsNullOrWhiteSpace(GetLeftHandIkPointPath(action)))
                {
                    Assert.That(Quaternion.Angle(animatedWrist, supportHand.rotation),
                        Is.LessThan(1f), id + " 旧回退枪左腕被第一人称掌面轴强行扭转");
                }
                yield return VerifySprintGripAndWeaponChange(model, grip, body,
                    weaponView, firstGunProperty, thirdGunProperty, action,
                    currentAction, heightScale, id);
                Assert.That(arms.sharedMesh, Is.Not.Null, id);
                Assert.That(arms.bones.Length, Is.EqualTo(arms.sharedMesh.bindposes.Length), id);
                foreach (Transform bone in arms.bones)
                {
                    Assert.That(bone, Is.Not.Null, id + " 手臂存在空骨骼");
                    Assert.That(originalBoneScales.ContainsKey(bone.name), Is.True, id + " " + bone.name);
                    AssertScaleNear(bone.lossyScale, originalBoneScales[bone.name],
                        id + " 第一人称骨骼 " + bone.name);
                }

                Type hitboxType = Type.GetType("BodyHitbox, Assembly-CSharp");
                Component[] hitboxes = model.GetComponentsInChildren(hitboxType, true);
                Assert.That(hitboxes.Length, Is.GreaterThan(0), id);
                foreach (Component hitbox in hitboxes)
                {
                    Collider shape = hitbox.GetComponent<Collider>();
                    Assert.That(shape, Is.Not.Null, id);
                    AssertColliderSize(shape, playerScale, id + " 命中框");
                }
                Component ragdoll = GetField(model, "_ragdoll") as Component;
                IList corpseShapes = GetField(ragdoll, "_colliders") as IList;
                Assert.That(corpseShapes.Count, Is.GreaterThan(0), id);
                foreach (Collider shape in corpseShapes)
                {
                    AssertColliderSize(shape, playerScale, id + " 布娃娃碰撞体");
                }
            }
        }

        private static object GetField(Component component, string name)
        {
            return component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(component);
        }

        private static void GetRelativePose(
            Transform parent,
            Transform child,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = parent.InverseTransformPoint(child.position);
            rotation = Quaternion.Inverse(parent.rotation) * child.rotation;
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

        private static void AssertFirstPersonFingerSafety(Component kinemation, string context)
        {
            Transform[] bones = GetField(kinemation, "_rightFingerBones") as Transform[];
            Quaternion[] bindRotations =
                GetField(kinemation, "_rightFingerBindRotations") as Quaternion[];
            Vector3[] bindPositions =
                GetField(kinemation, "_rightFingerBindPositions") as Vector3[];
            Vector3[] bindScales = GetField(kinemation, "_rightFingerBindScales") as Vector3[];
            Assert.That(bones, Is.Not.Null, context + " 缺少第一人称右指约束");
            Assert.That(bones.Length, Is.EqualTo(15), context + " 右指骨数量错误");

            float[] swingLimits = { 40f, 50f, 35f };
            float[] twistLimits = { 8f, 5f, 5f };
            for (int i = 0; i < bones.Length; i++)
            {
                Assert.That(bones[i], Is.Not.Null, context + " 缺少右指骨索引 " + i);
                Assert.That(Vector3.Distance(bones[i].localPosition, bindPositions[i]),
                    Is.LessThanOrEqualTo(0.0001f),
                    context + " " + bones[i].name + " 骨长被动画拉伸");
                Assert.That(Vector3.Distance(bones[i].localScale, bindScales[i]),
                    Is.LessThanOrEqualTo(0.0001f),
                    context + " " + bones[i].name + " 被缩放拉伸");

                Quaternion delta = Quaternion.Inverse(bindRotations[i]) * bones[i].localRotation;
                GetSwingTwist(delta, Vector3.right, out float swing, out float twist);
                int joint = i % 3;
                Assert.That(swing, Is.LessThanOrEqualTo(swingLimits[joint] + 0.5f),
                    context + " " + bones[i].name + " 过度弯曲 " + swing.ToString("F2") + "°");
                Assert.That(Mathf.Abs(twist), Is.LessThanOrEqualTo(twistLimits[joint] + 0.5f),
                    context + " " + bones[i].name + " 轴向扭转 " + twist.ToString("F2") + "°");
            }
        }

        private static void GetSwingTwist(
            Quaternion rotation,
            Vector3 twistAxis,
            out float swingAngle,
            out float signedTwist)
        {
            Vector3 vector = new Vector3(rotation.x, rotation.y, rotation.z);
            Vector3 projected = Vector3.Project(vector, twistAxis);
            Quaternion twist = new Quaternion(projected.x, projected.y, projected.z, rotation.w);
            float magnitude = Mathf.Sqrt(
                twist.x * twist.x + twist.y * twist.y + twist.z * twist.z + twist.w * twist.w);
            twist = magnitude > 0.00001f
                ? new Quaternion(
                    twist.x / magnitude,
                    twist.y / magnitude,
                    twist.z / magnitude,
                    twist.w / magnitude)
                : Quaternion.identity;
            Quaternion swing = rotation * Quaternion.Inverse(twist);
            swingAngle = Quaternion.Angle(Quaternion.identity, swing);
            signedTwist = 2f * Mathf.Atan2(
                Vector3.Dot(new Vector3(twist.x, twist.y, twist.z), twistAxis),
                twist.w) * Mathf.Rad2Deg;
            signedTwist = Mathf.DeltaAngle(0f, signedTwist);
        }

        private static string GetLeftHandIkPointPath(Component action)
        {
            PropertyInfo currentWeapon = action.GetType().GetProperty("CurrentWeaponData");
            Assert.That(currentWeapon, Is.Not.Null, "WeaponActionCtrl 缺少 CurrentWeaponData");
            object weaponData = currentWeapon.GetValue(action);
            Assert.That(weaponData, Is.Not.Null, "当前武器数据为空，无法判定锚点来源");
            object ikData = weaponData.GetType().GetProperty("Ik").GetValue(weaponData);
            Assert.That(ikData, Is.Not.Null, "当前武器缺少 Ik 数据");
            object path = ikData.GetType().GetProperty("LeftHandIkPointPath").GetValue(ikData);
            return path as string ?? string.Empty;
        }

        private static IEnumerator VerifySprintGripAndWeaponChange(Component player,
            Component grip, Animator body, Component weaponView, PropertyInfo firstGunProperty,
            PropertyInfo thirdGunProperty, Component action, PropertyInfo currentAction,
            float heightScale, string id)
        {
            Type moveType = Type.GetType("PlayerMoveCtrl, Assembly-CSharp");
            Behaviour move = player.GetComponent(moveType) as Behaviour;
            FieldInfo sprint = moveType.GetField("_isSprinting",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo updateGrip = grip.GetType().GetMethod("LateUpdate",
                BindingFlags.Instance | BindingFlags.NonPublic);
            bool wasEnabled = move.enabled;
            bool wasSprinting = (bool)sprint.GetValue(move);
            Type inputType = Type.GetType("PlayerInputCtrl, Assembly-CSharp");
            Behaviour input = player.GetComponent(inputType) as Behaviour;
            bool inputEnabled = input.enabled;
            input.enabled = false;
            move.enabled = false;
            sprint.SetValue(move, true);
            try
            {
                GameObject originalGun = firstGunProperty.GetValue(weaponView) as GameObject;
                body.Play("Sprint", 0, 0.35f);
                body.Update(0f);
                updateGrip.Invoke(grip, null);
                Transform hand = body.GetBoneTransform(HumanBodyBones.LeftHand);
                GameObject thirdGun = thirdGunProperty.GetValue(weaponView) as GameObject;
                // 仅保留粗略防脱枪检查；正式握持判据见 PlayerThirdPersonGripPlayModeTests。
                Assert.That(GetRendererDistance(thirdGun, hand.position),
                    Is.LessThan(0.35f * heightScale), id + " 疾跑时左手疑似完全脱离武器");

                Type inventoryType = Type.GetType("PlayerWeaponInventoryCtrl, Assembly-CSharp");
                Component inventory = player.GetComponent(inventoryType);
                Assert.That((bool)inventoryType.GetMethod("TryCycle").Invoke(inventory,
                    new object[] { 1 }), Is.True, id + " 持续疾跑切枪未启动");
                float deadline = Time.realtimeSinceStartup + 15f;
                while ((firstGunProperty.GetValue(weaponView) as GameObject == originalGun ||
                    currentAction.GetValue(action) != null) && Time.realtimeSinceStartup < deadline)
                {
                    sprint.SetValue(move, true);
                    yield return null;
                }
                Assert.That(firstGunProperty.GetValue(weaponView) as GameObject,
                    Is.Not.SameAs(originalGun), id + " 持续疾跑切枪超时");
                Assert.That(currentAction.GetValue(action), Is.Null, id + " 切枪动作未结束");
                // 中立抓握点判据已随机制变更删除；握持合格判据迁到
                // PlayerThirdPersonGripPlayModeTests 的位置、掌框前向、扭转与翻掌指标。
                body.Play("Sprint", 0, 0.35f);
                body.Update(0f);
                updateGrip.Invoke(grip, null);
                thirdGun = thirdGunProperty.GetValue(weaponView) as GameObject;
                // 仅保留粗略防脱枪检查；正式握持判据见 PlayerThirdPersonGripPlayModeTests。
                Assert.That(GetRendererDistance(thirdGun, hand.position),
                    Is.LessThan(0.35f * heightScale), id + " 持续疾跑换枪后左手疑似完全脱离武器");
            }
            finally
            {
                sprint.SetValue(move, wasSprinting);
                move.enabled = wasEnabled;
                input.enabled = inputEnabled;
            }
        }

        private static float GetRendererDistance(GameObject root, Vector3 point)
        {
            float nearest = float.PositiveInfinity;
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                nearest = Mathf.Min(nearest,
                    Vector3.Distance(point, renderer.bounds.ClosestPoint(point)));
            }
            return nearest;
        }

        private static void AssertScaleNear(Vector3 actual, Vector3 expected, string message)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(Mathf.Abs(expected.x) * 0.02f + 0.001f), message);
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(Mathf.Abs(expected.y) * 0.02f + 0.001f), message);
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(Mathf.Abs(expected.z) * 0.02f + 0.001f), message);
        }

        private static void AssertColliderSize(Collider shape, Vector3 playerScale, string message)
        {
            Vector3 localSize;
            if (shape is SphereCollider sphere)
            {
                localSize = Vector3.one * sphere.radius * 2f;
            }
            else if (shape is CapsuleCollider capsule)
            {
                localSize = Vector3.one * capsule.radius * 2f;
                localSize[capsule.direction] = capsule.height;
            }
            else if (shape is BoxCollider box)
            {
                localSize = box.size;
            }
            else
            {
                Assert.Fail(message + " 不支持的碰撞体类型 " + shape.GetType().Name);
                return;
            }

            Vector3 worldSize = Vector3.Scale(localSize, shape.transform.lossyScale);
            float relativeSize = worldSize.magnitude / Mathf.Max(0.001f, playerScale.magnitude);
            Assert.That(relativeSize, Is.InRange(0.005f, 3f), message + " 尺寸不合理（含禁用碰撞体）");
        }
    }
}
