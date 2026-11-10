using UnityEditor;

/// <summary>
/// 「下次试玩的默认角色」属于本地玩家的选择数据，只存编辑器偏好，
/// 不写入任何会被其他实例继承的资产，避免污染 Player.prefab 及其实例变体。
/// </summary>
internal static class CombatDebugDefaultModelStore
{
    private const string DefaultModelKey = "GunDemo.CombatDebug.DefaultModelId";

    public static string Load()
    {
        return EditorPrefs.GetString(DefaultModelKey, string.Empty);
    }

    public static void Save(string modelId)
    {
        if (string.IsNullOrEmpty(modelId))
        {
            EditorPrefs.DeleteKey(DefaultModelKey);
            return;
        }

        EditorPrefs.SetString(DefaultModelKey, modelId);
    }

    public static void Clear()
    {
        EditorPrefs.DeleteKey(DefaultModelKey);
    }
}
