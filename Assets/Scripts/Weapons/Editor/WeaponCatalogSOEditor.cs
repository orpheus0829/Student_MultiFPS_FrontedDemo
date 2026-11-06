using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(WeaponCatalogSO))]
public sealed class WeaponCatalogSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.LabelField("枪械总目录", EditorStyles.boldLabel);
        EditorGUILayout.Space(4f);
        ResourceIdDrawer.DrawLayout(
            serializedObject.FindProperty("_defaultWeaponResourceId"),
            new GUIContent("默认枪械"),
            typeof(WeaponDefinitionSO));

        SerializedProperty weaponsProperty = serializedObject.FindProperty("_weaponResourceIds");
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("全部枪械", GUILayout.ExpandWidth(true));
        if (GUILayout.Button("添加", GUILayout.Width(48f)))
        {
            weaponsProperty.arraySize++;
        }
        if (GUILayout.Button("删除末项", GUILayout.Width(72f)) && weaponsProperty.arraySize > 0)
        {
            weaponsProperty.arraySize--;
        }
        EditorGUILayout.EndHorizontal();

        EditorGUI.indentLevel++;
        for (int i = 0; i < weaponsProperty.arraySize; i++)
        {
            SerializedProperty item = weaponsProperty.GetArrayElementAtIndex(i);
            ResourceIdDrawer.DrawLayout(
                item,
                new GUIContent("第 " + (i + 1) + " 把"),
                typeof(WeaponDefinitionSO));
        }
        EditorGUI.indentLevel--;

        serializedObject.ApplyModifiedProperties();
    }
}
