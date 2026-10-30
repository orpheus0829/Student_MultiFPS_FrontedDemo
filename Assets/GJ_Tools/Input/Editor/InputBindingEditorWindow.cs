using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputBindingEditorWindow : EditorWindow
{
    private readonly Dictionary<GJInputAction, bool> _foldouts = new Dictionary<GJInputAction, bool>();

    private InputActionCatalogSO _catalog;
    private InputActionAsset _previewAsset;
    private InputBindingService _bindingService;
    private Vector2 _scroll;
    private string _search;
    private string _message;
    private MessageType _messageType;
    private GJInputAction? _waitingAction;
    private int _waitingBindingIndex = -1;
    private bool _showPaths;

    [MenuItem("GJ Tools/输入/按键映射")]
    public static void Open()
    {
        InputBindingEditorWindow window = GetWindow<InputBindingEditorWindow>("按键映射");
        window.minSize = new Vector2(520f, 520f);
    }

    [MenuItem("Assets/Create/GJ Tools/输入/默认按键数据")]
    public static void CreateDefaultCatalog()
    {
        EnsureFolder("Assets", "SO");
        EnsureFolder("Assets/SO", "Input");

        string path = AssetDatabase.GenerateUniqueAssetPath("Assets/SO/Input/InputActionCatalog.asset");
        InputActionCatalogSO catalog = CreateInstance<InputActionCatalogSO>();
        catalog.ResetToDefaults();
        AssetDatabase.CreateAsset(catalog, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = catalog;
    }

    private void OnEnable()
    {
        FindDefaultCatalog();
        RebuildPreview();
    }

    private void OnDisable()
    {
        DisposePreview();
    }

    private void OnGUI()
    {
        DrawTitle();
        DrawCatalogPanel();

        if (_catalog == null)
        {
            EditorGUILayout.HelpBox("没有找到输入数据源，请创建或选择 InputActionCatalogSO。", MessageType.Info);
            return;
        }

        DrawValidation();
        DrawMessage();
        DrawToolbar();

        _scroll = EditorGUILayout.BeginScrollView(_scroll);
        foreach (InputActionData action in _catalog.Actions)
        {
            if (MatchesSearch(action.Id))
            {
                DrawActionCard(action);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawTitle()
    {
        Rect rect = EditorGUILayout.GetControlRect(false, 56f);
        EditorGUI.DrawRect(rect, EditorGUIUtility.isProSkin ? new Color(0.12f, 0.15f, 0.2f) : new Color(0.72f, 0.8f, 0.92f));

        GUIContent icon = EditorGUIUtility.IconContent("d_Settings");
        Rect iconRect = new Rect(rect.x + 12f, rect.y + 10f, 36f, 36f);
        GUI.DrawTexture(iconRect, icon.image, ScaleMode.ScaleToFit);

        GUIStyle titleStyle = new GUIStyle(EditorStyles.boldLabel)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleLeft
        };
        GUIStyle subtitleStyle = new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.MiddleLeft
        };

        EditorGUI.LabelField(new Rect(rect.x + 56f, rect.y + 7f, rect.width - 64f, 24f), "GunDemo 按键映射", titleStyle);
        EditorGUI.LabelField(new Rect(rect.x + 57f, rect.y + 30f, rect.width - 65f, 18f), "编辑默认绑定，运行时设置界面与这里共用同一份数据源", subtitleStyle);
        EditorGUILayout.Space(6f);
    }

    private void DrawCatalogPanel()
    {
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUI.BeginChangeCheck();
            _catalog = (InputActionCatalogSO)EditorGUILayout.ObjectField("输入数据源", _catalog, typeof(InputActionCatalogSO), false);
            if (EditorGUI.EndChangeCheck())
            {
                ClearMessage();
                RebuildPreview();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUI.enabled = _catalog != null;
                if (GUILayout.Button("保存", GUILayout.ExpandWidth(false), GUILayout.Height(24f)))
                {
                    EditorUtility.SetDirty(_catalog);
                    AssetDatabase.SaveAssets();
                    SetMessage("输入映射已保存。", MessageType.Info);
                }

                if (GUILayout.Button("恢复全部默认", GUILayout.ExpandWidth(false), GUILayout.Height(24f)))
                {
                    ResetAllToDefaults();
                }

                if (_bindingService != null && _bindingService.IsRebinding && GUILayout.Button("取消监听", GUILayout.ExpandWidth(false), GUILayout.Height(24f)))
                {
                    CancelRebind();
                }
                GUILayout.FlexibleSpace();
                GUI.enabled = true;
            }
        }
    }

    private void DrawToolbar()
    {
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUIStyle searchStyle = GUI.skin.FindStyle("ToolbarSearchTextField") ?? EditorStyles.toolbarTextField;
            _search = GUILayout.TextField(_search ?? string.Empty, searchStyle, GUILayout.MinWidth(160f));
            if (GUILayout.Button("清空", EditorStyles.toolbarButton, GUILayout.Width(42f)))
            {
                _search = string.Empty;
                GUI.FocusControl(null);
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("展开全部", EditorStyles.toolbarButton, GUILayout.Width(58f)))
            {
                SetAllFoldouts(true);
            }
            if (GUILayout.Button("全部收起", EditorStyles.toolbarButton, GUILayout.Width(58f)))
            {
                SetAllFoldouts(false);
            }
            _showPaths = GUILayout.Toggle(_showPaths, "显示控制路径", EditorStyles.toolbarButton, GUILayout.Width(92f));
        }

        EditorGUILayout.Space(4f);
    }

    private void DrawValidation()
    {
        IReadOnlyList<string> errors = _catalog.GetValidationErrors();
        foreach (string error in errors)
        {
            EditorGUILayout.HelpBox(error, MessageType.Error);
        }
    }

    private void DrawMessage()
    {
        if (!string.IsNullOrWhiteSpace(_message))
        {
            EditorGUILayout.HelpBox(_message, _messageType);
        }
    }

    private void DrawActionCard(InputActionData action)
    {
        if (!_foldouts.TryGetValue(action.Id, out bool expanded))
        {
            expanded = false;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                expanded = EditorGUILayout.Foldout(expanded, GetActionName(action.Id), true, EditorStyles.foldoutHeader);
                GUILayout.FlexibleSpace();
                GUILayout.Label(GetActionKind(action), EditorStyles.miniLabel, GUILayout.Width(64f));
            }

            _foldouts[action.Id] = expanded;
            if (!expanded)
            {
                return;
            }

            EditorGUILayout.Space(2f);
            DrawActivationMode(action);
            for (int index = 0; index < action.Bindings.Count; index++)
            {
                DrawBinding(action, action.Bindings[index], index);
            }
        }

        EditorGUILayout.Space(2f);
    }

    private void DrawBinding(InputActionData action, InputBindingData binding, int bindingIndex)
    {
        if (binding.IsComposite)
        {
            EditorGUILayout.LabelField(string.IsNullOrWhiteSpace(binding.Name) ? "组合按键" : binding.Name, "二维组合", EditorStyles.miniBoldLabel);
            return;
        }

        string label = GetBindingName(binding.Name, bindingIndex);
        string displayName = _bindingService?.GetDisplayString(action.Id, bindingIndex);
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = binding.Path;
        }

        using (new EditorGUILayout.VerticalScope(GUI.skin.box))
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(label, EditorStyles.miniBoldLabel, GUILayout.Width(62f));

                bool isWaiting = IsWaiting(action.Id, bindingIndex);
                string keyText = isWaiting ? "监听中…" : displayName;
                GUIStyle keyStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
                float displayWidth = keyStyle.CalcSize(new GUIContent(displayName)).x;
                float waitingWidth = keyStyle.CalcSize(new GUIContent("监听中…")).x;
                float keyWidth = Mathf.Clamp(Mathf.Max(displayWidth, waitingWidth) + 8f, 42f, 160f);
                using (new EditorGUI.DisabledScope(isWaiting))
                {
                    if (GUILayout.Button(keyText, keyStyle, GUILayout.Width(keyWidth), GUILayout.Height(22f)))
                    {
                        StartRebind(action.Id, bindingIndex);
                    }

                    if (GUILayout.Button("恢复默认", GUILayout.ExpandWidth(false), GUILayout.Height(22f)))
                    {
                        ResetBindingToDefault(action.Id, bindingIndex);
                    }
                }
                GUILayout.FlexibleSpace();
            }

            if (_showPaths)
            {
                EditorGUI.indentLevel++;
                string path = EditorGUILayout.DelayedTextField("控制路径", binding.Path);
                EditorGUI.indentLevel--;
                if (path != binding.Path)
                {
                    SetCatalogBinding(action.Id, bindingIndex, path);
                }
            }
        }

        if (_bindingService != null && _bindingService.TryFindConflict(action.Id, bindingIndex, binding.Path, out InputBindingConflict conflict))
        {
            EditorGUILayout.HelpBox($"与 {GetActionName(conflict.Action)} 的 {conflict.DisplayName} 冲突。", MessageType.Warning);
        }
    }

    private void StartRebind(GJInputAction id, int bindingIndex)
    {
        if (_bindingService == null)
        {
            RebuildPreview();
        }

        CancelRebind();
        _waitingAction = id;
        _waitingBindingIndex = bindingIndex;
        ClearMessage();
        InputActionRebindingExtensions.RebindingOperation operation = _bindingService?.StartRebind(
            id,
            bindingIndex,
            result =>
            {
                ClearWaiting();
                if (!result.IsSuccessful)
                {
                    SetMessage($"按键冲突：{GetActionName(result.Conflict.Action)} 已使用 {result.Conflict.DisplayName}。本次修改未保存。", MessageType.Warning);
                    Repaint();
                    return;
                }

                SetCatalogBinding(id, bindingIndex, result.Path);
                SetMessage("按键已修改并写入输入数据源。", MessageType.Info);
                Repaint();
            },
            () =>
            {
                ClearWaiting();
                SetMessage("已取消按键监听。", MessageType.Info);
                Repaint();
            });

        if (operation == null)
        {
            ClearWaiting();
            SetMessage("该绑定无法进入监听状态。", MessageType.Error);
        }

        Repaint();
    }

    private void DrawActivationMode(InputActionData action)
    {
        if (!CanChangeActivationMode(action.Id))
        {
            return;
        }

        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("触发方式", EditorStyles.miniBoldLabel);
            int selected = action.ActivationMode == InputActivationMode.Toggle ? 1 : 0;
            int next = selected;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Toggle(selected == 0, "按住生效", GUI.skin.button, GUILayout.ExpandWidth(false), GUILayout.Height(24f)))
                {
                    next = 0;
                }
                if (GUILayout.Toggle(selected == 1, "点击切换", GUI.skin.button, GUILayout.ExpandWidth(false), GUILayout.Height(24f)))
                {
                    next = 1;
                }
                GUILayout.FlexibleSpace();
            }
            InputActivationMode nextMode = next == 1 ? InputActivationMode.Toggle : InputActivationMode.Hold;
            if (nextMode != action.ActivationMode)
            {
                Undo.RecordObject(_catalog, "修改输入触发方式");
                _catalog.SetActivationMode(action.Id, nextMode);
                EditorUtility.SetDirty(_catalog);
                AssetDatabase.SaveAssets();
                SetMessage($"{GetActionName(action.Id)}已改为{GetActivationModeName(nextMode)}。", MessageType.Info);
            }

            EditorGUILayout.LabelField(
                nextMode == InputActivationMode.Hold ? "松开按键后结束状态。" : "每次点击在开启与关闭之间切换。",
                EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(4f);
    }

    private void SetCatalogBinding(GJInputAction id, int bindingIndex, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            SetMessage("绑定路径不能为空。", MessageType.Error);
            return;
        }

        Undo.RecordObject(_catalog, "修改按键映射");
        _catalog.SetBindingPath(id, bindingIndex, path.Trim());
        EditorUtility.SetDirty(_catalog);
        AssetDatabase.SaveAssets();
        RebuildPreview();
    }

    private void ResetBindingToDefault(GJInputAction id, int bindingIndex)
    {
        Undo.RecordObject(_catalog, "恢复默认按键");
        if (!_catalog.ResetBindingToDefault(id, bindingIndex))
        {
            SetMessage("当前绑定无法恢复默认值。", MessageType.Error);
            return;
        }

        EditorUtility.SetDirty(_catalog);
        AssetDatabase.SaveAssets();
        RebuildPreview();
        SetMessage("该绑定已恢复默认值。", MessageType.Info);
    }

    private void ResetAllToDefaults()
    {
        CancelRebind();
        Undo.RecordObject(_catalog, "恢复全部默认按键");
        _catalog.ResetToDefaults();
        EditorUtility.SetDirty(_catalog);
        AssetDatabase.SaveAssets();
        RebuildPreview();
        SetMessage("全部绑定已恢复默认值。", MessageType.Info);
    }

    private void CancelRebind()
    {
        _bindingService?.CancelRebind();
        ClearWaiting();
    }

    private void RebuildPreview()
    {
        DisposePreview();
        if (_catalog == null || _catalog.GetValidationErrors().Count > 0)
        {
            return;
        }

        _previewAsset = _catalog.BuildAsset();
        _bindingService = new InputBindingService(_previewAsset);
    }

    private void DisposePreview()
    {
        _bindingService?.Dispose();
        _bindingService = null;
        if (_previewAsset != null)
        {
            DestroyImmediate(_previewAsset);
            _previewAsset = null;
        }
    }

    private void FindDefaultCatalog()
    {
        InputActionCatalogSO selected = Selection.activeObject as InputActionCatalogSO;
        if (selected != null)
        {
            _catalog = selected;
            return;
        }

        InputActionCatalogSO defaultCatalog = AssetDatabase.LoadAssetAtPath<InputActionCatalogSO>("Assets/SO/Input/InputActionCatalog.asset");
        if (defaultCatalog != null)
        {
            _catalog = defaultCatalog;
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:InputActionCatalogSO");
        if (guids.Length > 0)
        {
            _catalog = AssetDatabase.LoadAssetAtPath<InputActionCatalogSO>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }

    private bool MatchesSearch(GJInputAction id)
    {
        if (string.IsNullOrWhiteSpace(_search))
        {
            return true;
        }

        string search = _search.ToLowerInvariant();
        return id.ToString().ToLowerInvariant().Contains(search) || GetActionName(id).Contains(_search);
    }

    private bool IsWaiting(GJInputAction id, int bindingIndex)
    {
        return _waitingAction == id && _waitingBindingIndex == bindingIndex;
    }

    private void SetAllFoldouts(bool expanded)
    {
        if (_catalog == null)
        {
            return;
        }

        foreach (InputActionData action in _catalog.Actions)
        {
            _foldouts[action.Id] = expanded;
        }
    }

    private void ClearWaiting()
    {
        _waitingAction = null;
        _waitingBindingIndex = -1;
    }

    private void SetMessage(string message, MessageType messageType)
    {
        _message = message;
        _messageType = messageType;
    }

    private void ClearMessage()
    {
        _message = null;
    }

    private static string GetActionName(GJInputAction id)
    {
        switch (id)
        {
            case GJInputAction.Move:
                return "移动";
            case GJInputAction.Look:
                return "视角";
            case GJInputAction.JumpVault:
                return "跳跃 / 翻越";
            case GJInputAction.Sprint:
                return "疾跑";
            case GJInputAction.Fire:
                return "开火";
            case GJInputAction.Aim:
                return "开镜";
            case GJInputAction.Reload:
                return "换弹";
            case GJInputAction.Inspect:
                return "检视";
            case GJInputAction.Scoreboard:
                return "战绩板";
            case GJInputAction.Settings:
                return "设置";
            default:
                return id.ToString();
        }
    }

    private static string GetActionKind(InputActionData action)
    {
        if (CanChangeActivationMode(action.Id))
        {
            return GetActivationModeName(action.ActivationMode);
        }

        return action.Type == InputActionType.Button ? "按键" : "输入轴";
    }

    private static bool CanChangeActivationMode(GJInputAction id)
    {
        return id == GJInputAction.Sprint || id == GJInputAction.Aim || id == GJInputAction.Scoreboard;
    }

    private static string GetActivationModeName(InputActivationMode activationMode)
    {
        return activationMode == InputActivationMode.Toggle ? "点击切换" : "按住生效";
    }

    private static string GetBindingName(string bindingName, int bindingIndex)
    {
        switch (bindingName)
        {
            case "up":
                return "上";
            case "down":
                return "下";
            case "left":
                return "左";
            case "right":
                return "右";
            default:
                return string.IsNullOrWhiteSpace(bindingName) ? $"绑定 {bindingIndex + 1}" : bindingName;
        }
    }

    private static void EnsureFolder(string parent, string name)
    {
        string path = $"{parent}/{name}";
        if (!AssetDatabase.IsValidFolder(path))
        {
            AssetDatabase.CreateFolder(parent, name);
        }
    }
}
