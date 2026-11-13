public interface IModeRules
{
    void OnMatchStart();
    void OnRoundStart();
    void OnRoundEnd(bool attackerWin);
    bool IsRoundFinished(out int winningTeam);
    void Tick(float deltaTime);
}

public abstract class ModeRulesBase : IModeRules
{
    protected readonly MatchManager Match;
    protected readonly MatchConfig Config;

    protected ModeRulesBase(MatchManager match, MatchConfig config)
    {
        Match = match;
        Config = config;
    }

    public virtual void OnMatchStart()
    {
    }

    public virtual void OnRoundStart()
    {
    }

    public virtual void OnRoundEnd(bool attackerWin)
    {
    }

    public abstract bool IsRoundFinished(out int winningTeam);

    public virtual void Tick(float deltaTime)
    {
    }
}
