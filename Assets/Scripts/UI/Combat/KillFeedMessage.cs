using System;

[Serializable]
public struct KillFeedMessage
{
    private readonly string _killerName;
    private readonly string _victimName;
    private readonly string _weaponSpriteToken;
    private readonly bool _isHeadshot;

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
    }
}
