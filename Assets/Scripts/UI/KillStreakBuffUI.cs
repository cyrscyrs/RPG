using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 连续杀敌加速的头顶显示：一阶 1 个图标、二阶 2 个、三阶 3 个，下面是剩余时间条。
///
/// 预制体：Assets/Resources/KillStreak/KillStreakBuffUI.prefab
/// 改外观（图标大小 / 间距 / 颜色 / 贴图）直接编辑那个预制体，运行时用的就是它。
///
/// 也可以在场景里自己放一个实例调试；管理器会优先用场景里的那一个。
/// 图标引用没接全时会按名字找子物体，找不到才在运行时补建，所以删掉子物体也不会报错。
/// </summary>
public class KillStreakBuffUI : MonoBehaviour
{
    public const string IconNamePrefix = "Icon_";
    public const string BarBackName = "BarBack";
    public const string BarFillName = "BarFill";

    public const string SortingLayerName = "Player";
    public const int SortingOrder = 100;

    [Header("位置")]
    [Tooltip("玩家头顶再往上加的高度（世界单位）")]
    [SerializeField] private float extraHeight = 0.45f;

    [Tooltip("> 0 时直接把这个值当玩家头顶高度，0 = 自动按玩家的贴图高度算")]
    [SerializeField] private float manualHeadHeight = 0f;

    [Header("尺寸（世界单位）")]
    [Tooltip("单个图标的大小")]
    [SerializeField] private float iconSize = 0.4f;

    [Tooltip("图标之间的间距")]
    [SerializeField] private float iconSpacing = 0.07f;

    [Tooltip("剩余时间条的高度")]
    [SerializeField] private float barHeight = 0.06f;

    [Tooltip("时间条和图标行之间的间距")]
    [SerializeField] private float barGap = 0.08f;

    [Header("外观")]
    [Tooltip("图标贴图，留空就用 KillStreakIconArt 现画一张闪电图标")]
    [SerializeField] private Sprite iconSprite;

    [Tooltip("时间条底图，留空就用现画的纯白图")]
    [SerializeField] private Sprite barSprite;

    [SerializeField] private Color iconColor = Color.white;
    [SerializeField] private Color barBackColor = new Color(0f, 0f, 0f, 0.6f);

    [Tooltip("一 / 二 / 三阶的时间条颜色")]
    [SerializeField] private Color[] barColors =
    {
        new Color(1f, 0.86f, 0.33f, 1f),
        new Color(1f, 0.60f, 0.18f, 1f),
        new Color(1f, 0.34f, 0.24f, 1f),
    };

    [Header("动画")]
    [Tooltip("升阶时图标弹一下的幅度，0 = 不弹")]
    [SerializeField] private float pulseScale = 0.22f;

    [Header("引用（预制体里已经接好，一般不用动）")]
    [SerializeField] private Image[] icons = new Image[3];
    [SerializeField] private Image barBack;
    [SerializeField] private Image barFill;

    private KillStreakBuffManager manager;
    private Player player;

    private RectTransform root;
    private float baseY;
    private int shownStage = -1;
    private float pulse;

    // 上一次真正写进 Image 的外观值。只有变了才重写，免得每帧把 Canvas 刷脏
    private Sprite appliedIconSprite;
    private Sprite appliedBarSprite;
    private Color appliedIconColor;
    private float appliedIconSize = -1f;
    private float appliedBarHeight = -1f;
    private bool appearanceApplied;

    public void SetManager(KillStreakBuffManager _manager)
    {
        manager = _manager;
    }

    private void Awake()
    {
        root = GetComponent<RectTransform>();

        // 运行时兜底建出来的这个没有 Canvas，这里补一个；预制体上的 Canvas 保持你调好的设置
        if (GetComponent<Canvas>() == null)
            ApplyDefaultCanvasSettings();

        if (!HasLayout)
            BuildLayout();
    }

    /// <summary>把画布调成适合挂在玩家头顶的世界空间画布。</summary>
    public void ApplyDefaultCanvasSettings()
    {
        Canvas canvas = GetComponent<Canvas>();

        if (canvas == null)
            canvas = gameObject.AddComponent<Canvas>();

        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingLayerName = SortingLayerName;
        canvas.sortingOrder = SortingOrder;
    }

    private void LateUpdate()
    {
        if (manager == null)
            manager = KillStreakBuffManager.instance;

        if (manager == null)
            return;

        if (!HasLayout)
            BuildLayout();

        // 预制体上改了图标贴图 / 颜色 / 大小，这里立刻同步到各个 Image
        ApplyAppearanceIfChanged();

        if (player == null && PlayerManager.instance != null)
            player = PlayerManager.instance.player;

        if (player == null)
        {
            HideAll();
            return;
        }

        FollowPlayer();
        RefreshIcons();
    }

    /// <summary>图标和时间条的引用是不是都接上了。</summary>
    public bool HasLayout =>
        icons != null && icons.Length > 0 && icons[0] != null && barBack != null && barFill != null;

    #region 搭建

    /// <summary>
    /// 按当前的尺寸设置把图标和时间条建出来（缺哪个补哪个，已有的按名字复用）。
    /// 编辑器生成预制体时会调这个方法，运行时发现引用没接上也会调。
    /// </summary>
    public void BuildLayout()
    {
        int count = Mathf.Max(3, icons == null ? 0 : icons.Length);
        Image[] built = new Image[count];

        for (int i = 0; i < count; i++)
            built[i] = GetOrCreateImage(IconNamePrefix + i);

        icons = built;

        barBack = GetOrCreateImage(BarBackName);
        barFill = GetOrCreateImage(BarFillName);

        barFill.type = Image.Type.Filled;
        barFill.fillMethod = Image.FillMethod.Horizontal;
        barFill.fillOrigin = (int)Image.OriginHorizontal.Left;
        barFill.fillAmount = 1f;

        ApplyAppearanceNow();
    }

    /// <summary>外观变了才重写；没变就什么都不做，不会每帧把 Canvas 刷脏。</summary>
    private void ApplyAppearanceIfChanged()
    {
        if (icons == null || icons.Length == 0)
            return;

        bool changed =
            appliedIconSprite != iconSprite ||
            appliedBarSprite != barSprite ||
            appliedIconColor != iconColor ||
            !Mathf.Approximately(appliedIconSize, iconSize) ||
            !Mathf.Approximately(appliedBarHeight, barHeight);

        if (!appearanceApplied || changed)
            ApplyAppearanceNow();
    }

    /// <summary>
    /// 把 iconSprite / barSprite / iconColor / 图标大小真正写进各个 Image。
    /// 改组件上的字段不会自动改子物体（图是画在子物体上的），所以这里负责同步过去。
    /// </summary>
    [ContextMenu("把外观应用到图标子物体")]
    public void ApplyAppearanceNow()
    {
        RefreshSprites();
        ApplyStaticColors();
        ApplySizes();

        appliedIconSprite = iconSprite;
        appliedBarSprite = barSprite;
        appliedIconColor = iconColor;
        appliedIconSize = iconSize;
        appliedBarHeight = barHeight;
        appearanceApplied = true;
    }

    private Image GetOrCreateImage(string _name)
    {
        Transform child = transform.Find(_name);
        GameObject go;

        if (child == null)
        {
            go = new GameObject(_name, typeof(RectTransform));
            go.transform.SetParent(transform, false);
        }
        else
        {
            go = child.gameObject;
        }

        RectTransform rt = go.GetComponent<RectTransform>();

        if (rt == null)
            rt = go.AddComponent<RectTransform>();

        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        Image image = go.GetComponent<Image>();

        if (image == null)
            image = go.AddComponent<Image>();

        image.raycastTarget = false;

        return image;
    }

    private void RefreshSprites()
    {
        if (iconSprite == null)
            iconSprite = KillStreakIconArt.CreateIconSprite();

        if (barSprite == null)
            barSprite = KillStreakIconArt.CreateBarSprite();

        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null)
                continue;

            icons[i].sprite = iconSprite;
            icons[i].preserveAspect = true;
        }

        if (barBack != null)
            barBack.sprite = barSprite;

        if (barFill != null)
            barFill.sprite = barSprite;
    }

    private void ApplyStaticColors()
    {
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] != null)
                icons[i].color = iconColor;
        }

        if (barBack != null)
            barBack.color = barBackColor;

        if (barFill != null)
            barFill.color = BarColor(Mathf.Max(1, shownStage));
    }

    /// <summary>固定尺寸的那几项（图标 / 时间条本身的大小），位置在 RefreshIcons 里排。</summary>
    private void ApplySizes()
    {
        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] != null)
                icons[i].rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
        }

        if (barBack != null)
            barBack.rectTransform.sizeDelta = new Vector2(iconSize, barHeight);

        if (barFill != null)
            barFill.rectTransform.sizeDelta = new Vector2(iconSize, barHeight);
    }

    private void FollowPlayer()
    {
        Vector3 position = player.transform.position;
        transform.position = new Vector3(position.x, position.y + baseY, 0f);
    }

    /// <summary>算出图标行应该离玩家中心多高。</summary>
    private float ResolveHeadHeight()
    {
        float head = manualHeadHeight;

        if (head <= 0f)
        {
            SpriteRenderer sr = player.GetComponentInChildren<SpriteRenderer>();

            head = sr != null && sr.sprite != null
                ? sr.bounds.extents.y
                : 1f;
        }

        // 图标行本身还有一半高度，时间条挂在下面，所以再往上让一点
        float rowHalf = (iconSize + barGap + barHeight) * 0.5f;

        return head + extraHeight + rowHalf;
    }

    #endregion

    #region 刷新

    private void RefreshIcons()
    {
        if (baseY <= 0f)
            baseY = ResolveHeadHeight();

        int stage = Mathf.Clamp(manager.Stage, 0, icons.Length);

        if (stage <= 0)
        {
            HideAll();
            shownStage = 0;
            return;
        }

        if (stage != shownStage)
        {
            // 只有升阶才弹一下，掉档不用
            if (stage > shownStage && shownStage >= 0)
                pulse = 1f;

            shownStage = stage;
        }

        float dt = Time.unscaledDeltaTime;
        pulse = Mathf.Max(0f, pulse - dt * 3.5f);

        float popScale = 1f + pulseScale * pulse * Mathf.Sin(pulse * Mathf.PI);
        transform.localScale = new Vector3(popScale, popScale, 1f);

        // 图标行居中排列
        float totalWidth = stage * iconSize + (stage - 1) * iconSpacing;

        for (int i = 0; i < icons.Length; i++)
        {
            if (icons[i] == null)
                continue;

            bool active = i < stage;

            icons[i].gameObject.SetActive(active);

            if (!active)
                continue;

            float x = -totalWidth * 0.5f + iconSize * 0.5f + i * (iconSize + iconSpacing);
            icons[i].rectTransform.anchoredPosition = new Vector2(x, 0f);
        }

        // 时间条：宽度跟图标行一样，挂在图标行下面
        float barWidth = Mathf.Max(iconSize, totalWidth);
        float barY = -(iconSize * 0.5f + barGap + barHeight * 0.5f);

        if (barBack != null)
        {
            barBack.gameObject.SetActive(true);
            barBack.rectTransform.sizeDelta = new Vector2(barWidth, barHeight);
            barBack.rectTransform.anchoredPosition = new Vector2(0f, barY);
        }

        if (barFill != null)
        {
            barFill.gameObject.SetActive(true);
            barFill.rectTransform.sizeDelta = new Vector2(barWidth, barHeight);
            barFill.rectTransform.anchoredPosition = new Vector2(0f, barY);
            barFill.fillAmount = manager.BuffNormalized;
            barFill.color = BarColor(stage);
        }
    }

    private void HideAll()
    {
        if (icons != null)
        {
            for (int i = 0; i < icons.Length; i++)
            {
                if (icons[i] != null)
                    icons[i].gameObject.SetActive(false);
            }
        }

        if (barBack != null)
            barBack.gameObject.SetActive(false);

        if (barFill != null)
            barFill.gameObject.SetActive(false);

        // 收起来的时候别留个放大的残影
        transform.localScale = Vector3.one;
    }

    private Color BarColor(int _stage)
    {
        if (barColors == null || barColors.Length == 0)
            return new Color(1f, 0.86f, 0.33f, 1f);

        return barColors[Mathf.Clamp(_stage - 1, 0, barColors.Length - 1)];
    }

    #endregion
}
