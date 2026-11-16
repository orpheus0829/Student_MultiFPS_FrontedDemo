using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class MatchDebugWindow : EditorWindow
{
    private static readonly string[] ModeNames = { "个人竞技", "短团队竞技", "爆破模式" };
    private static readonly string[] ModePaths =
    {
        MatchContentConfigurator.SoloModePath,
        MatchContentConfigurator.TdmModePath,
        MatchContentConfigurator.BombModePath
    };

    private int _modeIndex;
    private bool _healthRecoveryEnabled = true;

    [MenuItem("工具/Gun Demo/对局/对局调试窗口")]
    private static void Open()
    {
        GetWindow<MatchDebugWindow>("对局调试");
    }

    private void OnEnable()
    {
        EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
    }

    private void OnDisable()
    {
        EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("对局模式调试", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "场景直接 Play 仍保持 Idle 空名册。仅点击下方按钮才开始对局。",
            MessageType.Info);
        _modeIndex = EditorGUILayout.Popup("模式", _modeIndex, ModeNames);
        bool isBombMode = _modeIndex == (int)ModeKind.Bomb;
        using (new EditorGUI.DisabledScope(isBombMode))
        {
            bool displayedRecovery = EditorGUILayout.Toggle(
                "呼吸回血",
                !isBombMode && _healthRecoveryEnabled);
            if (!isBombMode)
            {
                _healthRecoveryEnabled = displayedRecovery;
            }
        }
        if (isBombMode)
        {
            EditorGUILayout.HelpBox("赏金爆破固定禁用呼吸回血。", MessageType.None);
        }
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            if (EditorApplication.isPlaying)
            {
                MatchManager manager = MatchManager.Instance;
                EditorGUILayout.LabelField("当前状态", manager.State.ToString());
                EditorGUILayout.LabelField("名册人数", manager.Roster.Count.ToString());
            }

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("开始对局", GUILayout.Width(90f)))
            {
                StartMatch();
            }
            if (GUILayout.Button("下一回合", GUILayout.Width(90f)))
            {
                MatchManager.Instance.BeginRound();
            }
            if (GUILayout.Button("停止对局", GUILayout.Width(90f)))
            {
                MatchManager.Instance.StopMatch();
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private void StartMatch()
    {
        ModeDefinitionSO mode = AssetDatabase.LoadAssetAtPath<ModeDefinitionSO>(
            ModePaths[Mathf.Clamp(_modeIndex, 0, ModePaths.Length - 1)]);
        if (!mode)
        {
            Debug.LogError("缺少内置模式资产，请先运行配置内置模式命令。");
            return;
        }

        StartConfiguredMatch(mode, _healthRecoveryEnabled);
    }

    public static bool StartConfiguredMatch(
        ModeDefinitionSO mode,
        bool healthRecoveryEnabled = true)
    {
        if (!EditorApplication.isPlaying || !mode)
        {
            return false;
        }

        if (!LocalPlayerLocator.TryGet(out GameObject localPlayer) ||
            !localPlayer.TryGetComponent(out PlayerInfo localInfo))
        {
            Debug.LogError("正式对局需要场景中存在本地玩家。");
            return false;
        }

        MapCatalogSO catalog = AssetDatabase.LoadAssetAtPath<MapCatalogSO>(
            MatchContentConfigurator.MapCatalogPath);
        MapDefinitionSO map = catalog
            ? catalog.FindByScene(SceneManager.GetActiveScene().name)
            : null;
        if (map && !map.SupportsMode(mode.Kind))
        {
            Debug.LogError($"地图 {map.DisplayName} 不支持模式 {mode.DisplayName}。");
            return false;
        }

        List<MatchSlotConfig> slots = new List<MatchSlotConfig>();
        int participantCount = Mathf.Max(1, mode.TeamCount * mode.TeamSize);
        for (int slot = 0; slot < participantCount; slot++)
        {
            int team = mode.Kind == ModeKind.Solo
                ? slot
                : slot % Mathf.Max(1, mode.TeamCount);
            bool isLocalPlayer = slot == 0;
            slots.Add(new MatchSlotConfig(
                slot,
                team,
                isLocalPlayer ? localInfo.PlayerName : $"AI 玩家 {slot}",
                !isLocalPlayer,
                isLocalPlayer,
                string.Empty,
                AIDifficulty.Normal));
        }

        MatchConfig config = new MatchConfig(mode, slots, map);
        config.Registry.Set(
            "combat.healthRecoveryEnabled",
            mode.Kind != ModeKind.Bomb && healthRecoveryEnabled);
        MatchManager manager = MatchManager.Instance;
        return manager.StartMatch(config);
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        Repaint();
    }
}
