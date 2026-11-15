using UnityEngine;

[DisallowMultipleComponent]
public sealed class PlayerRequiredEnvironmentCtrl : MonoBehaviour
{
    private const float BindRetryInterval = 0.25f;

    [Header("玩家界面")]
    [SerializeField, InspectorName("战斗状态 HUD")] private PlayerCombatHudUI _combatHud;
    [SerializeField, InspectorName("伤害反馈控制器")] private CombatFeedbackCtrl _combatFeedback;

    private GameObject _boundPlayer;
    private float _nextBindTime;

    private void Awake()
    {
        if (!_combatHud)
        {
            _combatHud = GetComponentInChildren<PlayerCombatHudUI>(true);
        }

        if (!_combatFeedback)
        {
            _combatFeedback = GetComponentInChildren<CombatFeedbackCtrl>(true);
        }
    }

    private void Start()
    {
        TryBindLocalPlayer();
    }

    private void Update()
    {
        if (_boundPlayer && _boundPlayer.activeInHierarchy)
        {
            return;
        }

        if (Time.unscaledTime < _nextBindTime)
        {
            return;
        }

        _nextBindTime = Time.unscaledTime + BindRetryInterval;
        TryBindLocalPlayer();
    }

    private void TryBindLocalPlayer()
    {
        if (!LocalPlayerLocator.TryGet(out GameObject player) || !player ||
            !player.TryGetComponent(out PlayerInfo playerInfo))
        {
            return;
        }

        _boundPlayer = player;
        if (!player.TryGetComponent(out PlayerInteractionCtrl _))
        {
            player.AddComponent<PlayerInteractionCtrl>();
        }

        if (_combatHud)
        {
            _combatHud.Bind(player);
        }

        if (_combatFeedback)
        {
            _combatFeedback.SetLocalPlayer(playerInfo);
        }
    }
}
