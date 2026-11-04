using System.Collections;
using NUnit.Framework;
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
    }
}
