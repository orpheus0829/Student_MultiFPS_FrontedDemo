public sealed class BombModeRules : ModeRulesBase
{
    private float _remainingTime;
    private bool _hasTimeLimit;
    private int _objectiveWinner = -1;

    public BombModeRules(MatchManager match, MatchConfig config) : base(match, config)
    {
    }

    public override float RemainingTime
    {
        get { return UnityEngine.Mathf.Max(0f, _remainingTime); }
    }
    public override bool HasTimeLimit { get { return _hasTimeLimit; } }

    public override void OnRoundStart()
    {
        _objectiveWinner = -1;
        _remainingTime = Config.Registry.GetFloat(
            "bomb.roundTimeLimit",
            Config.Mode ? Config.Mode.RoundTimeLimit : 120f);
        _hasTimeLimit = _remainingTime > 0f;
    }

    public override void Tick(float deltaTime)
    {
        if (_hasTimeLimit && !Match.IsBombPlanted && _remainingTime > 0f)
        {
            _remainingTime -= deltaTime;
        }
    }

    public override bool IsRoundFinished(out int winningTeam)
    {
        if (_objectiveWinner >= 0)
        {
            winningTeam = _objectiveWinner;
            return true;
        }

        if (Match.TryGetOnlyLivingTeam(out winningTeam))
        {
            int defenderTeam = Config.Registry.GetInt("bomb.defenderTeam", 1);
            if (Match.IsBombPlanted && winningTeam == defenderTeam)
            {
                winningTeam = -1;
                return false;
            }
            return true;
        }

        if (Match.IsBombPlanted)
        {
            winningTeam = -1;
            return false;
        }

        if (_hasTimeLimit && _remainingTime <= 0f)
        {
            winningTeam = Config.Registry.GetInt("bomb.defenderTeam", 1);
            return true;
        }

        winningTeam = -1;
        return false;
    }

    public void ReportObjectiveWinner(int team)
    {
        _objectiveWinner = team;
    }
}
