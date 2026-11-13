using UnityEditor;

[CustomEditor(typeof(ModeDefinitionSO))]
public sealed class ModeDefinitionSOEditor : Editor
{
    private SerializedProperty _items;

    private void OnEnable()
    {
        _items = serializedObject.FindProperty("_items");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawPropertiesExcluding(serializedObject, "m_Script", "_items");
        ModeDefinitionSO mode = (ModeDefinitionSO)target;
        if (mode.IsBuiltIn)
        {
            EditorGUILayout.LabelField("内置配置项（不可删除）", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                for (int i = 0; i < _items.arraySize; i++)
                {
                    EditorGUILayout.PropertyField(
                        _items.GetArrayElementAtIndex(i),
                        true);
                }
            }
        }
        else
        {
            EditorGUILayout.PropertyField(_items, true);
        }
        serializedObject.ApplyModifiedProperties();
    }
}
