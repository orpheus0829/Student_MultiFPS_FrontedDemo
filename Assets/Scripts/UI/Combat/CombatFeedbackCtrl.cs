using System;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatFeedbackCtrl : MonoBehaviour
{
    private const string FeedbackAudioDataId = "audio.combat-feedback.data";

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

    private AudioSource _feedbackAudioSource;
    private AudioMgr _audioMgr;
    private CombatFeedbackAudioSO _feedbackAudioData;
    private AudioClip _hitAudioClip;
    private AudioClip _killAudioClip;
    private AudioClip _headshotKillAudioClip;
    private bool _hasLoadedSpriteAsset;
    private bool _hasLoadedFeedbackAudioData;
    private bool _hasLoadedHitAudio;
    private bool _hasLoadedKillAudio;
    private bool _hasLoadedHeadshotKillAudio;
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

        _feedbackAudioSource = gameObject.AddComponent<AudioSource>();
        _feedbackAudioSource.playOnAwake = false;
        _feedbackAudioSource.loop = false;
        _feedbackAudioSource.spatialBlend = 0f;
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

    private void Start()
    {
        if (!_crosshairFeedbackUI)
        {
            _crosshairFeedbackUI = FindFirstObjectByType<CrosshairHitFeedbackUI>(
                FindObjectsInactive.Include);
        }

        _resourceMgr = ResourceMgr.instance;
        _audioMgr = AudioMgr.instance;
        _ = LoadSpriteAssetAsync();
        _ = LoadFeedbackAudioAsync();
    }

    private void OnDestroy()
    {
        if (_audioMgr && _feedbackAudioSource)
        {
            _audioMgr.UnregisterSource(_feedbackAudioSource);
        }

        ReleaseFeedbackAudio();
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

    private async Task LoadSpriteAssetAsync()
    {
        if (string.IsNullOrWhiteSpace(_spriteAssetId))
        {
            return;
        }

        try
        {
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

    private async Task LoadFeedbackAudioAsync()
    {
        try
        {
            CombatFeedbackAudioSO audioData =
                await _resourceMgr.LoadAsync<CombatFeedbackAudioSO>(FeedbackAudioDataId);
            if (!this)
            {
                _resourceMgr.Release<CombatFeedbackAudioSO>(FeedbackAudioDataId);
                return;
            }

            if (!audioData)
            {
                _resourceMgr.Release<CombatFeedbackAudioSO>(FeedbackAudioDataId);
                return;
            }

            _feedbackAudioData = audioData;
            _hasLoadedFeedbackAudioData = true;
            _hitAudioClip = await LoadFeedbackClipAsync(audioData.HitResourceId);
            _hasLoadedHitAudio = _hitAudioClip;
            _killAudioClip = await LoadFeedbackClipAsync(audioData.KillResourceId);
            _hasLoadedKillAudio = _killAudioClip;
            _headshotKillAudioClip =
                await LoadFeedbackClipAsync(audioData.HeadshotKillResourceId);
            _hasLoadedHeadshotKillAudio = _headshotKillAudioClip;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"战斗反馈音频加载失败：{exception.Message}", this);
        }
    }

    private async Task<AudioClip> LoadFeedbackClipAsync(string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        AudioClip clip = await _resourceMgr.LoadAsync<AudioClip>(resourceId);
        if (!this || !clip)
        {
            _resourceMgr.Release<AudioClip>(resourceId);
            return null;
        }

        return clip;
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

        if (ShouldShowLocalFeedback(result))
        {
            PlayCombatFeedback(result);
        }

        if ((result.AwardedScore <= 0 && !result.WasKill) ||
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
            result.WasHeadshot,
            result.SourceName));
    }

    private void ShowKillFeed(DamageResult result)
    {
        string killerName = result.Instigator && result.Instigator != result.Victim
            ? result.Instigator.PlayerName
            : string.Empty;
        string victimName = result.Victim ? result.Victim.PlayerName : string.Empty;
        _killFeedUI.ShowKill(new KillFeedMessage(
            killerName,
            victimName,
            result.SourceIconId,
            result.WasHeadshot,
            ShouldShowLocalFeedback(result)
                ? KillFeedKind.LocalKill
                : KillFeedKind.Neutral));
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

    private void PlayCombatFeedback(DamageResult result)
    {
        if (!_feedbackAudioData || !_feedbackAudioSource || !_audioMgr)
        {
            return;
        }

        AudioClip clip;
        float volume;
        Vector2 pitchRange;
        if (result.WasKill && result.WasHeadshot)
        {
            clip = _headshotKillAudioClip;
            volume = _feedbackAudioData.HeadshotKillVolume;
            pitchRange = _feedbackAudioData.HeadshotKillPitchRange;
        }
        else if (result.WasKill)
        {
            clip = _killAudioClip;
            volume = _feedbackAudioData.KillVolume;
            pitchRange = _feedbackAudioData.KillPitchRange;
        }
        else
        {
            clip = _hitAudioClip;
            volume = _feedbackAudioData.HitVolume;
            pitchRange = _feedbackAudioData.HitPitchRange;
        }

        if (!clip)
        {
            return;
        }

        if (result.WasKill)
        {
            // 击杀反馈必须取代尚未播完的命中反馈，避免爆头击杀听成两层叠音。
            _feedbackAudioSource.Stop();
        }

        _feedbackAudioSource.pitch = UnityEngine.Random.Range(
            Mathf.Min(pitchRange.x, pitchRange.y),
            Mathf.Max(pitchRange.x, pitchRange.y));
        _audioMgr.PlayOneShot(
            _feedbackAudioSource,
            clip,
            GJAudioBus.FeedbackSfx,
            volume);
    }

    private void ReleaseFeedbackAudio()
    {
        if (!_resourceMgr)
        {
            return;
        }

        if (_hasLoadedHitAudio && _feedbackAudioData)
        {
            _resourceMgr.Release<AudioClip>(_feedbackAudioData.HitResourceId);
        }
        if (_hasLoadedKillAudio && _feedbackAudioData)
        {
            _resourceMgr.Release<AudioClip>(_feedbackAudioData.KillResourceId);
        }
        if (_hasLoadedHeadshotKillAudio && _feedbackAudioData)
        {
            _resourceMgr.Release<AudioClip>(_feedbackAudioData.HeadshotKillResourceId);
        }
        if (_hasLoadedFeedbackAudioData)
        {
            _resourceMgr.Release<CombatFeedbackAudioSO>(FeedbackAudioDataId);
        }

        _hasLoadedHitAudio = false;
        _hasLoadedKillAudio = false;
        _hasLoadedHeadshotKillAudio = false;
        _hasLoadedFeedbackAudioData = false;
        _hitAudioClip = null;
        _killAudioClip = null;
        _headshotKillAudioClip = null;
        _feedbackAudioData = null;
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
