using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;

namespace GunDemo.Tests
{
    public sealed class ProjectFoundationTests
    {
        [Test]
        public void RequiredProjectFoldersExist()
        {
            Assert.That(AssetDatabase.IsValidFolder("Assets/Scripts"), Is.True, "缺少项目业务脚本目录");
            Assert.That(AssetDatabase.IsValidFolder("Assets/CLI"), Is.True, "缺少项目 CLI 目录");
            Assert.That(AssetDatabase.IsValidFolder("Assets/Prefabs"), Is.True, "缺少正式 Prefab 目录");
            Assert.That(AssetDatabase.IsValidFolder("Assets/SO"), Is.True, "缺少 ScriptableObject 目录");
        }

        [Test]
        public void EnabledBuildScenesExist()
        {
            Assert.That(EditorBuildSettings.scenes.Length, Is.GreaterThan(0), "Build Settings 中没有场景");

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (!scene.enabled)
                {
                    continue;
                }

                Assert.That(File.Exists(scene.path), Is.True, "构建场景不存在: " + scene.path);
            }
        }

        [Test]
        public void WeaponDataAndPrefabsHaveMatchingNames()
        {
            HashSet<string> dataNames = GetFirearmDataNames();
            HashSet<string> prefabNames = GetRootWeaponPrefabNames();

            CollectionAssert.AreEquivalent(dataNames, prefabNames, "枪械数据盒与正式枪械 Prefab 名称不一致");
        }

        private static HashSet<string> GetFirearmDataNames()
        {
            HashSet<string> names = new HashSet<string>();
            string[] guids = AssetDatabase.FindAssets(
                "t:WeaponDefinitionSO",
                new[] { "Assets/SO/Weapons" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                SerializedObject data = new SerializedObject(
                    AssetDatabase.LoadMainAssetAtPath(path));
                if (data.FindProperty("_basic._category").enumValueIndex == 9)
                {
                    continue;
                }

                names.Add(Path.GetFileNameWithoutExtension(path));
            }

            return names;
        }

        private static HashSet<string> GetRootWeaponPrefabNames()
        {
            HashSet<string> names = new HashSet<string>();
            string[] guids = AssetDatabase.FindAssets(
                "t:Prefab",
                new[] { "Assets/Prefabs/Weapons" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string directory = Path.GetDirectoryName(path)?.Replace('\\', '/');
                if (directory != "Assets/Prefabs/Weapons")
                {
                    continue;
                }

                names.Add(Path.GetFileNameWithoutExtension(path));
            }

            return names;
        }
    }
}
