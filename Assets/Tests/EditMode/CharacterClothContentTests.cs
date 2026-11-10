using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GunDemo.Tests
{
    public sealed class CharacterClothContentTests
    {
        [TestCase("Ellen", 1, 9, 1)]
        public void CharacterClothUsesSmallAnchoredBoneChains(string model, int expectedGroups,
            int expectedNodes, int expectedColliders)
        {
            string path = "Assets/Prefabs/Characters/Models/" + model + ".prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Assert.That(prefab, Is.Not.Null);
            int groups = 0;
            int colliders = 0;
            HashSet<Transform> nodes = new HashSet<Transform>();
            foreach (Component component in prefab.GetComponentsInChildren<Component>(true))
            {
                Assert.That(component, Is.Not.Null, "角色 Prefab 存在丢失脚本。");
                string type = component.GetType().FullName;
                if (type == "MagicaCloth2.MagicaCapsuleCollider")
                {
                    colliders++;
                }
                if (type != "MagicaCloth2.MagicaCloth")
                {
                    continue;
                }
                groups++;
                SerializedObject serialized = new SerializedObject(component);
                SerializedProperty data = serialized.FindProperty("serializeData");
                Assert.That(data.FindPropertyRelative("clothType").intValue, Is.EqualTo(1));
                Assert.That(data.FindPropertyRelative("rootRotation").floatValue, Is.Zero);
                Assert.That(data.FindPropertyRelative("angleLimitConstraint.useAngleLimit").boolValue, Is.True);
                Assert.That(data.FindPropertyRelative("motionConstraint.useMaxDistance").boolValue, Is.True);
                Assert.That(data.FindPropertyRelative("motionConstraint.maxDistance.value").floatValue,
                    Is.InRange(0.02f, 0.101f));
                Assert.That(data.FindPropertyRelative("wind.influence").floatValue, Is.Zero);
                Assert.That(data.FindPropertyRelative("inertiaConstraint.teleportMode").intValue, Is.EqualTo(1));
                SerializedProperty roots = data.FindPropertyRelative("rootBones");
                Assert.That(roots.arraySize, Is.GreaterThan(0));
                for (int i = 0; i < roots.arraySize; i++)
                {
                    Transform root = roots.GetArrayElementAtIndex(i).objectReferenceValue as Transform;
                    Assert.That(root, Is.Not.Null);
                    if (component.name == "RightSleeveCloth")
                    {
                        Assert.That(roots.arraySize, Is.EqualTo(3));
                        Assert.That(root.name, Does.StartWith("Furisode").And.EndWith("_01"));
                        Assert.That(root.parent.name, Is.EqualTo("Bn_SleeveUP").Or.EqualTo("Bone015"));
                        Assert.That(data.FindPropertyRelative("motionConstraint.maxDistance.value").floatValue,
                            Is.LessThanOrEqualTo(0.02f));
                    }
                    foreach (Transform node in root.GetComponentsInChildren<Transform>(true))
                    {
                        Assert.That(nodes.Add(node), Is.True, "同一骨节点被多个布料组驱动。");
                    }
                }
                Assert.That(data.FindPropertyRelative("colliderCollisionConstraint.colliderList").arraySize,
                    Is.InRange(1, 3));
            }
            Assert.That(groups, Is.EqualTo(expectedGroups));
            Assert.That(nodes.Count, Is.EqualTo(expectedNodes));
            Assert.That(colliders, Is.EqualTo(expectedColliders));
            foreach (string dependency in AssetDatabase.GetDependencies(path, true))
            {
                Assert.That(dependency, Does.Not.StartWith("Assets/source_unselected/"));
            }
        }
    }
}
