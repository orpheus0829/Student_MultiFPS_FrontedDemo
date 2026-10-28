using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(EffectAudioClip))]
public class EffectAudioClipInspector : Editor
{
    private SerializedProperty useRepeatSpawn;
    private SerializedProperty spawnInterval;
    private SerializedProperty sound;
    private SerializedProperty effectPrefab;
    private SerializedProperty spawnOffset;
    private SerializedProperty spawnEuler;
    private SerializedProperty spawnScale;

    private void OnEnable()
    {
        useRepeatSpawn = serializedObject.FindProperty("useRepeatSpawn");
        spawnInterval = serializedObject.FindProperty("spawnInterval");
        sound = serializedObject.FindProperty("sound");
        effectPrefab = serializedObject.FindProperty("effectPrefab");
        spawnOffset = serializedObject.FindProperty("spawnOffset");
        spawnEuler = serializedObject.FindProperty("spawnEuler");
        spawnScale = serializedObject.FindProperty("spawnScale");
    }

    public override void OnInspectorGUI()
    {
        TimelineHelp.DrawBoxFor(target);
        serializedObject.Update();
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        EditorGUILayout.Space(2);

        EditorGUILayout.LabelField("播放内容", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(sound, new GUIContent("音效", "片段触发时播放；留空则只生成特效"));
        EditorGUILayout.PropertyField(effectPrefab, new GUIContent("特效预制体", "片段触发时生成；留空则只播放音效"));
        bool hasSound = sound.objectReferenceValue != null;
        bool hasVfx = effectPrefab.objectReferenceValue != null;
        if (!hasSound && !hasVfx)
        {
            EditorGUILayout.LabelField("未配置输出：请至少指定音效或特效预制体之一", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("特效位姿", EditorStyles.boldLabel);
        EditorGUI.BeginDisabledGroup(!hasVfx);
        EditorGUILayout.PropertyField(spawnOffset, new GUIContent("生成点偏移(本地)", "生成点相对角色的本地偏移"));
        EditorGUILayout.PropertyField(spawnEuler, new GUIContent("生成旋转(本地)", "特效的本地欧拉旋转"));
        EditorGUILayout.PropertyField(spawnScale, new GUIContent("生成缩放", "特效的缩放"));
        EditorGUI.EndDisabledGroup();
        if (!hasVfx)
        {
            EditorGUILayout.LabelField("指定特效预制体后，可在 Scene 视图中直接拖动预览调整", EditorStyles.miniLabel);
        }
        else
        {
            EditorGUILayout.LabelField("选中片段时可在 Scene 视图中拖动预览：W 移动 · E 旋转 · R 缩放", EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("触发方式", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useRepeatSpawn, new GUIContent("按间隔重复触发", "关闭=进入片段瞬间触发一次；开启=片段内按间隔循环触发"));
        if (useRepeatSpawn.boolValue)
        {
            EditorGUILayout.PropertyField(spawnInterval, new GUIContent("触发间隔(秒)", "相邻两次触发的间隔"));
        }
        EditorGUILayout.LabelField("片段结束或被打断时，本片段生成的特效会自动回收", EditorStyles.miniLabel);

        serializedObject.ApplyModifiedProperties();
        if (serializedObject.hasModifiedProperties)
        {
            EditorApplication.delayCall += () => { SceneView.RepaintAll(); };
        }
    }
}
