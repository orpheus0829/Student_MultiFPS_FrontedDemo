using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TimelineCamRig))]
public class TimelineCamRigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("跟随目标", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("target"), new GUIContent("跟随目标"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoFollow"), new GUIContent("自动跟随"));
        if (serializedObject.FindProperty("target").objectReferenceValue == null)
        {
            EditorGUILayout.LabelField("未指定目标：不会自动跟随（运镜锚点退回相机自身）", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("环绕机位", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rotX"), new GUIContent("俯仰角"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("rotY"), new GUIContent("环绕角"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("distance"), new GUIContent("相机距离"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("height"), new GUIContent("相机高度"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("lookHeight"), new GUIContent("看向抬高"));

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("状态", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("isPlayingAnim"), new GUIContent("运镜接管中"));

        serializedObject.ApplyModifiedProperties();
    }
}
