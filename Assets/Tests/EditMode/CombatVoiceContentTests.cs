using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GunDemo.Tests
{
    public sealed class CombatVoiceContentTests
    {
        [Test]
        public void PlayerAndAiShareAnbiVoiceProfile()
        {
            GameObject player = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Characters/Player.prefab");
            GameObject ai = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Characters/IdleAIPlayer.prefab");
            Assert.That(player, Is.Not.Null);
            Assert.That(ai, Is.Not.Null);

            Component playerVoice = player.GetComponent("CombatVoiceCtrl");
            Component aiVoice = ai.GetComponent("CombatVoiceCtrl");
            Assert.That(playerVoice, Is.Not.Null);
            Assert.That(aiVoice, Is.Not.Null);
            Assert.That(new SerializedObject(playerVoice).FindProperty("_voiceResourceId").stringValue,
                Is.EqualTo("character.anbi.voice"));
            Assert.That(new SerializedObject(aiVoice).FindProperty("_voiceResourceId").stringValue,
                Is.EqualTo("character.anbi.voice"));
        }

        [Test]
        public void AnbiVoiceHasFiveEventGroupsAndTextWithoutAudio()
        {
            ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                "Assets/SO/Player/Voice/AnbiCombatVoice.asset");
            Assert.That(profile, Is.Not.Null);
            SerializedObject data = new SerializedObject(profile);
            Assert.That(data.FindProperty("_characterModelId").stringValue,
                Is.EqualTo("character.anbi"));
            SerializedProperty groups = data.FindProperty("_groups");
            Assert.That(groups.arraySize, Is.EqualTo(5));

            for (int i = 0; i < groups.arraySize; i++)
            {
                SerializedProperty group = groups.GetArrayElementAtIndex(i);
                Assert.That(group.FindPropertyRelative("_cue").enumValueIndex, Is.EqualTo(i));
                SerializedProperty lines = group.FindPropertyRelative("_lines");
                Assert.That(lines.arraySize, Is.GreaterThan(0));
                for (int j = 0; j < lines.arraySize; j++)
                {
                    SerializedProperty line = lines.GetArrayElementAtIndex(j);
                    Assert.That(line.FindPropertyRelative("_subtitle").stringValue,
                        Is.Not.Empty);
                    Assert.That(line.FindPropertyRelative("_audioResourceId").stringValue,
                        Is.Empty);
                }
            }
        }

        [Test]
        public void OptionalCharactersHaveIndependentVoiceProfiles()
        {
            string[] names = { "Ellen", "ZhuYuan", "Belle" };
            string[] ids = { "ellen", "zhuyuan", "belle" };
            for (int i = 0; i < names.Length; i++)
            {
                string path = "Assets/SO/Player/Voice/" + names[i] + "CombatVoice.asset";
                ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
                Assert.That(profile, Is.Not.Null, path);
                SerializedObject data = new SerializedObject(profile);
                Assert.That(data.FindProperty("_characterModelId").stringValue,
                    Is.EqualTo("character." + ids[i]));
                Assert.That(data.FindProperty("_groups").arraySize, Is.EqualTo(5));
            }
        }

        [Test]
        public void AllCharactersUseReducedAutomaticChatter()
        {
            string[] names = { "Anbi", "Ellen", "ZhuYuan", "Belle" };
            float[] chances = { 0.18f, 0.12f, 0.25f, 0.65f, 0.25f };
            for (int i = 0; i < names.Length; i++)
            {
                ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    "Assets/SO/Player/Voice/" + names[i] + "CombatVoice.asset");
                Assert.That(profile, Is.Not.Null, names[i]);
                SerializedObject data = new SerializedObject(profile);
                Assert.That(data.FindProperty("_automaticCooldown").floatValue,
                    Is.EqualTo(6f), names[i]);
                SerializedProperty groups = data.FindProperty("_groups");
                Assert.That(groups.arraySize, Is.EqualTo(chances.Length));
                for (int j = 0; j < groups.arraySize; j++)
                {
                    SerializedProperty group = groups.GetArrayElementAtIndex(j);
                    Assert.That(group.FindPropertyRelative("_cue").enumValueIndex,
                        Is.EqualTo(j));
                    Assert.That(group.FindPropertyRelative("_chance").floatValue,
                        Is.EqualTo(chances[j]).Within(0.0001f), names[i] + " / " + j);
                }
            }
        }

        [Test]
        public void WordNotePrefabHasCombatSubtitleComponent()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/UI/WordNote/WordNote_UI.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent("CombatVoiceSubtitleUI"), Is.Not.Null);
        }
    }
}
