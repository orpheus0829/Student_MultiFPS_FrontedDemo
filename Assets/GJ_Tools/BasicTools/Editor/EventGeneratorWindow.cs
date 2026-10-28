#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

public class EventGeneratorWindow : EditorWindow
{
    private const string DefaultRelPath = "Assets/GJ_Tools/BasicTools/EventMgr.Generated.cs";
    private const string CustomLabel = "✎ 自定义…";

    private static readonly string[] PresetTypes =
    {
        "float", "int", "string", "bool", "double", "long",
        "Vector2", "Vector3", "Vector2Int", "Vector3Int",
        "Color", "GameObject", "Transform", "Component",
        "Collider", "Collider2D", "Rigidbody", "Rigidbody2D",
        "Animator", "AudioClip", "Sprite", "LayerMask", "Material"
    };
    private static readonly string[] TypeOptions = BuildTypeOptions();
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    private static readonly HashSet<string> Keywords = new HashSet<string>
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked",
        "class", "const", "continue", "decimal", "default", "delegate", "do", "double", "else",
        "enum", "event", "explicit", "extern", "false", "finally", "fixed", "float", "for",
        "foreach", "goto", "if", "implicit", "in", "int", "interface", "internal", "is", "lock",
        "long", "namespace", "new", "null", "object", "operator", "out", "override", "params",
        "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true",
        "try", "typeof", "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual",
        "void", "volatile", "while"
    };

    private class Param
    {
        public int TypeIndex;
        public string CustomType = "";
        public string Name = "";
        public bool IsCustom => TypeIndex >= PresetTypes.Length;
        public string Type => IsCustom ? CustomType.Trim() : PresetTypes[TypeIndex];
    }

    private string _eventName = "";
    private readonly List<Param> _params = new List<Param>();
    private Vector2 _scroll;
    private readonly List<string> _events = new List<string>();
    private string _tip = "";
    private double _tipTime;

    private static string _cachedGeneratedPath;

    private static string GeneratedAssetPath
    {
        get
        {
            if (!string.IsNullOrEmpty(_cachedGeneratedPath)) return _cachedGeneratedPath;

            string[] guids = AssetDatabase.FindAssets("EventMgr.Generated t:MonoScript");
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(p)) continue;
                if (!p.EndsWith("EventMgr.Generated.cs")) continue;

                _cachedGeneratedPath = p;
                return p;
            }

            _cachedGeneratedPath = DefaultRelPath;
            return DefaultRelPath;
        }
    }

    private static string FullPath
    {
        get
        {
            string assetPath = GeneratedAssetPath;
            string relative = assetPath.StartsWith("Assets/")
                ? assetPath.Substring("Assets/".Length)
                : assetPath;

            return Path.Combine(Application.dataPath, relative.Replace('/', Path.DirectorySeparatorChar));
        }
    }

    private static string[] BuildTypeOptions()
    {
        var options = new List<string>(PresetTypes) { CustomLabel };
        return options.ToArray();
    }

    [MenuItem("Tools/GJ_Tools/事件生成器")]
    private static void Open()
    {
        EventGeneratorWindow win = GetWindow<EventGeneratorWindow>("GJ 事件生成器");
        win.RefreshList();
    }

    private void OnGUI()
    {
        EditorGUILayout.Space(2);
        EditorGUILayout.LabelField("事件生成器 → EventMgr.Generated.cs", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "订阅 EventMgr.BindXxx(this, H1, H2, ...) · 发布 EventMgr.BroadcastXxx(...)",
            EditorStyles.miniLabel);
        EditorGUILayout.Space(2);

        EditorGUILayout.LabelField("事件名", GUILayout.Width(64));
        _eventName = EditorGUILayout.TextField(_eventName);
        EditorGUILayout.LabelField(
            "例：PlayerDamaged → 生成 OnPlayerDamaged / BindPlayerDamaged（勿带 On 前缀）",
            EditorStyles.miniLabel);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField($"参数（{_params.Count} 个，可留空）", EditorStyles.boldLabel);

        int removeAt = -1;
        for (int i = 0; i < _params.Count; i++)
        {
            Param p = _params[i];
            EditorGUILayout.BeginHorizontal();
            int pick = EditorGUILayout.Popup(p.TypeIndex, TypeOptions, GUILayout.Width(104));
            if (pick != p.TypeIndex)
            {
                p.TypeIndex = pick;
            }
            if (p.IsCustom)
            {
                p.CustomType = EditorGUILayout.TextField(p.CustomType, GUILayout.MinWidth(90));
            }
            p.Name = EditorGUILayout.TextField(p.Name);
            if (GUILayout.Button("×", GUILayout.Width(22)))
            {
                removeAt = i;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (removeAt >= 0)
        {
            _params.RemoveAt(removeAt);
        }

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("+ 添加参数", GUILayout.Width(104)))
        {
            _params.Add(new Param());
        }
        if (GUILayout.Button("清空", GUILayout.Width(56)))
        {
            _params.Clear();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);
        if (GUILayout.Button("生 成 事 件", GUILayout.Height(28)))
        {
            Generate();
        }

        if (!string.IsNullOrEmpty(_tip) && EditorApplication.timeSinceStartup - _tipTime < 8)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField(_tip, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("生成文件：" + GeneratedAssetPath, EditorStyles.miniLabel);
        EditorGUILayout.Space(2f);
        EditorGUILayout.LabelField($"已有事件（{_events.Count}）", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MinHeight(40), GUILayout.MaxHeight(140));
        for (int i = 0; i < _events.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("· " + _events[i]);
            if (GUILayout.Button("删除", GUILayout.Width(56)))
            {
                RemoveEvent(_events[i]);
                RefreshList();
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("刷新"))
        {
            RefreshList();
        }
        if (GUILayout.Button("打开生成文件"))
        {
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(GeneratedAssetPath);
            if (script)
            {
                AssetDatabase.OpenAsset(script);
            }
            else
            {
                SetTip("未找到生成文件，先生成一个事件试试");
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void Generate()
    {
        string name = _eventName.Trim();
        if (name.StartsWith("On") && name.Length > 2 && char.IsUpper(name[2]))
        {
            name = name.Substring(2);
        }

        if (!IsValidIdentifier(name))
        {
            EditorUtility.DisplayDialog("生成失败", $"事件名非法：{_eventName}\n请输入 C# 标识符，例如 PlayerDamaged", "好");
            return;
        }
        if (Keywords.Contains(name))
        {
            EditorUtility.DisplayDialog("生成失败", $"事件名不能是 C# 关键字：{name}", "好");
            return;
        }
        if (_events.Contains(name) || ExistsHandwritten(name))
        {
            EditorUtility.DisplayDialog("生成失败", $"事件 {name} 已存在（生成区或 EventMgr.cs 手写区），不能重复生成", "好");
            return;
        }

        for (int i = 0; i < _params.Count; i++)
        {
            Param p = _params[i];
            if (string.IsNullOrEmpty(p.Type))
            {
                EditorUtility.DisplayDialog("生成失败", $"第 {i + 1} 个参数：类型为空（选了自定义请填类型名）", "好");
                return;
            }
            if (!IsValidIdentifier(p.Name) || Keywords.Contains(p.Name))
            {
                EditorUtility.DisplayDialog("生成失败", $"第 {i + 1} 个参数：参数名非法：\"{p.Name}\"", "好");
                return;
            }
            for (int j = 0; j < i; j++)
            {
                if (_params[j].Name == p.Name)
                {
                    EditorUtility.DisplayDialog("生成失败", $"参数名重复：{p.Name}", "好");
                    return;
                }
            }
        }

        var sb = new StringBuilder();
        string types = string.Join(", ", _params.ConvertAll(p => p.Type));
        string args = string.Join(", ", _params.ConvertAll(p => $"{p.Type} {p.Name}"));
        string callArgs = string.Join(", ", _params.ConvertAll(p => p.Name));
        string handlerType = _params.Count == 0 ? "Action" : $"Action<{types}>";

        sb.AppendLine();
        sb.AppendLine($"    #region Event: {name}");
        sb.AppendLine($"    public static {handlerType} On{name};");
        sb.AppendLine($"    public static void Broadcast{name}({args})");
        sb.AppendLine("    {");
        sb.AppendLine($"        On{name}?.Invoke({callArgs});");
        sb.AppendLine("    }");
        sb.AppendLine($"    public static void Bind{name}(MonoBehaviour owner, params {handlerType}[] handlers)");
        sb.AppendLine("    {");
        sb.AppendLine("        foreach (var h in handlers)");
        sb.AppendLine("        {");
        sb.AppendLine($"            Register(owner, () => On{name} += h, () => On{name} -= h, h);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine($"    #endregion");

        InsertIntoFile(sb.ToString());
        RefreshList();
        SetTip($"已生成事件 {name}\n订阅 EventMgr.Bind{name}(this, H1, H2, ...)（可一次绑多个）\n发布 EventMgr.Broadcast{name}({callArgs})");
    }

    private void InsertIntoFile(string snippet)
    {
        string path = FullPath;
        string text;
        if (File.Exists(path))
        {
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        else
        {
            text = CreateShell();
        }
        int close = text.LastIndexOf('}');
        if (close < 0)
        {
            Debug.LogError("[事件生成器] 生成文件结构异常，已跳过写入");
            return;
        }
        text = text.Insert(close, snippet);

        string dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, text, Utf8NoBom);
        AssetDatabase.ImportAsset(GeneratedAssetPath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static string CreateShell()
    {
        return "using System;\n" +
               "using UnityEngine;\n\n" +
               "public partial class EventMgr\n" +
               "{\n" +
               "}\n";
    }

    private void RemoveEvent(string name)
    {
        string path = FullPath;
        if (!File.Exists(path))
        {
            return;
        }
        string text = File.ReadAllText(path, Encoding.UTF8);
        string marker = "#region Event: " + name;
        int start = text.IndexOf(marker, System.StringComparison.Ordinal);
        if (start < 0)
        {
            return;
        }
        int lineStart = text.LastIndexOf('\n', start) + 1;
        int end = text.IndexOf("#endregion", start, System.StringComparison.Ordinal);
        if (end < 0)
        {
            return;
        }
        end += "#endregion".Length;
        text = text.Remove(lineStart, end - lineStart);
        text = Regex.Replace(text, "\n{3,}", "\n\n");
        File.WriteAllText(path, text, Utf8NoBom);
        AssetDatabase.ImportAsset(GeneratedAssetPath, ImportAssetOptions.ForceSynchronousImport);
    }

    private void RefreshList()
    {
        _events.Clear();
        _cachedGeneratedPath = null;
        string path = FullPath;
        if (!File.Exists(path))
        {
            return;
        }
        string text = File.ReadAllText(path, Encoding.UTF8);
        foreach (Match m in Regex.Matches(text, @"#region\s+Event:\s*([A-Za-z_][A-Za-z0-9_]*)"))
        {
            string n = m.Groups[1].Value;
            if (!_events.Contains(n))
            {
                _events.Add(n);
            }
        }
        Repaint();
    }

    private static bool IsValidIdentifier(string s)
    {
        return Regex.IsMatch(s, @"^[A-Za-z_][A-Za-z0-9_]*$");
    }

    private static bool ExistsHandwritten(string name)
    {
        string path = null;

        string[] guids = AssetDatabase.FindAssets("EventMgr t:MonoScript");
        for (int i = 0; i < guids.Length; i++)
        {
            string p = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (string.IsNullOrEmpty(p)) continue;
            if (!p.EndsWith("/EventMgr.cs")) continue;

            path = p;
            break;
        }

        if (string.IsNullOrEmpty(path)) return false;

        string full = Path.Combine(Application.dataPath,
            path.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(full)) return false;

        string text = File.ReadAllText(full, Encoding.UTF8);
        return Regex.IsMatch(text, @"static\s+Action(?:<[^;]*>)?\s+On" + name + @"\s*;");
    }

    private void SetTip(string msg)
    {
        _tip = msg;
        _tipTime = EditorApplication.timeSinceStartup;
    }
}
#endif