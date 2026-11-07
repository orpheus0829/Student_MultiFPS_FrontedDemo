using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponActionSetSO))]
public sealed class WeaponActionSetSOEditor : Editor
{
    private bool _showBasicActions = true;
    private bool _showMagazineReload = true;
    private bool _showPerRoundReload;

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField("枪械动作集", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "动作集保存玩法时间、操作限制与强类型生效节点。Animator 只按固定状态契约播放表现。",
            MessageType.Info);

        DrawBasicActions();
        DrawMagazineReload();
        DrawPerRoundReload();

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawBasicActions()
    {
        _showBasicActions = EditorGUILayout.BeginFoldoutHeaderGroup(
            _showBasicActions,
            "开火与切换枪械");
        if (_showBasicActions)
        {
            DrawAction("_fire", "开火", WeaponActionType.Fire, false);
            DrawAction("_equip", "抽出枪械", WeaponActionType.Equip, false);
            DrawAction("_holster", "收起枪械", WeaponActionType.Holster, false);
            DrawAction("_inspect", "检视枪械", WeaponActionType.Inspect, false);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawMagazineReload()
    {
        _showMagazineReload = EditorGUILayout.BeginFoldoutHeaderGroup(
            _showMagazineReload,
            "弹匣换弹");
        if (_showMagazineReload)
        {
            DrawAction("_reload", "普通换弹", WeaponActionType.Reload, true);
            DrawAction("_emptyReload", "空仓换弹（可选）", WeaponActionType.EmptyReload, true);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawPerRoundReload()
    {
        _showPerRoundReload = EditorGUILayout.BeginFoldoutHeaderGroup(
            _showPerRoundReload,
            "逐发装填");
        if (_showPerRoundReload)
        {
            EditorGUILayout.HelpBox(
                "开始和结束动作可以停用；装入一发动作需要配置“装入一发”节点。",
                MessageType.None);
            DrawAction("_perRoundStart", "开始装填（可选）", WeaponActionType.PerRoundStart, true);
            DrawAction("_perRoundInsert", "装入一发", WeaponActionType.PerRoundInsert, true);
            DrawAction("_perRoundEnd", "结束装填（可选）", WeaponActionType.PerRoundEnd, true);
        }
        EditorGUILayout.EndFoldoutHeaderGroup();
    }

    private void DrawAction(
        string path,
        string label,
        WeaponActionType actionType,
        bool allowAimOverride)
    {
        SerializedProperty action = serializedObject.FindProperty(path);
        SerializedProperty enabledProperty = action.FindPropertyRelative("_enabled");

        EditorGUILayout.Space(4f);
        EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);
        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(enabledProperty, new GUIContent("启用此动作"));
        if (!enabledProperty.boolValue)
        {
            EditorGUI.indentLevel--;
            return;
        }

        EditorGUILayout.PropertyField(
            action.FindPropertyRelative("_duration"),
            new GUIContent("权威持续时间"));

        SerializedProperty canInterrupt = action.FindPropertyRelative("_canInterrupt");
        EditorGUILayout.PropertyField(canInterrupt, new GUIContent("允许主动打断"));
        if (canInterrupt.boolValue)
        {
            EditorGUILayout.PropertyField(
                action.FindPropertyRelative("_interruptStartProgress"),
                new GUIContent("允许打断起点", "0 表示动作开始即可打断，1 表示接近结束才可打断。"));
        }

        DrawRestrictions(action);
        DrawAnimatorState(action, actionType, allowAimOverride);
        DrawMarkers(action.FindPropertyRelative("_markers"));
        EditorGUI.indentLevel--;
    }

    private void DrawRestrictions(SerializedProperty action)
    {
        EditorGUILayout.LabelField("动作期间操作", EditorStyles.miniBoldLabel);
        SerializedProperty allowsMovement = action.FindPropertyRelative("_allowsMovement");
        EditorGUILayout.PropertyField(allowsMovement, new GUIContent("允许移动"));
        if (allowsMovement.boolValue)
        {
            EditorGUILayout.PropertyField(
                action.FindPropertyRelative("_movementSpeedMultiplier"),
                new GUIContent("移动速度倍率"));
        }
        EditorGUILayout.PropertyField(
            action.FindPropertyRelative("_allowsAim"),
            new GUIContent("允许瞄准"));
        EditorGUILayout.PropertyField(
            action.FindPropertyRelative("_allowsFire"),
            new GUIContent("允许开火"));
    }

    private void DrawAnimatorState(
        SerializedProperty action,
        WeaponActionType actionType,
        bool allowAimOverride)
    {
        EditorGUILayout.LabelField("Animator 状态", EditorStyles.miniBoldLabel);
        EditorGUILayout.LabelField(
            "默认状态",
            WeaponActionStateContract.GetStateName(actionType));

        SerializedProperty overrideState = action.FindPropertyRelative("_overrideAnimatorState");
        EditorGUILayout.PropertyField(overrideState, new GUIContent("覆写默认状态"));
        if (overrideState.boolValue)
        {
            EditorGUILayout.PropertyField(
                action.FindPropertyRelative("_firstPersonStateOverride"),
                new GUIContent("第一人称状态"));
            EditorGUILayout.PropertyField(
                action.FindPropertyRelative("_thirdPersonStateOverride"),
                new GUIContent("第三人称状态"));
        }

        if (!allowAimOverride)
        {
            return;
        }

        SerializedProperty aimOverride = action.FindPropertyRelative("_hasAimStateOverride");
        EditorGUILayout.PropertyField(aimOverride, new GUIContent("具有瞄准换弹表现"));
        if (!aimOverride.boolValue)
        {
            return;
        }

        EditorGUILayout.PropertyField(
            action.FindPropertyRelative("_firstPersonAimStateOverride"),
            new GUIContent("第一人称瞄准状态"));
        EditorGUILayout.PropertyField(
            action.FindPropertyRelative("_thirdPersonAimStateOverride"),
            new GUIContent("第三人称瞄准状态"));
    }

    private void DrawMarkers(SerializedProperty markers)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("玩法生效节点", EditorStyles.miniBoldLabel);
        if (GUILayout.Button("添加", GUILayout.Width(48f)))
        {
            int index = markers.arraySize;
            markers.InsertArrayElementAtIndex(index);
            SerializedProperty marker = markers.GetArrayElementAtIndex(index);
            marker.FindPropertyRelative("_markerType").enumValueIndex = 0;
            marker.FindPropertyRelative("_progress").floatValue = 0f;
        }
        if (GUILayout.Button("删除末项", GUILayout.Width(72f)) && markers.arraySize > 0)
        {
            markers.DeleteArrayElementAtIndex(markers.arraySize - 1);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        for (int i = 0; i < markers.arraySize; i++)
        {
            SerializedProperty marker = markers.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(
                marker.FindPropertyRelative("_markerType"),
                new GUIContent("节点类型"));
            EditorGUILayout.PropertyField(
                marker.FindPropertyRelative("_progress"),
                new GUIContent("动作进度"));
            EditorGUILayout.EndVertical();
        }
        EditorGUI.indentLevel--;
    }
}
