using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

[CustomEditor(typeof(TimelineActorHost))]
public class TimelineActorHostInspector : Editor
{
    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        TimelineActorHost host = (TimelineActorHost)target;
        if (host != null && host.GetComponent<PlayableDirector>() == null)
        {
            Undo.AddComponent<PlayableDirector>(host.gameObject);
        }
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("命中扫描", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("attackPoint"), new GUIContent("判定锚点"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("targetMask"), new GUIContent("可命中的层"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ignoreTags"), new GUIContent("忽略的标签"), true);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("useHitForce"), new GUIContent("命中击退"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoExcludeSelf"), new GUIContent("排除自身"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("ignoreNonDamageable"), new GUIContent("只打可伤害目标"));

        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("音效与特效", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("sfxSource"), new GUIContent("音效源(留空自动创建)"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("randomizePitch"), new GUIContent("随机音调"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("pitchRange"), new GUIContent("音调范围"));

        serializedObject.ApplyModifiedProperties();
    }
}
