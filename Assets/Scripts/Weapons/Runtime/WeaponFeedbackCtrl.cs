using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WeaponFeedbackCtrl : MonoBehaviour
{
    [Header("枪械来源")]
    [SerializeField, InspectorName("枪械动作")] private WeaponActionCtrl _actionCtrl;
    [SerializeField, InspectorName("枪械实战")] private WeaponCombatCtrl _combatCtrl;
    [SerializeField, InspectorName("双视角枪械表现")] private PlayerWeaponViewCtrl _weaponViewCtrl;

    [Header("声音")]
    [SerializeField, InspectorName("枪械音源")] private AudioSource _audioSource;

    private readonly List<AudioClip> _fireClips = new List<AudioClip>();
    private readonly List<AudioClip> _weaponEventClips = new List<AudioClip>();
    private readonly List<string> _loadedAudioIds = new List<string>();

    private ResourceMgr _resourceMgr;
    private WeaponDefinitionSO _weaponData;
    private GameObject _firstPersonMuzzleFlash;
    private GameObject _thirdPersonMuzzleFlash;
    private AudioClip _equipClip;
    private AudioClip _unequipClip;
    private int _loadVersion;

    private void Awake()
    {
        if (!_actionCtrl)
        {
            TryGetComponent(out _actionCtrl);
        }
        if (!_combatCtrl)
        {
            TryGetComponent(out _combatCtrl);
        }
        if (!_weaponViewCtrl)
        {
            TryGetComponent(out _weaponViewCtrl);
        }
        if (!_audioSource)
        {
            TryGetComponent(out _audioSource);
        }

        if (!_actionCtrl || !_combatCtrl || !_weaponViewCtrl || !_audioSource)
        {
            Debug.LogError($"{name} 的枪械声音与特效引用未配置完整。", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (!_actionCtrl || !_combatCtrl || !_weaponViewCtrl)
        {
            return;
        }

        _actionCtrl.WeaponChanged += ReceiveWeaponChanged;
        _combatCtrl.ShotFired += ReceiveShotFired;
        _weaponViewCtrl.FirstPersonWeaponReady += ReceiveFirstPersonWeaponReady;
        _weaponViewCtrl.ThirdPersonWeaponReady += ReceiveThirdPersonWeaponReady;
        ReceiveWeaponChanged(_actionCtrl.CurrentWeaponData);
    }

    private void OnDisable()
    {
        if (_actionCtrl)
        {
            _actionCtrl.WeaponChanged -= ReceiveWeaponChanged;
        }
        if (_combatCtrl)
        {
            _combatCtrl.ShotFired -= ReceiveShotFired;
        }
        if (_weaponViewCtrl)
        {
            _weaponViewCtrl.FirstPersonWeaponReady -= ReceiveFirstPersonWeaponReady;
            _weaponViewCtrl.ThirdPersonWeaponReady -= ReceiveThirdPersonWeaponReady;
        }

        _loadVersion++;
        ReleaseFeedbackResources();
    }

    private void ReceiveWeaponChanged(WeaponDefinitionSO weaponData)
    {
        ReleaseMuzzleFlash();
        _weaponData = weaponData;
        _ = LoadAudioAsync(weaponData, ++_loadVersion);
    }

    private async Task LoadAudioAsync(WeaponDefinitionSO weaponData, int loadVersion)
    {
        ReleaseAudio();
        if (!weaponData)
        {
            return;
        }

        _resourceMgr = ResourceMgr.instance;
        IReadOnlyList<string> ids = weaponData.Audio.FireSoundResourceIds;
        await LoadClipListAsync(ids, _fireClips, loadVersion);
        if (loadVersion != _loadVersion || !this)
        {
            return;
        }

        await LoadClipListAsync(
            weaponData.Audio.WeaponEventSoundResourceIds,
            _weaponEventClips,
            loadVersion);
        _equipClip = await LoadClipAsync(
            weaponData.Audio.EquipSoundResourceId,
            loadVersion);
        _unequipClip = await LoadClipAsync(
            weaponData.Audio.UnequipSoundResourceId,
            loadVersion);
    }

    private async Task LoadClipListAsync(
        IReadOnlyList<string> resourceIds,
        List<AudioClip> target,
        int loadVersion)
    {
        for (int i = 0; i < resourceIds.Count; i++)
        {
            AudioClip clip = await LoadClipAsync(resourceIds[i], loadVersion);
            if (loadVersion != _loadVersion || !this)
            {
                return;
            }
            if (clip)
            {
                target.Add(clip);
            }
        }
    }

    private async Task<AudioClip> LoadClipAsync(string resourceId, int loadVersion)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
        {
            return null;
        }

        AudioClip clip = await _resourceMgr.LoadAsync<AudioClip>(resourceId);
        if (loadVersion != _loadVersion || !this)
        {
            if (clip)
            {
                _resourceMgr.Release<AudioClip>(resourceId);
            }
            return null;
        }
        if (clip)
        {
            _loadedAudioIds.Add(resourceId);
        }
        return clip;
    }

    private void ReceiveFirstPersonWeaponReady(
        WeaponDefinitionSO weaponData,
        Transform muzzlePoint)
    {
        if (!weaponData || weaponData != _weaponData || !muzzlePoint)
        {
            return;
        }

        _ = LoadMuzzleFlashAsync(
            weaponData.Effects.MuzzleFlashResourceId,
            muzzlePoint,
            _loadVersion,
            false);
    }

    private void ReceiveThirdPersonWeaponReady(
        WeaponDefinitionSO weaponData,
        Transform muzzlePoint)
    {
        if (!weaponData || weaponData != _weaponData || !muzzlePoint)
        {
            return;
        }

        _ = LoadMuzzleFlashAsync(
            weaponData.Effects.MuzzleFlashResourceId,
            muzzlePoint,
            _loadVersion,
            true);
    }

    private async Task LoadMuzzleFlashAsync(
        string resourceId,
        Transform muzzlePoint,
        int loadVersion,
        bool thirdPerson)
    {
        if (string.IsNullOrWhiteSpace(resourceId) || !muzzlePoint)
        {
            return;
        }

        _resourceMgr = ResourceMgr.instance;
        GameObject instance = await _resourceMgr.InstantiateAsync(resourceId, muzzlePoint);
        if (!instance || loadVersion != _loadVersion || !this)
        {
            if (instance)
            {
                _resourceMgr.ReleaseInstance(instance);
            }
            return;
        }

        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        SetLayerRecursively(instance.transform, thirdPerson ? 8 : 9);
        if (thirdPerson)
        {
            ReleaseInstance(ref _thirdPersonMuzzleFlash);
            _thirdPersonMuzzleFlash = instance;
        }
        else
        {
            ReleaseInstance(ref _firstPersonMuzzleFlash);
            _firstPersonMuzzleFlash = instance;
        }
        instance.SetActive(false);
    }

    private void ReceiveShotFired(WeaponDefinitionSO weaponData)
    {
        if (!weaponData || weaponData != _weaponData)
        {
            return;
        }

        PlayFireSound(weaponData.Audio);
        PlayMuzzleFlash();
    }

    private void PlayFireSound(WeaponAudioData audioData)
    {
        if (!_audioSource || _fireClips.Count == 0)
        {
            return;
        }

        Vector2 pitchRange = audioData.FirePitchRange;
        Vector2 volumeRange = audioData.FireVolumeRange;
        _audioSource.pitch = UnityEngine.Random.Range(pitchRange.x, pitchRange.y);
        float volume = UnityEngine.Random.Range(volumeRange.x, volumeRange.y);
        AudioClip clip = _fireClips[UnityEngine.Random.Range(0, _fireClips.Count)];
        _audioSource.PlayOneShot(clip, volume);
    }

    private void PlayMuzzleFlash()
    {
        PlayMuzzleFlash(_firstPersonMuzzleFlash);
        PlayMuzzleFlash(_thirdPersonMuzzleFlash);
    }

    private static void PlayMuzzleFlash(GameObject flash)
    {
        if (!flash)
        {
            return;
        }

        flash.SetActive(true);
        ParticleSystem[] particles = flash.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
        {
            particles[i].Play(true);
        }
    }

    public void PlayEquipSound()
    {
        PlayOneShot(_equipClip);
    }

    public void PlayUnEquipSound()
    {
        PlayOneShot(_unequipClip);
    }

    public void PlayWeaponSound(int clipIndex)
    {
        if (clipIndex < 0 || clipIndex >= _weaponEventClips.Count)
        {
            return;
        }

        PlayOneShot(_weaponEventClips[clipIndex]);
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (_audioSource && clip)
        {
            _audioSource.PlayOneShot(clip);
        }
    }

    private void ReleaseFeedbackResources()
    {
        ReleaseAudio();
        ReleaseMuzzleFlash();
        _weaponData = null;
    }

    private void ReleaseAudio()
    {
        if (_resourceMgr)
        {
            for (int i = 0; i < _loadedAudioIds.Count; i++)
            {
                _resourceMgr.Release<AudioClip>(_loadedAudioIds[i]);
            }
        }

        _loadedAudioIds.Clear();
        _fireClips.Clear();
        _weaponEventClips.Clear();
        _equipClip = null;
        _unequipClip = null;
    }

    private void ReleaseMuzzleFlash()
    {
        ReleaseInstance(ref _firstPersonMuzzleFlash);
        ReleaseInstance(ref _thirdPersonMuzzleFlash);
    }

    private void ReleaseInstance(ref GameObject instance)
    {
        if (_resourceMgr && instance)
        {
            _resourceMgr.ReleaseInstance(instance);
        }
        instance = null;
    }

    private static void SetLayerRecursively(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        for (int index = 0; index < root.childCount; index++)
        {
            SetLayerRecursively(root.GetChild(index), layer);
        }
    }
}
