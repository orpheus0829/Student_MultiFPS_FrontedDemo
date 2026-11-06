using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GunDemo.Tests
{
    public sealed class CombatCoreTests
    {
        private readonly List<GameObject> _createdObjects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject createdObject in _createdObjects)
            {
                if (createdObject)
                {
                    Component score = createdObject.GetComponent(GetRuntimeType("CombatScore"));
                    if (score)
                    {
                        InvokeNonPublic(score, "OnDisable");
                    }

                    UnityEngine.Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void PlayerNameUpdatesRootObjectName()
        {
            GameObject player = CreateObject("OldName");
            Component info = AddComponent(player, "PlayerInfo");
            Invoke(info, "SetPlayerName", "  Alice  ");

            Assert.That(GetProperty<string>(info, "PlayerName"), Is.EqualTo("Alice"));
            Assert.That(player.name, Is.EqualTo("Alice"));

            SerializedObject serializedInfo = new SerializedObject(info);
            serializedInfo.FindProperty("_playerName").stringValue = "Bob";
            serializedInfo.ApplyModifiedPropertiesWithoutUndo();
            InvokeNonPublic(info, "OnValidate");

            Assert.That(GetProperty<string>(info, "PlayerName"), Is.EqualTo("Bob"));
            Assert.That(player.name, Is.EqualTo("Bob"));
        }

        [Test]
        public void PlayerInfoOnChildOnlyUpdatesItsOwnGameObjectName()
        {
            GameObject playerRoot = CreateObject("PlayerRoot");
            GameObject identityObject = CreateObject("Identity");
            identityObject.transform.SetParent(playerRoot.transform);
            Component info = AddComponent(identityObject, "PlayerInfo");
            Invoke(info, "SetPlayerName", "Alice");

            Assert.That(GetProperty<string>(info, "PlayerName"), Is.EqualTo("Alice"));
            Assert.That(playerRoot.name, Is.EqualTo("PlayerRoot"));
            Assert.That(identityObject.name, Is.EqualTo("Alice"));
        }

        [Test]
        public void AppliedDamageAwardsCalculatedHitScore()
        {
            Component attacker = CreatePlayer("Attacker", out Component score, out Component unusedHealth);
            CreatePlayer("Victim", out Component unusedScore, out Component victimHealth);
            object request = CreateDamageRequest(victimHealth, 25f);
            SetProperty(request, "Instigator", attacker);
            SetProperty(request, "BaseHitScore", 10f);
            SetProperty(request, "WeaponScoreFactor", 2f);
            SetProperty(request, "DistanceScoreFactor", 1.5f);
            SetProperty(request, "BodyScoreFactor", 1f);

            object result = ApplyDamage(request);

            Assert.That(GetProperty(result, "Outcome").ToString(), Is.EqualTo("Hit"));
            Assert.That(GetProperty<float>(result, "AppliedDamage"), Is.EqualTo(25f));
            Assert.That(GetProperty<float>(victimHealth, "CurrentHealth"), Is.EqualTo(75f));
            Assert.That(GetProperty<int>(result, "AwardedScore"), Is.EqualTo(30));
            Assert.That(GetProperty<int>(score, "TotalScore"), Is.EqualTo(30));
        }

        [Test]
        public void KillAwardsOnceAndDeadTargetIgnoresFurtherDamage()
        {
            Component attacker = CreatePlayer("Attacker", out Component score, out Component unusedHealth);
            CreatePlayer("Victim", out Component unusedScore, out Component victimHealth);
            object request = CreateDamageRequest(victimHealth, 150f);
            SetProperty(request, "Instigator", attacker);
            SetProperty(request, "BaseHitScore", 20f);
            SetProperty(request, "KillBonus", 100f);

            int resolvedCount = 0;
            EventInfo damageResolved = GetDamageResolvedEvent();
            Delegate handler = CreateResultHandler(
                GetRuntimeType("DamageResult"),
                delegate { resolvedCount++; });
            damageResolved.AddEventHandler(null, handler);

            try
            {
                object killResult = ApplyDamage(request);
                object ignoredResult = ApplyDamage(request);

                Assert.That(GetProperty(killResult, "Outcome").ToString(), Is.EqualTo("Kill"));
                Assert.That(GetProperty<float>(killResult, "AppliedDamage"), Is.EqualTo(100f));
                Assert.That(GetProperty<int>(killResult, "AwardedScore"), Is.EqualTo(120));
                Assert.That(GetProperty(ignoredResult, "Outcome").ToString(), Is.EqualTo("Ignored"));
                Assert.That(GetProperty<int>(score, "TotalScore"), Is.EqualTo(120));
                Assert.That(resolvedCount, Is.EqualTo(1));
            }
            finally
            {
                damageResolved.RemoveEventHandler(null, handler);
            }
        }

        [Test]
        public void EmptyInstigatorCanKillWithoutAwardingScore()
        {
            CreatePlayer("Victim", out Component victimScore, out Component victimHealth);
            object request = CreateDamageRequest(victimHealth, 0f);
            SetProperty(request, "KillBonus", 100f);
            SetProperty(request, "SourceId", "environment.fall");

            object result = InvokeStatic(
                GetRuntimeType("CombatDamage"),
                "Kill",
                new[] { GetRuntimeType("DamageRequest") },
                request);

            Assert.That(GetProperty(result, "Outcome").ToString(), Is.EqualTo("Kill"));
            Assert.That(GetProperty(result, "Instigator"), Is.Null);
            Assert.That(GetProperty<string>(result, "SourceId"), Is.EqualTo("environment.fall"));
            Assert.That(GetProperty<int>(result, "AwardedScore"), Is.Zero);
            Assert.That(GetProperty<int>(victimScore, "TotalScore"), Is.Zero);
        }

        [Test]
        public void SimpleDamageApiAcceptsEmptyInstigator()
        {
            CreatePlayer("Victim", out Component unusedScore, out Component victimHealth);
            object result = InvokeStatic(
                GetRuntimeType("CombatDamage"),
                "Apply",
                new[]
                {
                    GetRuntimeType("CharacterHealth"),
                    typeof(float),
                    GetRuntimeType("PlayerInfo")
                },
                victimHealth,
                15f,
                null);

            Assert.That(GetProperty(result, "Outcome").ToString(), Is.EqualTo("Hit"));
            Assert.That(GetProperty(result, "Instigator"), Is.Null);
            Assert.That(GetProperty<float>(result, "AppliedDamage"), Is.EqualTo(15f));
            Assert.That(GetProperty<float>(victimHealth, "CurrentHealth"), Is.EqualTo(85f));
        }

        [Test]
        public void SelfKillDoesNotAwardScore()
        {
            Component player = CreatePlayer("Player", out Component score, out Component health);
            object result = InvokeStatic(
                GetRuntimeType("CombatDamage"),
                "Kill",
                new[] { GetRuntimeType("CharacterHealth"), GetRuntimeType("PlayerInfo") },
                health,
                player);

            Assert.That(GetProperty(result, "Outcome").ToString(), Is.EqualTo("Kill"));
            Assert.That(GetProperty<int>(result, "AwardedScore"), Is.Zero);
            Assert.That(GetProperty<int>(score, "TotalScore"), Is.Zero);
        }

        [Test]
        public void BodyHitboxSuppliesDamageAndScoreFactors()
        {
            Component attacker = CreatePlayer("Attacker", out Component score, out Component unusedHealth);
            GameObject victim = CreatePlayerObject("Victim", out Component victimHealth);
            GameObject headObject = CreateObject("Head");
            headObject.transform.SetParent(victim.transform);
            Component hitbox = AddComponent(headObject, "BodyHitbox");
            SerializedObject serializedHitbox = new SerializedObject(hitbox);
            SerializedProperty bodyPart = serializedHitbox.FindProperty("_bodyPart");
            bodyPart.enumValueIndex = FindEnumIndex(bodyPart, "Head");
            serializedHitbox.FindProperty("_damageFactor").floatValue = 2f;
            serializedHitbox.FindProperty("_scoreFactor").floatValue = 3f;
            serializedHitbox.ApplyModifiedPropertiesWithoutUndo();
            InvokeNonPublic(hitbox, "OnValidate");

            object request = CreateDamageRequest(victimHealth, 20f);
            SetProperty(request, "Instigator", attacker);
            SetProperty(request, "BaseHitScore", 10f);
            object result = Invoke(
                hitbox,
                "ReceiveHit",
                new[] { GetRuntimeType("DamageRequest") },
                request);

            Assert.That(GetProperty(result, "Outcome").ToString(), Is.EqualTo("Hit"));
            Assert.That(GetProperty<bool>(result, "WasHeadshot"), Is.True);
            Assert.That(GetProperty<float>(result, "AppliedDamage"), Is.EqualTo(40f));
            Assert.That(GetProperty<int>(result, "AwardedScore"), Is.EqualTo(30));
            Assert.That(GetProperty<int>(score, "TotalScore"), Is.EqualTo(30));
        }

        [Test]
        public void CombatScoreOnDisableRemovesSubscription()
        {
            int subscriberCountBefore = GetDamageResolvedSubscriberCount();
            CreatePlayer("Player", out Component score, out Component unusedHealth);

            Assert.That(
                GetDamageResolvedSubscriberCount(),
                Is.EqualTo(subscriberCountBefore + 1));

            InvokeNonPublic(score, "OnDisable");
            UnityEngine.Object.DestroyImmediate(score);

            Assert.That(
                GetDamageResolvedSubscriberCount(),
                Is.EqualTo(subscriberCountBefore));
        }

        [Test]
        public void DamageResolvedCannotReenterSameTarget()
        {
            Component attacker = CreatePlayer("Attacker", out Component unusedScore, out Component unusedHealth);
            CreatePlayer("Victim", out Component victimScore, out Component victimHealth);
            object request = CreateDamageRequest(victimHealth, 10f);
            SetProperty(request, "Instigator", attacker);
            SetProperty(request, "BaseHitScore", 10f);

            object nestedResult = null;
            bool attemptedReentry = false;
            EventInfo damageResolved = GetDamageResolvedEvent();
            Delegate handler = CreateResultHandler(
                GetRuntimeType("DamageResult"),
                delegate
                {
                    if (attemptedReentry)
                    {
                        return;
                    }

                    attemptedReentry = true;
                    nestedResult = ApplyDamage(request);
                });
            damageResolved.AddEventHandler(null, handler);

            try
            {
                object result = ApplyDamage(request);

                Assert.That(GetProperty(result, "Outcome").ToString(), Is.EqualTo("Hit"));
                Assert.That(attemptedReentry, Is.True);
                Assert.That(nestedResult, Is.Not.Null);
                Assert.That(GetProperty(nestedResult, "Outcome").ToString(), Is.EqualTo("Ignored"));
                Assert.That(GetProperty<float>(victimHealth, "CurrentHealth"), Is.EqualTo(90f));
                Assert.That(GetProperty<int>(victimScore, "TotalScore"), Is.Zero);
            }
            finally
            {
                damageResolved.RemoveEventHandler(null, handler);
            }
        }

        private Component CreatePlayer(
            string playerName,
            out Component score,
            out Component health)
        {
            GameObject player = CreatePlayerObject(playerName, out health);
            Component info = player.GetComponent(GetRuntimeType("PlayerInfo"));
            score = AddComponent(player, "CombatScore");
            InvokeNonPublic(score, "Awake");
            InvokeNonPublic(score, "OnEnable");
            return info;
        }

        private GameObject CreatePlayerObject(string playerName, out Component health)
        {
            GameObject player = CreateObject(playerName);
            Component info = AddComponent(player, "PlayerInfo");
            Invoke(info, "SetPlayerName", playerName);
            health = AddComponent(player, "CharacterHealth");
            InvokeNonPublic(health, "Awake");
            return player;
        }

        private GameObject CreateObject(string objectName)
        {
            GameObject createdObject = new GameObject(objectName);
            _createdObjects.Add(createdObject);
            return createdObject;
        }

        private Component AddComponent(GameObject target, string typeName)
        {
            return target.AddComponent(GetRuntimeType(typeName));
        }

        private object CreateDamageRequest(Component health, float damage)
        {
            return Activator.CreateInstance(GetRuntimeType("DamageRequest"), health, damage);
        }

        private object ApplyDamage(object request)
        {
            return InvokeStatic(
                GetRuntimeType("CombatDamage"),
                "Apply",
                new[] { GetRuntimeType("DamageRequest") },
                request);
        }

        private Type GetRuntimeType(string typeName)
        {
            Type type = Type.GetType(typeName + ", Assembly-CSharp");
            Assert.That(type, Is.Not.Null, "Assembly-CSharp 中缺少类型: " + typeName);
            return type;
        }

        private T GetProperty<T>(object target, string propertyName)
        {
            return (T)GetProperty(target, propertyName);
        }

        private object GetProperty(object target, string propertyName)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, target.GetType().Name + " 缺少属性: " + propertyName);
            return property.GetValue(target);
        }

        private void SetProperty(object target, string propertyName, object value)
        {
            PropertyInfo property = target.GetType().GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public);
            Assert.That(property, Is.Not.Null, target.GetType().Name + " 缺少属性: " + propertyName);
            property.SetValue(target, value);
        }

        private object Invoke(object target, string methodName, params object[] arguments)
        {
            return Invoke(target, methodName, GetArgumentTypes(arguments), arguments);
        }

        private object Invoke(
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

        private object InvokeNonPublic(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null, target.GetType().Name + " 缺少非公开方法: " + methodName);
            return method.Invoke(target, null);
        }

        private object InvokeStatic(
            Type targetType,
            string methodName,
            Type[] parameterTypes,
            params object[] arguments)
        {
            MethodInfo method = targetType.GetMethod(
                methodName,
                BindingFlags.Static | BindingFlags.Public,
                null,
                parameterTypes,
                null);
            Assert.That(method, Is.Not.Null, targetType.Name + " 缺少静态方法: " + methodName);
            return method.Invoke(null, arguments);
        }

        private Type[] GetArgumentTypes(object[] arguments)
        {
            Type[] types = new Type[arguments.Length];
            for (int index = 0; index < arguments.Length; index++)
            {
                types[index] = arguments[index].GetType();
            }

            return types;
        }

        private int FindEnumIndex(SerializedProperty property, string enumName)
        {
            for (int index = 0; index < property.enumNames.Length; index++)
            {
                if (property.enumNames[index] == enumName)
                {
                    return index;
                }
            }

            Assert.Fail("枚举中缺少选项: " + enumName);
            return 0;
        }

        private EventInfo GetDamageResolvedEvent()
        {
            EventInfo damageResolved = GetRuntimeType("CombatDamage").GetEvent(
                "DamageResolved",
                BindingFlags.Public | BindingFlags.Static);
            Assert.That(damageResolved, Is.Not.Null, "CombatDamage 缺少 DamageResolved 事件");
            return damageResolved;
        }

        private int GetDamageResolvedSubscriberCount()
        {
            FieldInfo eventField = GetRuntimeType("CombatDamage").GetField(
                "DamageResolved",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(eventField, Is.Not.Null, "无法读取 DamageResolved 事件后端字段");
            Delegate handlers = eventField.GetValue(null) as Delegate;
            return handlers == null ? 0 : handlers.GetInvocationList().Length;
        }

        private Delegate CreateResultHandler(Type resultType, Action<object> callback)
        {
            MethodInfo factory = GetType().GetMethod(
                nameof(CreateResultHandlerGeneric),
                BindingFlags.Static | BindingFlags.NonPublic);
            MethodInfo genericFactory = factory.MakeGenericMethod(resultType);
            return (Delegate)genericFactory.Invoke(null, new object[] { callback });
        }

        private static Delegate CreateResultHandlerGeneric<TResult>(Action<object> callback)
        {
            Action<TResult> handler = delegate(TResult result)
            {
                callback(result);
            };
            return handler;
        }
    }
}
