using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GunDemo.Tests
{
    public sealed class CombatFeedbackPlayModeTests
    {
        [UnityTest]
        public IEnumerator KillFeedKeepsNewestMessageAtTop()
        {
            yield return LoadExampleScene();

            Type killFeedType = GetRuntimeType("KillFeedUI");
            UnityEngine.Object[] killFeeds = UnityEngine.Object.FindObjectsByType(
                killFeedType,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(killFeeds.Length, Is.EqualTo(1));

            Type messageType = GetRuntimeType("KillFeedMessage");
            MethodInfo showKill = killFeedType.GetMethod("ShowKill", BindingFlags.Instance | BindingFlags.Public);
            object firstMessage = Activator.CreateInstance(
                messageType,
                "玩家甲",
                "目标甲",
                "weapon.ak.silhouette",
                false);
            object secondMessage = Activator.CreateInstance(
                messageType,
                "玩家乙",
                "目标乙",
                "weapon.svd.silhouette",
                true);

            showKill.Invoke(killFeeds[0], new[] { firstMessage });
            showKill.Invoke(killFeeds[0], new[] { secondMessage });

            GameObject topRight = GameObject.Find("TopRightRegion");
            Assert.That(topRight, Is.Not.Null);
            Transform content = topRight.transform.Find("Content");
            Assert.That(content, Is.Not.Null);
            Assert.That(content.childCount, Is.EqualTo(2));

            Type textType = Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro");
            Component newestText = content.GetChild(0).GetComponentInChildren(textType, true);
            string newestContent = (string)textType.GetProperty("text").GetValue(newestText);
            Assert.That(newestContent, Does.Contain("目标乙"));

            ClearWordNoteRegion("TopRight");
            yield return DestroyWordNoteUI();
        }

        [UnityTest]
        public IEnumerator ScoreFeedbackAccumulatesAndResetsAfterFade()
        {
            yield return LoadExampleScene();

            Type feedbackType = GetRuntimeType("CombatScoreFeedbackUI");
            UnityEngine.Object[] feedbacks = UnityEngine.Object.FindObjectsByType(
                feedbackType,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(feedbacks.Length, Is.EqualTo(1));

            Type dataType = GetRuntimeType("CombatScoreFeedbackData");
            MethodInfo show = feedbackType.GetMethod("Show", BindingFlags.Instance | BindingFlags.Public);
            PropertyInfo currentScore = feedbackType.GetProperty("CurrentCycleScore", BindingFlags.Instance | BindingFlags.Public);

            object firstScore = Activator.CreateInstance(dataType, 30, false, string.Empty, false);
            object secondScore = Activator.CreateInstance(dataType, 20, true, "目标", true);
            show.Invoke(feedbacks[0], new[] { firstScore });
            Assert.That((int)currentScore.GetValue(feedbacks[0]), Is.EqualTo(30));

            show.Invoke(feedbacks[0], new[] { secondScore });
            Assert.That((int)currentScore.GetValue(feedbacks[0]), Is.EqualTo(50));

            yield return new WaitForSecondsRealtime(2.5f);
            Assert.That((int)currentScore.GetValue(feedbacks[0]), Is.Zero);
            yield return DestroyWordNoteUI();
        }

        [UnityTest]
        public IEnumerator KillFeedSpriteAssetLoadsThroughResourceManager()
        {
            Type managerType = GetRuntimeType("ResourceMgr");
            PropertyInfo instanceProperty = managerType.GetProperty(
                "instance",
                BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
            Assert.That(instanceProperty, Is.Not.Null);
            object manager = instanceProperty.GetValue(null);

            MethodInfo loadMethod = managerType.GetMethod("LoadAsync", BindingFlags.Instance | BindingFlags.Public);
            MethodInfo loadObject = loadMethod.MakeGenericMethod(typeof(UnityEngine.Object));
            Task loadTask = (Task)loadObject.Invoke(manager, new object[] { "ui.killfeed.sprite_asset" });
            yield return new WaitUntil(() => loadTask.IsCompleted);

            Assert.That(loadTask.IsFaulted, Is.False, loadTask.Exception?.ToString());
            UnityEngine.Object loadedAsset = loadTask.GetType().GetProperty("Result").GetValue(loadTask) as UnityEngine.Object;
            Assert.That(loadedAsset, Is.Not.Null);

            MethodInfo releaseMethod = null;
            MethodInfo[] methods = managerType.GetMethods(BindingFlags.Instance | BindingFlags.Public);
            for (int i = 0; i < methods.Length; i++)
            {
                ParameterInfo[] parameters = methods[i].GetParameters();
                if (methods[i].Name == "Release" &&
                    !methods[i].IsGenericMethod &&
                    parameters.Length == 1 &&
                    parameters[0].ParameterType == typeof(string))
                {
                    releaseMethod = methods[i];
                    break;
                }
            }

            Assert.That(releaseMethod, Is.Not.Null);
            releaseMethod.Invoke(manager, new object[] { "ui.killfeed.sprite_asset" });
        }

        [UnityTest]
        public IEnumerator CrosshairHitFeedbackRespectsTriggerInterval()
        {
            yield return LoadExampleScene();

            Type feedbackType = GetRuntimeType("CrosshairHitFeedbackUI");
            UnityEngine.Object[] feedbacks = UnityEngine.Object.FindObjectsByType(
                feedbackType,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(feedbacks.Length, Is.EqualTo(1));

            MethodInfo showNormalHit = feedbackType.GetMethod(
                "ShowNormalHit",
                BindingFlags.Instance | BindingFlags.Public);
            MethodInfo showHeadshot = feedbackType.GetMethod(
                "ShowHeadshot",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(showNormalHit, Is.Not.Null);
            Assert.That(showHeadshot, Is.Not.Null);

            bool firstAccepted = (bool)showNormalHit.Invoke(feedbacks[0], null);
            bool repeatedAccepted = (bool)showHeadshot.Invoke(feedbacks[0], null);
            Assert.That(firstAccepted, Is.True);
            Assert.That(repeatedAccepted, Is.False);

            yield return new WaitForSecondsRealtime(0.13f);
            bool nextTickAccepted = (bool)showHeadshot.Invoke(feedbacks[0], null);
            Assert.That(nextTickAccepted, Is.True);
        }

        private static IEnumerator LoadExampleScene()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("example", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;
        }

        private static IEnumerator DestroyWordNoteUI()
        {
            Type wordNoteType = GetRuntimeType("WordNoteUI");
            UnityEngine.Object[] wordNotes = UnityEngine.Object.FindObjectsByType(
                wordNoteType,
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            for (int i = 0; i < wordNotes.Length; i++)
            {
                Component component = wordNotes[i] as Component;
                if (component)
                {
                    UnityEngine.Object.Destroy(component.gameObject);
                }
            }

            yield return null;
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = Type.GetType(typeName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, "Assembly-CSharp 中缺少类型: " + typeName);
            return type;
        }

        private static void ClearWordNoteRegion(string regionName)
        {
            Type wordNoteType = GetRuntimeType("WordNoteUI");
            PropertyInfo instance = wordNoteType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            object wordNote = instance.GetValue(null);
            if (wordNote == null)
            {
                return;
            }

            Type regionType = GetRuntimeType("WordNoteRegion");
            object region = Enum.Parse(regionType, regionName);
            MethodInfo clear = wordNoteType.GetMethod(
                "Clear",
                BindingFlags.Instance | BindingFlags.Public,
                null,
                new[] { regionType },
                null);
            clear.Invoke(wordNote, new[] { region });
        }
    }
}
