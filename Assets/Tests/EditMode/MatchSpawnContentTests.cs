using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GunDemo.Tests
{
    public sealed class MatchSpawnContentTests
    {
        [Test]
        public void BattleField1SupportsTeamDeathmatchAndBomb()
        {
            Object map = AssetDatabase.LoadMainAssetAtPath(
                "Assets/SO/Maps/BattleField1.asset");
            Assert.That(map, Is.Not.Null);

            SerializedObject data = new SerializedObject(map);
            Assert.That(data.FindProperty("_mapId").stringValue,
                Is.EqualTo("map.battlefield1"));
            Assert.That(data.FindProperty("_sceneName").stringValue,
                Is.EqualTo("BattleField1"));
            SerializedProperty modes = data.FindProperty("_supportedModes");
            Assert.That(modes.arraySize, Is.EqualTo(2));
            Assert.That(modes.GetArrayElementAtIndex(0).enumValueIndex, Is.EqualTo(1));
            Assert.That(modes.GetArrayElementAtIndex(1).enumValueIndex, Is.EqualTo(2));
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
    }
}
