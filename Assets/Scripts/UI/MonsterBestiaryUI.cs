using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 怪物图鉴界面：按 L 打开，每只怪物显示图片、名字、累计击杀数和一个「掉落物」按钮，
/// 点图片会放大并显示描述，点「掉落物」会弹出这只怪的掉落列表。
///
/// 界面是在运行时用代码搭出来的（这样不用手改场景里的 UI），会自己挂到项目的 Canvas 上，
/// 并且接进 UI.cs 的菜单系统——所以它和其他菜单一样：按 L 开/关、打开时自动暂停、
/// 打开别的菜单（C/B/K/O）会自动关掉它。
///
/// 使用方式：在场景里任意一个物体（比如挂 UI 的那个 Canvas 下面）挂上本脚本即可，
/// 怪物列表会自动带上同物体的 MonsterBestiary 组件；列表内容可以用菜单
/// 「Tools/怪物图鉴/从敌人预制体自动填充」一键生成。
/// </summary>
public class MonsterBestiaryUI : MonoBehaviour
{
    [Header("引用（留空会自动找）")]
    [SerializeField] private MonsterBestiary bestiary;
    [SerializeField] private UI uiManager;
    [Tooltip("中文字体。留空会在编辑器里自动挑一个带中文的字体")]
    [SerializeField] private TMP_FontAsset font;

    [Header("按键（没接入 UI.cs 时才由本脚本处理）")]
    [SerializeField] private KeyCode toggleKey = KeyCode.L;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Header("外观")]
    [SerializeField] private string title = "怪物图鉴";
    [SerializeField] private int columns = 4;
    [SerializeField] private Vector2 slotSize = new Vector2(215f, 300f);
    [SerializeField] private Color panelColor = new Color(0.04f, 0.05f, 0.08f, 0.97f);
    [SerializeField] private Color slotColor = new Color(0.13f, 0.15f, 0.2f, 1f);
    [SerializeField] private Color popupColor = new Color(0.09f, 0.1f, 0.14f, 1f);
    [SerializeField] private Color textColor = new Color(0.92f, 0.9f, 0.85f, 1f);
    [SerializeField] private Color accentColor = new Color(0.85f, 0.72f, 0.4f, 1f);

    /// <summary>运行时创建出来的整个图鉴面板，UI.cs 会接管它的显示/隐藏。</summary>
    public GameObject PanelRoot { get; private set; }

    private RectTransform content;

    private GameObject detailPopup;
    private Image detailIcon;
    private TextMeshProUGUI detailName;
    private TextMeshProUGUI detailKills;
    private TextMeshProUGUI detailDescription;

    private GameObject dropsPopup;
    private TextMeshProUGUI dropsTitle;
    private RectTransform dropsList;

    private bool standalone;
    private bool wasOpen;
    private bool pausedByUs;

    #region 生命周期

    private void Awake()
    {
        if (bestiary == null)
            bestiary = GetComponent<MonsterBestiary>();

        if (bestiary == null)
            bestiary = FindObjectOfType<MonsterBestiary>();

        if (bestiary == null)
            bestiary = gameObject.AddComponent<MonsterBestiary>();

        if (font == null)
            font = TMP_Settings.defaultFontAsset;

        if (uiManager == null)
            uiManager = FindObjectOfType<UI>();

        Transform parent;

        if (uiManager != null)
        {
            // 并进项目自己的菜单系统，L 键 / 暂停 / 互斥都由 UI.cs 统一处理
            parent = uiManager.transform;
            standalone = false;
        }
        else
        {
            parent = CreateStandaloneCanvas();
            standalone = true;
        }

        BuildUI(parent);

        if (uiManager != null)
            uiManager.SetBestiaryPanel(PanelRoot);

        PanelRoot.SetActive(false);
        wasOpen = false;

        CheckFont();
    }

    /// <summary>面板的显示/隐藏由 UI.cs 控制，这里只监听「刚被打开 / 刚被关掉」来接刷新和收尾。</summary>
    private void Update()
    {
        if (PanelRoot == null)
            return;

        bool open = PanelRoot.activeSelf;

        if (open != wasOpen)
        {
            wasOpen = open;

            if (open)
                Refresh();
            else
                ClosePopups();
        }

        if (!standalone)
            return;

        // 没接入 UI.cs 时自己处理按键
        if (Input.GetKeyDown(toggleKey))
            Toggle();

        if (open && Input.GetKeyDown(closeKey))
            ClosePanel();
    }

    #endregion

    #region 打开 / 关闭

    public void Toggle()
    {
        if (PanelRoot == null)
            return;

        if (PanelRoot.activeSelf)
            ClosePanel();
        else
            OpenPanel();
    }

    public void OpenPanel()
    {
        if (PanelRoot == null)
            return;

        PanelRoot.SetActive(true);
        SetPaused(true);
    }

    /// <summary>关闭按钮用。接进了 UI.cs 就走它的切换逻辑，保证互斥和暂停状态一致。</summary>
    public void ClosePanel()
    {
        if (PanelRoot == null)
            return;

        if (uiManager != null)
        {
            uiManager.SwitchWithKeyTo(PanelRoot);
            return;
        }

        PanelRoot.SetActive(false);
        SetPaused(false);
    }

    /// <summary>被 UI.cs 切到别的菜单时调用，用来收掉弹窗并恢复时间。</summary>
    private void ClosePopups()
    {
        if (detailPopup != null)
            detailPopup.SetActive(false);

        if (dropsPopup != null)
            dropsPopup.SetActive(false);

        SetPaused(false);
    }

    private void SetPaused(bool _paused)
    {
        if (!standalone || GameManager.instance == null)
            return;

        if (_paused && !pausedByUs && Time.timeScale > 0f)
        {
            GameManager.instance.PauseGame(true);
            pausedByUs = true;
        }
        else if (!_paused && pausedByUs)
        {
            GameManager.instance.PauseGame(false);
            pausedByUs = false;
        }
    }

    #endregion

    #region 刷新列表

    private void Refresh()
    {
        ClosePopups();
        ClearChildren(content);

        if (bestiary == null)
            return;

        if (bestiary.MonsterCount == 0)
        {
            TextMeshProUGUI empty = NewText("Empty", content, "还没有配置怪物。\n用菜单「Tools/怪物图鉴/从敌人预制体自动填充」可以一键生成。",
                28, TextAlignmentOptions.Center, textColor);
            SetRect(empty.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 120f), new Vector2(0f, -80f));
            return;
        }

        for (int i = 0; i < bestiary.MonsterCount; i++)
        {
            MonsterEntry entry = bestiary.GetMonster(i);

            if (entry != null)
                CreateSlot(entry);
        }
    }

    private void CreateSlot(MonsterEntry _entry)
    {
        int killCount = bestiary.GetKills(_entry);
        bool hidden = _entry.hideUntilFirstKill && killCount <= 0;
        Sprite icon = MonsterBestiary.GetIcon(_entry);

        GameObject slot = NewUI("Monster_" + MonsterBestiary.ResolveId(_entry), content);
        slot.AddComponent<Image>().color = slotColor;

        VerticalLayoutGroup layout = slot.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 6f;
        layout.padding = new RectOffset(10, 10, 8, 8);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // 图片：点它放大看描述
        Button iconButton = NewImageButton("Icon", slot.transform, icon, hidden ? new Color(1f, 1f, 1f, 0.35f) : Color.white);
        LayoutElement iconLayout = iconButton.gameObject.AddComponent<LayoutElement>();
        iconLayout.preferredHeight = 150f;
        iconButton.onClick.AddListener(() => OpenDetail(_entry));

        if (icon == null)
        {
            TextMeshProUGUI noIcon = NewText("NoIcon", iconButton.transform, "暂无图片", 22, TextAlignmentOptions.Center, textColor);
            Stretch(noIcon.rectTransform);
        }

        NewText("Name", slot.transform, hidden ? "？？？" : MonsterBestiary.GetDisplayName(_entry),
            26, TextAlignmentOptions.Center, accentColor);

        NewText("Kills", slot.transform, hidden ? "累计击杀：？" : $"累计击杀：{killCount}",
            22, TextAlignmentOptions.Center, textColor);

        Button dropsButton = NewButton("DropsButton", slot.transform, "掉落物", 24, new Vector2(0f, 44f));
        dropsButton.onClick.AddListener(() => OpenDrops(_entry));
    }

    #endregion

    #region 放大详情 / 掉落物弹窗

    private void OpenDetail(MonsterEntry _entry)
    {
        bool hidden = _entry.hideUntilFirstKill && bestiary.GetKills(_entry) <= 0;

        detailName.text = hidden ? "？？？" : MonsterBestiary.GetDisplayName(_entry);
        detailKills.text = hidden ? "累计击杀：？" : $"累计击杀：{bestiary.GetKills(_entry)}";

        Sprite icon = MonsterBestiary.GetIcon(_entry);
        detailIcon.sprite = icon;
        detailIcon.color = hidden ? new Color(1f, 1f, 1f, 0.35f) : Color.white;

        if (hidden)
            detailDescription.text = "还没遇到过这种怪物。";
        else if (string.IsNullOrEmpty(_entry.description))
            detailDescription.text = "（还没有写描述：在 MonsterBestiary 的怪物列表里填 Description）";
        else
            detailDescription.text = _entry.description;

        detailPopup.SetActive(true);
    }

    private void OpenDrops(MonsterEntry _entry)
    {
        bool hidden = _entry.hideUntilFirstKill && bestiary.GetKills(_entry) <= 0;

        dropsTitle.text = (hidden ? "？？？" : MonsterBestiary.GetDisplayName(_entry)) + " 的掉落物";

        ClearChildren(dropsList);

        if (hidden)
        {
            CreateDropRow(null, "还没遇到过这种怪物。", "");
        }
        else
        {
            ItemData[] drops = MonsterBestiary.GetDrops(_entry);

            if (drops == null || drops.Length == 0)
            {
                CreateDropRow(null, "这种怪物没有掉落物。", "");
            }
            else
            {
                foreach (ItemData item in drops)
                {
                    if (item == null)
                        continue;

                    CreateDropRow(item.icon, item.itemName, $"{item.dropChance:0.#}%");
                }
            }
        }

        dropsPopup.SetActive(true);
    }

    private void CreateDropRow(Sprite _icon, string _name, string _chance)
    {
        GameObject row = NewUI("Drop", dropsList);
        row.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.06f);

        LayoutElement layout = row.AddComponent<LayoutElement>();
        layout.preferredHeight = 74f;

        float textLeft = 20f;

        if (_icon != null)
        {
            Image icon = NewImage("Icon", row.transform, _icon, Color.white);
            SetRect(icon.rectTransform, new Vector2(0f, 0.5f), new Vector2(56f, 56f), new Vector2(12f, 0f));
            textLeft = 82f;
        }

        TextMeshProUGUI nameText = NewText("Name", row.transform, _name, 26, TextAlignmentOptions.Left, textColor);
        SetRect(nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(360f, 54f), new Vector2(textLeft, 0f));

        if (!string.IsNullOrEmpty(_chance))
        {
            TextMeshProUGUI chanceText = NewText("Chance", row.transform, _chance, 24, TextAlignmentOptions.Right, accentColor);
            SetRect(chanceText.rectTransform, new Vector2(1f, 0.5f), new Vector2(140f, 54f), new Vector2(-16f, 0f));
        }
    }

    #endregion

    #region 界面搭建

    private void BuildUI(Transform _parent)
    {
        PanelRoot = NewUI("MonsterBestiaryPanel", _parent);
        Stretch(RT(PanelRoot));

        Image background = PanelRoot.AddComponent<Image>();
        background.color = panelColor;

        TextMeshProUGUI titleText = NewText("Title", PanelRoot.transform, title, 46, TextAlignmentOptions.Center, accentColor);
        SetRect(titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 70f), new Vector2(0f, -50f));

        TextMeshProUGUI hint = NewText("Hint", PanelRoot.transform, "点击图片放大看描述 · 点击「掉落物」查看掉落列表", 24,
            TextAlignmentOptions.Center, new Color(textColor.r, textColor.g, textColor.b, 0.65f));
        SetRect(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(900f, 34f), new Vector2(0f, -100f));

        Button closeButton = NewButton("CloseButton", PanelRoot.transform, "×", 36, new Vector2(64f, 64f));
        SetRect(RT(closeButton.gameObject), new Vector2(1f, 1f), new Vector2(64f, 64f), new Vector2(-52f, -52f));
        closeButton.onClick.AddListener(ClosePanel);

        // 怪物网格
        GameObject scrollGo = NewUI("Scroll", PanelRoot.transform);
        Stretch(RT(scrollGo), 70f, 70f, 140f, 70f);

        ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 45f;

        GameObject viewport = NewUI("Viewport", scrollGo.transform);
        Stretch(RT(viewport));
        viewport.AddComponent<RectMask2D>();
        scroll.viewport = RT(viewport);

        GameObject contentGo = NewUI("Content", viewport.transform);
        content = RT(contentGo);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.offsetMin = Vector2.zero;
        content.offsetMax = Vector2.zero;

        GridLayoutGroup grid = contentGo.AddComponent<GridLayoutGroup>();
        grid.cellSize = slotSize;
        grid.spacing = new Vector2(22f, 22f);
        grid.padding = new RectOffset(12, 12, 12, 12);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = Mathf.Max(1, columns);
        grid.childAlignment = TextAnchor.UpperCenter;

        ContentSizeFitter fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = content;

        BuildDetailPopup();
        BuildDropsPopup();
    }

    private void BuildDetailPopup()
    {
        detailPopup = NewUI("DetailPopup", PanelRoot.transform);
        Stretch(RT(detailPopup));
        detailPopup.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

        GameObject box = NewUI("Box", detailPopup.transform);
        SetRect(RT(box), new Vector2(0.5f, 0.5f), new Vector2(760f, 690f), Vector2.zero);
        box.AddComponent<Image>().color = popupColor;

        Button closeButton = NewButton("Close", box.transform, "×", 32, new Vector2(56f, 56f));
        SetRect(RT(closeButton.gameObject), new Vector2(1f, 1f), new Vector2(56f, 56f), new Vector2(-16f, -16f));
        closeButton.onClick.AddListener(() => detailPopup.SetActive(false));

        GameObject iconGo = NewUI("Icon", box.transform);
        SetRect(RT(iconGo), new Vector2(0.5f, 1f), new Vector2(360f, 300f), new Vector2(0f, -30f));
        detailIcon = iconGo.AddComponent<Image>();
        detailIcon.preserveAspect = true;
        detailIcon.raycastTarget = false;

        detailName = NewText("Name", box.transform, "", 38, TextAlignmentOptions.Center, accentColor);
        SetRect(detailName.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 50f), new Vector2(0f, -345f));

        detailKills = NewText("Kills", box.transform, "", 24, TextAlignmentOptions.Center, textColor);
        SetRect(detailKills.rectTransform, new Vector2(0.5f, 1f), new Vector2(700f, 34f), new Vector2(0f, -400f));

        detailDescription = NewText("Description", box.transform, "", 26, TextAlignmentOptions.TopLeft, textColor);
        SetRect(detailDescription.rectTransform, new Vector2(0.5f, 1f), new Vector2(660f, 210f), new Vector2(0f, -450f));

        detailPopup.SetActive(false);
    }

    private void BuildDropsPopup()
    {
        dropsPopup = NewUI("DropsPopup", PanelRoot.transform);
        Stretch(RT(dropsPopup));
        dropsPopup.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.8f);

        GameObject box = NewUI("Box", dropsPopup.transform);
        SetRect(RT(box), new Vector2(0.5f, 0.5f), new Vector2(620f, 560f), Vector2.zero);
        box.AddComponent<Image>().color = popupColor;

        Button closeButton = NewButton("Close", box.transform, "×", 32, new Vector2(56f, 56f));
        SetRect(RT(closeButton.gameObject), new Vector2(1f, 1f), new Vector2(56f, 56f), new Vector2(-16f, -16f));
        closeButton.onClick.AddListener(() => dropsPopup.SetActive(false));

        dropsTitle = NewText("Title", box.transform, "", 34, TextAlignmentOptions.Center, accentColor);
        SetRect(dropsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(520f, 50f), new Vector2(0f, -30f));

        GameObject listGo = NewUI("List", box.transform);
        Stretch(RT(listGo), 30f, 30f, 100f, 30f);

        dropsList = RT(listGo);
        VerticalLayoutGroup layout = listGo.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = listGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        dropsPopup.SetActive(false);
    }

    /// <summary>没找到项目的 UI 时，自己建一个 Canvas，这样脚本单独用也能跑。</summary>
    private Transform CreateStandaloneCanvas()
    {
        GameObject canvasGo = new GameObject("BestiaryCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        if (FindObjectOfType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));

        return canvasGo.transform;
    }

    private void CheckFont()
    {
        if (font == null)
        {
            Debug.LogWarning("MonsterBestiaryUI：没有指定字体，图鉴里的中文可能显示不出来。" +
                             "请把 Assets/Graphics/Font 里的中文字体拖到 Font 字段上。", this);
            return;
        }

        // 动态字体（Atlas Population Mode = Dynamic）运行时会自己把缺的字形补进图集，
        // 即使 HasCharacter 现在返回 false 也能正常显示中文，只有静态字体缺字形才需要提醒。
        if (font.HasCharacter('怪') || font.atlasPopulationMode == AtlasPopulationMode.Dynamic)
            return;

        Debug.LogWarning("MonsterBestiaryUI：当前字体里没有中文字形，图鉴会显示成方块。" +
                         "请把 Assets/Graphics/Font 里的中文字体（例如 NotoSansCJKsc-DemiLight SDF）拖到 Font 字段上。", this);
    }

    #endregion

    #region 小工具

    private static GameObject NewUI(string _name, Transform _parent)
    {
        GameObject go = new GameObject(_name, typeof(RectTransform));
        go.transform.SetParent(_parent, false);
        return go;
    }

    private static RectTransform RT(GameObject _go) => _go.GetComponent<RectTransform>();

    private static void Stretch(RectTransform _rt, float _left = 0f, float _right = 0f, float _top = 0f, float _bottom = 0f)
    {
        _rt.anchorMin = Vector2.zero;
        _rt.anchorMax = Vector2.one;
        _rt.pivot = new Vector2(0.5f, 0.5f);
        _rt.offsetMin = new Vector2(_left, _bottom);
        _rt.offsetMax = new Vector2(-_right, -_top);
    }

    private static void SetRect(RectTransform _rt, Vector2 _anchor, Vector2 _size, Vector2 _offset)
    {
        _rt.anchorMin = _anchor;
        _rt.anchorMax = _anchor;
        _rt.pivot = _anchor;
        _rt.sizeDelta = _size;
        _rt.anchoredPosition = _offset;
    }

    private static void ClearChildren(Transform _parent)
    {
        if (_parent == null)
            return;

        for (int i = _parent.childCount - 1; i >= 0; i--)
            Destroy(_parent.GetChild(i).gameObject);
    }

    private TextMeshProUGUI NewText(string _name, Transform _parent, string _text, float _size, TextAlignmentOptions _align, Color _color)
    {
        GameObject go = NewUI(_name, _parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();

        text.text = _text;
        text.fontSize = _size;
        text.alignment = _align;
        text.color = _color;
        text.raycastTarget = false;

        if (font != null)
            text.font = font;

        return text;
    }

    private Image NewImage(string _name, Transform _parent, Sprite _sprite, Color _color)
    {
        GameObject go = NewUI(_name, _parent);
        Image image = go.AddComponent<Image>();

        image.sprite = _sprite;
        image.color = _color;
        image.preserveAspect = true;
        image.raycastTarget = false;

        return image;
    }

    private Button NewImageButton(string _name, Transform _parent, Sprite _sprite, Color _color)
    {
        GameObject go = NewUI(_name, _parent);
        Image image = go.AddComponent<Image>();

        image.sprite = _sprite;
        image.color = _color;
        image.preserveAspect = true;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        return button;
    }

    private Button NewButton(string _name, Transform _parent, string _label, float _fontSize, Vector2 _size)
    {
        GameObject go = NewUI(_name, _parent);
        Image image = go.AddComponent<Image>();
        image.color = new Color(1f, 1f, 1f, 0.12f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        LayoutElement layout = go.AddComponent<LayoutElement>();
        layout.preferredHeight = _size.y;
        layout.preferredWidth = _size.x;

        TextMeshProUGUI text = NewText("Label", go.transform, _label, _fontSize, TextAlignmentOptions.Center, textColor);
        Stretch(text.rectTransform);

        return button;
    }

    #endregion

    #region 编辑器里的字体自动填充

    private void Reset()
    {
        TryAssignChineseFont();

        // 在编辑器里挂上这个脚本时，顺手把数据组件也加上，方便直接把怪物列表拖进去
        if (bestiary == null)
        {
            bestiary = GetComponent<MonsterBestiary>();

            if (bestiary == null)
                bestiary = gameObject.AddComponent<MonsterBestiary>();
        }
    }

    private void OnValidate()
    {
        if (font == null)
            TryAssignChineseFont();
    }

    private void TryAssignChineseFont()
    {
#if UNITY_EDITOR
        TMP_FontAsset dynamicFallback = null;

        foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            TMP_FontAsset candidate = UnityEditor.AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));

            if (candidate == null)
                continue;

            if (candidate.HasCharacter('怪'))
            {
                font = candidate;
                return;
            }

            // 动态字体可以运行时补字形，名字看着像中文字体的先当备选
            if (dynamicFallback == null &&
                candidate.atlasPopulationMode == AtlasPopulationMode.Dynamic &&
                LooksLikeCjkFont(candidate.name))
            {
                dynamicFallback = candidate;
            }
        }

        if (dynamicFallback != null)
            font = dynamicFallback;
#endif
    }

    private static bool LooksLikeCjkFont(string _name)
    {
        if (string.IsNullOrEmpty(_name))
            return false;

        string name = _name.ToLowerInvariant();

        return name.Contains("cjk") || name.Contains("noto") || name.Contains("han")
            || name.Contains("xinwei") || name.Contains("song") || name.Contains("hei");
    }

    #endregion
}
