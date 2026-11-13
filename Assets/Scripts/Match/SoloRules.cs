using System.Collections.Generic;

public sealed class SoloRules : ModeRulesBase
{
    private float _remainingTime;
    private bool _hasTimeLimit;

    public SoloRules(MatchManager match, MatchConfig config) : base(match, config)
    {
    }

    public override void OnRoundStart()
    {
        _remainingTime = Config.Registry.GetFloat(
            "solo.roundTimeLimit",
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
            "solo.scoreToWin",
            Config.Mode ? Config.Mode.ScoreToWin : 30);
        IReadOnlyList<MatchParticipant> roster = Match.Roster;
        for (int i = 0; i < roster.Count; i++)
        {
            MatchParticipant participant = roster[i];
            if (participant.Kills < scoreToWin)
            {
                continue;
            }

            winningTeam = participant.Team;
            return true;
        }

        if (_hasTimeLimit && _remainingTime <= 0f)
        {
            return Match.TryGetLeadingTeam(out winningTeam);
        }

        return false;
    }
}
