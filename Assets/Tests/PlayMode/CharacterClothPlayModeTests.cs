#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace GunDemo.Tests
{
    public sealed class CharacterClothPlayModeTests
    {
        [UnityTest]
        public IEnumerator CharacterBoneClothBuildsAndSurvivesMovementAndTeleport()
        {
            Type clothType = Type.GetType("MagicaCloth2.MagicaCloth, MagicaClothV2");
            Assert.That(clothType, Is.Not.Null, "正式插件程序集未载入。");
            MethodInfo isValid = clothType.GetMethod("IsValid");
            foreach (string model in new[] { "Ellen" })
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Prefabs/Characters/Models/" + model + ".prefab");
                GameObject instance = UnityEngine.Object.Instantiate(prefab, new Vector3(0f, 100f, 0f), Quaternion.identity);
                try
                {
                    Animator animator = instance.GetComponentInChildren<Animator>();
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    Component[] cloths = instance.GetComponentsInChildren(clothType, true);
                    Assert.That(cloths.Length, Is.EqualTo(1));
                    Dictionary<Transform, float> lengths = new Dictionary<Transform, float>();
                    foreach (Component cloth in cloths)
                    {
                        object data = clothType.GetProperty("SerializeData").GetValue(cloth);
                        IEnumerable roots = data.GetType().GetField("rootBones").GetValue(data) as IEnumerable;
                        foreach (Transform root in roots)
                        {
                            foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
                            {
                                if (bone != root)
                                {
                                    lengths.Add(bone, Vector3.Distance(bone.position, bone.parent.position));
                                }
                            }
                        }
                    }
                    float deadline = Time.realtimeSinceStartup + 15f;
                    bool built = false;
                    while (!built && Time.realtimeSinceStartup < deadline)
                    {
                        built = true;
                        foreach (Component cloth in cloths)
                        {
                            built &= (bool)isValid.Invoke(cloth, null);
                        }
                        yield return null;
                    }
                    Assert.That(built, Is.True, model + " 布料运行时构建失败或超时。");
                    for (int frame = 0; frame < 30; frame++)
                    {
                        instance.transform.position += Vector3.forward * 0.08f;
                        yield return null;
                        AssertStableBones(model, lengths);
                    }
                    instance.transform.position += Vector3.forward * 3f;
                    float settleDeadline = Time.realtimeSinceStartup + 0.35f;
                    while (Time.realtimeSinceStartup < settleDeadline)
                    {
                        yield return null;
                        AssertStableBones(model, lengths);
                    }
                }
                finally
                {
                    UnityEngine.Object.Destroy(instance);
                }
                yield return null;
            }
        }

        private static void AssertStableBones(string model, Dictionary<Transform, float> lengths)
        {
            foreach (KeyValuePair<Transform, float> entry in lengths)
            {
                Transform bone = entry.Key;
                Vector3 position = bone.position;
                bool finite = !float.IsNaN(position.x) && !float.IsInfinity(position.x)
                    && !float.IsNaN(position.y) && !float.IsInfinity(position.y)
                    && !float.IsNaN(position.z) && !float.IsInfinity(position.z);
                Assert.That(finite, Is.True, model + " " + bone.name + " 模拟坐标无效。");
                Assert.That(Vector3.Distance(position, bone.parent.position),
                    Is.LessThan(entry.Value * 1.2f + 0.005f),
                    model + " " + bone.name + " 移动/瞬移过程中骨链过度拉伸。");
            }
        }
    }
}
#endif
