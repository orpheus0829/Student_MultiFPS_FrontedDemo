using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GunDemo.Tests
{
    public sealed class RespawnContentTests
    {
        [Test]
        public void SpawnZonePrefabKeepsFourAiWithoutCollider()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Gameplay/PlayerSpawnZone.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent<Collider>(), Is.Null);

            Component zone = prefab.GetComponent("PlayerSpawnZone");
            Assert.That(zone, Is.Not.Null);
            SerializedObject data = new SerializedObject(zone);
            Assert.That(data.FindProperty("_mode").enumValueIndex, Is.EqualTo(0));
            Assert.That(data.FindProperty("_aiCount").intValue, Is.EqualTo(4));
            Assert.That(data.FindProperty("_aiPrefabResourceId").stringValue,
                Is.EqualTo("character.ai_player.prefab"));
        }

        [Test]
        public void IdleAiUsesPlayerBodyButDoesNotControlOrSelfRespawn()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Characters/IdleAIPlayer.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.GetComponent("CharacterHealth"), Is.Not.Null);
            Assert.That(prefab.GetComponent("PlayerRagdollCtrl"), Is.Not.Null);
            Animator bodyAnimator = prefab.GetComponent("PlayerAnimationCtrl")
                ?.GetType().GetProperty("ThirdPersonView")
                ?.GetValue(prefab.GetComponent("PlayerAnimationCtrl")) is Component view
                    ? view.GetType().GetProperty("CharacterAnimator")?.GetValue(view) as Animator
                    : null;
            Assert.That(bodyAnimator, Is.Not.Null);
            Assert.That(bodyAnimator.runtimeAnimatorController, Is.Not.Null);
            GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Characters/Player.prefab");
            Component playerAnimation = playerPrefab.GetComponent("PlayerAnimationCtrl");
            Component playerView = playerAnimation.GetType().GetProperty("ThirdPersonView")
                .GetValue(playerAnimation) as Component;
            Animator playerAnimator = playerView.GetType().GetProperty("CharacterAnimator")
                .GetValue(playerView) as Animator;
            Assert.That(bodyAnimator.runtimeAnimatorController,
                Is.SameAs(playerAnimator.runtimeAnimatorController));
            Assert.That((prefab.GetComponent("PlayerAnimationCtrl") as Behaviour).enabled,
                Is.True);

            Behaviour input = prefab.GetComponent("PlayerInputCtrl") as Behaviour;
            Assert.That(input, Is.Not.Null);
            Assert.That(input.enabled, Is.False);

            Component life = prefab.GetComponent("PlayerLifeCtrl");
            Assert.That(life, Is.Not.Null);
            SerializedObject data = new SerializedObject(life);
            Assert.That(data.FindProperty("_autoRespawn").boolValue, Is.False);
        }
    }
}
