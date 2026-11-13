public sealed class CustomRules : ModeRulesBase
{
    private float _remainingTime;

    public CustomRules(MatchManager match, MatchConfig config) : base(match, config)
    {
    }

    public override void OnRoundStart()
    {
        _remainingTime = Config.Registry.GetFloat(
            "custom.roundTimeLimit",
            Config.Mode ? Config.Mode.RoundTimeLimit : 0f);
    }

    public override void Tick(float deltaTime)
    {
        if (_remainingTime > 0f)
        {
            _remainingTime -= deltaTime;
        }
    }

    public override bool IsRoundFinished(out int winningTeam)
    {
        winningTeam = -1;
        int scoreToWin = Config.Registry.GetInt(
            "custom.scoreToWin",
            Config.Mode ? Config.Mode.ScoreToWin : 30);
        WinConditionType condition = Config.Mode
            ? Config.Mode.WinCondition
            : WinConditionType.Score;
        if (condition == WinConditionType.Time && _remainingTime <= 0f)
        {
            return Match.TryGetLeadingTeam(out winningTeam);
        }

        return Match.TryGetLeadingTeamAtScore(scoreToWin, out winningTeam);
    }
}
