using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GunDemo.Tests
{
    public sealed class MatchSpawnContentTests
    {
        [Test]
        public void BuiltInMapsExposeExpectedNamesAndModes()
        {
            AssertMap("BattleField1", "小镇", true, true, true);
            AssertMap("BattleField2", "沙城", false, true, true);
            AssertMap("BattleField3", "办公中心", false, true, true);
            AssertMap("BattleField4", "仓库", true, true, false);
            AssertMap("BattleField5", "火车口", true, true, true);
        }

        [Test]
        public void MapCatalogContainsBattleField1()
        {
            Object catalog = AssetDatabase.LoadMainAssetAtPath(
                "Assets/SO/Maps/MapCatalog.asset");
            Object map = AssetDatabase.LoadMainAssetAtPath(
                "Assets/SO/Maps/BattleField1.asset");

            Assert.That(catalog, Is.Not.Null);
            SerializedProperty maps = new SerializedObject(catalog).FindProperty("_maps");
            bool found = false;
            for (int i = 0; i < maps.arraySize; i++)
            {
                if (maps.GetArrayElementAtIndex(i).objectReferenceValue == map)
                {
                    found = true;
                    break;
                }
            }
            Assert.That(found, Is.True);
        }

        [Test]
        public void SoloSpawnPointIsSmallAndMatchManaged()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Gameplay/单人复活点.prefab");
            Assert.That(prefab, Is.Not.Null);

            Component zone = prefab.GetComponent("PlayerSpawnZone");
            Assert.That(zone, Is.Not.Null);
            SerializedObject data = new SerializedObject(zone);
            Assert.That(data.FindProperty("_mode").enumValueIndex, Is.EqualTo(2));
            Assert.That(data.FindProperty("_controlMode").enumValueIndex, Is.EqualTo(0));
            Assert.That(data.FindProperty("_size").vector3Value,
                Is.EqualTo(new Vector3(0.6f, 1f, 0.6f)));
            Assert.That(data.FindProperty("_aiCount").intValue, Is.EqualTo(0));
        }

        [Test]
        public void LegacyAiSpawnZoneIsExplicitlyTestLocal()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Gameplay/PlayerSpawnZone.prefab");
            Component zone = prefab.GetComponent("PlayerSpawnZone");

            Assert.That(zone, Is.Not.Null);
            SerializedObject data = new SerializedObject(zone);
            Assert.That(data.FindProperty("_controlMode").enumValueIndex, Is.EqualTo(1));
        }

        [Test]
        public void TeamSpawnZoneIsMatchManagedAndAcceptsAnyTeam()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Gameplay/正式团队复活区.prefab");
            Assert.That(prefab, Is.Not.Null);

            Component zone = prefab.GetComponent("PlayerSpawnZone");
            Assert.That(zone, Is.Not.Null);
            SerializedObject data = new SerializedObject(zone);
            Assert.That(data.FindProperty("_mode").enumValueIndex, Is.EqualTo(3));
            Assert.That(data.FindProperty("_controlMode").enumValueIndex, Is.EqualTo(0));
            Assert.That(data.FindProperty("_team").intValue, Is.EqualTo(-1));
        }

        private static void AssertMap(
            string internalName,
            string displayName,
            bool supportsSolo,
            bool supportsTeamDeathmatch,
            bool supportsBomb)
        {
            Object map = AssetDatabase.LoadMainAssetAtPath(
                $"Assets/SO/Maps/{internalName}.asset");

            Assert.That(map, Is.Not.Null, internalName);
            SerializedObject data = new SerializedObject(map);
            Assert.That(data.FindProperty("_displayName").stringValue,
                Is.EqualTo(displayName), internalName);
            Assert.That(data.FindProperty("_sceneName").stringValue,
                Is.EqualTo(internalName), internalName);

            SerializedProperty modes = data.FindProperty("_supportedModes");
            Assert.That(ContainsMode(modes, 0), Is.EqualTo(supportsSolo), internalName);
            Assert.That(ContainsMode(modes, 1), Is.EqualTo(supportsTeamDeathmatch),
                internalName);
            Assert.That(ContainsMode(modes, 2), Is.EqualTo(supportsBomb), internalName);
        }

        private static bool ContainsMode(SerializedProperty modes, int mode)
        {
            for (int i = 0; i < modes.arraySize; i++)
            {
                if (modes.GetArrayElementAtIndex(i).enumValueIndex == mode)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
