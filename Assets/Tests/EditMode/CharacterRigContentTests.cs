using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GunDemo.Tests
{
    public sealed class CharacterRigContentTests
    {
        [TestCase("Ellen")]
        [TestCase("ZhuYuan")]
        [TestCase("Belle")]
        public void CharacterRetargetingPreservesNativeBoneLengths(string model)
        {
            ModelImporter importer = AssetImporter.GetAtPath(
                "Assets/Resource/Characters/" + model + "/" + model + ".fbx") as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(importer.humanDescription.hasTranslationDoF, Is.False,
                model + " 不应把动画源的肩部、手腕位移重定向到不同身材和服饰骨架");
        }

        [Test]
        public void EllenThumbAndIndexMappingsFollowTheirAnatomicalNames()
        {
            ModelImporter importer = AssetImporter.GetAtPath(
                "Assets/Resource/Characters/Ellen/Ellen.fbx") as ModelImporter;
            foreach (HumanBone bone in importer.humanDescription.human)
            {
                if (bone.humanName.Contains("Thumb"))
                {
                    Assert.That(bone.boneName, Does.StartWith("親指"), bone.humanName);
                }
                if (bone.humanName.Contains("Index"))
                {
                    Assert.That(bone.boneName, Does.StartWith("人指"), bone.humanName);
                }
            }
        }
    }
}
