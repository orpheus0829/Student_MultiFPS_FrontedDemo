using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using System.Reflection;

namespace GunDemo.Tests
{
    public sealed class WeaponActionDataTests
    {
        [Test]
        public void OnlyPdw90HasNativeInspectAction()
        {
            string[] weaponGuids = AssetDatabase.FindAssets(
                "t:WeaponDefinitionSO",
                new[] { "Assets/SO/Weapons/Data" });
            for (int i = 0; i < weaponGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(weaponGuids[i]);
                Object weapon = AssetDatabase.LoadMainAssetAtPath(path);
                object animation = weapon.GetType().GetProperty("Animation").GetValue(weapon);
                bool canInspect = (bool)animation.GetType().GetProperty("CanInspect")
                    .GetValue(animation);
                bool hasNativeInspect = System.IO.Path.GetFileNameWithoutExtension(path) == "PDW90";
                Assert.That(canInspect, Is.EqualTo(hasNativeInspect), path);
            }
        }

        [Test]
        public void MagazineReloadAllowsFireOnlyAfterAmmoCommit()
        {
            GameObject gameObject = new GameObject("换弹动作测试");
            try
            {
                System.Type controllerType = System.Type.GetType("WeaponActionCtrl, Assembly-CSharp");
                System.Type actionType = System.Type.GetType("WeaponActionType, Assembly-CSharp");
                System.Type cancelType = System.Type.GetType("WeaponActionCancelReason, Assembly-CSharp");
                Assert.That(controllerType, Is.Not.Null);
                Assert.That(actionType, Is.Not.Null);
                Assert.That(cancelType, Is.Not.Null);
                Component controller = gameObject.AddComponent(controllerType);
                Object weapon = AssetDatabase.LoadMainAssetAtPath(
                    "Assets/SO/Weapons/Data/AK.asset");
                Object actions = AssetDatabase.LoadMainAssetAtPath(
                    "Assets/SO/Weapons/Actions/AK.asset");
                controllerType.GetMethod("SetWeaponData").Invoke(
                    controller, new object[] { weapon, actions });
                MethodInfo start = controllerType.GetMethod("TryStartAction");
                object reload = System.Enum.Parse(actionType, "Reload");
                object fire = System.Enum.Parse(actionType, "Fire");
                Assert.That(start.Invoke(controller, new[] { reload, (object)false }), Is.True);
                PropertyInfo canFire = controllerType.GetProperty("CanFireAfterReloadCommit");
                Assert.That(canFire.GetValue(controller), Is.False);

                MethodInfo tick = controllerType.GetMethod(
                    "TickAction", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(tick, Is.Not.Null);
                object reloadAction = actions.GetType().GetProperty("Reload").GetValue(actions);
                float duration = (float)reloadAction.GetType().GetProperty("Duration")
                    .GetValue(reloadAction);
                tick.Invoke(controller, new object[] { duration * 0.6f });
                Assert.That(canFire.GetValue(controller), Is.False);
                tick.Invoke(controller, new object[] { duration * 0.2f });
                Assert.That(canFire.GetValue(controller), Is.True);

                object manual = System.Enum.Parse(cancelType, "Manual");
                MethodInfo cancel = controllerType.GetMethod("CancelCurrentAction");
                Assert.That(cancel.Invoke(controller, new[] { manual, (object)true }), Is.True);
                Assert.That(start.Invoke(controller, new[] { fire, (object)false }), Is.True);
                Assert.That(canFire.GetValue(controller), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void EveryWeaponHasUsableActionSet()
        {
            string[] weaponGuids = AssetDatabase.FindAssets(
                "t:WeaponDefinitionSO",
                new[] { "Assets/SO/Weapons/Data" });
            string[] actionGuids = AssetDatabase.FindAssets(
                "t:WeaponActionSetSO",
                new[] { "Assets/SO/Weapons/Actions" });

            Assert.That(weaponGuids.Length, Is.EqualTo(19));
            Assert.That(actionGuids.Length, Is.EqualTo(weaponGuids.Length));

            for (int i = 0; i < weaponGuids.Length; i++)
            {
                string weaponPath = AssetDatabase.GUIDToAssetPath(weaponGuids[i]);
                Object weapon = AssetDatabase.LoadMainAssetAtPath(weaponPath);
                Assert.That(weapon, Is.Not.Null, weaponPath);
                SerializedObject serializedWeapon = new SerializedObject(weapon);
                Assert.That(
                    serializedWeapon.FindProperty("_actionSetResourceId").stringValue,
                    Is.Not.Empty,
                    weaponPath);

                string fileName = System.IO.Path.GetFileNameWithoutExtension(weaponPath);
                string actionPath = "Assets/SO/Weapons/Actions/" + fileName + ".asset";
                Object actionSet = AssetDatabase.LoadMainAssetAtPath(actionPath);
                Assert.That(actionSet, Is.Not.Null, actionPath);
                SerializedObject serializedActions = new SerializedObject(actionSet);
                Assert.That(serializedActions.FindProperty("_dataVersion").intValue, Is.EqualTo(1));
                Assert.That(
                    serializedActions.FindProperty("_fire._enabled").boolValue,
                    Is.True,
                    actionPath + " 未启用开火动作");

                AssertRequiredMarker(
                    serializedActions,
                    "_fire",
                    0,
                    actionPath);
                AssertRequiredMarker(
                    serializedActions,
                    "_reload",
                    1,
                    actionPath);
                AssertRequiredMarker(
                    serializedActions,
                    "_emptyReload",
                    1,
                    actionPath);
                AssertRequiredMarker(
                    serializedActions,
                    "_perRoundInsert",
                    2,
                    actionPath);
            }
        }

        private static void AssertRequiredMarker(
            SerializedObject actionSet,
            string actionPath,
            int markerType,
            string assetPath)
        {
            SerializedProperty action = actionSet.FindProperty(actionPath);
            if (!action.FindPropertyRelative("_enabled").boolValue)
            {
                return;
            }

            SerializedProperty markers = action.FindPropertyRelative("_markers");
            for (int i = 0; i < markers.arraySize; i++)
            {
                SerializedProperty marker = markers.GetArrayElementAtIndex(i);
                if (marker.FindPropertyRelative("_markerType").enumValueIndex == markerType)
                {
                    return;
                }
            }

            Assert.Fail(assetPath + " 的 " + actionPath + " 缺少类型 " + markerType + " 节点");
        }
    }
}
