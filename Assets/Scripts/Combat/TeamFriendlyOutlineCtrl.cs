using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TeamFriendlyOutlineCtrl : MonoBehaviour
{
    private static readonly Color FriendlyColor = new Color(0.12f, 0.56f, 1f, 0.9f);
    private readonly Dictionary<PlayerInfo, DeathKillerOutline> _outlines =
        new Dictionary<PlayerInfo, DeathKillerOutline>();
    private readonly List<PlayerInfo> _removeBuffer = new List<PlayerInfo>();

    private MatchManager _manager;
    private PlayerInfo _localPlayer;
    private Camera _camera;

    private void OnEnable()
    {
        _manager = MatchManager.Instance;
        _manager.RosterChanged += Refresh;
        _manager.RoundStateChanged += HandleRoundStateChanged;
        Refresh();
    }

    private void Update()
    {
        if (!_localPlayer || !_camera)
        {
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (_manager)
        {
            _manager.RosterChanged -= Refresh;
            _manager.RoundStateChanged -= HandleRoundStateChanged;
            _manager = null;
        }
        ClearOutlines();
    }

    private void Refresh()
    {
        RefreshLocalReferences();
        if (!_manager || !_localPlayer || !_camera || _manager.State == MatchState.Idle)
        {
            ClearOutlines();
            return;
        }

        _removeBuffer.Clear();
        foreach (KeyValuePair<PlayerInfo, DeathKillerOutline> entry in _outlines)
        {
            if (!entry.Key || !entry.Value ||
                !_manager.IsFriendly(_localPlayer, entry.Key) || entry.Key == _localPlayer)
            {
                _removeBuffer.Add(entry.Key);
            }
        }
        for (int i = 0; i < _removeBuffer.Count; i++)
        {
            RemoveOutline(_removeBuffer[i]);
        }

        IReadOnlyList<MatchParticipant> roster = _manager.Roster;
        for (int i = 0; i < roster.Count; i++)
        {
            PlayerInfo teammate = roster[i].Info;
            if (!teammate || teammate == _localPlayer ||
                !_manager.IsFriendly(_localPlayer, teammate) || _outlines.ContainsKey(teammate))
            {
                continue;
            }

            DeathKillerOutline outline = DeathKillerOutline.Show(
                teammate.gameObject,
                _camera,
                FriendlyColor,
                "友方角色穿墙蓝色描边");
            if (outline)
            {
                _outlines.Add(teammate, outline);
            }
        }
    }

    private void RefreshLocalReferences()
    {
        if (!_localPlayer && LocalPlayerLocator.TryGet(out GameObject player))
        {
            _localPlayer = player.GetComponent<PlayerInfo>();
            _camera = player.GetComponentInChildren<Camera>(true);
        }
        if (!_camera)
        {
            _camera = Camera.main;
        }
    }

    private void HandleRoundStateChanged(MatchState state)
    {
        Refresh();
    }

    private void RemoveOutline(PlayerInfo player)
    {
        if (!player || !_outlines.TryGetValue(player, out DeathKillerOutline outline))
        {
            _outlines.Remove(player);
            return;
        }
        if (outline)
        {
            outline.Hide();
        }
        _outlines.Remove(player);
    }

    private void ClearOutlines()
    {
        foreach (DeathKillerOutline outline in _outlines.Values)
        {
            if (outline)
            {
                outline.Hide();
            }
        }
        _outlines.Clear();
        _removeBuffer.Clear();
    }
}
