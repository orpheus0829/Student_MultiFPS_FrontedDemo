using System.Collections.Generic;
using UnityEngine;

public sealed class ModeConfigRegistry
{
    private readonly Dictionary<string, ModeConfigItem> _items =
        new Dictionary<string, ModeConfigItem>(System.StringComparer.Ordinal);
    private readonly List<ModeConfigItem> _orderedItems = new List<ModeConfigItem>();
    private readonly bool _lockNewItems;

    public IReadOnlyList<ModeConfigItem> Items
    {
        get { return _orderedItems; }
    }

    public ModeConfigRegistry(ModeDefinitionSO definition)
    {
        _lockNewItems = definition && definition.IsBuiltIn;
        if (!definition)
        {
            return;
        }

        IReadOnlyList<ModeConfigItem> source = definition.Items;
        for (int i = 0; i < source.Count; i++)
        {
            ModeConfigItem item = source[i];
            if (item == null || string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            AddOrReplace(new ModeConfigItem(item, _lockNewItems));
        }
    }

    public bool Contains(string key)
    {
        return TryFind(key, out _);
    }

    public bool TryGetFloat(string key, out float value)
    {
        value = 0f;
        if (!TryFind(key, out ModeConfigItem item) ||
            (item.Type != ModeValueType.Float && item.Type != ModeValueType.Int))
        {
            return false;
        }

        value = item.Number;
        return true;
    }

    public bool TryGetInt(string key, out int value)
    {
        value = 0;
        if (!TryFind(key, out ModeConfigItem item) || item.Type != ModeValueType.Int)
        {
            return false;
        }

        value = Mathf.RoundToInt(item.Number);
        return true;
    }

    public bool TryGetBool(string key, out bool value)
    {
        value = false;
        if (!TryFind(key, out ModeConfigItem item) || item.Type != ModeValueType.Bool)
        {
            return false;
        }

        value = item.Flag;
        return true;
    }

    public bool TryGetString(string key, out string value)
    {
        value = string.Empty;
        if (!TryFind(key, out ModeConfigItem item) || item.Type != ModeValueType.String)
        {
            return false;
        }

        value = item.Text;
        return true;
    }

    public float GetFloat(string key, float fallback)
    {
        return TryGetFloat(key, out float value) ? value : fallback;
    }

    public int GetInt(string key, int fallback)
    {
        if (TryGetInt(key, out int value))
        {
            return value;
        }

        return TryGetFloat(key, out float number) ? Mathf.RoundToInt(number) : fallback;
    }

    public bool GetBool(string key, bool fallback)
    {
        return TryGetBool(key, out bool value) ? value : fallback;
    }

    public string GetString(string key, string fallback)
    {
        return TryGetString(key, out string value) ? value : fallback;
    }

    public bool Set(string key, float value)
    {
        return SetNumber(key, value, ModeValueType.Float);
    }

    public bool Set(string key, int value)
    {
        return SetNumber(key, value, ModeValueType.Int);
    }

    public bool Set(string key, bool value)
    {
        if (TryFind(key, out ModeConfigItem item))
        {
            if (item.Type != ModeValueType.Bool)
            {
                return false;
            }

            item.Set(value);
            return true;
        }

        AddOrReplace(ModeConfigItem.CreateBool(key, value, !_lockNewItems));
        return true;
    }

    public bool Set(string key, string value)
    {
        if (TryFind(key, out ModeConfigItem item))
        {
            if (item.Type != ModeValueType.String)
            {
                return false;
            }

            item.Set(value);
            return true;
        }

        AddOrReplace(ModeConfigItem.CreateString(key, value, !_lockNewItems));
        return true;
    }

    public bool Remove(string key)
    {
        if (!TryFind(key, out ModeConfigItem item) || !item.IsRemovable)
        {
            return false;
        }

        _items.Remove(item.Key);
        _orderedItems.Remove(item);
        return true;
    }

    private bool SetNumber(string key, float value, ModeValueType type)
    {
        if (TryFind(key, out ModeConfigItem item))
        {
            if (item.Type != type)
            {
                return false;
            }

            item.Set(value, type);
            return true;
        }

        AddOrReplace(ModeConfigItem.CreateNumber(key, value, type, !_lockNewItems));
        return true;
    }

    private bool TryFind(string key, out ModeConfigItem item)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            item = null;
            return false;
        }

        return _items.TryGetValue(key.Trim(), out item);
    }

    private void AddOrReplace(ModeConfigItem item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Key))
        {
            return;
        }

        if (_items.TryGetValue(item.Key, out ModeConfigItem previous))
        {
            int index = _orderedItems.IndexOf(previous);
            if (index >= 0)
            {
                _orderedItems[index] = item;
            }
        }
        else
        {
            _orderedItems.Add(item);
        }

        _items[item.Key] = item;
    }
}
