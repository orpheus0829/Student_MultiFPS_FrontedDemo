using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GunDemo.Tests
{
    public sealed class AddressableResourceTests
    {
        [Test]
        public void WeaponDataHasMigratedResourceIds()
        {
            string[] guids = AssetDatabase.FindAssets(
                "t:WeaponDefinitionSO",
                new[] { "Assets/SO/Weapons/Data" });
            Assert.That(guids.Length, Is.EqualTo(19));

            HashSet<string> weaponIds = new HashSet<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                UnityEngine.Object weapon = AssetDatabase.LoadMainAssetAtPath(path);
                Assert.That(weapon, Is.Not.Null, path);

                SerializedObject serializedWeapon = new SerializedObject(weapon);
                string weaponId = serializedWeapon.FindProperty("_basic._weaponId").stringValue;
                string silhouetteId = serializedWeapon.FindProperty("_basic._silhouetteResourceId").stringValue;
                string firstPersonPrefabId = serializedWeapon.FindProperty("_resources._firstPersonPrefabResourceId").stringValue;
                string weaponControllerId = serializedWeapon.FindProperty("_animation._weaponAnimatorControllerResourceId").stringValue;
                string actionSetId = serializedWeapon.FindProperty("_actionSetResourceId").stringValue;

                Assert.That(weaponIds.Add(weaponId), Is.True, "重复枪械 ID: " + weaponId);
                Assert.That(silhouetteId, Is.Not.Empty, path + " 缺少剪影资源 ID");
                Assert.That(firstPersonPrefabId, Is.Not.Empty, path + " 缺少第一人称 Prefab ID");
                Assert.That(weaponControllerId, Is.Not.Empty, path + " 缺少第一人称武器控制器 ID");
                Assert.That(actionSetId, Is.EqualTo("weapon." + weaponId.Trim().ToLowerInvariant() + ".actions"));

                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                AssertAddress(path, "weapon." + weaponId.Trim().ToLowerInvariant() + ".data");
                AssertAddress("Assets/Resource/UI/WeaponSilhouettes/" + fileName + ".png", silhouetteId);
                AssertAddress("Assets/Prefabs/Weapons/" + fileName + ".prefab", firstPersonPrefabId);
                AssertAddress("Assets/SO/Weapons/Actions/" + fileName + ".asset", actionSetId);
            }
        }

        [Test]
        public void WeaponCatalogHasMigratedResourceIds()
        {
            UnityEngine.Object catalog = AssetDatabase.LoadMainAssetAtPath(
                "Assets/SO/Weapons/Catalog/WeaponCatalog.asset");
            Assert.That(catalog, Is.Not.Null);

            SerializedObject serializedCatalog = new SerializedObject(catalog);
            SerializedProperty weaponIds = serializedCatalog.FindProperty("_weaponResourceIds");
            SerializedProperty defaultWeaponId = serializedCatalog.FindProperty("_defaultWeaponResourceId");
            Assert.That(weaponIds.arraySize, Is.EqualTo(19));
            Assert.That(defaultWeaponId.stringValue, Is.Not.Empty);
            AssertAddress("Assets/SO/Weapons/Catalog/WeaponCatalog.asset", "weapon.catalog");
        }

        [Test]
        public void KillFeedResourcesAreAddressable()
        {
            AssertAddress("Assets/Resource/UI/KillIcons/KillSkull_White.png", "ui.kill.skull.white");
            AssertAddress("Assets/Resource/UI/KillIcons/KillSkull_Red.png", "ui.kill.skull.red");
            AssertAddress("Assets/Resource/UI/KillFeed/KillFeedSpriteAsset.asset", "ui.killfeed.sprite_asset");
        }

        private static void AssertAddress(string assetPath, string expectedAddress)
        {
            string guid = AssetDatabase.AssetPathToGUID(assetPath);
            Assert.That(guid, Is.Not.Empty, assetPath + " 不存在");

            Type defaultObjectType = Type.GetType(
                "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject, Unity.Addressables.Editor");
            Assert.That(defaultObjectType, Is.Not.Null, "未找到 Addressables Editor 程序集");

            PropertyInfo settingsProperty = defaultObjectType.GetProperty(
                "Settings",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(settingsProperty, Is.Not.Null, "未找到 Addressables Settings");
            object settings = settingsProperty.GetValue(null);
            Assert.That(settings, Is.Not.Null, "Addressables Settings 尚未创建");

            MethodInfo findEntry = null;
            MethodInfo[] methods = settings.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance);
            for (int i = 0; i < methods.Length; i++)
            {
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (methods[i].Name == "FindAssetEntry" && parameters.Length > 0 && parameters[0].ParameterType == typeof(string))
                {
                    findEntry = methods[i];
                    if (parameters.Length == 1)
                    {
                        break;
                    }
                }
            }

            Assert.That(findEntry, Is.Not.Null, "Addressables API 缺少 FindAssetEntry");
            ParameterInfo[] findParameters = findEntry.GetParameters();
            object entry = findParameters.Length == 1
                ? findEntry.Invoke(settings, new object[] { guid })
                : findEntry.Invoke(settings, new object[] { guid, false });
            Assert.That(entry, Is.Not.Null, assetPath + " 尚未登记 Addressables");

            PropertyInfo addressProperty = entry.GetType().GetProperty(
                "address",
                BindingFlags.Public | BindingFlags.Instance);
            Assert.That(addressProperty, Is.Not.Null, "Addressables Entry 缺少 address");
            Assert.That(addressProperty.GetValue(entry) as string, Is.EqualTo(expectedAddress), assetPath);
        }
    }
}
