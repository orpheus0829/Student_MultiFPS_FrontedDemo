using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(DamageableHealth))]
public class DamageableHealthInspector : Editor
{
    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("生命值", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("maxHp"), new GUIContent("生命上限"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_hp"), new GUIContent("当前生命"));

        serializedObject.ApplyModifiedProperties();
    }
}
