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
