using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

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

        StartConfiguredMatch(mode);
    }

    public static bool StartConfiguredMatch(ModeDefinitionSO mode)
    {
        if (!EditorApplication.isPlaying || !mode)
        {
            return false;
        }

        IReadOnlyList<CharacterHealth> actors = CharacterHealth.Actors;
        List<MatchSlotConfig> slots = new List<MatchSlotConfig>();
        List<CharacterHealth> participants = new List<CharacterHealth>();
        LocalPlayerLocator.TryGet(out GameObject localPlayer);
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterHealth actor = actors[i];
            if (!actor || !actor.PlayerInfo)
            {
                continue;
            }

            bool isAI = actor.TryGetComponent(out AIPlayerCtrl _);
            bool isLocalPlayer = localPlayer && actor.gameObject == localPlayer;
            int slot = slots.Count;
            int team = mode.Kind == ModeKind.Solo
                ? slot
                : slot % Mathf.Max(1, mode.TeamCount);
            slots.Add(new MatchSlotConfig(
                slot,
                team,
                actor.PlayerInfo.PlayerName,
                isAI,
                isLocalPlayer));
            participants.Add(actor);
        }

        MatchManager manager = MatchManager.Instance;
        if (!manager.StartMatch(new MatchConfig(mode, slots)))
        {
            return false;
        }

        for (int i = 0; i < participants.Count; i++)
        {
            manager.BindParticipant(i, participants[i]);
        }

        return true;
    }

    private void HandlePlayModeStateChanged(PlayModeStateChange state)
    {
        Repaint();
    }
}
