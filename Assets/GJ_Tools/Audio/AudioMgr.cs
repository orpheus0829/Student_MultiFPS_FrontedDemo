using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public sealed class AudioMgr : BaseMgr<AudioMgr>
{
    private const string SettingsResourceId = "audio.settings";
    private const string PrefsPrefix = "GunDemo.Audio.";

    private sealed class SourceBinding
    {
        public GJAudioBus Bus;
        public float BaseVolume;
    }

    private readonly Dictionary<AudioSource, SourceBinding> _sources =
        new Dictionary<AudioSource, SourceBinding>();
    private readonly float[] _volumes = { 1f, 0.8f, 1f, 1f, 1f, 1f };
    private ResourceMgr _resources;
    private AudioSettingsSO _settings;
    private AudioSource _musicSource;
    private string _musicResourceId;
    private int _musicRequestVersion;
    private bool _loadedSettings;
    private bool _isDestroyed;
    private bool _hasUnsavedVolumes;

    public event Action<GJAudioVolumeChannel, float> VolumeChanged;

    protected override bool Ddol
    {
        get { return true; }
    }

    protected override void Awake()
    {
        base.Awake();
        if (instance != this)
        {
            return;
        }

        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.playOnAwake = false;
        _musicSource.spatialBlend = 0f;
        _ = LoadSettingsAsync();
    }

    private void OnApplicationPause(bool isPaused)
    {
        if (isPaused)
        {
            SaveVolumes();
        }
    }

    private void OnApplicationQuit()
    {
        SaveVolumes();
    }

    private void OnDestroy()
    {
        _isDestroyed = true;
        _musicRequestVersion++;
        SaveVolumes();
        ReleaseMusic();
        if (_loadedSettings && _resources)
        {
            _resources.Release<AudioSettingsSO>(SettingsResourceId);
        }
        _sources.Clear();
    }

    public float GetVolume(GJAudioVolumeChannel channel)
    {
        return _volumes[(int)channel];
    }

    public void SetVolume(GJAudioVolumeChannel channel, float value)
    {
        value = Mathf.Clamp01(value);
        int index = (int)channel;
        if (Mathf.Approximately(_volumes[index], value))
        {
            return;
        }

        _volumes[index] = value;
        PlayerPrefs.SetFloat(PrefsPrefix + channel, value);
        _hasUnsavedVolumes = true;
        RefreshSources();
        VolumeChanged?.Invoke(channel, value);
    }

    public void SaveVolumes()
    {
        if (!_hasUnsavedVolumes)
        {
            return;
        }

        PlayerPrefs.Save();
        _hasUnsavedVolumes = false;
    }

    public float GetEffectiveVolume(GJAudioBus bus)
    {
        float master = GetVolume(GJAudioVolumeChannel.Master);
        if (bus == GJAudioBus.Music)
        {
            return master * GetVolume(GJAudioVolumeChannel.Music);
        }

        return master * GetVolume(GJAudioVolumeChannel.Sfx) * GetVolume(GetChannel(bus));
    }

    public void Play(
        AudioSource source,
        AudioClip clip,
        GJAudioBus bus,
        float baseVolume = 1f,
        bool loop = false)
    {
        if (!source || !clip)
        {
            return;
        }

        BindSource(source, bus, baseVolume);
        source.clip = clip;
        source.loop = loop;
        source.Play();
    }

    public void PlayOneShot(
        AudioSource source,
        AudioClip clip,
        GJAudioBus bus,
        float baseVolume = 1f)
    {
        if (!source || !clip)
        {
            return;
        }

        BindSource(source, bus, 1f);
        source.PlayOneShot(clip, Mathf.Max(0f, baseVolume));
    }

    public async Task PlayMusicAsync(
        string resourceId,
        bool loop = true,
        float volume = 1f)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            StopMusic();
            return;
        }

        int requestVersion = ++_musicRequestVersion;
        float baseVolume = Mathf.Max(0f, volume);
        if (_musicResourceId == resourceId && _musicSource && _musicSource.clip)
        {
            BindSource(_musicSource, GJAudioBus.Music, baseVolume);
            _musicSource.loop = loop;
            _musicSource.Play();
            return;
        }

        ResourceMgr resources = ResourceMgr.instance;
        AudioClip clip;
        try
        {
            clip = await resources.LoadAsync<AudioClip>(resourceId);
        }
        catch (Exception exception)
        {
            if (!_isDestroyed && requestVersion == _musicRequestVersion)
            {
                Debug.LogWarning($"读取音乐失败 [{resourceId}]：{exception.Message}", this);
            }
            return;
        }

        if (!clip)
        {
            if (resources)
            {
                resources.Release<AudioClip>(resourceId);
            }
            return;
        }

        if (_isDestroyed || !this || requestVersion != _musicRequestVersion)
        {
            if (resources)
            {
                resources.Release<AudioClip>(resourceId);
            }
            return;
        }

        ReleaseMusic();
        _resources = resources;
        _musicResourceId = resourceId;
        BindSource(_musicSource, GJAudioBus.Music, baseVolume);
        _musicSource.clip = clip;
        _musicSource.loop = loop;
        _musicSource.Play();
    }

    public void StopMusic()
    {
        _musicRequestVersion++;
        ReleaseMusic();
    }

    public void Stop(AudioSource source, bool clearClip = true)
    {
        if (!source)
        {
            return;
        }

        source.Stop();
        if (clearClip)
        {
            source.clip = null;
        }
    }

    public void UnregisterSource(AudioSource source)
    {
        if (source)
        {
            _sources.Remove(source);
        }
    }

    private async Task LoadSettingsAsync()
    {
        _resources = ResourceMgr.instance;
        AudioSettingsSO settings = null;
        try
        {
            settings = await _resources.LoadAsync<AudioSettingsSO>(SettingsResourceId);
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"读取音量设置失败：{exception.Message}", this);
        }

        if (!this || !settings)
        {
            if (settings && _resources)
            {
                _resources.Release<AudioSettingsSO>(SettingsResourceId);
            }
            LoadSavedVolumes(null);
            return;
        }

        _settings = settings;
        _loadedSettings = true;
        LoadSavedVolumes(settings);
    }

    private void LoadSavedVolumes(AudioSettingsSO settings)
    {
        SetInitialVolume(GJAudioVolumeChannel.Master, settings ? settings.Master : 1f);
        SetInitialVolume(GJAudioVolumeChannel.Music, settings ? settings.Music : 0.8f);
        SetInitialVolume(GJAudioVolumeChannel.Sfx, settings ? settings.Sfx : 1f);
        SetInitialVolume(GJAudioVolumeChannel.CharacterVoice,
            settings ? settings.CharacterVoice : 1f);
        SetInitialVolume(GJAudioVolumeChannel.CombatSfx,
            settings ? settings.CombatSfx : 1f);
        SetInitialVolume(GJAudioVolumeChannel.FeedbackSfx,
            settings ? settings.FeedbackSfx : 1f);
        RefreshSources();
    }

    private void SetInitialVolume(GJAudioVolumeChannel channel, float fallback)
    {
        _volumes[(int)channel] = Mathf.Clamp01(
            PlayerPrefs.GetFloat(PrefsPrefix + channel, fallback));
    }

    private void BindSource(AudioSource source, GJAudioBus bus, float baseVolume)
    {
        if (!_sources.TryGetValue(source, out SourceBinding binding))
        {
            binding = new SourceBinding();
            _sources.Add(source, binding);
        }

        binding.Bus = bus;
        binding.BaseVolume = Mathf.Max(0f, baseVolume);
        source.volume = binding.BaseVolume * GetEffectiveVolume(bus);
    }

    private void ReleaseMusic()
    {
        if (_musicSource)
        {
            _musicSource.Stop();
            _musicSource.clip = null;
        }

        if (!string.IsNullOrWhiteSpace(_musicResourceId) && _resources)
        {
            _resources.Release<AudioClip>(_musicResourceId);
        }
        _musicResourceId = null;
    }

    private void RefreshSources()
    {
        List<AudioSource> stale = null;
        foreach (KeyValuePair<AudioSource, SourceBinding> entry in _sources)
        {
            if (!entry.Key)
            {
                if (stale == null)
                {
                    stale = new List<AudioSource>();
                }
                stale.Add(entry.Key);
                continue;
            }

            entry.Key.volume = entry.Value.BaseVolume * GetEffectiveVolume(entry.Value.Bus);
        }

        if (stale == null)
        {
            return;
        }
        for (int i = 0; i < stale.Count; i++)
        {
            _sources.Remove(stale[i]);
        }
    }

    private static GJAudioVolumeChannel GetChannel(GJAudioBus bus)
    {
        switch (bus)
        {
            case GJAudioBus.CharacterVoice:
                return GJAudioVolumeChannel.CharacterVoice;
            case GJAudioBus.FeedbackSfx:
                return GJAudioVolumeChannel.FeedbackSfx;
            default:
                return GJAudioVolumeChannel.CombatSfx;
        }
    }
}
