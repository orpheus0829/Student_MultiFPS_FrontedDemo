using System;

public enum KillFeedKind
{
    Neutral = 0,
    LocalKill = 1,
    Assist = 2
}

[Serializable]
public struct KillFeedMessage
{
    private readonly string _killerName;
    private readonly string _victimName;
    private readonly string _weaponSpriteToken;
    private readonly bool _isHeadshot;
    private readonly KillFeedKind _kind;

    public string KillerName
    {
        get
        {
            return _killerName;
        }
    }

    public string VictimName
    {
        get
        {
            return _victimName;
        }
    }

    public string WeaponSpriteToken
    {
        get
        {
            return _weaponSpriteToken;
        }
    }

    public bool IsHeadshot
    {
        get
        {
            return _isHeadshot;
        }
    }

    public KillFeedKind Kind { get { return _kind; } }

    public KillFeedMessage(
        string killerName,
        string victimName,
        string weaponSpriteToken,
        bool isHeadshot)
    {
        _killerName = killerName;
        _victimName = victimName;
        _weaponSpriteToken = weaponSpriteToken;
        _isHeadshot = isHeadshot;
        _kind = KillFeedKind.Neutral;
    }

    public KillFeedMessage(
        string killerName,
        string victimName,
        string weaponSpriteToken,
        bool isHeadshot,
        KillFeedKind kind)
    {
        _killerName = killerName;
        _victimName = victimName;
        _weaponSpriteToken = weaponSpriteToken;
        _isHeadshot = isHeadshot;
        _kind = kind;
    }
}
