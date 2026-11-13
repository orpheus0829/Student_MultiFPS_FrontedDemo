public sealed class TdmRules : ModeRulesBase
{
    private float _remainingTime;
    private bool _hasTimeLimit;

    public TdmRules(MatchManager match, MatchConfig config) : base(match, config)
    {
    }

    public override void OnRoundStart()
    {
        _remainingTime = Config.Registry.GetFloat(
            "tdm.roundTimeLimit",
            Config.Mode ? Config.Mode.RoundTimeLimit : 300f);
        _hasTimeLimit = _remainingTime > 0f;
    }

    public override void Tick(float deltaTime)
    {
        if (_hasTimeLimit && _remainingTime > 0f)
        {
            _remainingTime = UnityEngine.Mathf.Max(0f, _remainingTime - deltaTime);
        }
    }

    public override bool IsRoundFinished(out int winningTeam)
    {
        winningTeam = -1;
        int scoreToWin = Config.Registry.GetInt(
            "tdm.scoreToWin",
            Config.Mode ? Config.Mode.ScoreToWin : 30);
        if (Match.TryGetLeadingTeamAtScore(scoreToWin, out winningTeam))
        {
            return true;
        }

        return _hasTimeLimit && _remainingTime <= 0f &&
            Match.TryGetLeadingTeam(out winningTeam);
    }
}
