using UnityEngine;

[DisallowMultipleComponent]
public sealed class MatchFeedbackBinding : MonoBehaviour
{
    private PlayerInfo _localPlayer;
    private MatchManager _manager;

    private void OnEnable()
    {
        _manager = MatchManager.Instance;
        _manager.AssistAwarded += HandleAssistAwarded;
        RefreshLocalPlayer();
    }

    private void OnDisable()
    {
        if (_manager)
        {
            _manager.AssistAwarded -= HandleAssistAwarded;
            _manager = null;
        }
    }

    private void HandleAssistAwarded(AssistAwardedEvent awarded)
    {
        if (!_localPlayer)
        {
            RefreshLocalPlayer();
        }

        if (awarded == null || !_localPlayer || awarded.Assister != _localPlayer)
        {
            return;
        }
        KillFeedUI.Request(new KillFeedMessage(
            _localPlayer.PlayerName,
            awarded.Victim ? awarded.Victim.PlayerName : string.Empty,
            string.Empty,
            false,
            KillFeedKind.Assist));
    }

    private void RefreshLocalPlayer()
    {
        _localPlayer = LocalPlayerLocator.TryGet(out GameObject player)
            ? player.GetComponent<PlayerInfo>()
            : null;
    }
}
