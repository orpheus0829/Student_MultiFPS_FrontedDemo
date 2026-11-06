using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

[CustomPropertyDrawer(typeof(ResourceIdAttribute))]
public sealed class ResourceIdDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType != SerializedPropertyType.String)
        {
            EditorGUI.PropertyField(position, property, label);
            return;
        }

        ResourceIdAttribute resourceId = attribute as ResourceIdAttribute;
        DrawIdField(
            position,
            property,
            label,
            resourceId != null ? resourceId.ResourceType : typeof(UnityEngine.Object));
    }

    public static void DrawLayout(SerializedProperty property, GUIContent label, Type resourceType)
    {
        Rect position = EditorGUILayout.GetControlRect();
        DrawIdField(position, property, label, resourceType);
    }

    private static void DrawIdField(
        Rect position,
        SerializedProperty property,
        GUIContent label,
        Type resourceType)
    {
        EditorGUI.BeginProperty(position, label, property);
        Rect valueRect = EditorGUI.PrefixLabel(position, label);
        Rect clearRect = new Rect(valueRect.xMax - 24f, valueRect.y, 24f, valueRect.height);
        Rect selectRect = new Rect(valueRect.x, valueRect.y, valueRect.width - 28f, valueRect.height);
        string buttonText = string.IsNullOrWhiteSpace(property.stringValue)
            ? "未选择"
            : property.stringValue;

        if (GUI.Button(selectRect, buttonText, EditorStyles.popup))
        {
            ResourceIdPickerWindow.Open(
                property.serializedObject.targetObjects,
                property.propertyPath,
                resourceType);
        }

        if (GUI.Button(clearRect, "×"))
        {
            property.stringValue = string.Empty;
        }
        EditorGUI.EndProperty();
    }
}

public sealed class ResourceIdPickerWindow : EditorWindow
{
    private UnityEngine.Object[] _targets;
    private string _propertyPath;
    private Type _resourceType;
    private string _search = string.Empty;
    private Vector2 _scroll;
    private readonly List<ResourceIdItem> _items = new List<ResourceIdItem>();

    public static void Open(UnityEngine.Object[] targets, string propertyPath, Type resourceType)
    {
        ResourceIdPickerWindow window = CreateInstance<ResourceIdPickerWindow>();
        window.titleContent = new GUIContent("选择资源 ID");
        window._targets = targets;
        window._propertyPath = propertyPath;
        window._resourceType = resourceType;
        window.minSize = new Vector2(520f, 360f);
        window.RefreshItems();
        window.ShowAuxWindow();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("资源类型：" + GetTypeName(_resourceType), EditorStyles.boldLabel);
        _search = EditorGUILayout.TextField("搜索", _search);
        EditorGUILayout.Space(4f);

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        for (int i = 0; i < _items.Count; i++)
        {
            ResourceIdItem item = _items[i];
            if (!Matches(item))
            {
                continue;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(item.Address, GUILayout.MinWidth(210f));
            EditorGUILayout.LabelField(item.AssetPath, EditorStyles.miniLabel);
            if (GUILayout.Button("选择", GUILayout.Width(48f)))
            {
                Select(item.Address);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private void RefreshItems()
    {
        _items.Clear();
        AddressableAssetSettings settings = AddressableAssetSettingsDefaultObject.Settings;
        if (!settings)
        {
            return;
        }

        for (int i = 0; i < settings.groups.Count; i++)
        {
            AddressableAssetGroup group = settings.groups[i];
            if (!group)
            {
                continue;
            }

            foreach (AddressableAssetEntry entry in group.entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.address))
                {
                    continue;
                }

                Type mainType = entry.MainAssetType;
                if (_resourceType != null &&
                    _resourceType != typeof(UnityEngine.Object) &&
                    (mainType == null || !_resourceType.IsAssignableFrom(mainType)))
                {
                    continue;
                }

                _items.Add(new ResourceIdItem(entry.address, entry.AssetPath));
            }
        }

        _items.Sort((left, right) => string.CompareOrdinal(left.Address, right.Address));
    }

    private bool Matches(ResourceIdItem item)
    {
        if (string.IsNullOrWhiteSpace(_search))
        {
            return true;
        }

        return item.Address.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
            item.AssetPath.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void Select(string resourceId)
    {
        for (int i = 0; i < _targets.Length; i++)
        {
            UnityEngine.Object target = _targets[i];
            if (!target)
            {
                continue;
            }

            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = serializedObject.FindProperty(_propertyPath);
            if (property == null || property.propertyType != SerializedPropertyType.String)
            {
                continue;
            }

            Undo.RecordObject(target, "选择资源 ID");
            property.stringValue = resourceId;
            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(target);
        }

        Close();
    }

    private static string GetTypeName(Type type)
    {
        if (type == typeof(Sprite))
        {
            return "Sprite 图片";
        }
        if (type == typeof(GameObject))
        {
            return "预制体";
        }
        if (type == typeof(AudioClip))
        {
            return "音频";
        }
        if (type == typeof(RuntimeAnimatorController))
        {
            return "Animator 控制器";
        }
        if (type == typeof(AnimationClip))
        {
            return "动画片段";
        }

        return type != null ? type.Name : "任意资源";
    }

    private readonly struct ResourceIdItem
    {
        public string Address { get; }
        public string AssetPath { get; }

        public ResourceIdItem(string address, string assetPath)
        {
            Address = address;
            AssetPath = assetPath;
        }
    }
}
