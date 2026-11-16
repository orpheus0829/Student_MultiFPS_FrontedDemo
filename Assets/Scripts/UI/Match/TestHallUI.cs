using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public sealed class TestHallUI : MonoBehaviour
{
    private static readonly int[] PlayerCounts = { 2, 4, 6, 8, 10 };

    [Header("测试数据")]
    [SerializeField] private MapCatalogSO _mapCatalog;
    [SerializeField] private List<ModeDefinitionSO> _modes = new List<ModeDefinitionSO>();

    private readonly List<MapDefinitionSO> _availableMaps = new List<MapDefinitionSO>();
    private Font _font;
    private Camera _uiCamera;
    private GameObject _configurationPanel;
    private Dropdown _modeDropdown;
    private Dropdown _mapDropdown;
    private Dropdown _playerCountDropdown;
    private Dropdown _teamDropdown;
    private Dropdown _aiCountDropdown;
    private Dropdown _difficultyDropdown;
    private Text _statusText;
    private bool _isLaunching;

    private void Awake()
    {
        _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        _uiCamera = Camera.main ? Camera.main : CreateUICamera();
        EnsureEventSystem();
        BuildInterface();
        RefreshMaps();
        RefreshAIOptions();
        RefreshTeamOptions();
    }

    public void Configure(MapCatalogSO mapCatalog, IEnumerable<ModeDefinitionSO> modes)
    {
        _mapCatalog = mapCatalog;
        _modes = modes != null
            ? new List<ModeDefinitionSO>(modes)
            : new List<ModeDefinitionSO>();
    }

    private void EnsureEventSystem()
    {
        if (EventSystem.current)
        {
            if (!EventSystem.current.TryGetComponent(out InputSystemUIInputModule inputModule))
            {
                EventSystem.current.gameObject.AddComponent<InputSystemUIInputModule>();
            }
            return;
        }

        GameObject eventSystemObject = new GameObject(
            "EventSystem",
            typeof(EventSystem),
            typeof(InputSystemUIInputModule));
        eventSystemObject.transform.SetParent(transform, false);
    }

    private Camera CreateUICamera()
    {
        GameObject cameraObject = new GameObject("TestHallCamera", typeof(Camera));
        cameraObject.transform.SetParent(transform, false);
        cameraObject.transform.SetPositionAndRotation(
            new Vector3(0f, 0f, -10f),
            Quaternion.identity);
        cameraObject.tag = "MainCamera";

        Camera camera = cameraObject.GetComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.02f, 0.025f, 0.04f, 1f);
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.nearClipPlane = 0.1f;
        camera.farClipPlane = 100f;
        return camera;
    }

    private void BuildInterface()
    {
        GameObject canvasObject = new GameObject(
            "TestHallCanvas",
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera = _uiCamera;
        canvas.planeDistance = 1f;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        Image background = CreateImage(canvasObject.transform, "Background", new Color(0.035f, 0.045f, 0.065f, 1f));
        Stretch(background.rectTransform);

        RectTransform card = CreateImage(
            canvasObject.transform,
            "RoomCard",
            new Color(0.075f, 0.09f, 0.125f, 0.98f)).rectTransform;
        SetRect(card, new Vector2(0.5f, 0.5f), new Vector2(900f, 820f), Vector2.zero);

        Text title = CreateText(card, "Title", "TEST HALL", 54, TextAnchor.MiddleCenter);
        SetRect(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(760f, 90f), new Vector2(0f, -70f));
        title.color = new Color(0.92f, 0.96f, 1f);

        Text subtitle = CreateText(card, "Subtitle", "临时测试大厅 · 本地房间配置", 24, TextAnchor.MiddleCenter);
        SetRect(subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(760f, 45f), new Vector2(0f, -125f));
        subtitle.color = new Color(0.55f, 0.68f, 0.82f);

        Button createRoomButton = CreateButton(card, "CreateRoomButton", "创建房间");
        SetRect(createRoomButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(300f, 72f), new Vector2(0f, 180f));
        createRoomButton.onClick.AddListener(ShowConfiguration);

        _configurationPanel = CreateImage(
            card,
            "ConfigurationPanel",
            new Color(0.045f, 0.055f, 0.078f, 0.96f)).gameObject;
        SetRect(_configurationPanel.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(760f, 525f), new Vector2(0f, -55f));

        _modeDropdown = CreateField(_configurationPanel.transform, "模式", 205f);
        _mapDropdown = CreateField(_configurationPanel.transform, "地图", 135f);
        _playerCountDropdown = CreateField(_configurationPanel.transform, "总人数", 65f);
        _teamDropdown = CreateField(_configurationPanel.transform, "我的队伍", -5f);
        _aiCountDropdown = CreateField(_configurationPanel.transform, "人机数量", -75f);
        _difficultyDropdown = CreateField(_configurationPanel.transform, "人机难度", -145f);

        PopulateModes();
        PopulatePlayerCounts();
        PopulateDifficulties();
        _modeDropdown.onValueChanged.AddListener(HandleModeChanged);
        _mapDropdown.onValueChanged.AddListener(HandleMapChanged);
        _playerCountDropdown.onValueChanged.AddListener(HandlePlayerCountChanged);

        Button confirmButton = CreateButton(_configurationPanel.transform, "ConfirmButton", "创建并开始游戏");
        SetRect(confirmButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(240f, 62f), new Vector2(0f, -215f));
        confirmButton.onClick.AddListener(ConfirmConfiguration);

        _statusText = CreateText(card, "Status", "点击“创建房间”开始配置。", 22, TextAnchor.MiddleCenter);
        SetRect(_statusText.rectTransform, new Vector2(0.5f, 0f), new Vector2(760f, 65f), new Vector2(0f, 45f));
        _statusText.color = new Color(0.62f, 0.74f, 0.86f);

        _configurationPanel.SetActive(false);
    }

    private Dropdown CreateField(Transform parent, string label, float verticalPosition)
    {
        Text labelText = CreateText(parent, label + "Label", label, 24, TextAnchor.MiddleLeft);
        SetRect(labelText.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(180f, 54f), new Vector2(-245f, verticalPosition));

        Dropdown dropdown = CreateDropdown(parent, label + "Dropdown");
        SetRect(dropdown.GetComponent<RectTransform>(), new Vector2(0.5f, 0.5f), new Vector2(430f, 54f), new Vector2(105f, verticalPosition));
        return dropdown;
    }

    private void PopulateModes()
    {
        List<string> options = new List<string>();
        for (int i = 0; i < _modes.Count; i++)
        {
            ModeDefinitionSO mode = _modes[i];
            if (mode)
            {
                options.Add(mode.DisplayName);
            }
        }

        if (options.Count == 0)
        {
            options.Add("未配置模式");
        }
        _modeDropdown.AddOptions(options);
    }

    private void PopulatePlayerCounts()
    {
        List<string> options = new List<string>();
        for (int i = 0; i < PlayerCounts.Length; i++)
        {
            options.Add(PlayerCounts[i] + " 人");
        }
        _playerCountDropdown.AddOptions(options);
        _playerCountDropdown.value = 4;
    }

    private void PopulateDifficulties()
    {
        _difficultyDropdown.AddOptions(new List<string> { "简单", "普通", "困难" });
        _difficultyDropdown.value = (int)AIDifficulty.Normal;
    }

    private void RefreshMaps()
    {
        _availableMaps.Clear();
        _mapDropdown.ClearOptions();
        if (_mapCatalog)
        {
            IReadOnlyList<MapDefinitionSO> maps = _mapCatalog.Maps;
            for (int i = 0; i < maps.Count; i++)
            {
                MapDefinitionSO map = maps[i];
                if (map)
                {
                    _availableMaps.Add(map);
                }
            }
        }

        List<string> options = new List<string>();
        for (int i = 0; i < _availableMaps.Count; i++)
        {
            options.Add(_availableMaps[i].DisplayName);
        }
        if (options.Count == 0)
        {
            options.Add("当前模式暂无可用地图");
        }
        _mapDropdown.AddOptions(options);
        _mapDropdown.interactable = _availableMaps.Count > 0;
    }

    private MapDefinitionSO GetSelectedMap()
    {
        if (_availableMaps.Count == 0)
        {
            return null;
        }

        int index = Mathf.Clamp(_mapDropdown.value, 0, _availableMaps.Count - 1);
        return _availableMaps[index];
    }

    private void RefreshAIOptions()
    {
        int totalPlayers = GetPlayerCount();
        int previousValue = _aiCountDropdown.value;
        _aiCountDropdown.ClearOptions();
        List<string> options = new List<string>();
        for (int i = 0; i < totalPlayers; i++)
        {
            options.Add(i + " 个");
        }
        _aiCountDropdown.AddOptions(options);
        _aiCountDropdown.value = Mathf.Clamp(previousValue, 0, totalPlayers - 1);
    }

    private void RefreshTeamOptions()
    {
        _teamDropdown.ClearOptions();
        ModeDefinitionSO mode = GetSelectedMode();
        bool isSolo = mode && mode.Kind == ModeKind.Solo;
        _teamDropdown.AddOptions(isSolo
            ? new List<string> { "个人" }
            : new List<string> { "队伍 1", "队伍 2" });
        _teamDropdown.interactable = !isSolo;
    }

    private ModeDefinitionSO GetSelectedMode()
    {
        int validIndex = 0;
        for (int i = 0; i < _modes.Count; i++)
        {
            ModeDefinitionSO mode = _modes[i];
            if (!mode)
            {
                continue;
            }
            if (validIndex == _modeDropdown.value)
            {
                return mode;
            }
            validIndex++;
        }
        return null;
    }

    private int GetPlayerCount()
    {
        int index = Mathf.Clamp(_playerCountDropdown.value, 0, PlayerCounts.Length - 1);
        return PlayerCounts[index];
    }

    private void ShowConfiguration()
    {
        _configurationPanel.SetActive(true);
        _statusText.text = "选择测试参数，确认后将加载地图并开始对局。";
    }

    private void HandleModeChanged(int value)
    {
        SelectFirstMapSupportingMode();
        RefreshTeamOptions();
    }

    private void HandleMapChanged(int value)
    {
        SelectFirstModeSupportedByMap();
        RefreshTeamOptions();
    }

    private void HandlePlayerCountChanged(int value)
    {
        RefreshAIOptions();
    }

    private void ConfirmConfiguration()
    {
        if (_isLaunching)
        {
            return;
        }

        ModeDefinitionSO mode = GetSelectedMode();
        MapDefinitionSO map = GetSelectedMap();
        if (!mode || !map || !map.SupportsMode(mode.Kind))
        {
            _statusText.text = "该地图不支持当前模式，请重新选择。";
            return;
        }

        List<MatchSlotConfig> slots = BuildRoster(mode);
        MatchConfig config = new MatchConfig(mode, slots, map);
        if (!TestMatchLauncher.Launch(config))
        {
            _statusText.text = "创建对局失败，请检查模式和地图配置。";
            return;
        }

        _isLaunching = true;
        _statusText.text = string.Format(
            "正在进入 {0}：{1}，参战 {2} 人……",
            map.DisplayName,
            mode.DisplayName,
            slots.Count);
    }

    private List<MatchSlotConfig> BuildRoster(ModeDefinitionSO mode)
    {
        int aiCount = Mathf.Clamp(_aiCountDropdown.value, 0, GetPlayerCount() - 1);
        int localTeam = mode.Kind == ModeKind.Solo ? 0 : _teamDropdown.value;
        AIDifficulty difficulty = (AIDifficulty)Mathf.Clamp(
            _difficultyDropdown.value,
            (int)AIDifficulty.Easy,
            (int)AIDifficulty.Hard);
        List<MatchSlotConfig> slots = new List<MatchSlotConfig>
        {
            new MatchSlotConfig(0, localTeam, "玩家", false, true)
        };

        for (int index = 0; index < aiCount; index++)
        {
            int slot = index + 1;
            int team = mode.Kind == ModeKind.Solo
                ? slot
                : GetBalancedAITeam(index, localTeam, Mathf.Max(1, mode.TeamCount));
            slots.Add(new MatchSlotConfig(
                slot,
                team,
                "AI 玩家 " + slot,
                true,
                false,
                string.Empty,
                difficulty));
        }

        return slots;
    }

    private void SelectFirstMapSupportingMode()
    {
        ModeDefinitionSO mode = GetSelectedMode();
        MapDefinitionSO selectedMap = GetSelectedMap();
        if (!mode || (selectedMap && selectedMap.SupportsMode(mode.Kind)))
        {
            return;
        }

        for (int i = 0; i < _availableMaps.Count; i++)
        {
            if (_availableMaps[i] && _availableMaps[i].SupportsMode(mode.Kind))
            {
                _mapDropdown.SetValueWithoutNotify(i);
                _mapDropdown.RefreshShownValue();
                return;
            }
        }
    }

    private void SelectFirstModeSupportedByMap()
    {
        MapDefinitionSO map = GetSelectedMap();
        ModeDefinitionSO selectedMode = GetSelectedMode();
        if (!map || (selectedMode && map.SupportsMode(selectedMode.Kind)))
        {
            return;
        }

        int validIndex = 0;
        for (int i = 0; i < _modes.Count; i++)
        {
            ModeDefinitionSO mode = _modes[i];
            if (!mode)
            {
                continue;
            }
            if (map.SupportsMode(mode.Kind))
            {
                _modeDropdown.SetValueWithoutNotify(validIndex);
                _modeDropdown.RefreshShownValue();
                return;
            }
            validIndex++;
        }
    }

    private static int GetBalancedAITeam(int aiIndex, int localTeam, int teamCount)
    {
        if (teamCount <= 1)
        {
            return 0;
        }

        return (aiIndex + localTeam + 1) % teamCount;
    }

    private Image CreateImage(Transform parent, string objectName, Color color)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        target.transform.SetParent(parent, false);
        Image image = target.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private Text CreateText(Transform parent, string objectName, string content, int fontSize, TextAnchor alignment)
    {
        GameObject target = new GameObject(objectName, typeof(RectTransform), typeof(Text));
        target.transform.SetParent(parent, false);
        Text text = target.GetComponent<Text>();
        text.font = _font;
        text.text = content;
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        return text;
    }

    private Button CreateButton(Transform parent, string objectName, string label)
    {
        Image image = CreateImage(parent, objectName, new Color(0.1f, 0.38f, 0.62f, 1f));
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        Text text = CreateText(image.transform, "Label", label, 26, TextAnchor.MiddleCenter);
        Stretch(text.rectTransform);
        return button;
    }

    private Dropdown CreateDropdown(Transform parent, string objectName)
    {
        Image image = CreateImage(parent, objectName, new Color(0.12f, 0.145f, 0.19f, 1f));
        Dropdown dropdown = image.gameObject.AddComponent<Dropdown>();
        Text label = CreateText(image.transform, "Label", string.Empty, 22, TextAnchor.MiddleLeft);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(18f, 0f);
        label.rectTransform.offsetMax = new Vector2(-48f, 0f);

        Text arrow = CreateText(image.transform, "Arrow", "▼", 20, TextAnchor.MiddleCenter);
        SetRect(arrow.rectTransform, new Vector2(1f, 0.5f), new Vector2(44f, 44f), new Vector2(-25f, 0f));

        Image template = CreateImage(image.transform, "Template", new Color(0.08f, 0.1f, 0.14f, 1f));
        template.rectTransform.anchorMin = new Vector2(0f, 0f);
        template.rectTransform.anchorMax = new Vector2(1f, 0f);
        template.rectTransform.pivot = new Vector2(0.5f, 1f);
        template.rectTransform.anchoredPosition = new Vector2(0f, -4f);
        template.rectTransform.sizeDelta = new Vector2(0f, 180f);
        ScrollRect scrollRect = template.gameObject.AddComponent<ScrollRect>();

        GameObject viewportObject = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewportObject.transform.SetParent(template.transform, false);
        RectTransform viewport = viewportObject.GetComponent<RectTransform>();
        Stretch(viewport);
        viewportObject.GetComponent<Image>().color = Color.white;
        viewportObject.GetComponent<Mask>().showMaskGraphic = false;

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(viewport, false);
        RectTransform content = contentObject.GetComponent<RectTransform>();
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = Vector2.one;
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, 32f);

        Toggle item = CreateToggle(content, "Item");
        RectTransform itemRect = item.GetComponent<RectTransform>();
        itemRect.anchorMin = new Vector2(0f, 0.5f);
        itemRect.anchorMax = new Vector2(1f, 0.5f);
        itemRect.sizeDelta = new Vector2(0f, 32f);
        Text itemLabel = item.GetComponentInChildren<Text>();

        scrollRect.viewport = viewport;
        scrollRect.content = content;
        dropdown.targetGraphic = image;
        dropdown.template = template.rectTransform;
        dropdown.captionText = label;
        dropdown.itemText = itemLabel;
        template.gameObject.SetActive(false);
        return dropdown;
    }

    private Toggle CreateToggle(Transform parent, string objectName)
    {
        Image background = CreateImage(parent, objectName, new Color(0.1f, 0.13f, 0.18f, 1f));
        Toggle toggle = background.gameObject.AddComponent<Toggle>();
        toggle.targetGraphic = background;
        Text label = CreateText(background.transform, "Item Label", "选项", 20, TextAnchor.MiddleLeft);
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(18f, 0f);
        label.rectTransform.offsetMax = new Vector2(-8f, 0f);
        // 选项文字不能作为 Toggle 的勾选图形，否则未选中项会被自动隐藏。
        toggle.graphic = null;
        return toggle;
    }

    private static void SetRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
    {
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
