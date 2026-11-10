using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GunDemo.Tests
{
    /// <summary>
    /// SPEC-003：调试默认角色必须存编辑器偏好，Player.prefab 基类必须恒为默认角色，
    /// 且存储/应用两端都不得写资产。测试程序集不引用 Assembly-CSharp(-Editor)，
    /// 因此运行时类型统一走反射，与现有 EditMode 测试一致。
    /// </summary>
    public sealed class PlayerPrefabDefaultModelEditModeTests
    {
        private const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
        private const string PlayerPrefabFileRelativePath = "Prefabs/Characters/Player.prefab";
        private const string AiPrefabPath = "Assets/Prefabs/Characters/IdleAIPlayer.prefab";
        private const string DefaultModelId = "character.anbi";
        private const string StoreSourcePath = "Scripts/Player/Editor/CombatDebugDefaultModelStore.cs";
        private const string ApplierSourcePath = "Scripts/Player/Editor/CombatDebugDefaultModelApplier.cs";
        private const string WindowSourcePath = "Scripts/Player/Editor/CombatDebugWindow.cs";

        /// <summary>
        /// 每条用例自己的结果落到文本文件，用于绕开"控制台只回传错误"造成的
        /// "没报错就当通过"。路径相对工程根，文件在 Temp 下不进版本库。
        /// </summary>
        private static readonly string ResultLogPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", "Temp", "AgentReports",
                "spec003-editmode-results.txt"));

        // 每条用例都会 Append，[SetUp] 不能清空文件，否则只剩最后一条。
        // 清空由运行侧在开跑前做一次。
        [TearDown]
        public void RecordResult()
        {
            string directory = Path.GetDirectoryName(ResultLogPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }
            string status = "PASS " + TestContext.CurrentContext.Test.Name;
            if (TestContext.CurrentContext.Result.Outcome.Status != NUnit.Framework.Interfaces.TestStatus.Passed)
            {
                status = "FAIL " + TestContext.CurrentContext.Test.Name + " | " +
                    TestContext.CurrentContext.Result.Message.Replace('\n', ' ').Replace('\r', ' ');
            }
            File.AppendAllText(ResultLogPath, status + Environment.NewLine);
        }

        [Test]
        public void PlayerPrefab_InitialModelId_EqualsDefaultAnbi()
        {
            AssertPrefabInitialModelId(PlayerPrefabPath);
        }

        [Test]
        public void IdleAIPlayer_EffectiveInitialModelId_EqualsAnbi()
        {
            AssertPrefabInitialModelId(AiPrefabPath);
        }

        [Test]
        public void Store_RoundTripsThroughEditorPrefsOnly()
        {
            Type storeType = RequireStoreType();
            string original = (string)InvokeStore(storeType, "Load", null);
            try
            {
                InvokeStore(storeType, "Save", new object[] { "character.zhuyuan" });
                Assert.That((string)InvokeStore(storeType, "Load", null),
                    Is.EqualTo("character.zhuyuan"), "Save 后 Load 未取回写入值");
                InvokeStore(storeType, "Save", new object[] { string.Empty });
                Assert.That((string)InvokeStore(storeType, "Load", null), Is.Empty,
                    "Save 空串后应视为未设置");
                InvokeStore(storeType, "Clear", null);
                Assert.That((string)InvokeStore(storeType, "Load", null), Is.Empty,
                    "Clear 后应视为未设置");
            }
            finally
            {
                // 不把用户本机的调试偏好弄丢。
                InvokeStore(storeType, "Save", new object[] { original });
            }
        }

        [Test]
        public void Store_DoesNotTouchAssets()
        {
            string path = FullPath(PlayerPrefabFileRelativePath);
            byte[] before = File.ReadAllBytes(path);
            Type storeType = RequireStoreType();
            string original = (string)InvokeStore(storeType, "Load", null);
            try
            {
                InvokeStore(storeType, "Save", new object[] { "character.zhuyuan" });
                InvokeStore(storeType, "Load", null);
                InvokeStore(storeType, "Save", new object[] { string.Empty });
                InvokeStore(storeType, "Clear", null);
            }
            finally
            {
                InvokeStore(storeType, "Save", new object[] { original });
            }

            byte[] after = File.ReadAllBytes(path);
            Assert.That(after, Is.EqualTo(before),
                "默认角色存取循环改动了 Player.prefab 文件字节");
        }

        [Test]
        public void Store_SourceHasNoAssetWriteCalls()
        {
            string source = ReadSource(StoreSourcePath);
            AssertSourceExcludes(source, StoreSourcePath, new string[]
            {
                "AssetDatabase", "SerializedObject", "PrefabUtility",
                "SaveAssets", "SetDirty", "EditorUtility"
            });
        }

        [Test]
        public void Applier_SourceHasNoAssetWriteCalls()
        {
            string source = ReadSource(ApplierSourcePath);
            AssertSourceExcludes(source, ApplierSourcePath, new string[]
            {
                "AssetDatabase", "SerializedObject", "PrefabUtility",
                "SaveAssets", "SetDirty"
            });
        }

        [Test]
        public void Migrate_HasPlayModeGuard()
        {
            string source = ReadSource(WindowSourcePath);
            int signature = source.IndexOf(
                "MigrateDebugDefaultModel()", StringComparison.Ordinal);
            Assert.That(signature, Is.GreaterThanOrEqualTo(0),
                "CombatDebugWindow 里找不到 MigrateDebugDefaultModel");
            int bodyEnd = source.IndexOf(
                "private static", signature + 40, StringComparison.Ordinal);
            string body = bodyEnd > signature
                ? source.Substring(signature, bodyEnd - signature)
                : source.Substring(signature);
            int guard = body.IndexOf("EditorApplication.isPlaying", StringComparison.Ordinal);
            int assetWrite = body.IndexOf("AssetDatabase", StringComparison.Ordinal);
            Assert.That(guard, Is.GreaterThanOrEqualTo(0),
                "迁移菜单缺少 Play Mode 防护");
            Assert.That(assetWrite, Is.GreaterThan(guard),
                "Play Mode 防护必须写在迁移方法开头、任何资产写入之前");
            Assert.That(body.IndexOf("Play Mode 中禁止执行迁移", StringComparison.Ordinal),
                Is.GreaterThanOrEqualTo(0), "迁移菜单的 Play Mode 报错文案缺失");
        }

        private static void AssertPrefabInitialModelId(string prefabPath)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath + " 不存在");
            Type modelType = Type.GetType("PlayerCharacterModelCtrl, Assembly-CSharp");
            Assert.That(modelType, Is.Not.Null, "缺少 PlayerCharacterModelCtrl 类型");
            Component models = prefab.GetComponentInChildren(modelType, true) as Component;
            Assert.That(models, Is.Not.Null, prefabPath + " 缺少 PlayerCharacterModelCtrl");
            SerializedObject serialized = new SerializedObject(models);
            SerializedProperty initialModel = serialized.FindProperty("_initialModelId");
            Assert.That(initialModel, Is.Not.Null, "缺少 _initialModelId 字段");
            Assert.That(initialModel.stringValue, Is.EqualTo(DefaultModelId),
                prefabPath + " 生效初始模型是 " + initialModel.stringValue +
                "，不是 " + DefaultModelId);
        }

        private static Type RequireStoreType()
        {
            Type storeType = Type.GetType(
                "CombatDebugDefaultModelStore, Assembly-CSharp-Editor");
            Assert.That(storeType, Is.Not.Null, "缺少 CombatDebugDefaultModelStore");
            return storeType;
        }

        private static object InvokeStore(Type storeType, string methodName, object[] arguments)
        {
            MethodInfo method = storeType.GetMethod(
                methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null,
                "CombatDebugDefaultModelStore 缺少 " + methodName);
            return method.Invoke(null, arguments);
        }

        private static string ReadSource(string relativePath)
        {
            string path = FullPath(relativePath);
            Assert.That(File.Exists(path), Is.True, path + " 不存在");
            string source = File.ReadAllText(path);
            return source;
        }

        private static string FullPath(string assetRelativePath)
        {
            // Application.dataPath 本身已以 Assets 结尾，这里只接 "Prefabs/..." 这类
            // 不含 Assets 前缀的路径，避免拼出 Assets\Assets\...
            Assert.That(assetRelativePath.StartsWith("Assets/", StringComparison.Ordinal),
                Is.False, "传入路径不应再带 Assets/ 前缀：" + assetRelativePath);
            return Path.Combine(Application.dataPath, assetRelativePath);
        }

        private static void AssertSourceExcludes(
            string source, string sourcePath, string[] forbiddenTokens)
        {
            for (int i = 0; i < forbiddenTokens.Length; i++)
            {
                Assert.That(source.IndexOf(forbiddenTokens[i], StringComparison.Ordinal),
                    Is.LessThan(0),
                    sourcePath + " 不应出现 " + forbiddenTokens[i]);
            }
        }
    }
}
