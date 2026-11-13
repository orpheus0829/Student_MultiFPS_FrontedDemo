using UnityEngine;

public sealed class MatchParticipant
{
    private readonly int _slot;
    private readonly int _team;
    private readonly bool _isAI;
    private readonly bool _isLocalPlayer;
    private readonly string _configuredName;
    private PlayerInfo _info;
    private CharacterHealth _health;
    private int _kills;
    private int _deaths;
    private int _assists;
    private int _score;
    private bool _isAlive;

    public int Slot { get { return _slot; } }
    public int Team { get { return _team; } }
    public bool IsAI { get { return _isAI; } }
    public bool IsLocalPlayer { get { return _isLocalPlayer; } }
    public string ConfiguredName { get { return _configuredName; } }
    public PlayerInfo Info { get { return _info; } }
    public CharacterHealth Health { get { return _health; } }
    public int Kills { get { return _kills; } }
    public int Deaths { get { return _deaths; } }
    public int Assists { get { return _assists; } }
    public int Score { get { return _score; } }
    public bool IsAlive { get { return _isAlive; } }
    public bool IsBound { get { return _info; } }

    internal MatchParticipant(MatchSlotConfig slot)
    {
        _slot = slot.Slot;
        _team = slot.Team;
        _isAI = slot.IsAI;
        _isLocalPlayer = slot.IsLocalPlayer;
        _configuredName = slot.DisplayName;
    }

    internal void Bind(PlayerInfo info, CharacterHealth health)
    {
        _info = info;
        _health = health;
        _isAlive = health && !health.IsDead;
    }

    internal void Unbind()
    {
        _info = null;
        _health = null;
        _isAlive = false;
    }

    internal void AddKill(int score)
    {
        _kills++;
        _score += Mathf.Max(0, score);
    }

    internal void AddDeath()
    {
        _deaths++;
        _isAlive = false;
    }

    internal void AddAssist(int score)
    {
        _assists++;
        _score += Mathf.Max(0, score);
    }

    internal void AddScore(int score)
    {
        _score += score;
    }

    internal void MarkAlive()
    {
        _isAlive = _health && !_health.IsDead;
    }
}

public sealed class KillAwardedEvent
{
    public MatchParticipant Killer { get; private set; }
    public MatchParticipant Victim { get; private set; }
    public string WeaponName { get; private set; }
    public string WeaponId { get; private set; }
    public bool WasHeadshot { get; private set; }

    public KillAwardedEvent(
        MatchParticipant killer,
        MatchParticipant victim,
        string weaponId,
        string weaponName,
        bool wasHeadshot)
    {
        Killer = killer;
        Victim = victim;
        WeaponId = weaponId ?? string.Empty;
        WeaponName = weaponName ?? string.Empty;
        WasHeadshot = wasHeadshot;
    }
}

public sealed class AssistAwardedEvent
{
    public PlayerInfo Assister { get; private set; }
    public PlayerInfo Victim { get; private set; }
    public string WeaponName { get; private set; }

    public AssistAwardedEvent(PlayerInfo assister, PlayerInfo victim, string weaponName)
    {
        Assister = assister;
        Victim = victim;
        WeaponName = weaponName ?? string.Empty;
    }
}
