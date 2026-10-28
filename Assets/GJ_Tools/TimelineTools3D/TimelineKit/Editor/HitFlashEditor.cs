using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(HitFlash))]
public class HitFlashEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("受击反馈", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("duration"), new GUIContent("闪红时长"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("flashColor"), new GUIContent("闪红颜色"));

        serializedObject.ApplyModifiedProperties();
    }
}
