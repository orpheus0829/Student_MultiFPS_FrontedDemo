using UnityEditor;
using UnityEngine;

/// <summary>
/// 进入 Play 模式后，把编辑器偏好里的"下次试玩的默认角色"应用到本地玩家。
/// AI 是独立实例，本来就不该被本地玩家的偏好影响，因此只对本地玩家生效。
/// 本类只读 EditorPrefs，不写任何资产。
/// </summary>
[InitializeOnLoad]
internal static class CombatDebugDefaultModelApplier
{
    private const float ApplyTimeoutSeconds = 20f;
    private static double _deadline;
    private static bool _multiplePlayersReported;

    static CombatDebugDefaultModelApplier()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            _deadline = EditorApplication.timeSinceStartup + ApplyTimeoutSeconds;
            _multiplePlayersReported = false;
            EditorApplication.update += ApplyUntilReady;
        }
        else if (change == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= ApplyUntilReady;
        }
    }

    private static void ApplyUntilReady()
    {
        string modelId = CombatDebugDefaultModelStore.Load();
        if (string.IsNullOrEmpty(modelId))
        {
            EditorApplication.update -= ApplyUntilReady;
            return;
        }

        if (EditorApplication.timeSinceStartup > _deadline)
        {
            EditorApplication.update -= ApplyUntilReady;
            Debug.LogWarning("战斗调试：等待本地玩家超时，未应用默认角色。");
            return;
        }

        PlayerInputCtrl localPlayer = FindLocalPlayer();
        if (!localPlayer)
        {
            return;
        }

        PlayerCharacterModelCtrl models =
            localPlayer.GetComponent<PlayerCharacterModelCtrl>();
        if (!models || string.IsNullOrEmpty(models.CurrentModelId) || models.IsSwitching)
        {
            // 尚未 Awake / 正在切换，下一帧再试。
            return;
        }

        EditorApplication.update -= ApplyUntilReady;
        if (!string.Equals(models.CurrentModelId, modelId, System.StringComparison.Ordinal))
        {
            _ = models.SwitchModelAsync(modelId);
        }
    }

    private static PlayerInputCtrl FindLocalPlayer()
    {
        PlayerInputCtrl[] players = Object.FindObjectsByType<PlayerInputCtrl>(
            FindObjectsSortMode.None);
        PlayerInputCtrl localPlayer = null;
        int enabledCount = 0;
        for (int i = 0; i < players.Length; i++)
        {
            if (!players[i].enabled)
            {
                continue;
            }

            enabledCount++;
            if (enabledCount == 1)
            {
                localPlayer = players[i];
            }
        }

        // 本地玩家必须唯一：误取多个中的一个会把 AI 当成本地玩家换模型，
        // 正好违反"实例数据隔离"原则，所以宁可放弃应用也不猜。
        if (enabledCount > 1)
        {
            if (!_multiplePlayersReported)
            {
                _multiplePlayersReported = true;
                Debug.LogError("发现多个本地玩家，已放弃应用默认角色");
            }
            return null;
        }

        return localPlayer;
    }
}
