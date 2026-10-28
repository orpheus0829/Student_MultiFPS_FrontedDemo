using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TransformTimelineClip))]
public class TransformTimelineClipInspector : Editor
{
    private SerializedProperty moveMode;
    private SerializedProperty direction;
    private SerializedProperty useCollision;
    private SerializedProperty castRadius;
    private SerializedProperty endPos;
    private SerializedProperty endEuler;
    private SerializedProperty moveSpeed;
    private SerializedProperty totalDistance;
    private SerializedProperty startSpeed;
    private SerializedProperty endSpeed;
    private SerializedProperty circleCenterLocal;
    private SerializedProperty circleRadius;
    private SerializedProperty circleTotalAngle;
    private SerializedProperty circleClockwise;
    private SerializedProperty circleVariableSpeed;
    private SerializedProperty circleStartAngSpeed;
    private SerializedProperty circleEndAngSpeed;

    private void OnEnable()
    {
        moveMode = serializedObject.FindProperty("moveMode");
        direction = serializedObject.FindProperty("direction");
        useCollision = serializedObject.FindProperty("useCollision");
        castRadius = serializedObject.FindProperty("castRadius");
        endPos = serializedObject.FindProperty("endPos");
        endEuler = serializedObject.FindProperty("endEuler");
        moveSpeed = serializedObject.FindProperty("moveSpeed");
        totalDistance = serializedObject.FindProperty("totalDistance");
        startSpeed = serializedObject.FindProperty("startSpeed");
        endSpeed = serializedObject.FindProperty("endSpeed");
        circleCenterLocal = serializedObject.FindProperty("circleCenterLocal");
        circleRadius = serializedObject.FindProperty("circleRadius");
        circleTotalAngle = serializedObject.FindProperty("circleTotalAngle");
        circleClockwise = serializedObject.FindProperty("circleClockwise");
        circleVariableSpeed = serializedObject.FindProperty("circleVariableSpeed");
        circleStartAngSpeed = serializedObject.FindProperty("circleStartAngSpeed");
        circleEndAngSpeed = serializedObject.FindProperty("circleEndAngSpeed");
    }

    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.Space(2);

        EditorGUILayout.LabelField("位移模式", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(moveMode, new GUIContent("模式", "位移方式，下方参数随模式切换"));
        MoveMode mode = (MoveMode)moveMode.enumValueIndex;
        bool isLine = mode == MoveMode.SpeedAndDistance || mode == MoveMode.VariableSpeed;
        if (isLine)
        {
            EditorGUILayout.LabelField("碰撞处理", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(useCollision, new GUIContent("贴墙滑行", "直线推进撞墙时沿墙滑行，不穿透"));
            if (useCollision.boolValue)
            {
                EditorGUILayout.PropertyField(castRadius, new GUIContent("探测半径", "碰撞探测半径，数值越大越早判定碰墙"));
            }
        }

        EditorGUILayout.Space(4);
        switch (mode)
        {
            case MoveMode.FixedEndPos:
                EditorGUILayout.LabelField("终点(瞬移·可穿墙)", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("片段开始时立即传送到终点，途中不检测碰撞", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(endPos, new GUIContent("终点坐标(本地)", "目标位置的本地偏移"));
                EditorGUILayout.PropertyField(endEuler, new GUIContent("终点朝向(本地)", "传送完成后的本地欧拉朝向"));
                EditorGUILayout.LabelField("在 Scene 视图中拖动终点手柄可实时调整", EditorStyles.miniLabel);
                break;

            case MoveMode.SpeedAndDistance:
                EditorGUILayout.LabelField("匀速直线", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("按方向匀速推进；总距离大于 0 时推进完毕即停", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(direction, new GUIContent("方向(本地,留空=面朝)", "水平方向；留空时沿角色面朝方向"));
                EditorGUILayout.PropertyField(totalDistance, new GUIContent("总距离", "推进总长；0=不限，推进至片段结束"));
                EditorGUILayout.PropertyField(moveSpeed, new GUIContent("速度(米/秒)", "匀速推进速度"));
                EditorGUILayout.LabelField("拖动 Scene 视图中的终点手柄可反推速度与距离", EditorStyles.miniLabel);
                break;

            case MoveMode.VariableSpeed:
                EditorGUILayout.LabelField("变速直线", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("速度由起始值线性过渡至结束值", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(direction, new GUIContent("方向(本地,留空=面朝)", "水平方向；留空时沿角色面朝方向"));
                EditorGUILayout.PropertyField(totalDistance, new GUIContent("总距离", "推进总长；0=不限，推进至片段结束"));
                EditorGUILayout.PropertyField(startSpeed, new GUIContent("起始速度", "0=由静止起步"));
                EditorGUILayout.PropertyField(endSpeed, new GUIContent("结束速度", "片段结束时达到的速度"));
                EditorGUILayout.LabelField("拖动 Scene 视图中的终点手柄可反推结束速度", EditorStyles.miniLabel);
                break;

            case MoveMode.CircleRotate:
                EditorGUILayout.LabelField("绕圈", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("以指定圆心与半径做圆弧运动", EditorStyles.miniLabel);
                EditorGUILayout.PropertyField(circleCenterLocal, new GUIContent("圆心(本地)", "相对片段开始时位置的本地偏移"));
                EditorGUILayout.PropertyField(circleRadius, new GUIContent("半径", "圆弧半径"));
                EditorGUILayout.PropertyField(circleTotalAngle, new GUIContent("总转角(度)", "360=完整一圈；负值反向"));
                EditorGUILayout.PropertyField(circleClockwise, new GUIContent("俯视顺时针", "旋转方向"));
                EditorGUILayout.PropertyField(circleVariableSpeed, new GUIContent("角速度变速", "以起始/结束角速度做线性变速"));
                if (circleVariableSpeed.boolValue)
                {
                    EditorGUILayout.PropertyField(circleStartAngSpeed, new GUIContent("起始角速度(度/秒)"));
                    EditorGUILayout.PropertyField(circleEndAngSpeed, new GUIContent("结束角速度(度/秒)"));
                }
                EditorGUILayout.LabelField("在 Scene 视图中可拖动圆心与半径手柄", EditorStyles.miniLabel);
                break;
        }

        serializedObject.ApplyModifiedProperties();
        if (serializedObject.hasModifiedProperties)
        {
            EditorApplication.delayCall += () => { SceneView.RepaintAll(); };
        }
    }
}
