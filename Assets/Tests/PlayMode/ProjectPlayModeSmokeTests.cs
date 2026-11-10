using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GunDemo.Tests
{
    public sealed class ProjectPlayModeSmokeTests
    {
        [UnityTest]
        public IEnumerator ActiveSceneCanAdvanceOneFrame()
        {
            yield return null;

            Scene scene = SceneManager.GetActiveScene();
            Assert.That(scene.IsValid(), Is.True, "PlayMode 中没有有效的活动场景");
            Assert.That(scene.isLoaded, Is.True, "PlayMode 活动场景没有完成加载");
        }

        [UnityTest]
        public IEnumerator CameraCanSwitchToThirdPersonAndBack()
        {
            yield return null;

            GameObject player = GameObject.FindWithTag("Player");
            Assert.That(player, Is.Not.Null);
            Component cameraControl = player.GetComponentInChildren(
                GetRuntimeType("FirstPersonCameraCtrl"), true);
            Assert.That(cameraControl, Is.Not.Null);
            Camera viewCamera = cameraControl.GetComponent<Camera>();
            Assert.That(viewCamera, Is.Not.Null);
            MethodInfo setView = cameraControl.GetType().GetMethod("SetThirdPerson");
            Assert.That(setView, Is.Not.Null);
            Assert.That(viewCamera.cullingMask & (1 << 8), Is.Not.Zero,
                "第一人称相机必须看见其他玩家所在的第三人称层");

            try
            {
                setView.Invoke(cameraControl, new object[] { true });
                yield return null;
                Assert.That(GetProperty<bool>(cameraControl, "IsThirdPerson"), Is.True);
                Assert.That(viewCamera.cullingMask & (1 << 8), Is.Not.Zero);
                Assert.That(viewCamera.cullingMask & (1 << 9), Is.Zero);
            }
            finally
            {
                setView.Invoke(cameraControl, new object[] { false });
            }
            Assert.That(viewCamera.cullingMask & (1 << 8), Is.Not.Zero);
        }

        [UnityTest]
        public IEnumerator WeaponInventoryCyclesAfterHolsterAndEquip()
        {
            yield return new WaitForSeconds(1f);

            GameObject player = GameObject.FindWithTag("Player");
            Assert.That(player, Is.Not.Null, "场景中找不到玩家");

            Component inventory = player.GetComponent(GetRuntimeType("PlayerWeaponInventoryCtrl"));
            Component weaponActions = player.GetComponent(GetRuntimeType("WeaponActionCtrl"));
            Assert.That(inventory, Is.Not.Null, "玩家缺少枪械栏");
            Assert.That(weaponActions, Is.Not.Null, "玩家缺少枪械动作组件");

            int startingIndex = GetProperty<int>(inventory, "CurrentIndex");
            bool accepted = Invoke<bool>(inventory, "TryCycle", 1);
            Assert.That(accepted, Is.True, "枪械栏没有接受滚轮切换");
            Assert.That(GetProperty<bool>(inventory, "IsSwitching"), Is.True);

            float timeout = Time.realtimeSinceStartup + 6f;
            while (GetProperty<bool>(inventory, "IsSwitching") &&
                Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            Assert.That(GetProperty<bool>(inventory, "IsSwitching"), Is.False,
                "收枪、换枪、掏枪流程超时");
            Assert.That(GetProperty<int>(inventory, "CurrentIndex"), Is.Not.EqualTo(startingIndex));

            object weaponData = GetProperty<object>(weaponActions, "CurrentWeaponData");
            Assert.That(weaponData, Is.Not.Null, "切换后没有当前枪械数据");
            object basic = GetProperty<object>(weaponData, "Basic");
            Assert.That(GetProperty<string>(basic, "WeaponId"), Is.EqualTo("as-val"));
        }

        [UnityTest]
        public IEnumerator EveryEquippedFirstPersonWeaponBecomesVisible()
        {
            SceneManager.LoadScene("example");
            yield return new WaitForSeconds(1f);

            GameObject player = GameObject.FindWithTag("Player");
            Assert.That(player, Is.Not.Null);
            Component inventory = player.GetComponent(GetRuntimeType("PlayerWeaponInventoryCtrl"));
            Component view = player.GetComponent(GetRuntimeType("PlayerWeaponViewCtrl"));
            Component kinemation = player.GetComponent(GetRuntimeType("PlayerKinemationViewCtrl"));
            Component feedback = player.GetComponent(GetRuntimeType("WeaponFeedbackCtrl"));
            Assert.That(inventory, Is.Not.Null);
            Assert.That(view, Is.Not.Null);

            int count = GetProperty<int>(inventory, "ConfiguredWeaponCount");
            for (int slot = 0; slot < count; slot++)
            {
                if (slot > 0)
                {
                    Assert.That(Invoke<bool>(inventory, "TryCycle", 1), Is.True);
                    float deadline = Time.realtimeSinceStartup + 8f;
                    while (GetProperty<bool>(inventory, "IsSwitching") &&
                        Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                    }
                    Assert.That(GetProperty<bool>(inventory, "IsSwitching"), Is.False,
                        "切换第 " + slot + " 把枪超时");
                }

                yield return null;
                GameObject weapon = GetProperty<GameObject>(view, "FirstPersonWeapon");
                Assert.That(weapon, Is.Not.Null, "第 " + slot + " 把枪没有第一人称实例");
                Renderer[] renderers = weapon.GetComponentsInChildren<Renderer>(true);
                Assert.That(renderers.Length, Is.GreaterThan(0), weapon.name + " 没有渲染器");
                bool visible = false;
                foreach (Renderer renderer in renderers)
                {
                    visible |= renderer.enabled && !renderer.forceRenderingOff &&
                        renderer.gameObject.activeInHierarchy;
                }
                Assert.That(visible, Is.True,
                    weapon.name + " 的渲染器仍全部隐藏；configured=" +
                    GetField<bool>(kinemation, "_hasWeaponConfigured") +
                    " pending=" + GetField<bool>(kinemation, "_showWeaponNextFrame") +
                    " frame=" + GetField<int>(kinemation, "_showWeaponFrame") +
                    " now=" + Time.frameCount);

                float flashDeadline = Time.realtimeSinceStartup + 2f;
                while ((!GetField<GameObject>(feedback, "_firstPersonMuzzleFlash") ||
                    !GetField<GameObject>(feedback, "_thirdPersonMuzzleFlash")) &&
                    Time.realtimeSinceStartup < flashDeadline)
                {
                    yield return null;
                }
                GameObject firstFlash = GetField<GameObject>(feedback, "_firstPersonMuzzleFlash");
                GameObject thirdFlash = GetField<GameObject>(feedback, "_thirdPersonMuzzleFlash");
                Assert.That(firstFlash, Is.Not.Null, weapon.name + " 缺少第一人称火花");
                Assert.That(thirdFlash, Is.Not.Null, weapon.name + " 缺少第三人称火花");
                Assert.That(firstFlash.transform.parent,
                    Is.EqualTo(GetProperty<Transform>(view, "MuzzlePoint")),
                    weapon.name + " 的第一人称火花没有绑定枪口");
                Assert.That(firstFlash.transform.localPosition.sqrMagnitude,
                    Is.LessThan(0.000001f), weapon.name + " 的火花偏离枪口节点");

                MethodInfo playFlash = feedback.GetType().GetMethod("PlayMuzzleFlash",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null, Type.EmptyTypes, null);
                Assert.That(playFlash, Is.Not.Null);
                playFlash.Invoke(feedback, null);
                Assert.That(firstFlash.activeSelf, Is.True);
                Assert.That(thirdFlash.activeSelf, Is.False,
                    weapon.name + " 在第一人称同时播放了第三人称火花");
            }
        }

        [UnityTest]
        public IEnumerator SpawnedAiStandsOnGroundInIdlePose()
        {
            SceneManager.LoadScene("example");
            yield return new WaitForSeconds(2f);

            UnityEngine.Object[] lives = UnityEngine.Object.FindObjectsByType(
                GetRuntimeType("PlayerLifeCtrl"),
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int checkedCount = 0;
            foreach (UnityEngine.Object lifeObject in lives)
            {
                Component life = (Component)lifeObject;
                Behaviour movement = (Behaviour)life.GetComponent(GetRuntimeType("PlayerMoveCtrl"));
                if (movement && movement.enabled)
                {
                    continue;
                }

                Component animation = life.GetComponent(GetRuntimeType("PlayerAnimationCtrl"));
                Assert.That(animation, Is.Not.Null);
                object thirdPersonView = GetProperty<object>(animation, "ThirdPersonView");
                Animator animator = GetProperty<Animator>(thirdPersonView, "CharacterAnimator");
                Assert.That(animator, Is.Not.Null);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Idle"), Is.True,
                    life.name + " 没有播放玩家共用的 Idle 状态");
                Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                float footY = Mathf.Min(leftFoot.position.y, rightFoot.position.y);
                RaycastHit[] hits = Physics.RaycastAll(
                    new Vector3(life.transform.position.x, footY + 0.5f,
                        life.transform.position.z),
                    Vector3.down, 2f, ~0,
                    QueryTriggerInteraction.Ignore);
                bool hitGround = false;
                float groundY = 0f;
                foreach (RaycastHit hit in hits)
                {
                    if (hit.collider.GetComponentInParent(GetRuntimeType("CharacterHealth")))
                    {
                        continue;
                    }
                    groundY = hit.point.y;
                    hitGround = true;
                    break;
                }
                Assert.That(hitGround, Is.True, life.name + " 脚下没有地面");
                Assert.That(Mathf.Abs(footY - groundY), Is.LessThan(0.25f),
                    life.name + " 脚底离地 " + (footY - groundY) + " 米");
                checkedCount++;
            }

            Assert.That(checkedCount, Is.EqualTo(4), "场景应生成 4 个 AI 玩家");
        }

        private static Type GetRuntimeType(string typeName)
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                Type type = assemblies[i].GetType(typeName);
                if (type != null)
                {
                    return type;
                }
            }

            Assert.Fail("找不到运行时类型 " + typeName);
            return null;
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, target.GetType().Name + " 缺少属性 " + propertyName);
            return (T)property.GetValue(target);
        }

        private static T GetField<T>(object target, string fieldName)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + " 缺少字段 " + fieldName);
            return (T)field.GetValue(target);
        }

        private static T Invoke<T>(object target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, target.GetType().Name + " 缺少方法 " + methodName);
            return (T)method.Invoke(target, arguments);
        }
    }
}
