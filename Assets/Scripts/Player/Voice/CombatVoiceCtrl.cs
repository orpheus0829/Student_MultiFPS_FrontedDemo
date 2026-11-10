using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class CombatVoiceCtrl : MonoBehaviour
{
    [Header("角色语音")]
    [SerializeField, ResourceId(typeof(CombatVoiceSO)), InspectorName("语音盒资源 ID")]
    private string _voiceResourceId = "character.anbi.voice";
    [SerializeField, InspectorName("语音播放组件（可留空自动创建）")]
    private AudioSource _audioSource;

    private readonly Queue<CombatVoiceLine> _audioQueue = new Queue<CombatVoiceLine>();
    private static CombatVoiceCtrl _localListener;
    private PlayerInfo _playerInfo;
    private PlayerInputCtrl _inputCtrl;
    private PlayerLifeCtrl _lifeCtrl;
    private WeaponActionCtrl _weaponActionCtrl;
    private CombatVoiceSO _voiceData;
    private ResourceMgr _resources;
    private string _loadedVoiceResourceId;
    private string _playingAudioResourceId;
    private bool _isLoadingAudio;
    private int _loadVersion;
    private int _killStreak;
    private float _lastKillTime;
    private float _nextAutomaticVoiceTime;

    public string VoiceResourceId { get { return _voiceResourceId; } }

    private void Awake()
    {
        TryGetComponent(out _playerInfo);
        TryGetComponent(out _inputCtrl);
        TryGetComponent(out _lifeCtrl);
        TryGetComponent(out _weaponActionCtrl);
        if (!_audioSource)
        {
            GameObject voiceObject = new GameObject("战斗语音音源");
            voiceObject.transform.SetParent(transform, false);
            _audioSource = voiceObject.AddComponent<AudioSource>();
        }
        _audioSource.playOnAwake = false;
        _audioSource.spatialBlend = 1f;
        _audioSource.rolloffMode = AudioRolloffMode.Linear;
        _audioSource.minDistance = 2f;
    }

    private void OnEnable()
    {
        CombatDamage.DamageResolved += HandleDamageResolved;
        if (_weaponActionCtrl)
        {
            _weaponActionCtrl.ActionStarted += HandleActionStarted;
        }
        if (_lifeCtrl)
        {
            _lifeCtrl.Respawned += HandleRespawned;
        }
    }

    private void Start()
    {
        if (_inputCtrl && _inputCtrl.enabled)
        {
            _localListener = this;
        }
        _ = SetVoiceProfileAsync(_voiceResourceId);
    }

    private void Update()
    {
        if (_isLoadingAudio || !_audioSource || _audioSource.isPlaying)
        {
            return;
        }

        ReleasePlayingAudio();
        PlayNextAudio();
    }

    private void OnDisable()
    {
        CombatDamage.DamageResolved -= HandleDamageResolved;
        if (_weaponActionCtrl)
        {
            _weaponActionCtrl.ActionStarted -= HandleActionStarted;
        }
        if (_lifeCtrl)
        {
            _lifeCtrl.Respawned -= HandleRespawned;
        }
        StopVoice();
    }

    private void OnDestroy()
    {
        _loadVersion++;
        if (_localListener == this)
        {
            _localListener = null;
        }
        ReleaseVoiceProfile();
    }

    public async Task<bool> SetVoiceProfileAsync(string voiceResourceId)
    {
        int version = ++_loadVersion;
        if (string.IsNullOrWhiteSpace(voiceResourceId))
        {
            StopVoice();
            ReleaseVoiceProfile();
            _voiceResourceId = string.Empty;
            return true;
        }

        ResourceMgr resources = ResourceMgr.instance;
        CombatVoiceSO loaded;
        try
        {
            loaded = await resources.LoadAsync<CombatVoiceSO>(voiceResourceId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{name} 读取角色语音盒 {voiceResourceId} 失败：{exception.Message}", this);
            return false;
        }

        if (!this || version != _loadVersion)
        {
            resources.Release<CombatVoiceSO>(voiceResourceId);
            return false;
        }
        if (!loaded)
        {
            resources.Release<CombatVoiceSO>(voiceResourceId);
            return false;
        }

        StopVoice();
        ReleaseVoiceProfile();
        _resources = resources;
        _voiceData = loaded;
        _voiceResourceId = voiceResourceId;
        _loadedVoiceResourceId = voiceResourceId;
        _audioSource.maxDistance = Mathf.Max(2f, loaded.HearingDistance);
        TrySpeak(CombatVoiceCue.Respawn);
        return true;
    }

    public void SetVoiceProfile(CombatVoiceSO profile)
    {
        _loadVersion++;
        StopVoice();
        ReleaseVoiceProfile();
        _voiceData = profile;
        _voiceResourceId = string.Empty;
        if (_audioSource && profile)
        {
            _audioSource.maxDistance = Mathf.Max(2f, profile.HearingDistance);
        }
    }

    public void TrySpeak(CombatVoiceCue cue)
    {
        if (!_voiceData || Time.time < _nextAutomaticVoiceTime ||
            _isLoadingAudio || (_audioSource && _audioSource.isPlaying) ||
            !_voiceData.TryPickLine(cue, out CombatVoiceLine line))
        {
            return;
        }

        _nextAutomaticVoiceTime = Time.time + _voiceData.AutomaticCooldown;
        PlayLine(line);
    }

    public void Speak(CombatVoiceCue cue)
    {
        if (!_voiceData || !_voiceData.TryGetLine(cue, out CombatVoiceLine line))
        {
            return;
        }

        PlayLine(line);
    }

    private void PlayLine(CombatVoiceLine line)
    {

        if (string.IsNullOrWhiteSpace(line.AudioResourceId))
        {
            PublishSubtitle(line.Subtitle, _voiceData.TextDuration);
            return;
        }

        _audioQueue.Enqueue(line);
        if (!_isLoadingAudio && (!_audioSource || !_audioSource.isPlaying))
        {
            PlayNextAudio();
        }
    }

    private void HandleActionStarted(WeaponActionDefinition action, bool startedWhileAiming)
    {
        if (action == null)
        {
            return;
        }
        if (action.ActionType == WeaponActionType.Reload ||
            action.ActionType == WeaponActionType.EmptyReload ||
            action.ActionType == WeaponActionType.PerRoundStart)
        {
            TrySpeak(CombatVoiceCue.Reload);
        }
    }

    private void HandleDamageResolved(DamageResult result)
    {
        if (result == null || !result.WasApplied)
        {
            return;
        }
        if (result.Victim == _playerInfo)
        {
            if (result.WasKill)
            {
                _killStreak = 0;
                StopVoice();
            }
            else
            {
                TrySpeak(CombatVoiceCue.Hurt);
            }
        }
        if (!_voiceData || !result.WasKill || result.Instigator != _playerInfo ||
            result.Victim == _playerInfo)
        {
            return;
        }

        _killStreak = Time.time - _lastKillTime <= _voiceData.StreakWindow
            ? _killStreak + 1 : 1;
        _lastKillTime = Time.time;
        if (_killStreak == 3)
        {
            TrySpeak(CombatVoiceCue.TripleKill);
        }
        else
        {
            TrySpeak(CombatVoiceCue.Kill);
        }
    }

    private void HandleRespawned()
    {
        _killStreak = 0;
        _nextAutomaticVoiceTime = 0f;
        TrySpeak(CombatVoiceCue.Respawn);
    }

    private async void PlayNextAudio()
    {
        if (_isLoadingAudio || _audioQueue.Count == 0)
        {
            return;
        }
        ReleasePlayingAudio();
        CombatVoiceLine line = _audioQueue.Dequeue();
        string resourceId = line.AudioResourceId;
        int version = _loadVersion;
        _isLoadingAudio = true;
        AudioClip clip = null;
        try
        {
            _resources = ResourceMgr.instance;
            clip = await _resources.LoadAsync<AudioClip>(resourceId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"{name} 读取战斗语音 {resourceId} 失败：{exception.Message}", this);
        }
        if (!this || version != _loadVersion)
        {
            _resources?.Release<AudioClip>(resourceId);
            return;
        }
        _isLoadingAudio = false;
        if (!clip)
        {
            _resources?.Release<AudioClip>(resourceId);
            PublishSubtitle(line.Subtitle, _voiceData ? _voiceData.TextDuration : 2.2f);
            PlayNextAudio();
            return;
        }

        _playingAudioResourceId = resourceId;
        _audioSource.clip = clip;
        _audioSource.Play();
        PublishSubtitle(line.Subtitle, clip.length);
    }

    private void PublishSubtitle(string subtitle, float duration)
    {
        if (!_playerInfo || !_localListener || !_localListener._playerInfo)
        {
            return;
        }
        bool isLocal = _localListener == this;
        if (!isLocal && Vector3.Distance(transform.position,
            _localListener.transform.position) > _voiceData.HearingDistance)
        {
            return;
        }
        CombatVoiceEvents.RaiseLineStarted(_playerInfo, subtitle,
            Mathf.Max(0.1f, duration), isLocal);
    }

    private void StopVoice()
    {
        _loadVersion++;
        _audioQueue.Clear();
        if (_audioSource)
        {
            _audioSource.Stop();
            _audioSource.clip = null;
        }
        ReleasePlayingAudio();
        _isLoadingAudio = false;
    }

    private void ReleasePlayingAudio()
    {
        if (string.IsNullOrWhiteSpace(_playingAudioResourceId))
        {
            return;
        }
        if (_audioSource)
        {
            _audioSource.clip = null;
        }
        _resources?.Release<AudioClip>(_playingAudioResourceId);
        _playingAudioResourceId = string.Empty;
    }

    private void ReleaseVoiceProfile()
    {
        if (!string.IsNullOrWhiteSpace(_loadedVoiceResourceId))
        {
            _resources?.Release<CombatVoiceSO>(_loadedVoiceResourceId);
        }
        _loadedVoiceResourceId = string.Empty;
        _voiceData = null;
    }
}
