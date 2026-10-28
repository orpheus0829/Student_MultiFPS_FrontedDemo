using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class ComponentHelp
{
    private const string PrefKeyPrefix = "GJ_Tools.ComponentHelp.";

    private static double s_frameStamp;
    private static readonly HashSet<int> s_drawn = new HashSet<int>();

    static ComponentHelp()
    {
        Editor.finishedDefaultHeaderGUI -= OnFinishedHeaderGUI;
        Editor.finishedDefaultHeaderGUI += OnFinishedHeaderGUI;
    }

    private static void OnFinishedHeaderGUI(Editor editor)
    {
        if (editor == null || editor.target == null) return;
        if (editor.targets != null && editor.targets.Length > 1) return;

        Draw(editor.target.GetType());
    }

    public static void DrawBoxFor(UnityEngine.Object target)
    {
        if (target == null) return;

        Draw(target.GetType());
    }

    private static void Draw(Type type)
    {
        string title;
        string body;
        if (!ComponentHelpText.TryGet(type, out title, out body)) return;
        if (!BeginDraw(type)) return;

        DrawBox(type, title, body);
    }

    private static bool BeginDraw(Type type)
    {
        double now = EditorApplication.timeSinceStartup;
        if (now != s_frameStamp)
        {
            s_frameStamp = now;
            s_drawn.Clear();
        }

        return s_drawn.Add(type.GetHashCode());
    }

    public static void DrawBox(Type type, string title, string body)
    {
        string prefKey = PrefKeyPrefix + (type.FullName ?? type.Name);
        bool open = EditorPrefs.GetBool(prefKey, false);

        EditorGUILayout.BeginVertical(EditorStyles.helpBox);

        bool newOpen = EditorGUILayout.Foldout(open, "组件说明 · " + title, true);
        if (newOpen != open)
        {
            EditorPrefs.SetBool(prefKey, newOpen);
        }

        if (newOpen)
        {
            EditorGUILayout.Space(4f);

            GUIStyle style = new GUIStyle(EditorStyles.label);
            style.wordWrap = true;

            float width = EditorGUIUtility.currentViewWidth - 70f;
            if (width < 80f) width = 80f;

            EditorGUILayout.LabelField(body, style,
                GUILayout.MinHeight(style.CalcHeight(new GUIContent(body), width)));
        }

        EditorGUILayout.EndVertical();
    }
}

[CustomEditor(typeof(MonoBehaviour), true)]
public class ComponentHelpFallbackEditor : Editor
{
    public override void OnInspectorGUI()
    {
        ComponentHelp.DrawBoxFor(target);
        DrawDefaultInspector();
    }
}
