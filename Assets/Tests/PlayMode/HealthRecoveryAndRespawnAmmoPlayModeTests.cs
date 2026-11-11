using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GunDemo.Tests
{
    public sealed class HealthRecoveryAndRespawnAmmoPlayModeTests
    {
        [UnityTest]
        public IEnumerator LivingCharacterRecoversAfterRefreshedCooldownButDeadCharacterDoesNot()
        {
            Type dataType = RequireType("PlayerDataSO");
            Type lifeType = RequireType("PlayerLifeCtrl");
            Type healthType = RequireType("CharacterHealth");
            Type damageType = RequireType("CombatDamage");
            Type infoType = RequireType("PlayerInfo");
            ScriptableObject data = ScriptableObject.CreateInstance(dataType);
            SetField(data, "_healthRecoveryDelay", 0.1f);
            SetField(data, "_healthRecoveryDuration", 0.2f);

            GameObject actor = new GameObject("HealthRecoveryTestActor");
            Component life = actor.AddComponent(lifeType);
            SetField(life, "_playerData", data);
            SetField(life, "_autoRespawn", false);
            Component health = actor.AddComponent(healthType);
            MethodInfo apply = damageType.GetMethod(
                "Apply", new[] { healthType, typeof(float), infoType });
            MethodInfo kill = damageType.GetMethod("Kill", new[] { healthType, infoType });
            PropertyInfo currentHealth = healthType.GetProperty("CurrentHealth");
            PropertyInfo isDead = healthType.GetProperty("IsDead");

            apply.Invoke(null, new object[] { health, 30f, null });
            Assert.That((float)currentHealth.GetValue(health), Is.EqualTo(70f).Within(0.001f));

            yield return new WaitForSeconds(0.05f);
            Assert.That((float)currentHealth.GetValue(health), Is.EqualTo(70f).Within(0.001f),
                "回血冷却结束前不应恢复生命");

            yield return new WaitForSeconds(0.08f);
            yield return null;
            Assert.That((float)currentHealth.GetValue(health), Is.GreaterThan(70f),
                "存活角色在回血冷却结束后应开始平滑恢复");

            apply.Invoke(null, new object[] { health, 10f, null });
            float healthAfterSecondHit = (float)currentHealth.GetValue(health);
            yield return new WaitForSeconds(0.05f);
            Assert.That((float)currentHealth.GetValue(health),
                Is.EqualTo(healthAfterSecondHit).Within(0.001f),
                "再次受伤必须刷新回血冷却");

            kill.Invoke(null, new object[] { health, null });
            Assert.That((bool)isDead.GetValue(health), Is.True);
            yield return new WaitForSeconds(0.25f);
            Assert.That((float)currentHealth.GetValue(health), Is.Zero.Within(0.001f),
                "死亡角色不应继续回血");

            UnityEngine.Object.Destroy(actor);
            UnityEngine.Object.Destroy(data);
        }

        [Test]
        public void ResetAllAmmoToFullRestoresEveryRegisteredWeapon()
        {
            GameObject actor = new GameObject("AmmoResetTestActor");
            actor.SetActive(false);
            Type combatType = RequireType("WeaponCombatCtrl");
            Component combat = actor.AddComponent(combatType);

            ScriptableObject firstWeapon = CreateWeapon(30, 180);
            ScriptableObject secondWeapon = CreateWeapon(8, 48);
            Type stateType = combatType.GetNestedType(
                "WeaponAmmoState", BindingFlags.NonPublic);
            Assert.That(stateType, Is.Not.Null);
            object firstState = Activator.CreateInstance(stateType, 3, 7);
            object secondState = Activator.CreateInstance(stateType, 1, 2);

            object states = GetField(combat, "_ammoStates");
            MethodInfo add = states.GetType().GetMethod("Add");
            add.Invoke(states, new[] { firstWeapon, firstState });
            add.Invoke(states, new[] { secondWeapon, secondState });
            SetField(combat, "_weaponData", firstWeapon);
            SetField(combat, "_ammoState", firstState);

            combatType.GetMethod("ResetAllAmmoToFull").Invoke(combat, null);

            AssertAmmo(stateType, firstState, 30, 180, "当前枪械");
            AssertAmmo(stateType, secondState, 8, 48, "已登记的非当前枪械");

            UnityEngine.Object.DestroyImmediate(actor);
            UnityEngine.Object.DestroyImmediate(firstWeapon);
            UnityEngine.Object.DestroyImmediate(secondWeapon);
        }

        private static ScriptableObject CreateWeapon(int magazine, int reserve)
        {
            Type weaponType = RequireType("WeaponDefinitionSO");
            Type ammoType = RequireType("WeaponAmmoData");
            ScriptableObject weapon = ScriptableObject.CreateInstance(weaponType);
            object ammo = Activator.CreateInstance(ammoType);
            SetField(ammo, "_magazineCapacity", magazine);
            SetField(ammo, "_startingReserveAmmo", 0);
            SetField(ammo, "_maximumReserveAmmo", reserve);
            SetField(weapon, "_ammo", ammo);
            return weapon;
        }

        private static Type RequireType(string typeName)
        {
            Type type = Type.GetType(typeName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, "找不到运行时类型 " + typeName);
            return type;
        }

        private static void AssertAmmo(
            Type stateType,
            object state,
            int expectedMagazine,
            int expectedReserve,
            string context)
        {
            Assert.That(
                (int)stateType.GetField("MagazineAmmo").GetValue(state),
                Is.EqualTo(expectedMagazine),
                context + " 的弹匣未回满");
            Assert.That(
                (int)stateType.GetField("ReserveAmmo").GetValue(state),
                Is.EqualTo(expectedReserve),
                context + " 的备弹未回满");
        }

        private static object GetField(object target, string name)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + " 缺少字段 " + name);
            return field.GetValue(target);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = target.GetType().GetField(
                name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + " 缺少字段 " + name);
            field.SetValue(target, value);
        }
    }
}
