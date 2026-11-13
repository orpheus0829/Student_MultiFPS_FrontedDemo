using System;
using UnityEngine;

public enum ModeKind
{
    Solo = 0,
    TeamDeathmatch = 1,
    Bomb = 2,
    Custom = 3
}

public enum ModeValueType
{
    Bool = 0,
    Int = 1,
    Float = 2,
    String = 3
}

public enum WinConditionType
{
    Score = 0,
    TeamScore = 1,
    RoundWins = 2,
    Time = 3
}

[Serializable]
public sealed class ModeConfigItem
{
    [SerializeField, InspectorName("配置键")] private string _key = string.Empty;
    [SerializeField, InspectorName("值类型")] private ModeValueType _type;
    [SerializeField, InspectorName("数值")] private float _number;
    [SerializeField, InspectorName("开关")] private bool _flag;
    [SerializeField, InspectorName("文本")] private string _text = string.Empty;
    [SerializeField, InspectorName("允许删除")] private bool _isRemovable = true;

    public string Key
    {
        get { return _key; }
    }

    public ModeValueType Type
    {
        get { return _type; }
    }

    public float Number
    {
        get { return _number; }
    }

    public bool Flag
    {
        get { return _flag; }
    }

    public string Text
    {
        get { return _text; }
    }

    public bool IsRemovable
    {
        get { return _isRemovable; }
    }

    public ModeConfigItem()
    {
    }

    public ModeConfigItem(ModeConfigItem source, bool forceLocked)
    {
        if (source == null)
        {
            return;
        }

        _key = source._key;
        _type = source._type;
        _number = source._number;
        _flag = source._flag;
        _text = source._text;
        _isRemovable = forceLocked ? false : source._isRemovable;
    }

    public static ModeConfigItem CreateNumber(
        string key,
        float value,
        ModeValueType type = ModeValueType.Float,
        bool isRemovable = true)
    {
        ModeConfigItem item = new ModeConfigItem();
        item._key = NormalizeKey(key);
        item._type = type == ModeValueType.Int ? ModeValueType.Int : ModeValueType.Float;
        item._number = value;
        item._isRemovable = isRemovable;
        return item;
    }

    public static ModeConfigItem CreateBool(string key, bool value, bool isRemovable = true)
    {
        ModeConfigItem item = new ModeConfigItem();
        item._key = NormalizeKey(key);
        item._type = ModeValueType.Bool;
        item._flag = value;
        item._isRemovable = isRemovable;
        return item;
    }

    public static ModeConfigItem CreateString(
        string key,
        string value,
        bool isRemovable = true)
    {
        ModeConfigItem item = new ModeConfigItem();
        item._key = NormalizeKey(key);
        item._type = ModeValueType.String;
        item._text = value ?? string.Empty;
        item._isRemovable = isRemovable;
        return item;
    }

    internal void Set(float value, ModeValueType type)
    {
        _type = type == ModeValueType.Int ? ModeValueType.Int : ModeValueType.Float;
        _number = value;
    }

    internal void Set(bool value)
    {
        _type = ModeValueType.Bool;
        _flag = value;
    }

    internal void Set(string value)
    {
        _type = ModeValueType.String;
        _text = value ?? string.Empty;
    }

    internal void LockRemoval()
    {
        _isRemovable = false;
    }

    private static string NormalizeKey(string key)
    {
        return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
    }
}
