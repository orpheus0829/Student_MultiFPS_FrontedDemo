using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CameraTimelineClip))]
public class CameraTimelineClipInspector : Editor
{
    private SerializedProperty cameraMoveMode;
    private SerializedProperty resetSubMode;
    private SerializedProperty resetLerpFactor;
    private SerializedProperty useSurroundMode;
    private SerializedProperty surroundRadius;
    private SerializedProperty surroundTotalAngle;
    private SerializedProperty surroundFixedHeight;
    private SerializedProperty cameraTargetLocalPos;
    private SerializedProperty cameraTargetEuler;
    private SerializedProperty lockLookAtPlayer;
    private SerializedProperty smoothLerpFactor;
    private SerializedProperty useVariableSpeed;
    private SerializedProperty startSpeed;
    private SerializedProperty endSpeed;
    private SerializedProperty useLastFrameAsOrigin;
    private SerializedProperty restoreOriginOnEnd;
    private SerializedProperty allowManualCamera;

    private void OnEnable()
    {
        cameraMoveMode = serializedObject.FindProperty("cameraMoveMode");
        resetSubMode = serializedObject.FindProperty("resetSubMode");
        resetLerpFactor = serializedObject.FindProperty("resetLerpFactor");
        useSurroundMode = serializedObject.FindProperty("useSurroundMode");
        surroundRadius = serializedObject.FindProperty("surroundRadius");
        surroundTotalAngle = serializedObject.FindProperty("surroundTotalAngle");
        surroundFixedHeight = serializedObject.FindProperty("surroundFixedHeight");
        cameraTargetLocalPos = serializedObject.FindProperty("cameraTargetLocalPos");
        cameraTargetEuler = serializedObject.FindProperty("cameraTargetEuler");
        lockLookAtPlayer = serializedObject.FindProperty("lockLookAtPlayer");
        smoothLerpFactor = serializedObject.FindProperty("smoothLerpFactor");
        useVariableSpeed = serializedObject.FindProperty("useVariableSpeed");
        startSpeed = serializedObject.FindProperty("startSpeed");
        endSpeed = serializedObject.FindProperty("endSpeed");
        useLastFrameAsOrigin = serializedObject.FindProperty("useLastFrameAsOrigin");
        restoreOriginOnEnd = serializedObject.FindProperty("restoreOriginOnEnd");
        allowManualCamera = serializedObject.FindProperty("allowManualCamera");
    }

    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.Space(2);

        EditorGUILayout.LabelField("运镜方式", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(cameraMoveMode, new GUIContent("模式", "平滑运镜 / 瞬移切镜 / 归位至片段前机位"));
        CamMoveMode mode = (CamMoveMode)cameraMoveMode.enumValueIndex;
        EditorGUILayout.Space(2);

        if (mode == CamMoveMode.ResetOrigin)
        {
            EditorGUILayout.LabelField("归位", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("拉回片段开始前的机位，适合动作演出后的收镜", EditorStyles.miniLabel);
            EditorGUILayout.PropertyField(resetSubMode, new GUIContent("归位方式", "瞬移直接回 / 平滑拉回"));
            if (resetSubMode.enumValueIndex == 1)
            {
                EditorGUILayout.PropertyField(resetLerpFactor, new GUIContent("归位平滑速度", "数值越大拉回越快"));
            }
            EditorGUILayout.PropertyField(lockLookAtPlayer, new GUIContent("看向角色", "归位过程中持续看向角色"));
            EditorGUILayout.PropertyField(allowManualCamera, new GUIContent("允许手动拖动", "归位期间手动拖动会临时接管，停止输入后继续归位"));
            serializedObject.ApplyModifiedProperties();
            return;
        }

        EditorGUILayout.LabelField(
            mode == CamMoveMode.Teleport ? "瞬移切镜" : "平滑运镜",
            EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            mode == CamMoveMode.Teleport ? "片段开始即切到目标机位，适合甩镜/切镜" : "镜头自起始机位平滑过渡到目标机位",
            EditorStyles.miniLabel);

        EditorGUILayout.PropertyField(useLastFrameAsOrigin, new GUIContent("以上一帧位置为起点", "以片段开始瞬间的相机位置作为插值起点；连续多段运镜建议开启"));
        EditorGUILayout.PropertyField(useSurroundMode, new GUIContent("圆弧环绕", "按圆弧轨迹环绕角色，忽略目标坐标的距离分量"));
        if (useSurroundMode.boolValue)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("环绕参数", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(surroundRadius, new GUIContent("环绕半径", "圆弧半径"));
            EditorGUILayout.PropertyField(surroundTotalAngle, new GUIContent("扫过角度", "180=绕至侧面/脑后；负值反向"));
            EditorGUILayout.PropertyField(surroundFixedHeight, new GUIContent("圆心高度", "圆弧圆心相对角色脚底的高度"));
        }
        else
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("目标机位", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(cameraTargetLocalPos, new GUIContent("机位坐标(相对角色)", "目标坐标随角色移动与转向换算"));
            EditorGUILayout.PropertyField(cameraTargetEuler, new GUIContent("机位朝向(相对角色)", "目标朝向的本地欧拉角"));
            EditorGUILayout.LabelField("在 Scene 视图中可拖动机位手柄调整", EditorStyles.miniLabel);
        }

        EditorGUILayout.PropertyField(lockLookAtPlayer, new GUIContent("看向角色", "全程看向角色；关闭则使用机位朝向"));

        if (mode == CamMoveMode.SmoothLerp)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("过渡手感", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(smoothLerpFactor, new GUIContent("平滑系数", "插值系数，数值越大过渡越跟手"));
            EditorGUILayout.PropertyField(useVariableSpeed, new GUIContent("变速过渡", "速度由起始值线性过渡至结束值"));
            if (useVariableSpeed.boolValue)
            {
                EditorGUILayout.PropertyField(startSpeed, new GUIContent("起始速度", "0=静止起步"));
                EditorGUILayout.PropertyField(endSpeed, new GUIContent("结束速度", "片段结束时达到的速度"));
            }
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("片段收尾", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(restoreOriginOnEnd, new GUIContent("结束后恢复原机位", "恢复片段前的机位；不勾选则停留在本段结束机位"));
        EditorGUILayout.PropertyField(allowManualCamera, new GUIContent("运镜中允许手动拖动", "拖动时本段暂停写入，停止输入后自当前机位继续"));
        EditorGUILayout.LabelField("允许手动时建议同时开启“以上一帧位置为起点”，松手后运镜才不跳变", EditorStyles.miniLabel);

        serializedObject.ApplyModifiedProperties();
        if (serializedObject.hasModifiedProperties)
        {
            EditorApplication.delayCall += () => { SceneView.RepaintAll(); };
        }
    }
}
