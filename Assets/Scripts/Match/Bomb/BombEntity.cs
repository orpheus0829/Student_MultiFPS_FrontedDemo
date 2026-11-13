using System;
using UnityEngine;

public enum BombEntityState
{
    OnGround = 0,
    Carried = 1,
    Planted = 2,
    Defused = 3,
    Exploded = 4
}

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public sealed class BombEntity : MonoBehaviour
{
    private const float MinimumPulsePhase = -Mathf.PI * 0.5f;

    [Header("炸弹")]
    [SerializeField, Min(0.1f), InspectorName("拾取半径")] private float _pickupRadius = 1.8f;
    [SerializeField, InspectorName("携带偏移")] private Vector3 _carrierOffset = new Vector3(0f, 0.2f, 0f);

    [Header("安装指示灯")]
    [SerializeField, InspectorName("红色点光源")] private Light _armedLight;
    [SerializeField, Min(0f), InspectorName("最低亮度")] private float _minimumLightIntensity = 0.12f;
    [SerializeField, Min(0f), InspectorName("最高亮度")] private float _maximumLightIntensity = 4.5f;
    [SerializeField, Min(0.01f), InspectorName("常规闪烁频率")] private float _normalPulseFrequency = 0.65f;
    [SerializeField, Min(0.01f), InspectorName("最后十秒闪烁频率")] private float _urgentPulseFrequency = 5f;

    private Renderer[] _renderers;
    private Collider[] _colliders;
    private CharacterHealth _carrierHealth;
    private BombEntityState _state = BombEntityState.OnGround;
    private PlayerInfo _carrier;
    private BombSite _plantedSite;
    private float _fuseRemaining;
    private float _lightPulsePhase;
    private bool _fuseRunning;

    public BombEntityState State { get { return _state; } }
    public PlayerInfo Carrier { get { return _carrier; } }
    public BombSite PlantedSite { get { return _plantedSite; } }
    public float FuseRemaining { get { return _fuseRemaining; } }
    public float PickupRadius { get { return _pickupRadius; } }

    public event Action<BombEntity, PlayerInfo> PickedUp;
    public event Action<BombEntity, PlayerInfo> Dropped;
    public event Action<BombEntity, BombSite> Planted;
    public event Action<BombEntity, PlayerInfo> Defused;
    public event Action<BombEntity> Exploded;
    public event Action<BombEntity, float> FuseChanged;

    private void Awake()
    {
        if (!_armedLight)
        {
            _armedLight = GetComponentInChildren<Light>(true);
        }

        _renderers = GetComponentsInChildren<Renderer>(true);
        _colliders = GetComponentsInChildren<Collider>(true);
        SetArmedLightActive(false);
        ApplyWorldVisibility(true);
    }

    private void Update()
    {
        if (_state == BombEntityState.Carried && _carrier)
        {
            transform.position = _carrier.transform.TransformPoint(_carrierOffset);
        }

        if (_state != BombEntityState.Planted || !_fuseRunning)
        {
            return;
        }

        _fuseRemaining = Mathf.Max(0f, _fuseRemaining - Time.deltaTime);
        UpdateArmedLight();
        FuseChanged?.Invoke(this, _fuseRemaining);
        if (_fuseRemaining <= 0f)
        {
            TryExplode();
        }
    }

    private void OnDisable()
    {
        SetArmedLightActive(false);
        UnsubscribeCarrier();
    }

    public bool TryPickUp(PlayerInfo carrier)
    {
        if (_state != BombEntityState.OnGround || !carrier)
        {
            return false;
        }

        _carrier = carrier;
        _state = BombEntityState.Carried;
        SetArmedLightActive(false);
        transform.SetParent(carrier.transform, true);
        transform.localPosition = _carrierOffset;
        SubscribeCarrier(carrier);
        ApplyWorldVisibility(false);
        PickedUp?.Invoke(this, carrier);
        return true;
    }

    public bool TryDrop(Vector3 worldPosition)
    {
        if (_state != BombEntityState.Carried)
        {
            return false;
        }

        PlayerInfo previousCarrier = _carrier;
        UnsubscribeCarrier();
        _carrier = null;
        _state = BombEntityState.OnGround;
        SetArmedLightActive(false);
        transform.SetParent(null, true);
        transform.position = worldPosition;
        ApplyWorldVisibility(true);
        Dropped?.Invoke(this, previousCarrier);
        return true;
    }

    public bool TryPlant(PlayerInfo planter, BombSite site, Vector3 worldPosition, float fuseDuration)
    {
        if (_state != BombEntityState.Carried || _carrier != planter || !site)
        {
            return false;
        }

        UnsubscribeCarrier();
        _carrier = null;
        _plantedSite = site;
        _state = BombEntityState.Planted;
        _fuseRemaining = Mathf.Max(0.1f, fuseDuration);
        _fuseRunning = true;
        _lightPulsePhase = MinimumPulsePhase;
        transform.SetParent(null, true);
        transform.position = worldPosition;
        ApplyWorldVisibility(true);
        SetArmedLightActive(true);
        Planted?.Invoke(this, site);
        FuseChanged?.Invoke(this, _fuseRemaining);
        return true;
    }

    public bool TryDefuse(PlayerInfo defuser)
    {
        if (_state != BombEntityState.Planted || !defuser)
        {
            return false;
        }

        _fuseRunning = false;
        _state = BombEntityState.Defused;
        SetArmedLightActive(false);
        Defused?.Invoke(this, defuser);
        return true;
    }

    public bool TryExplode()
    {
        if (_state != BombEntityState.Planted)
        {
            return false;
        }

        _fuseRunning = false;
        _fuseRemaining = 0f;
        _state = BombEntityState.Exploded;
        SetArmedLightActive(false);
        Exploded?.Invoke(this);
        return true;
    }

    public void PauseFuse(bool paused)
    {
        _fuseRunning = _state == BombEntityState.Planted && !paused;
        SetArmedLightActive(_fuseRunning);
    }

    public void ConfigureArmedLight(Light armedLight)
    {
        _armedLight = armedLight;
        SetArmedLightActive(false);
    }

    public void ConfigurePickupRadius(float radius)
    {
        _pickupRadius = Mathf.Max(0.1f, radius);
    }

    private void SubscribeCarrier(PlayerInfo carrier)
    {
        _carrierHealth = carrier.GetComponent<CharacterHealth>();
        if (_carrierHealth)
        {
            _carrierHealth.Died += HandleCarrierDied;
        }
    }

    private void UnsubscribeCarrier()
    {
        if (_carrierHealth)
        {
            _carrierHealth.Died -= HandleCarrierDied;
        }
        _carrierHealth = null;
    }

    private void HandleCarrierDied()
    {
        Vector3 dropPosition = _carrier ? _carrier.transform.position : transform.position;
        TryDrop(dropPosition);
    }

    private void UpdateArmedLight()
    {
        if (!_armedLight)
        {
            return;
        }

        float frequency = _normalPulseFrequency;
        if (_fuseRemaining <= 10f)
        {
            float urgency = 1f - Mathf.Clamp01(_fuseRemaining / 10f);
            frequency = Mathf.Lerp(
                _normalPulseFrequency,
                _urgentPulseFrequency,
                urgency * urgency);
        }

        _lightPulsePhase += Time.deltaTime * frequency * Mathf.PI * 2f;
        float pulse = (Mathf.Sin(_lightPulsePhase) + 1f) * 0.5f;
        _armedLight.intensity = Mathf.Lerp(
            _minimumLightIntensity,
            _maximumLightIntensity,
            pulse);
    }

    private void SetArmedLightActive(bool active)
    {
        if (!_armedLight)
        {
            return;
        }

        _armedLight.enabled = active;
        _armedLight.intensity = active
            ? _minimumLightIntensity
            : 0f;
        if (!active)
        {
            _lightPulsePhase = MinimumPulsePhase;
        }
    }

    private void ApplyWorldVisibility(bool visible)
    {
        if (_renderers != null)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = visible;
            }
        }

        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliders[i].enabled = visible;
            }
        }
    }

    private void OnValidate()
    {
        _pickupRadius = Mathf.Max(0.1f, _pickupRadius);
        _minimumLightIntensity = Mathf.Max(0f, _minimumLightIntensity);
        _maximumLightIntensity = Mathf.Max(
            _minimumLightIntensity,
            _maximumLightIntensity);
        _normalPulseFrequency = Mathf.Max(0.01f, _normalPulseFrequency);
        _urgentPulseFrequency = Mathf.Max(
            _normalPulseFrequency,
            _urgentPulseFrequency);
    }
}
