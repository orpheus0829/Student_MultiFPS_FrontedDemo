using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace GunDemo.Tests
{
    public sealed class WeaponProjectileFlightPlayModeTests
    {
        private const int TestCollisionLayer = 31;

        [TestCase("Automatic", 600f, 0, 0f)]
        [TestCase("SemiAutomatic", 300f, 0, 0f)]
        [TestCase("Burst", 900f, 2, 0f)]
        [TestCase("Burst", 900f, 1, 0.12f)]
        public void ProjectileShotPreservesModeRpmAndBurstCooldown(
            string mode, float rpm, int burstShots, float extraCooldown)
        {
            GameObject shooter = new GameObject("RpmTestShooter");
            shooter.SetActive(false);
            GameObject prefab = new GameObject("RpmTestProjectile");
            prefab.SetActive(false);
            UnityEngine.Object weapon = null;
            object pool = null;
            try
            {
                shooter.transform.position = Vector3.one * 1300f;
                Component camera = shooter.AddComponent(GetRuntimeType("FirstPersonCameraCtrl"));
                SetField(camera, "_pitchPivot", shooter.transform);
                Component view = shooter.AddComponent(GetRuntimeType("PlayerWeaponViewCtrl"));
                SetField(view, "_muzzlePoint", shooter.transform);
                Component combat = shooter.AddComponent(GetRuntimeType("WeaponCombatCtrl"));
                prefab.AddComponent(GetRuntimeType("WeaponProjectileCtrl"));
                weapon = ScriptableObject.CreateInstance(GetRuntimeType("WeaponDefinitionSO"));
                object fire = GetProperty(weapon, "Fire");
                SetField(fire, "_automaticRpm", rpm);
                SetField(fire, "_semiAutomaticRpm", rpm);
                SetField(fire, "_burstRpm", rpm);
                SetField(fire, "_burstCooldown", 0.12f);
                Type ammoType = combat.GetType().GetNestedType("WeaponAmmoState", BindingFlags.NonPublic);
                object ammo = Activator.CreateInstance(ammoType, new object[] { 10, 0 });
                SetField(combat, "_weaponData", weapon);
                SetField(combat, "_ammoState", ammo);
                SetField(combat, "_cameraCtrl", camera);
                SetField(combat, "_weaponViewCtrl", view);
                SetField(combat, "_projectilePrefab", prefab);
                SetField(combat, "_hitMask", (LayerMask)0);
                SetField(combat, "_fireMode", Enum.Parse(GetRuntimeType("WeaponFireMode"), mode));
                SetField(combat, "_burstShotsRemaining", burstShots);

                float firedAt = Time.time;
                MethodInfo execute = combat.GetType().GetMethod("ExecuteShot", BindingFlags.Instance | BindingFlags.NonPublic);
                execute.Invoke(combat, null);
                pool = GetRuntimeType("ObjectPool").GetProperty("instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
                float nextShot = (float)combat.GetType().GetField("_nextShotTime",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(combat);
                Assert.That(nextShot - firedAt, Is.EqualTo(60f / rpm + extraCooldown).Within(0.0001f));
                Assert.That(GetProperty<int>(combat, "MagazineAmmo"), Is.EqualTo(9));
                Assert.That((bool)Invoke(combat, "TryFire"), Is.False, "冷却结束前不得再次发射实体弹丸。");
                Assert.That(GetProperty<int>(combat, "MagazineAmmo"), Is.EqualTo(9));
            }
            finally
            {
                if (pool != null)
                {
                    Invoke(pool, "ClearPool", new[] { typeof(GameObject) }, prefab);
                }
                UnityEngine.Object.DestroyImmediate(shooter);
                UnityEngine.Object.DestroyImmediate(prefab);
                if (weapon)
                {
                    UnityEngine.Object.DestroyImmediate(weapon);
                }
            }
        }

        [UnityTest]
        public IEnumerator ThirdPersonShoulderAimConvergesOnNearbyTarget()
        {
            yield return VerifyAimedProjectile(true, false, false, false, false);
        }

        [UnityTest]
        public IEnumerator FirstPersonAimConvergesOnNearbyTarget()
        {
            yield return VerifyAimedProjectile(false, false, false, false, false);
        }

        [UnityTest]
        public IEnumerator MuzzleObstructionStopsDamageDespiteClearCameraAim()
        {
            yield return VerifyAimedProjectile(true, true, false, false, false);
        }

        [UnityTest]
        public IEnumerator MuzzleInsideWallCannotDamageTargetBeyondWall()
        {
            yield return VerifyAimedProjectile(true, false, true, false, false);
        }

        [UnityTest]
        public IEnumerator MuzzleInsideBodyTriggerStillAppliesContactDamage()
        {
            yield return VerifyAimedProjectile(false, false, false, true, false);
        }

        [UnityTest]
        public IEnumerator NonDamageTriggersDoNotBlockAimOrProjectile()
        {
            yield return VerifyAimedProjectile(true, false, false, false, true);
        }

        [UnityTest]
        public IEnumerator ProjectileAppliesDamageOnlyAfterTravelTime()
        {
            GameObject shooter = new GameObject("ProjectileTestShooter");
            GameObject target = new GameObject("ProjectileTestTarget");
            GameObject projectileObject = new GameObject("ProjectileTestBullet");
            UnityEngine.Object weapon = null;

            try
            {
                Vector3 testOrigin = new Vector3(1000f, 1000f, 1000f);
                shooter.transform.position = testOrigin;
                target.transform.position = testOrigin + Vector3.forward * 5f;
                projectileObject.transform.position = testOrigin;
                shooter.layer = TestCollisionLayer;
                target.layer = TestCollisionLayer;
                projectileObject.layer = TestCollisionLayer;

                Component shooterInfo = shooter.AddComponent(GetRuntimeType("PlayerInfo"));
                Invoke(shooterInfo, "SetPlayerName", "Shooter");

                target.AddComponent<SphereCollider>().radius = 0.5f;
                target.AddComponent(GetRuntimeType("PlayerInfo"));
                Component health = target.AddComponent(GetRuntimeType("CharacterHealth"));

                Type weaponType = GetRuntimeType("WeaponDefinitionSO");
                weapon = ScriptableObject.CreateInstance(weaponType);
                object basic = GetProperty(weapon, "Basic");
                object damage = GetProperty(weapon, "Damage");
                SetField(basic, "_weaponId", "projectile_test");
                SetField(basic, "_weaponName", "Projectile Test");
                SetField(damage, "_closeDamage", 25f);
                SetField(damage, "_farDamage", 25f);
                SetField(damage, "_maximumHitRange", 20f);
                SetField(damage, "_projectileSpeed", 10f);
                SetField(damage, "_projectileRadius", 0.01f);

                Type projectileType = GetRuntimeType("WeaponProjectileCtrl");
                Component projectile = projectileObject.AddComponent(projectileType);
                Physics.SyncTransforms();
                Invoke(
                    projectile,
                    "Initialize",
                    new[]
                    {
                        weaponType,
                        GetRuntimeType("PlayerInfo"),
                        typeof(Transform),
                        GetRuntimeType("BulletImpactCtrl"),
                        typeof(LayerMask),
                        typeof(Vector3)
                    },
                    weapon,
                    shooterInfo,
                    shooter.transform,
                    null,
                    (LayerMask)(1 << TestCollisionLayer),
                    Vector3.forward);

                yield return null;

                Assert.That(GetProperty<float>(health, "CurrentHealth"), Is.EqualTo(100f));
                float startedAt = Time.time;
                while (projectileObject.activeSelf && Time.time - startedAt < 1f)
                {
                    yield return null;
                }

                Assert.That(Time.time - startedAt, Is.GreaterThan(0.25f));
                Assert.That(GetProperty<float>(health, "CurrentHealth"), Is.EqualTo(75f));
                Assert.That(projectileObject.activeSelf, Is.False);
            }
            finally
            {
                UnityEngine.Object.Destroy(projectileObject);
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(shooter);
                if (weapon)
                {
                    UnityEngine.Object.Destroy(weapon);
                }
            }
        }

        [UnityTest]
        public IEnumerator TracerStopsAtImpactAndResetsWhenReused()
        {
            GameObject target = new GameObject("TracerTestTarget");
            GameObject projectileObject = new GameObject("TracerTestBullet");
            UnityEngine.Object weapon = null;
            Material material = null;
            try
            {
                Vector3 origin = new Vector3(1100f, 1100f, 1100f);
                target.transform.position = origin + Vector3.forward;
                target.layer = TestCollisionLayer;
                target.AddComponent<SphereCollider>().radius = 0.25f;
                target.AddComponent(GetRuntimeType("PlayerInfo"));
                Component health = target.AddComponent(GetRuntimeType("CharacterHealth"));
                projectileObject.transform.position = origin;
                projectileObject.AddComponent<ParticleSystem>();
                Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                Assert.That(shader, Is.Not.Null);
                material = new Material(shader);
                projectileObject.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
                Component projectile = projectileObject.AddComponent(GetRuntimeType("WeaponProjectileCtrl"));

                Type weaponType = GetRuntimeType("WeaponDefinitionSO");
                weapon = ScriptableObject.CreateInstance(weaponType);
                object damage = GetProperty(weapon, "Damage");
                SetField(damage, "_closeDamage", 25f);
                SetField(damage, "_farDamage", 25f);
                SetField(damage, "_maximumHitRange", 20f);
                SetField(damage, "_projectileSpeed", 10f);
                Physics.SyncTransforms();
                Type[] parameterTypes =
                {
                    weaponType, GetRuntimeType("PlayerInfo"), typeof(Transform),
                    GetRuntimeType("BulletImpactCtrl"), typeof(LayerMask), typeof(Vector3)
                };
                Invoke(projectile, "Initialize", parameterTypes, weapon, null, null, null,
                    (LayerMask)(1 << TestCollisionLayer), Vector3.forward);

                float deadline = Time.time + 1f;
                while (GetProperty<float>(health, "CurrentHealth") == 100f && Time.time < deadline)
                {
                    yield return null;
                }

                Assert.That(GetProperty<float>(health, "CurrentHealth"), Is.EqualTo(75f));
                Assert.That(projectileObject.activeSelf, Is.True, "命中后光段应短暂保留。");
                LineRenderer tracer = projectileObject.GetComponent<LineRenderer>();
                Assert.That(tracer.enabled, Is.True);
                Vector3 tail = tracer.transform.TransformPoint(tracer.GetPosition(0));
                Assert.That(tail.z, Is.GreaterThanOrEqualTo(origin.z - 0.001f));
                Assert.That(projectileObject.transform.position.z, Is.LessThan(target.transform.position.z));
                GradientColorKey[] colors = tracer.colorGradient.colorKeys;
                Assert.That(colors[0].color.b, Is.LessThan(colors[colors.Length - 1].color.b));
                Vector3 impactPosition = projectileObject.transform.position;
                deadline = Time.time + 0.3f;
                while (projectileObject.activeSelf && Time.time < deadline)
                {
                    yield return null;
                }

                Assert.That(projectileObject.activeSelf, Is.False);
                Assert.That(projectileObject.transform.position, Is.EqualTo(impactPosition));
                Assert.That(GetProperty<float>(health, "CurrentHealth"), Is.EqualTo(75f));
                projectileObject.transform.position = origin;
                projectileObject.SetActive(true);
                Invoke(projectile, "Initialize", parameterTypes, weapon, null, null, null,
                    (LayerMask)(1 << TestCollisionLayer), Vector3.forward);
                Assert.That(tracer.enabled, Is.False, "复用前不应留下旧光段。");
                Assert.That(tracer.widthMultiplier, Is.EqualTo(0.085f).Within(0.0001f));
            }
            finally
            {
                UnityEngine.Object.Destroy(projectileObject);
                UnityEngine.Object.Destroy(target);
                if (weapon)
                {
                    UnityEngine.Object.Destroy(weapon);
                }
                if (material)
                {
                    UnityEngine.Object.Destroy(material);
                }
            }
        }

        private static IEnumerator VerifyAimedProjectile(
            bool thirdPerson, bool obstructMuzzle, bool overlapWall, bool overlapBody, bool addTrigger)
        {
            GameObject shooter = new GameObject("AimedProjectileTestShooter");
            shooter.SetActive(false);
            GameObject target = new GameObject("AimedProjectileTestTarget");
            GameObject prefab = new GameObject("AimedProjectileTestPrefab");
            prefab.SetActive(false);
            GameObject obstacle = null;
            UnityEngine.Object weapon = null;
            object pool = null;
            try
            {
                Vector3 origin = new Vector3(1200f, 1200f, 1200f);
                shooter.transform.position = origin;
                Vector3 muzzle = thirdPerson ? origin : origin + new Vector3(0.6f, -0.15f, 0.8f);
                Vector3 cameraPosition = thirdPerson
                    ? origin + new Vector3(0.65f, 0f, -1.6f)
                    : origin + new Vector3(0f, 0f, -0.3f);
                target.transform.position = overlapBody
                    ? muzzle
                    : origin + new Vector3(thirdPerson ? 0.65f : 0f, 0f, 2f);
                target.layer = TestCollisionLayer;
                SphereCollider body = target.AddComponent<SphereCollider>();
                body.radius = 0.25f;
                body.isTrigger = true;
                target.AddComponent(GetRuntimeType("PlayerInfo"));
                Component health = target.AddComponent(GetRuntimeType("CharacterHealth"));
                target.AddComponent(GetRuntimeType("BodyHitbox"));
                prefab.AddComponent(GetRuntimeType("WeaponProjectileCtrl"));
                Component combat = shooter.AddComponent(GetRuntimeType("WeaponCombatCtrl"));
                ((Behaviour)combat).enabled = false;

                Type weaponType = GetRuntimeType("WeaponDefinitionSO");
                weapon = ScriptableObject.CreateInstance(weaponType);
                object damage = GetProperty(weapon, "Damage");
                SetField(damage, "_closeDamage", 25f);
                SetField(damage, "_farDamage", 25f);
                SetField(damage, "_maximumHitRange", 20f);
                SetField(damage, "_projectileSpeed", 20f);
                // 同时覆盖纯射线弹丸的零半径路径，避免依赖SphereCast半径0未定义行为。
                SetField(damage, "_projectileRadius", thirdPerson ? 0.015f : 0f);
                SetField(combat, "_weaponData", weapon);
                SetField(combat, "_projectilePrefab", prefab);
                SetField(combat, "_hitMask", (LayerMask)(1 << TestCollisionLayer));

                if (obstructMuzzle || overlapWall || addTrigger)
                {
                    obstacle = new GameObject("AimedProjectileTestObstacle");
                    obstacle.layer = TestCollisionLayer;
                    BoxCollider box = obstacle.AddComponent<BoxCollider>();
                    obstacle.transform.position = overlapWall || addTrigger
                        ? muzzle
                        : origin + new Vector3(0.13f, 0f, 0.4f);
                    box.size = addTrigger ? Vector3.one * 4f : new Vector3(0.3f, 0.6f, 0.15f);
                    box.isTrigger = addTrigger;
                }

                Physics.SyncTransforms();
                Vector3 cameraDirection = overlapBody
                    ? (target.transform.position - cameraPosition).normalized
                    : Vector3.forward;
                MethodInfo createRay = combat.GetType().GetMethod(
                    "CreateProjectileRay", BindingFlags.Instance | BindingFlags.NonPublic);
                Ray shotRay = (Ray)createRay.Invoke(combat,
                    new object[] { muzzle, new Ray(cameraPosition, cameraDirection), 20f });
                if (!overlapBody)
                {
                    Assert.That(Vector3.Angle(shotRay.direction, target.transform.position - muzzle),
                        Is.LessThan(7f), "枪口弹道应收敛到准星附近目标。");
                }
                MethodInfo spawn = combat.GetType().GetMethod(
                    "SpawnProjectile", BindingFlags.Instance | BindingFlags.NonPublic);
                spawn.Invoke(combat, new object[] { shotRay });
                pool = GetRuntimeType("ObjectPool").GetProperty("instance",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy).GetValue(null);
                Assert.That(GetProperty<float>(health, "CurrentHealth"), Is.EqualTo(100f),
                    "选准星目标和生成弹丸都不能直接结算伤害。");
                float deadline = Time.time + 0.5f;
                while (Time.time < deadline)
                {
                    yield return null;
                }

                float expectedHealth = obstructMuzzle || overlapWall ? 100f : 75f;
                Assert.That(GetProperty<float>(health, "CurrentHealth"), Is.EqualTo(expectedHealth));
            }
            finally
            {
                if (pool != null)
                {
                    Invoke(pool, "ClearPool", new[] { typeof(GameObject) }, prefab);
                }
                UnityEngine.Object.Destroy(shooter);
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(prefab);
                if (obstacle)
                {
                    UnityEngine.Object.Destroy(obstacle);
                }
                if (weapon)
                {
                    UnityEngine.Object.Destroy(weapon);
                }
            }
        }

        private static Type GetRuntimeType(string typeName)
        {
            Type type = Type.GetType(typeName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, "Assembly-CSharp 中缺少类型: " + typeName);
            return type;
        }

        private static object GetProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, target.GetType().Name + " 缺少属性: " + propertyName);
            return property.GetValue(target);
        }

        private static T GetProperty<T>(object target, string propertyName)
        {
            return (T)GetProperty(target, propertyName);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, target.GetType().Name + " 缺少字段: " + fieldName);
            field.SetValue(target, value);
        }

        private static object Invoke(object target, string methodName, params object[] arguments)
        {
            Type[] parameterTypes = new Type[arguments.Length];
            for (int i = 0; i < arguments.Length; i++)
            {
                parameterTypes[i] = arguments[i].GetType();
            }
            return Invoke(target, methodName, parameterTypes, arguments);
        }

        private static object Invoke(
            object target,
            string methodName,
            Type[] parameterTypes,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, target.GetType().Name + " 缺少方法: " + methodName);
            return method.Invoke(target, arguments);
        }
    }
}
