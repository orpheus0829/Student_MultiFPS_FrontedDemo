using System;

[Serializable]
public struct CombatScoreFeedbackData
{
    private readonly int _scoreDelta;
    private readonly bool _isKill;
    private readonly string _defeatedName;
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

    public CombatScoreFeedbackData(
        int scoreDelta,
        bool isKill,
        string defeatedName,
        bool isHeadshot)
    {
        _scoreDelta = scoreDelta;
        _isKill = isKill;
        _defeatedName = defeatedName;
        _isHeadshot = isHeadshot;
    }
}
