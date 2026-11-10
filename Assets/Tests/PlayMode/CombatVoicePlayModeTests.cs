using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace GunDemo.Tests
{
    public sealed class CombatVoicePlayModeTests
    {
        [UnityTest]
        public IEnumerator CombatVoiceUsesItsOwnThreeDimensionalAudioSource()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("example", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Type inputType = Type.GetType("PlayerInputCtrl, Assembly-CSharp");
            Type voiceType = Type.GetType("CombatVoiceCtrl, Assembly-CSharp");
            Assert.That(inputType, Is.Not.Null);
            Assert.That(voiceType, Is.Not.Null);
            UnityEngine.Object[] inputs = UnityEngine.Object.FindObjectsByType(
                inputType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            Component local = null;
            for (int i = 0; i < inputs.Length; i++)
            {
                if (inputs[i] is Behaviour input && input.enabled)
                {
                    local = input;
                    break;
                }
            }

            Assert.That(local, Is.Not.Null);
            Component voice = local.GetComponent(voiceType);
            Assert.That(voice, Is.Not.Null);
            AudioSource voiceSource = voiceType.GetField("_audioSource",
                BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(voice) as AudioSource;
            AudioSource weaponSource = local.GetComponent<AudioSource>();
            Assert.That(voiceSource, Is.Not.Null);
            Assert.That(weaponSource, Is.Not.Null);
            Assert.That(voiceSource, Is.Not.SameAs(weaponSource));
            Assert.That(voiceSource.transform.parent, Is.SameAs(local.transform));
            Assert.That(voiceSource.spatialBlend, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator LocalAndNearbyAiLinesAppearImmediatelyInBottomSubtitle()
        {
            AsyncOperation load = SceneManager.LoadSceneAsync("example", LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null);
            yield return load;
            yield return null;

            Type inputType = Type.GetType("PlayerInputCtrl, Assembly-CSharp");
            Type voiceType = Type.GetType("CombatVoiceCtrl, Assembly-CSharp");
            Type cueType = Type.GetType("CombatVoiceCue, Assembly-CSharp");
            Type subtitleType = Type.GetType("CombatVoiceSubtitleUI, Assembly-CSharp");
            Assert.That(inputType, Is.Not.Null);
            Assert.That(voiceType, Is.Not.Null);
            Assert.That(cueType, Is.Not.Null);
            Assert.That(subtitleType, Is.Not.Null);

            Component local = null;
            Component ai = null;
            float spawnDeadline = Time.realtimeSinceStartup + 10f;
            while ((!local || !ai) && Time.realtimeSinceStartup < spawnDeadline)
            {
                UnityEngine.Object[] inputs = UnityEngine.Object.FindObjectsByType(
                    inputType, FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (UnityEngine.Object item in inputs)
                {
                    if (item is Behaviour input && input.enabled)
                    {
                        local = input;
                    }
                    else if (item is Component candidate && !ai)
                    {
                        ai = candidate;
                    }
                }
                yield return null;
            }
            Assert.That(local, Is.Not.Null, "10 秒内未找到本地玩家。");
            Assert.That(ai, Is.Not.Null, "10 秒内复活点未完成 AI 异步生成。");
            Component localVoice = local.GetComponent(voiceType);
            Component aiVoice = ai.GetComponent(voiceType);
            Assert.That(localVoice, Is.Not.Null);
            Assert.That(aiVoice, Is.Not.Null);
            FieldInfo profileField = voiceType.GetField("_voiceData",
                BindingFlags.Instance | BindingFlags.NonPublic);
            float profileDeadline = Time.realtimeSinceStartup + 10f;
            while ((profileField.GetValue(localVoice) == null || profileField.GetValue(aiVoice) == null) &&
                Time.realtimeSinceStartup < profileDeadline)
            {
                yield return null;
            }
            Assert.That(profileField.GetValue(localVoice), Is.Not.Null);
            Assert.That(profileField.GetValue(aiVoice), Is.Not.Null);

            Component subtitle = UnityEngine.Object.FindFirstObjectByType(
                subtitleType, FindObjectsInactive.Exclude) as Component;
            Assert.That(subtitle, Is.Not.Null);
            MethodInfo clear = subtitleType.GetMethod("Clear");
            clear.Invoke(subtitle, null);
            PropertyInfo count = subtitleType.GetProperty("VisibleCount");
            MethodInfo speak = voiceType.GetMethod("Speak");

            speak.Invoke(localVoice, new[] { Enum.Parse(cueType, "Reload") });
            Assert.That(count.GetValue(subtitle), Is.EqualTo(1));
            Assert.That(GetVisibleText(subtitle, subtitleType), Does.Contain("我</color>："));

            ai.transform.position = local.transform.position + Vector3.right * 2f;
            speak.Invoke(aiVoice, new[] { Enum.Parse(cueType, "Hurt") });
            Assert.That(count.GetValue(subtitle), Is.EqualTo(2));
            string stacked = GetVisibleText(subtitle, subtitleType);
            Assert.That(stacked, Does.Contain("我</color>："));
            Assert.That(stacked.Split('：').Length, Is.EqualTo(3));

            ai.transform.position = local.transform.position + Vector3.right * 1000f;
            speak.Invoke(aiVoice, new[] { Enum.Parse(cueType, "Hurt") });
            Assert.That(count.GetValue(subtitle), Is.EqualTo(2), "范围外的 AI 不应进入本地字幕堆叠。");

            clear.Invoke(subtitle, null);
            Assert.That(count.GetValue(subtitle), Is.EqualTo(0));
            subtitleType.GetField("_minimumDuration", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(subtitle, 0.1f);
            MethodInfo show = subtitleType.GetMethod("Show");
            show.Invoke(subtitle, new object[] { "测试角色", "先到", false, 0.1f });
            show.Invoke(subtitle, new object[] { "测试角色", "后到", false, 2f });
            Assert.That(count.GetValue(subtitle), Is.EqualTo(2));
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(count.GetValue(subtitle), Is.EqualTo(1), "字幕需要分别计时，不能被新消息覆盖。");
            Assert.That(GetVisibleText(subtitle, subtitleType), Does.Contain("后到"));

            clear.Invoke(subtitle, null);
            for (int i = 0; i < 20; i++)
            {
                show.Invoke(subtitle, new object[] { "测试角色", "排队" + i, false, 2f });
            }
            Assert.That(count.GetValue(subtitle), Is.EqualTo(3));
            Assert.That(subtitleType.GetProperty("PendingCount").GetValue(subtitle), Is.EqualTo(12));
            clear.Invoke(subtitle, null);
        }

        private static string GetVisibleText(Component subtitle, Type subtitleType)
        {
            IList items = subtitleType.GetField("_active", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(subtitle) as IList;
            string content = string.Empty;
            foreach (object item in items)
            {
                Component view = item.GetType().GetField("Item").GetValue(item) as Component;
                Component label = view.GetComponent(Type.GetType("TMPro.TextMeshProUGUI, Unity.TextMeshPro"));
                Assert.That(label, Is.Not.Null);
                content += label.GetType().GetProperty("text").GetValue(label) as string;
            }
            return content;
        }
    }
}
