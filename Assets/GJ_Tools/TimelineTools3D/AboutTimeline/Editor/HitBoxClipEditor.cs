using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HitBoxClip))]
public class HitBoxClipInspector : Editor
{
    private SerializedProperty hitBoxShape;
    private SerializedProperty boxOffset;
    private SerializedProperty boxRadius;
    private SerializedProperty hitBoxSize;
    private SerializedProperty damage;
    private SerializedProperty HitForce;
    private SerializedProperty startTime;
    private SerializedProperty endTime;
    private SerializedProperty useRepeatScan;
    private SerializedProperty scanInterval;
    private SerializedProperty sectorAngle;
    private SerializedProperty sectorInnerRadius;
    private SerializedProperty sectorHeight;
    private SerializedProperty boxEuler;

    private void OnEnable()
    {
        hitBoxShape = serializedObject.FindProperty("hitBoxShape");
        boxOffset = serializedObject.FindProperty("boxOffset");
        boxRadius = serializedObject.FindProperty("boxRadius");
        hitBoxSize = serializedObject.FindProperty("hitBoxSize");
        sectorAngle = serializedObject.FindProperty("sectorAngle");
        sectorInnerRadius = serializedObject.FindProperty("sectorInnerRadius");
        sectorHeight = serializedObject.FindProperty("sectorHeight");
        boxEuler = serializedObject.FindProperty("boxEuler");
        damage = serializedObject.FindProperty("damage");
        HitForce = serializedObject.FindProperty("HitForce");
        startTime = serializedObject.FindProperty("startTime");
        endTime = serializedObject.FindProperty("endTime");
        useRepeatScan = serializedObject.FindProperty("useRepeatScan");
        scanInterval = serializedObject.FindProperty("scanInterval");
    }

    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.Space(2);

        EditorGUILayout.LabelField("判定形状", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(hitBoxShape, new GUIContent("形状", "球(半径)/盒(长宽高)/扇形(以中轴展开，可整体旋转)"));
        EditorGUILayout.PropertyField(boxOffset, new GUIContent("中心偏移(本地)", "判定中心相对角色的本地偏移，随角色移动与转向换算；可在 Scene 中拖动"));

        HitBoxShape shape = (HitBoxShape)hitBoxShape.enumValueIndex;
        if (shape == HitBoxShape.Sphere)
        {
            EditorGUILayout.PropertyField(boxRadius, new GUIContent("半径", "球形判定的半径"));
        }
        else if (shape == HitBoxShape.Sector)
        {
            EditorGUILayout.PropertyField(boxRadius, new GUIContent("外半径", "扇形的最远判定半径"));
            EditorGUILayout.PropertyField(sectorInnerRadius, new GUIContent("内径", "0=实心扇形柱；大于 0 时为空心圆弧刃，贴身内圈不参与判定"));
            EditorGUILayout.PropertyField(sectorAngle, new GUIContent("张角(度)", "以中轴为基准向两侧展开的总角度"));
            EditorGUILayout.PropertyField(sectorHeight, new GUIContent("柱高(米)", "竖直总高，以判定中心为中间上下均分"));
            EditorGUILayout.PropertyField(boxEuler, new GUIContent("旋转(本地)", "判定整体相对角色的欧拉旋转；扇形中轴沿本地 +Z"));
        }
        else
        {
            EditorGUILayout.PropertyField(hitBoxSize, new GUIContent("尺寸(长宽高)", "盒形判定的尺寸"));
            EditorGUILayout.PropertyField(boxEuler, new GUIContent("旋转(本地)", "判定整体相对角色的欧拉旋转"));
        }
        if (shape != HitBoxShape.Sphere)
        {
            EditorGUILayout.LabelField("在 Scene 视图：W 移动中心 · E 旋转朝向 · 拖动端点手柄调整尺寸参数", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("命中结算", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(damage, new GUIContent("伤害", "命中目标时扣除的生命值"));
        EditorGUILayout.PropertyField(HitForce, new GUIContent("击退冲量", "命中后施加给目标刚体的水平冲量；0=无击退"));

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("判定窗口(相对片段起点)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(startTime, new GUIContent("开启时刻(秒)", "相对片段起点的秒数，到达该时刻才开放判定"));
        EditorGUILayout.PropertyField(endTime, new GUIContent("关闭时刻(秒)", "相对片段起点的秒数，到达该时刻关闭判定"));
        EditorGUILayout.LabelField("窗口超出片段时长时以片段结束为限；同一目标在单个窗口内只结算一次", EditorStyles.miniLabel);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("扫描方式", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useRepeatScan, new GUIContent("窗口内反复扫描", "关闭=进入窗口瞬间结算一次；开启=按间隔持续扫描"));
        if (useRepeatScan.boolValue)
        {
            EditorGUILayout.PropertyField(scanInterval, new GUIContent("扫描间隔(秒)", "两次扫描的间隔；已结算目标不会重复受伤，仅补扫窗口开启后进入的目标"));
        }

        serializedObject.ApplyModifiedProperties();
        if (serializedObject.hasModifiedProperties)
        {
            EditorApplication.delayCall += () => { SceneView.RepaintAll(); };
        }
    }
}
