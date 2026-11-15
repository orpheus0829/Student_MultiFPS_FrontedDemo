using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ScoreboardUI : MonoBehaviour
{
    private readonly List<MatchParticipant> _sorted = new List<MatchParticipant>();
    private GameObject _topScorePanel;
    private TMP_Text _topScoreText;
    private TMP_Text _roundTimerText;
    private GameObject _panel;
    private TMP_Text _content;
    private PlayerInputCtrl _input;
    private PlayerCrosshairUI _crosshair;
    private MatchManager _manager;
    private bool _crosshairWasActive;
    private bool _hasCrosshairVisibilitySnapshot;
    private bool _visible;
    private float _nextTopRefreshTime;

    private void Awake()
    {
        BuildInterface();
        SetVisible(false);
    }

    private void OnEnable()
    {
        _manager = MatchManager.Instance;
        _manager.RosterChanged += Refresh;
        _manager.ParticipantStatChanged += HandleParticipantChanged;
        _manager.RoundStateChanged += HandleStateChanged;
        TryBindLocalPlayer();
    }

    private void Update()
    {
        if (!_input)
        {
            TryBindLocalPlayer();
        }

        if (Time.unscaledTime >= _nextTopRefreshTime)
        {
            _nextTopRefreshTime = Time.unscaledTime + 0.1f;
            RefreshTopScore();
        }
    }

    private void OnDisable()
    {
        if (_manager)
        {
            _manager.RosterChanged -= Refresh;
            _manager.ParticipantStatChanged -= HandleParticipantChanged;
            _manager.RoundStateChanged -= HandleStateChanged;
            _manager = null;
        }

        if (_input)
        {
            _input.CommandIssued -= HandleCommand;
            _input = null;
        }

        SetVisible(false);
    }

    private void TryBindLocalPlayer()
    {
        if (!LocalPlayerLocator.TryGet(out GameObject player) ||
            !player.TryGetComponent(out PlayerInputCtrl input))
        {
            return;
        }
        if (_input == input)
        {
            return;
        }
        if (_input)
        {
            _input.CommandIssued -= HandleCommand;
        }
        _input = input;
        _input.CommandIssued += HandleCommand;
        BindCrosshair(GetComponentInChildren<PlayerCrosshairUI>(true));
    }

    private void HandleCommand(PlayerInputCommand command)
    {
        if (command.Action != GJInputAction.Scoreboard)
        {
            return;
        }
        if (command.Phase == PlayerInputPhase.Started)
        {
            SetVisible(true);
        }
        else if (command.Phase == PlayerInputPhase.Canceled)
        {
            SetVisible(false);
        }
    }

    private void SetVisible(bool visible)
    {
        bool changed = _visible != visible;
        _visible = visible;
        if (_panel)
        {
            _panel.SetActive(visible);
        }

        if (changed)
        {
            ApplyCrosshairVisibility(visible);
        }

        if (visible)
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        RefreshTopScore();
        if (!_content)
        {
            return;
        }
        if (!_manager || _manager.State == MatchState.Idle)
        {
            _content.text = string.Empty;
            return;
        }
        _sorted.Clear();
        for (int i = 0; i < _manager.Roster.Count; i++)
        {
            _sorted.Add(_manager.Roster[i]);
        }
        string scoreStyle = GetScoreStyle();
        if (scoreStyle == "solo")
        {
            _sorted.Sort(CompareSoloParticipants);
        }
        else
        {
            _sorted.Sort(CompareParticipants);
        }
        StringBuilder builder = new StringBuilder(512);
        builder.AppendLine("<size=42><b>对局战绩</b></size>");
        builder.AppendLine("<color=#B9B9B9>玩家                         K     D     A</color>");
        int currentTeam = int.MinValue;
        for (int i = 0; i < _sorted.Count; i++)
        {
            MatchParticipant participant = _sorted[i];
            if (participant.Team != currentTeam)
            {
                currentTeam = participant.Team;
                builder.Append("\n<color=");
                builder.Append(currentTeam % 2 == 0 ? "#54A8FF" : "#FF765C");
                builder.Append("><b>队伍 ");
                builder.Append(currentTeam + 1);
                builder.AppendLine("</b></color>");
            }
            string displayName = participant.Info
                ? participant.Info.PlayerName
                : string.IsNullOrWhiteSpace(participant.ConfiguredName)
                    ? "空位 " + participant.Slot
                    : participant.ConfiguredName;
            if (participant.IsAI) displayName += " [AI]";
            if (participant.IsLocalPlayer) builder.Append("<color=#FFE26A>");
            builder.Append(displayName.PadRight(28));
            builder.Append(participant.Kills.ToString().PadLeft(4));
            builder.Append(participant.Deaths.ToString().PadLeft(6));
            builder.Append(participant.Assists.ToString().PadLeft(6));
            if (participant.IsLocalPlayer) builder.Append("</color>");
            builder.AppendLine();
        }
        _content.text = builder.ToString();
    }

    private void RefreshTopScore()
    {
        if (!_topScorePanel || !_topScoreText || !_roundTimerText)
        {
            return;
        }
        if (!_manager || _manager.State == MatchState.Idle || _manager.Config == null ||
            !_manager.Config.Mode)
        {
            _topScorePanel.SetActive(false);
            return;
        }

        _topScorePanel.SetActive(true);
        RefreshRoundTimer();
        ModeDefinitionSO mode = _manager.Config.Mode;
        string scoreStyle = GetScoreStyle();
        if (scoreStyle == "rounds")
        {
            int roundsToWin = _manager.Config.Registry.GetInt(
                "match.roundsToWin",
                _manager.Config.Registry.GetInt(
                    "bomb.roundsToWin",
                    mode.ScoreToWin));
            _topScoreText.text = string.Format(
                "<color=#54A8FF>{0}</color>  <size=26>:</size>  <color=#FF765C>{1}</color>\n<size=17>{2} · 先胜 {3} 回合</size>",
                _manager.GetTeamRoundWins(0),
                _manager.GetTeamRoundWins(1),
                mode.DisplayName,
                roundsToWin);
            return;
        }
        if (scoreStyle == "solo")
        {
            int localKills = 0;
            int leadingKills = 0;
            for (int i = 0; i < _manager.Roster.Count; i++)
            {
                MatchParticipant participant = _manager.Roster[i];
                leadingKills = Mathf.Max(leadingKills, participant.Kills);
                if (participant.IsLocalPlayer)
                {
                    localKills = participant.Kills;
                }
            }
            int target = _manager.Config.Registry.GetInt(
                "solo.scoreToWin",
                mode.ScoreToWin);
            _topScoreText.text = string.Format(
                "<color=#54A8FF>{0}</color>  <size=26>:</size>  <color=#FFE26A>{1}</color>\n<size=17>{2} · 我 / 第一名 · 目标 {3}</size>",
                localKills,
                leadingKills,
                mode.DisplayName,
                target);
            return;
        }

        int teamTarget = _manager.Config.Registry.GetInt(
            "tdm.scoreToWin",
            mode.ScoreToWin);
        _topScoreText.text = string.Format(
            "<color=#54A8FF>{0}</color>  <size=26>:</size>  <color=#FF765C>{1}</color>\n<size=17>{2} · 目标 {3}</size>",
            _manager.GetTeamScore(0),
            _manager.GetTeamScore(1),
            mode.DisplayName,
            teamTarget);
    }

    private string GetScoreStyle()
    {
        if (!_manager || _manager.Config == null || !_manager.Config.Mode)
        {
            return "team";
        }

        string fallback = _manager.Config.Mode.Kind == ModeKind.Bomb
            ? "rounds"
            : _manager.Config.Mode.Kind == ModeKind.Solo
                ? "solo"
                : "team";
        string rulesType = _manager.Config.Registry.GetString(
            "rules.type",
            fallback);
        return _manager.Config.Registry.GetString(
            "hud.scoreStyle",
            rulesType == "bomb" ? "rounds" : rulesType);
    }

    private void RefreshRoundTimer()
    {
        bool visible = _manager.State == MatchState.InRound &&
            (_manager.HasRoundTimeLimit || _manager.IsObjectiveTimerActive);
        _roundTimerText.gameObject.SetActive(visible);
        if (!visible)
        {
            return;
        }

        int totalSeconds = Mathf.CeilToInt(_manager.RoundTimeRemaining);
        if (totalSeconds <= 0 && GetScoreStyle() == "team" &&
            _manager.GetTeamScore(0) == _manager.GetTeamScore(1))
        {
            _roundTimerText.text = "加时";
            _roundTimerText.color = new Color(1f, 0.76f, 0.2f, 1f);
            return;
        }

        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        _roundTimerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        _roundTimerText.color = _manager.IsObjectiveTimerActive
            ? new Color(1f, 0.18f, 0.12f, 1f)
            : Color.white;
    }

    private static int CompareParticipants(MatchParticipant a, MatchParticipant b)
    {
        int team = a.Team.CompareTo(b.Team);
        if (team != 0) return team;
        int kills = b.Kills.CompareTo(a.Kills);
        return kills != 0 ? kills : a.Slot.CompareTo(b.Slot);
    }

    private static int CompareSoloParticipants(
        MatchParticipant a,
        MatchParticipant b)
    {
        int kills = b.Kills.CompareTo(a.Kills);
        if (kills != 0)
        {
            return kills;
        }

        int deaths = a.Deaths.CompareTo(b.Deaths);
        if (deaths != 0)
        {
            return deaths;
        }

        int assists = b.Assists.CompareTo(a.Assists);
        return assists != 0 ? assists : a.Slot.CompareTo(b.Slot);
    }

    private void BindCrosshair(PlayerCrosshairUI crosshair)
    {
        if (_crosshair == crosshair)
        {
            return;
        }

        if (_visible)
        {
            RestoreCrosshairVisibility();
        }

        _crosshair = crosshair;
        if (_visible)
        {
            CaptureAndHideCrosshair();
        }
    }

    private void ApplyCrosshairVisibility(bool scoreboardVisible)
    {
        if (scoreboardVisible)
        {
            CaptureAndHideCrosshair();
            return;
        }

        RestoreCrosshairVisibility();
    }

    private void CaptureAndHideCrosshair()
    {
        if (!_crosshair || _hasCrosshairVisibilitySnapshot)
        {
            return;
        }

        _crosshairWasActive = _crosshair.gameObject.activeSelf;
        _hasCrosshairVisibilitySnapshot = true;
        if (_crosshairWasActive)
        {
            _crosshair.gameObject.SetActive(false);
        }
    }

    private void RestoreCrosshairVisibility()
    {
        if (!_hasCrosshairVisibilitySnapshot)
        {
            return;
        }

        if (_crosshair)
        {
            _crosshair.gameObject.SetActive(_crosshairWasActive);
        }

        _hasCrosshairVisibilitySnapshot = false;
    }

    private void HandleParticipantChanged(MatchParticipant participant)
    {
        Refresh();
    }

    private void HandleStateChanged(MatchState state)
    {
        if (_visible)
        {
            Refresh();
        }
    }

    private void BuildInterface()
    {
        _topScorePanel = new GameObject("顶部大比分", typeof(RectTransform), typeof(Image));
        _topScorePanel.transform.SetParent(transform, false);
        RectTransform topRect = _topScorePanel.GetComponent<RectTransform>();
        topRect.anchorMin = new Vector2(0.5f, 1f);
        topRect.anchorMax = new Vector2(0.5f, 1f);
        topRect.pivot = new Vector2(0.5f, 1f);
        topRect.sizeDelta = new Vector2(390f, 112f);
        topRect.anchoredPosition = new Vector2(0f, -18f);
        _topScorePanel.GetComponent<Image>().color = new Color(0.02f, 0.025f, 0.035f, 0.76f);
        GameObject topTextObject = new GameObject("比分内容", typeof(RectTransform));
        topTextObject.transform.SetParent(_topScorePanel.transform, false);
        RectTransform topTextRect = topTextObject.GetComponent<RectTransform>();
        topTextRect.anchorMin = Vector2.zero;
        topTextRect.anchorMax = Vector2.one;
        topTextRect.offsetMin = new Vector2(10f, 34f);
        topTextRect.offsetMax = new Vector2(-10f, -4f);
        _topScoreText = topTextObject.AddComponent<TextMeshProUGUI>();
        _topScoreText.fontSize = 34f;
        _topScoreText.color = Color.white;
        _topScoreText.alignment = TextAlignmentOptions.Center;
        _topScoreText.raycastTarget = false;

        GameObject timerObject = new GameObject("本小局计时", typeof(RectTransform));
        timerObject.transform.SetParent(_topScorePanel.transform, false);
        RectTransform timerRect = timerObject.GetComponent<RectTransform>();
        timerRect.anchorMin = new Vector2(0f, 0f);
        timerRect.anchorMax = new Vector2(1f, 0f);
        timerRect.pivot = new Vector2(0.5f, 0f);
        timerRect.offsetMin = new Vector2(10f, 5f);
        timerRect.offsetMax = new Vector2(-10f, 35f);
        _roundTimerText = timerObject.AddComponent<TextMeshProUGUI>();
        _roundTimerText.fontSize = 25f;
        _roundTimerText.fontStyle = FontStyles.Bold;
        _roundTimerText.color = Color.white;
        _roundTimerText.alignment = TextAlignmentOptions.Center;
        _roundTimerText.raycastTarget = false;
        _roundTimerText.text = "00:00";
        _topScorePanel.SetActive(false);

        _panel = new GameObject("战绩面板", typeof(RectTransform), typeof(Image));
        _panel.transform.SetParent(transform, false);
        RectTransform panelRect = _panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.18f, 0.14f);
        panelRect.anchorMax = new Vector2(0.82f, 0.86f);
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        _panel.GetComponent<Image>().color = new Color(0.025f, 0.03f, 0.035f, 0.92f);
        GameObject textObject = new GameObject("战绩内容", typeof(RectTransform));
        textObject.transform.SetParent(_panel.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(42f, 34f);
        textRect.offsetMax = new Vector2(-42f, -34f);
        _content = textObject.AddComponent<TextMeshProUGUI>();
        _content.fontSize = 28f;
        _content.color = Color.white;
        _content.alignment = TextAlignmentOptions.TopLeft;
        _content.textWrappingMode = TextWrappingModes.NoWrap;
        _content.raycastTarget = false;
    }
}
