using System.Collections.Generic;

public static class ComponentHelpText
{
    private static readonly Dictionary<System.Type, string[]> s_map = Build();
    private static readonly Dictionary<string, string[]> s_byName = BuildNames(s_map);
    private static readonly Dictionary<string, string[]> s_extraByName = BuildExtraNames();

    private static Dictionary<System.Type, string[]> Build()
    {
        Dictionary<System.Type, string[]> map = new Dictionary<System.Type, string[]>();

        HelpTextTools.Fill(map);

        return map;
    }

    private static Dictionary<string, string[]> BuildNames(Dictionary<System.Type, string[]> map)
    {
        Dictionary<string, string[]> names = new Dictionary<string, string[]>();
        foreach (KeyValuePair<System.Type, string[]> pair in map)
        {
            string fullName = pair.Key.FullName;
            if (!string.IsNullOrEmpty(fullName) && !names.ContainsKey(fullName))
            {
                names[fullName] = pair.Value;
            }
            if (!names.ContainsKey(pair.Key.Name))
            {
                names[pair.Key.Name] = pair.Value;
            }
        }

        return names;
    }

    private static Dictionary<string, string[]> BuildExtraNames()
    {
        Dictionary<string, string[]> map = new Dictionary<string, string[]>();

        HelpTextExtra.Fill(map);

        return map;
    }

    public static bool TryGet(System.Type type, out string title, out string body)
    {
        System.Type current = type;
        while (current != null)
        {
            string[] value;
            if (s_map.TryGetValue(current, out value)
                || TryGetByName(current, out value)
                || TryGetGeneric(current, out value))
            {
                title = value[0];
                body = value[1];
                return true;
            }
            current = current.BaseType;
        }

        title = null;
        body = null;
        return false;
    }

    private static bool TryGetByName(System.Type type, out string[] value)
    {
        string fullName = type.FullName;
        if (!string.IsNullOrEmpty(fullName) && (s_byName.TryGetValue(fullName, out value) || s_extraByName.TryGetValue(fullName, out value)))
        {
            return true;
        }
        if (s_byName.TryGetValue(type.Name, out value) || s_extraByName.TryGetValue(type.Name, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetGeneric(System.Type type, out string[] value)
    {
        if (type.IsGenericType)
        {
            System.Type definition = type.GetGenericTypeDefinition();
            if (s_map.TryGetValue(definition, out value))
            {
                return true;
            }
            if (TryGetByName(definition, out value))
            {
                return true;
            }
        }

        value = null;
        return false;
    }

    public static void Register(Dictionary<System.Type, string[]> map, System.Type type, string title, string body)
    {
        map[type] = new[] { title, body };
    }

    public static void Register(Dictionary<string, string[]> map, string typeName, string title, string body)
    {
        map[typeName] = new[] { title, body };
    }

    public static void Register(string typeName, string title, string body)
    {
        s_extraByName[typeName] = new[] { title, body };
    }
}
