using System;

[Serializable]
public struct CombatScoreFeedbackData
{
    private readonly int _scoreDelta;
    private readonly bool _isKill;
    private readonly string _defeatedName;
    private readonly string _sourceName;
    private readonly bool _isHeadshot;

    public int ScoreDelta
    {
        get
        {
            return _scoreDelta;
        }
    }

    public bool IsKill
    {
        get
        {
            return _isKill;
        }
    }

    public string DefeatedName
    {
        get
        {
            return _defeatedName;
        }
    }

    public bool IsHeadshot
    {
        get
        {
            return _isHeadshot;
        }
    }

    public string SourceName
    {
        get
        {
            return _sourceName;
        }
    }

    public CombatScoreFeedbackData(
        int scoreDelta,
        bool isKill,
        string defeatedName,
        bool isHeadshot)
        : this(scoreDelta, isKill, defeatedName, isHeadshot, string.Empty)
    {
    }

    public CombatScoreFeedbackData(
        int scoreDelta,
        bool isKill,
        string defeatedName,
        bool isHeadshot,
        string sourceName)
    {
        _scoreDelta = scoreDelta;
        _isKill = isKill;
        _defeatedName = defeatedName;
        _isHeadshot = isHeadshot;
        _sourceName = sourceName;
    }
}
