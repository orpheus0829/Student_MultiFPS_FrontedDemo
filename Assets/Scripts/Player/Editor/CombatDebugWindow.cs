using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class CombatDebugWindow : EditorWindow
{
    private const string AiPrefabId = "character.ai_player.prefab";
    private static readonly List<GameObject> SpawnedAi = new List<GameObject>();
    private int _selectedModelIndex;
    private string _switchMessage;

    [MenuItem("工具/Gun Demo/战斗调试")]
    private static void OpenWindow()
    {
        GetWindow<CombatDebugWindow>("战斗调试");
    }

    private void OnGUI()
    {
        DrawDefaultModelSelection();
        GUILayout.Space(8f);
        EditorGUILayout.HelpBox("以下临时调试按钮仅在 Play 模式使用；生成的 AI 不受场景复活区管理。", MessageType.Info);
        using (new EditorGUI.DisabledScope(!EditorApplication.isPlaying))
        {
            GUILayout.Label("生成", EditorStyles.boldLabel);
            if (GUILayout.Button("在前方生成站桩 AI", GUILayout.ExpandWidth(false)))
            {
                SpawnAiInFront();
            }

            GUILayout.Space(8f);
            GUILayout.Label("生命", EditorStyles.boldLabel);
            if (GUILayout.Button("自杀", GUILayout.ExpandWidth(false)))
            {
                KillLocalPlayer();
            }
            if (GUILayout.Button("击杀最近的 AI", GUILayout.ExpandWidth(false)))
            {
                KillNearestAi();
            }
            if (GUILayout.Button("补满生命", GUILayout.ExpandWidth(false)))
            {
                RestoreLocalHealth();
            }

            PlayerInputCtrl localPlayer = FindLocalPlayer();
            PlayerCharacterModelCtrl models = localPlayer
                ? localPlayer.GetComponent<PlayerCharacterModelCtrl>() : null;
            if (models && models.ModelCount > 0)
            {
                GUILayout.Space(8f);
                GUILayout.Label("角色模型", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("当前角色 ID", models.CurrentModelId);
                if (!string.IsNullOrWhiteSpace(_switchMessage))
                {
                    EditorGUILayout.HelpBox(_switchMessage, MessageType.None);
                }
                string[] names = new string[models.ModelCount];
                for (int i = 0; i < names.Length; i++)
                {
                    names[i] = models.GetModelName(i);
                }
                _selectedModelIndex = Mathf.Clamp(_selectedModelIndex, 0, names.Length - 1);
                _selectedModelIndex = EditorGUILayout.Popup("切换为", _selectedModelIndex, names);
                using (new EditorGUI.DisabledScope(models.IsSwitching))
                {
                    if (GUILayout.Button("切换角色", GUILayout.ExpandWidth(false)))
                    {
                        SwitchLocalModel(models, models.GetModelId(_selectedModelIndex));
                    }
                }
            }

            GUILayout.Space(8f);
            GUILayout.Label("战斗字幕测试", EditorStyles.boldLabel);
            if (GUILayout.Button("让我说一条换弹语音", GUILayout.ExpandWidth(false)))
            {
                SpeakLocalVoice();
            }
            if (GUILayout.Button("让最近的 AI 说一条受伤语音", GUILayout.ExpandWidth(false)))
            {
                SpeakNearestAiVoice();
            }

            GUILayout.Space(8f);
            GUILayout.Label("清理", EditorStyles.boldLabel);
            if (GUILayout.Button("清理调试生成的 AI", GUILayout.ExpandWidth(false)))
            {
                ClearSpawnedAi();
            }
        }
    }

    private static void DrawDefaultModelSelection()
    {
        PlayerCharacterModelCtrl models = LoadPlayerPrefabModels();
        if (!models || models.ModelCount == 0)
        {
            return;
        }

        GUILayout.Label("下次试玩的默认角色", EditorStyles.boldLabel);
        string[] names = new string[models.ModelCount];
        string[] ids = new string[models.ModelCount];
        string saved = CombatDebugDefaultModelStore.Load();
        int currentIndex = 0;
        for (int i = 0; i < models.ModelCount; i++)
        {
            names[i] = models.GetModelName(i);
            ids[i] = models.GetModelId(i);
            if (string.Equals(ids[i], saved, System.StringComparison.Ordinal))
            {
                currentIndex = i;
            }
        }

        using (new EditorGUI.DisabledScope(EditorApplication.isPlaying))
        {
            int selectedIndex = EditorGUILayout.Popup("默认角色", currentIndex, names);
            if (selectedIndex != currentIndex)
            {
                CombatDebugDefaultModelStore.Save(ids[selectedIndex]);
            }
        }

        EditorGUILayout.HelpBox(
            "存在本机编辑器偏好，仅对本地玩家生效，不写入任何 Prefab。",
            MessageType.None);
    }

    /// <summary>
    /// 只读加载 Player.prefab 以取得模型名单（下拉项），不写回任何数据。
    /// </summary>
    private static PlayerCharacterModelCtrl LoadPlayerPrefabModels()
    {
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Characters/Player.prefab");
        return playerPrefab
            ? playerPrefab.GetComponentInChildren<PlayerCharacterModelCtrl>(true)
            : null;
    }

    [MenuItem("工具/Gun Demo/迁移调试默认角色到编辑器偏好")]
    private static void MigrateDebugDefaultModel()
    {
        const string prefabPath = "Assets/Prefabs/Characters/Player.prefab";
        // 必须与 PlayerCharacterModelCtrl.DefaultModelId 保持一致。
        const string defaultId = "character.anbi";

        if (EditorApplication.isPlaying)
        {
            Debug.LogError("战斗调试迁移：Play Mode 中禁止执行迁移，请先退出 Play Mode。");
            return;
        }

        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (!playerPrefab)
        {
            Debug.LogError($"战斗调试迁移：找不到 {prefabPath}。");
            return;
        }
        PlayerCharacterModelCtrl models = playerPrefab
            .GetComponentInChildren<PlayerCharacterModelCtrl>(true);
        if (!models)
        {
            Debug.LogError("战斗调试迁移：Player.prefab 缺少 PlayerCharacterModelCtrl。");
            return;
        }

        SerializedObject serialized = new SerializedObject(models);
        SerializedProperty initialModel = serialized.FindProperty("_initialModelId");
        string before = initialModel.stringValue;

        if (string.IsNullOrEmpty(before) ||
            string.Equals(before, defaultId, System.StringComparison.Ordinal))
        {
            Debug.Log($"战斗调试迁移：基类初始模型已是默认值 {before ?? "<空>"}，无需迁移。");
            return;
        }

        if (string.IsNullOrEmpty(CombatDebugDefaultModelStore.Load()))
        {
            CombatDebugDefaultModelStore.Save(before);
            Debug.Log($"战斗调试迁移：用户偏好 {before} 已存入编辑器偏好。");
        }
        else
        {
            Debug.Log(
                $"战斗调试迁移：编辑器偏好已存在 {CombatDebugDefaultModelStore.Load()}，不覆盖。");
        }

        Undo.RecordObject(models, "迁移调试默认角色");
        initialModel.stringValue = defaultId;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(models);
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"战斗调试迁移：Player.prefab._initialModelId 写前 {before} → 写后 {defaultId}。");
    }

    private static async void SpawnAiInFront()
    {
        PlayerInputCtrl player = FindLocalPlayer();
        if (!player)
        {
            Debug.LogWarning("战斗调试：没有找到正在操作的玩家。");
            return;
        }

        Camera camera = player.GetComponentInChildren<Camera>(true);
        Vector3 forward = camera ? camera.transform.forward : player.transform.forward;
        forward.y = 0f;
        forward.Normalize();
        Vector3 position = player.transform.position + forward * 5f;
        if (Physics.Raycast(position + Vector3.up * 5f, Vector3.down,
            out RaycastHit hit, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            position = hit.point + Vector3.up * 0.15f;
        }

        try
        {
            GameObject instance = await ResourceMgr.instance.InstantiateAsync(
                AiPrefabId, position, Quaternion.LookRotation(-forward));
            if (!instance)
            {
                Debug.LogError($"战斗调试：找不到 AI 资源 {AiPrefabId}。");
                return;
            }

            SpawnedAi.Add(instance);
            PlayerInfo info = instance.GetComponent<PlayerInfo>();
            if (info)
            {
                info.SetPlayerName("调试 AI " + SpawnedAi.Count);
            }
        }
        catch (Exception exception)
        {
            Debug.LogError($"战斗调试：生成 AI 失败。{exception}");
        }
    }

    private static void KillLocalPlayer()
    {
        PlayerInputCtrl player = FindLocalPlayer();
        CharacterHealth health = player ? player.GetComponent<CharacterHealth>() : null;
        if (health)
        {
            CombatDamage.Kill(health);
        }
    }

    private static void KillNearestAi()
    {
        PlayerInputCtrl player = FindLocalPlayer();
        if (!player)
        {
            return;
        }

        PlayerLifeCtrl[] actors = FindObjectsByType<PlayerLifeCtrl>(FindObjectsSortMode.None);
        CharacterHealth nearest = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < actors.Length; i++)
        {
            if (actors[i].GetComponent<PlayerInputCtrl>()?.enabled == true)
            {
                continue;
            }

            CharacterHealth health = actors[i].GetComponent<CharacterHealth>();
            if (!health || health.IsDead)
            {
                continue;
            }

            float distance = (actors[i].transform.position - player.transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = health;
            }
        }

        if (nearest)
        {
            CombatDamage.Kill(nearest, player.GetComponent<PlayerInfo>());
        }
    }

    private static void RestoreLocalHealth()
    {
        PlayerInputCtrl player = FindLocalPlayer();
        CharacterHealth health = player ? player.GetComponent<CharacterHealth>() : null;
        if (health && !health.IsDead)
        {
            health.ResetHealth();
        }
    }

    private async void SwitchLocalModel(PlayerCharacterModelCtrl models, string modelId)
    {
        _switchMessage = "正在切换为 " + modelId + "……";
        Repaint();
        bool switched = models && await models.SwitchModelAsync(modelId);
        _switchMessage = switched
            ? "已切换为 " + modelId
            : "切换失败：检查 Console 中的资源或骨架提示。";
        Repaint();
        if (!switched)
        {
            Debug.LogWarning("战斗调试：角色模型切换失败，检查模型骨架与资源 ID。");
        }
    }

    private static void SpeakLocalVoice()
    {
        PlayerInputCtrl player = FindLocalPlayer();
        CombatVoiceCtrl voice = player ? player.GetComponent<CombatVoiceCtrl>() : null;
        if (voice)
        {
            voice.Speak(CombatVoiceCue.Reload);
        }
    }

    private static void SpeakNearestAiVoice()
    {
        PlayerInputCtrl player = FindLocalPlayer();
        if (!player)
        {
            return;
        }
        CombatVoiceCtrl[] voices = FindObjectsByType<CombatVoiceCtrl>(FindObjectsSortMode.None);
        CombatVoiceCtrl nearest = null;
        float bestDistance = float.PositiveInfinity;
        for (int i = 0; i < voices.Length; i++)
        {
            PlayerInputCtrl input = voices[i].GetComponent<PlayerInputCtrl>();
            if (input && input.enabled)
            {
                continue;
            }
            float distance = (voices[i].transform.position - player.transform.position).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                nearest = voices[i];
            }
        }
        if (nearest)
        {
            nearest.Speak(CombatVoiceCue.Hurt);
        }
    }

    private static void ClearSpawnedAi()
    {
        for (int i = SpawnedAi.Count - 1; i >= 0; i--)
        {
            if (SpawnedAi[i])
            {
                ResourceMgr.instance.ReleaseInstance(SpawnedAi[i]);
            }
        }
        SpawnedAi.Clear();
    }

    private static PlayerInputCtrl FindLocalPlayer()
    {
        PlayerInputCtrl[] players = FindObjectsByType<PlayerInputCtrl>(FindObjectsSortMode.None);
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i].enabled)
            {
                return players[i];
            }
        }
        return null;
    }
}
