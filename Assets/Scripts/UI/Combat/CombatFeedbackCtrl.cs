using System;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatFeedbackCtrl : MonoBehaviour
{
    [Header("本地玩家")]
    [SerializeField, InspectorName("本地玩家身份")] private PlayerInfo _localPlayer;
    [SerializeField, InspectorName("未指定玩家时显示首位攻击者得分")] private bool _acceptAnyInstigator = true;

    [Header("界面引用")]
    [SerializeField, InspectorName("击杀播报")] private KillFeedUI _killFeedUI;
    [SerializeField, InspectorName("分数反馈")] private CombatScoreFeedbackUI _scoreFeedbackUI;
    [SerializeField, InspectorName("准心命中反馈")] private CrosshairHitFeedbackUI _crosshairFeedbackUI;

    [Header("图标资源")]
    [SerializeField, ResourceId(typeof(TMP_SpriteAsset)), InspectorName("击杀图标资源 ID")]
    private string _spriteAssetId = "ui.killfeed.sprite_asset";

    private bool _hasLoadedSpriteAsset;
    private EventMgr.Subscription _damageSubscription;
    private ResourceMgr _resourceMgr;

    private void Awake()
    {
        if (!_killFeedUI)
        {
            _killFeedUI = GetComponentInChildren<KillFeedUI>(true);
        }

        if (!_scoreFeedbackUI)
        {
            _scoreFeedbackUI = GetComponentInChildren<CombatScoreFeedbackUI>(true);
        }

    }

    private void OnEnable()
    {
        _damageSubscription = EventMgr.Bind(this, StartListening, StopListening);
    }

    private void OnDisable()
    {
        if (_damageSubscription != null)
        {
            _damageSubscription.Dispose();
            _damageSubscription = null;
        }
    }

    private async void Start()
    {
        if (!_crosshairFeedbackUI)
        {
            _crosshairFeedbackUI = FindFirstObjectByType<CrosshairHitFeedbackUI>(
                FindObjectsInactive.Include);
        }

        if (string.IsNullOrWhiteSpace(_spriteAssetId))
        {
            return;
        }

        try
        {
            _resourceMgr = ResourceMgr.instance;
            TMP_SpriteAsset spriteAsset = await _resourceMgr.LoadAsync<TMP_SpriteAsset>(_spriteAssetId);
            if (!this)
            {
                _resourceMgr.Release<TMP_SpriteAsset>(_spriteAssetId);
                return;
            }

            if (!spriteAsset)
            {
                _resourceMgr.Release<TMP_SpriteAsset>(_spriteAssetId);
                return;
            }

            _hasLoadedSpriteAsset = true;
            if (_killFeedUI)
            {
                _killFeedUI.SetSpriteAsset(spriteAsset);
            }

            if (_scoreFeedbackUI)
            {
                _scoreFeedbackUI.SetSpriteAsset(spriteAsset);
            }
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"击杀图标资源加载失败：{exception.Message}", this);
        }
    }

    private void OnDestroy()
    {
        if (_hasLoadedSpriteAsset && _resourceMgr)
        {
            _resourceMgr.Release<TMP_SpriteAsset>(_spriteAssetId);
            _hasLoadedSpriteAsset = false;
        }
    }

    public void SetLocalPlayer(PlayerInfo playerInfo)
    {
        _localPlayer = playerInfo;
    }

    private void ReceiveDamageResult(DamageResult result)
    {
        if (result == null || !result.WasApplied)
        {
            return;
        }

        if (result.WasKill && _killFeedUI)
        {
            ShowKillFeed(result);
        }

        if (_crosshairFeedbackUI && ShouldShowLocalFeedback(result))
        {
            ShowCrosshairFeedback(result.WasHeadshot);
        }

        if (result.AwardedScore <= 0 ||
            !_scoreFeedbackUI ||
            !ShouldShowLocalFeedback(result))
        {
            return;
        }

        string victimName = result.Victim ? result.Victim.PlayerName : string.Empty;
        _scoreFeedbackUI.Show(new CombatScoreFeedbackData(
            result.AwardedScore,
            result.WasKill,
            victimName,
            result.WasHeadshot));
    }

    private void ShowKillFeed(DamageResult result)
    {
        string killerName = result.Instigator
            ? result.Instigator.PlayerName
            : string.Empty;
        string victimName = result.Victim ? result.Victim.PlayerName : string.Empty;
        _killFeedUI.ShowKill(new KillFeedMessage(
            killerName,
            victimName,
            result.SourceIconId,
            result.WasHeadshot));
    }

    private void ShowCrosshairFeedback(bool isHeadshot)
    {
        if (isHeadshot)
        {
            _crosshairFeedbackUI.ShowHeadshot();
            return;
        }

        _crosshairFeedbackUI.ShowNormalHit();
    }

    private void StartListening()
    {
        CombatDamage.DamageResolved += ReceiveDamageResult;
    }

    private void StopListening()
    {
        CombatDamage.DamageResolved -= ReceiveDamageResult;
    }

    private bool ShouldShowLocalFeedback(DamageResult result)
    {
        if (!result.Instigator)
        {
            return false;
        }

        if (_localPlayer)
        {
            return result.Instigator == _localPlayer;
        }

        return _acceptAnyInstigator;
    }
}
