using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerCombatHudUI : MonoBehaviour
{
    private const float LowHealthThreshold = 0.85f;

    [Header("可替换美术素材")]
    [SerializeField, InspectorName("弹药底板")] private Sprite _ammoPanelSprite;
    [SerializeField, InspectorName("屏幕血污")] private Sprite _bloodOverlaySprite;
    [SerializeField, InspectorName("濒死红色辉光")] private Sprite _redGlowSprite;

    [Header("低血量表现")]
    [SerializeField, Range(0f, 1f), InspectorName("血污最大透明度")] private float _maximumBloodAlpha = 0.62f;
    [SerializeField, Range(0f, 1f), InspectorName("辉光最大透明度")] private float _maximumGlowAlpha = 0.72f;
    [SerializeField, Min(0.01f), InspectorName("淡入淡出速度")] private float _overlayFollowSpeed = 5f;
    [SerializeField, Min(0.01f), InspectorName("呼吸频率")] private float _breathingFrequency = 1.15f;
    [SerializeField, Range(0f, 0.5f), InspectorName("呼吸幅度")] private float _breathingAmplitude = 0.16f;

    private CanvasGroup _hudGroup;
    private Slider _healthSlider;
    private Image _healthBackground;
    private Image _healthFill;
    private Image _healthFrame;
    private Image _ammoPanel;
    private Image _weaponSilhouette;
    private TMP_Text _magazineText;
    private TMP_Text _reserveText;
    private Image _bloodOverlay;
    private Image _redGlow;
    private Sprite _fallbackBloodSprite;
    private Sprite _fallbackGlowSprite;

    private CharacterHealth _health;
    private PlayerLifeCtrl _life;
    private WeaponActionCtrl _weaponAction;
    private WeaponCombatCtrl _weaponCombat;
    private ResourceMgr _resources;
    private string _loadedSilhouetteId;
    private int _silhouetteLoadVersion;
    private float _targetDamageSeverity;
    private float _displayedDamageSeverity;
    private bool _isBound;

    public bool IsHudVisible { get { return _hudGroup && _hudGroup.alpha > 0.5f; } }
    public float HealthNormalized { get { return _healthSlider ? _healthSlider.value : 0f; } }
    public float DamageSeverity { get { return _displayedDamageSeverity; } }
    public Slider HealthSlider { get { return _healthSlider; } }
    public Image WeaponSilhouette { get { return _weaponSilhouette; } }
    public TMP_Text MagazineText { get { return _magazineText; } }
    public TMP_Text ReserveText { get { return _reserveText; } }
    public Image BloodOverlay { get { return _bloodOverlay; } }
    public Image RedGlow { get { return _redGlow; } }

    private void Awake()
    {
        BuildInterface();
        ApplyConfiguredSprites();
        SetAlive(false);
    }

    private void Start()
    {
        if (!_isBound)
        {
            Bind(FindLocalPlayer());
        }
    }

    private void Update()
    {
        float follow = 1f - Mathf.Exp(-_overlayFollowSpeed * Time.unscaledDeltaTime);
        _displayedDamageSeverity = Mathf.Lerp(
            _displayedDamageSeverity, _targetDamageSeverity, follow);
        if (_displayedDamageSeverity < 0.001f && _targetDamageSeverity <= 0f)
        {
            _displayedDamageSeverity = 0f;
        }

        UpdateDamageOverlays();
    }

    private void OnDestroy()
    {
        Unbind();
        ReleaseSilhouette();
        DestroyRuntimeSprite(_fallbackBloodSprite);
        DestroyRuntimeSprite(_fallbackGlowSprite);
    }

    public void Bind(GameObject localPlayer)
    {
        Unbind();
        if (!localPlayer || localPlayer.GetComponent<AIPlayerCtrl>())
        {
            SetAlive(false);
            return;
        }

        _health = localPlayer.GetComponent<CharacterHealth>();
        _life = localPlayer.GetComponent<PlayerLifeCtrl>();
        _weaponAction = localPlayer.GetComponent<WeaponActionCtrl>();
        _weaponCombat = localPlayer.GetComponent<WeaponCombatCtrl>();
        if (!_health || !_life || !_weaponAction || !_weaponCombat)
        {
            Unbind();
            SetAlive(false);
            return;
        }

        _health.HealthChanged += HandleHealthChanged;
        _health.Died += HandleDied;
        _life.Respawned += HandleRespawned;
        _weaponAction.WeaponChanged += HandleWeaponChanged;
        _weaponCombat.AmmoChanged += HandleAmmoChanged;
        _isBound = true;

        HandleHealthChanged(_health.CurrentHealth, _health.MaxHealth);
        HandleWeaponChanged(_weaponAction.CurrentWeaponData);
        HandleAmmoChanged(_weaponCombat.MagazineAmmo, _weaponCombat.ReserveAmmo);
        SetAlive(!_health.IsDead && !_life.IsDead);
    }

    public void ApplyArtSprites(
        Sprite healthBackground,
        Sprite healthFill,
        Sprite healthFrame,
        Sprite ammoPanel,
        Sprite bloodOverlay,
        Sprite redGlow)
    {
        // 生命条固定使用程序化黑框白填充；保留旧参数以兼容现有调用方。
        _ammoPanelSprite = ammoPanel;
        _bloodOverlaySprite = bloodOverlay;
        _redGlowSprite = redGlow;
        ApplyConfiguredSprites();
    }

    private void Unbind()
    {
        if (_health)
        {
            _health.HealthChanged -= HandleHealthChanged;
            _health.Died -= HandleDied;
        }
        if (_life)
        {
            _life.Respawned -= HandleRespawned;
        }
        if (_weaponAction)
        {
            _weaponAction.WeaponChanged -= HandleWeaponChanged;
        }
        if (_weaponCombat)
        {
            _weaponCombat.AmmoChanged -= HandleAmmoChanged;
        }

        _health = null;
        _life = null;
        _weaponAction = null;
        _weaponCombat = null;
        _isBound = false;
    }

    private GameObject FindLocalPlayer()
    {
        PlayerLifeCtrl[] candidates = FindObjectsByType<PlayerLifeCtrl>(
            FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] && !candidates[i].GetComponent<AIPlayerCtrl>())
            {
                return candidates[i].gameObject;
            }
        }
        return null;
    }

    private void HandleHealthChanged(float current, float maximum)
    {
        float normalized = maximum > 0f ? Mathf.Clamp01(current / maximum) : 0f;
        if (_healthSlider)
        {
            _healthSlider.SetValueWithoutNotify(normalized);
        }

        // 85% 开始显现，并让 50% 血量时已经达到强烈、猩红的表现。
        float linearSeverity = normalized < LowHealthThreshold && normalized > 0f
            ? Mathf.InverseLerp(LowHealthThreshold, 0f, normalized)
            : 0f;
        float fullSeverityPoint = Mathf.InverseLerp(LowHealthThreshold, 0f, 0.5f);
        _targetDamageSeverity = Mathf.Clamp01(linearSeverity / fullSeverityPoint);
        if (_health && _health.IsDead)
        {
            SetAlive(false);
        }
    }

    private void HandleDied()
    {
        _targetDamageSeverity = 0f;
        _displayedDamageSeverity = 0f;
        UpdateDamageOverlays();
        SetAlive(false);
    }

    private void HandleRespawned()
    {
        _displayedDamageSeverity = 0f;
        HandleHealthChanged(_health ? _health.CurrentHealth : 0f, _health ? _health.MaxHealth : 1f);
        HandleAmmoChanged(
            _weaponCombat ? _weaponCombat.MagazineAmmo : 0,
            _weaponCombat ? _weaponCombat.ReserveAmmo : 0);
        SetAlive(true);
    }

    private async void HandleWeaponChanged(WeaponDefinitionSO weapon)
    {
        int version = ++_silhouetteLoadVersion;
        ReleaseSilhouette();
        if (_weaponSilhouette)
        {
            _weaponSilhouette.sprite = null;
            _weaponSilhouette.enabled = false;
        }
        if (!weapon || string.IsNullOrWhiteSpace(weapon.Basic.SilhouetteResourceId))
        {
            return;
        }

        string resourceId = weapon.Basic.SilhouetteResourceId;
        try
        {
            _resources = ResourceMgr.instance;
            Sprite sprite = await _resources.LoadAsync<Sprite>(resourceId);
            if (!this || version != _silhouetteLoadVersion)
            {
                _resources.Release<Sprite>(resourceId);
                return;
            }
            if (!sprite)
            {
                _resources.Release<Sprite>(resourceId);
                return;
            }

            _loadedSilhouetteId = resourceId;
            _weaponSilhouette.sprite = sprite;
            _weaponSilhouette.enabled = true;
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"武器剪影加载失败：{exception.Message}", this);
        }
    }

    private void ReleaseSilhouette()
    {
        if (_resources && !string.IsNullOrWhiteSpace(_loadedSilhouetteId))
        {
            _resources.Release<Sprite>(_loadedSilhouetteId);
        }
        _loadedSilhouetteId = string.Empty;
    }

    private void HandleAmmoChanged(int magazine, int reserve)
    {
        if (_magazineText)
        {
            _magazineText.text = Mathf.Max(0, magazine).ToString("00") + "/" +
                Mathf.Max(0, reserve).ToString("000");
        }
        if (_reserveText)
        {
            // 保留独立数据节点供现有测试/调试读取；画面只显示上方组合文本。
            _reserveText.text = Mathf.Max(0, reserve).ToString("000");
        }
    }

    private void SetAlive(bool isAlive)
    {
        if (!_hudGroup)
        {
            return;
        }
        _hudGroup.alpha = isAlive ? 1f : 0f;
        _hudGroup.interactable = false;
        _hudGroup.blocksRaycasts = false;
    }

    private void UpdateDamageOverlays()
    {
        if (!_bloodOverlay || !_redGlow)
        {
            return;
        }

        float severity = IsHudVisible ? _displayedDamageSeverity : 0f;
        SetImageAlpha(_bloodOverlay, severity * _maximumBloodAlpha);
        float breathing = 1f - _breathingAmplitude * 0.5f +
            Mathf.Sin(Time.unscaledTime * Mathf.PI * 2f * _breathingFrequency) *
            _breathingAmplitude * 0.5f;
        SetImageAlpha(_redGlow, severity * _maximumGlowAlpha * breathing);
        _bloodOverlay.enabled = severity > 0.001f;
        _redGlow.enabled = severity > 0.001f;
    }

    private void BuildInterface()
    {
        GameObject root = CreateUiObject("战斗状态HUD", transform);
        Stretch(root.GetComponent<RectTransform>());
        _hudGroup = root.AddComponent<CanvasGroup>();

        _bloodOverlay = CreateImage("受伤血污", root.transform, Color.white);
        Stretch(_bloodOverlay.rectTransform);
        _bloodOverlay.raycastTarget = false;

        _redGlow = CreateImage("濒死红色辉光", root.transform, new Color(0.75f, 0.015f, 0.01f, 0f));
        Stretch(_redGlow.rectTransform);
        _redGlow.raycastTarget = false;

        BuildHealth(root.transform);
        BuildAmmo(root.transform);
    }

    private void BuildHealth(Transform root)
    {
        GameObject healthRoot = CreateUiObject("左下角生命", root);
        RectTransform rect = healthRoot.GetComponent<RectTransform>();
        Anchor(rect, new Vector2(0f, 0f), new Vector2(0f, 0f),
            new Vector2(520f, 34f), new Vector2(42f, 42f), new Vector2(0f, 0f));

        _healthSlider = healthRoot.AddComponent<Slider>();
        _healthSlider.minValue = 0f;
        _healthSlider.maxValue = 1f;
        _healthSlider.value = 1f;
        _healthSlider.direction = Slider.Direction.LeftToRight;
        _healthSlider.transition = Selectable.Transition.None;
        _healthSlider.interactable = false;

        _healthBackground = CreateImage("生命背景", healthRoot.transform,
            Color.black);
        Stretch(_healthBackground.rectTransform);

        GameObject fillArea = CreateUiObject("生命填充区域", healthRoot.transform);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        Stretch(fillAreaRect, new Vector2(4f, 4f), new Vector2(-4f, -4f));
        _healthFill = CreateImage("生命填充", fillArea.transform,
            Color.white);
        Stretch(_healthFill.rectTransform);
        // Slider 通过改变 Rect 宽度表现血量；不要再叠加 Image.fillAmount，
        // 否则动态创建的层级在部分分辨率下会看起来始终填满。
        _healthFill.type = Image.Type.Simple;
        _healthSlider.fillRect = _healthFill.rectTransform;
        _healthSlider.targetGraphic = _healthFill;

        _healthFrame = CreateImage("生命异形框", healthRoot.transform,
            Color.clear);
        Stretch(_healthFrame.rectTransform);
        _healthFrame.raycastTarget = false;
    }

    private void BuildAmmo(Transform root)
    {
        GameObject ammoRoot = CreateUiObject("右下角枪械弹药", root);
        RectTransform rect = ammoRoot.GetComponent<RectTransform>();
        Anchor(rect, new Vector2(1f, 0f), new Vector2(1f, 0f),
            new Vector2(360f, 128f), new Vector2(-54f, 58f), new Vector2(1f, 0f));

        _ammoPanel = CreateImage("弹药半透明底板", ammoRoot.transform,
            new Color(0.018f, 0.02f, 0.018f, 0.72f));
        Stretch(_ammoPanel.rectTransform);

        _weaponSilhouette = CreateImage("当前枪械剪影", ammoRoot.transform, Color.white);
        Anchor(_weaponSilhouette.rectTransform, new Vector2(0f, 0f), new Vector2(0.62f, 1f),
            new Vector2(-44f, -30f), new Vector2(8f, 1f), new Vector2(0f, 0.5f));
        _weaponSilhouette.preserveAspect = true;
        _weaponSilhouette.enabled = false;

        _magazineText = CreateText("弹药总览", ammoRoot.transform, 32f, FontStyles.Bold);
        Anchor(_magazineText.rectTransform, new Vector2(0.58f, 0f), new Vector2(1f, 1f),
            new Vector2(-28f, -34f), new Vector2(-18f, 0f), new Vector2(0f, 0.5f));
        _magazineText.alignment = TextAlignmentOptions.MidlineRight;
        _magazineText.text = "00/000";

        _reserveText = CreateText("备弹", ammoRoot.transform, 24f, FontStyles.Normal);
        Anchor(_reserveText.rectTransform, new Vector2(0.68f, 0f), new Vector2(1f, 0f),
            new Vector2(-14f, 48f), new Vector2(0f, 12f), new Vector2(0f, 0f));
        _reserveText.alignment = TextAlignmentOptions.BottomLeft;
        _reserveText.color = new Color(0.78f, 0.78f, 0.72f, 1f);
        _reserveText.text = "000";
        _reserveText.gameObject.SetActive(false);
    }

    private void ApplyConfiguredSprites()
    {
        ApplySprite(_ammoPanel, _ammoPanelSprite);
        if (_bloodOverlay)
        {
            if (!_fallbackBloodSprite)
            {
                _fallbackBloodSprite = CreateDamageFallbackSprite(true);
            }
            ApplySprite(_bloodOverlay, _bloodOverlaySprite
                ? _bloodOverlaySprite
                : _fallbackBloodSprite);
        }
        if (_redGlow)
        {
            if (!_fallbackGlowSprite)
            {
                _fallbackGlowSprite = CreateDamageFallbackSprite(false);
            }
            ApplySprite(_redGlow, _redGlowSprite
                ? _redGlowSprite
                : _fallbackGlowSprite);
        }
    }

    private static void ApplySprite(Image image, Sprite sprite)
    {
        if (!image || !sprite)
        {
            return;
        }
        image.sprite = sprite;
        image.type = Image.Type.Simple;
        image.preserveAspect = false;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject instance = new GameObject(objectName, typeof(RectTransform));
        instance.transform.SetParent(parent, false);
        return instance;
    }

    private static Image CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject instance = CreateUiObject(objectName, parent);
        Image image = instance.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI CreateText(
        string objectName, Transform parent, float size, FontStyles style)
    {
        GameObject instance = CreateUiObject(objectName, parent);
        TextMeshProUGUI text = instance.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = style;
        text.color = new Color(0.94f, 0.93f, 0.86f, 1f);
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    private static void Stretch(
        RectTransform rect, Vector2 offsetMin = default, Vector2 offsetMax = default)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static void Anchor(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 sizeDelta,
        Vector2 position,
        Vector2 pivot)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = position;
    }

    private static void SetImageAlpha(Image image, float alpha)
    {
        Color color = image.color;
        color.a = Mathf.Clamp01(alpha);
        image.color = color;
    }

    private static Sprite CreateDamageFallbackSprite(bool blood)
    {
        const int width = 512;
        const int height = 288;
        Texture2D texture = new Texture2D(
            width, height, TextureFormat.RGBA32, false, true)
        {
            name = blood ? "运行时血污兜底遮罩" : "运行时红光兜底遮罩",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = (y + 0.5f) / height;
            for (int x = 0; x < width; x++)
            {
                float u = (x + 0.5f) / width;
                float edgeDistance = Mathf.Min(
                    Mathf.Min(u, 1f - u),
                    Mathf.Min(v, 1f - v));
                float edgeRange = blood ? 0.22f : 0.3f;
                float edge = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0f, edgeRange, edgeDistance));
                float alpha;
                Color color;
                if (blood)
                {
                    float coarse = Mathf.PerlinNoise(x * 0.027f + 13.7f, y * 0.031f + 4.1f);
                    float fine = Mathf.PerlinNoise(x * 0.083f + 31.2f, y * 0.071f + 8.6f);
                    float breakup = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(
                            0.38f,
                            0.78f,
                            coarse * 0.72f + fine * 0.28f));
                    alpha = edge * breakup;
                    color = new Color(0.3f, 0.006f, 0.003f, alpha);
                }
                else
                {
                    alpha = edge * edge;
                    color = new Color(1f, 1f, 1f, alpha);
                }
                pixels[y * width + x] = color;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            100f,
            0u,
            SpriteMeshType.FullRect);
        sprite.name = blood ? "运行时血污兜底精灵" : "运行时红光兜底精灵";
        return sprite;
    }

    private static void DestroyRuntimeSprite(Sprite sprite)
    {
        if (!sprite)
        {
            return;
        }
        Texture texture = sprite.texture;
        Destroy(sprite);
        if (texture)
        {
            Destroy(texture);
        }
    }
}
