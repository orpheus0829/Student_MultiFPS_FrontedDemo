#if ENABLE_INPUT_SYSTEM
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(OrbitCameraControl))]
public class OrbitCameraControlEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("视角控制", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("requireMouseButton"), new GUIContent("按住右键才转"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lookSpeedX"), new GUIContent("水平灵敏度"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lookSpeedY"), new GUIContent("垂直灵敏度"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("invertY"), new GUIContent("反转垂直方向"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("scrollSpeed"), new GUIContent("滚轮缩放速度"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rotXClamp"), new GUIContent("俯仰角范围"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("distanceRange"), new GUIContent("相机距离范围"));

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
