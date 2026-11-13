using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class ScoreboardUI : MonoBehaviour
{
    private readonly List<MatchParticipant> _sorted = new List<MatchParticipant>();
    private GameObject _panel;
    private TMP_Text _content;
    private PlayerActionCtrl _actions;
    private PlayerCrosshairUI _crosshair;
    private MatchManager _manager;
    private bool _crosshairWasActive;
    private bool _hasCrosshairVisibilitySnapshot;
    private bool _visible;

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
        if (!_actions)
        {
            TryBindLocalPlayer();
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

        if (_actions)
        {
            _actions.CommandReceived -= HandleCommand;
            _actions = null;
        }

        SetVisible(false);
    }

    private void TryBindLocalPlayer()
    {
        if (!LocalPlayerLocator.TryGet(out GameObject player) ||
            !player.TryGetComponent(out PlayerActionCtrl actions))
        {
            return;
        }
        if (_actions == actions)
        {
            return;
        }
        if (_actions)
        {
            _actions.CommandReceived -= HandleCommand;
        }
        _actions = actions;
        _actions.CommandReceived += HandleCommand;
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
        _sorted.Sort(CompareParticipants);
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

    private static int CompareParticipants(MatchParticipant a, MatchParticipant b)
    {
        int team = a.Team.CompareTo(b.Team);
        if (team != 0) return team;
        int kills = b.Kills.CompareTo(a.Kills);
        return kills != 0 ? kills : a.Slot.CompareTo(b.Slot);
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
