using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 怪物图鉴界面（预制体版）。按 L 打开：每只怪显示图片、名字、累计击杀数和「掉落物」按钮，
/// 点图片放大看描述，点「掉落物」看掉落列表。
///
/// 两种模式：
/// - 挂在 Canvas 的 UI 物体下面（集成模式）：由 UI.cs 的菜单系统开关面板根节点，按一次 L 就能开；
/// - 挂在别的物体下（独立模式）：脚本自己处理 L / Esc 和暂停。
/// 根节点永远保持激活（击杀统计、L 键注册都靠它），真正显示/隐藏的是 Window 子节点。
/// </summary>
public class MonsterBestiaryUI : MonoBehaviour
{
    [Header("数据与菜单（一般留空自动找）")]
    [SerializeField] private MonsterBestiary bestiary;
    [SerializeField] private UI uiManager;

    [Header("界面引用（预制体里已经接好）")]
    [Tooltip("真正显示的面板内容节点；根节点保持激活，只开关它")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private RectTransform content;
    [SerializeField] private MonsterBestiarySlot slotPrefab;
    [Tooltip("一只怪物都没有时显示的提示，可以留空")]
    [SerializeField] private GameObject emptyHint;

    [Header("放大详情")]
    [SerializeField] private GameObject detailPopup;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TextMeshProUGUI detailName;
    [SerializeField] private TextMeshProUGUI detailKills;
    [SerializeField] private TextMeshProUGUI detailDescription;

    [Header("掉落物弹窗")]
    [SerializeField] private GameObject dropsPopup;
    [SerializeField] private TextMeshProUGUI dropsTitle;
    [SerializeField] private RectTransform dropsList;
    [SerializeField] private MonsterBestiaryDropRow dropRowPrefab;

    [Header("按键（独立模式才由本脚本处理）")]
    [SerializeField] private KeyCode toggleKey = KeyCode.L;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    [Tooltip("面板开着时每隔多少秒刷新一次列表（击杀数在面板外变了也能刷出来）")]
    [SerializeField] private float autoRefreshInterval = 0.5f;

    /// <summary>交给 UI.cs 开关的就是面板根节点（不是 Window 子节点），否则会出现要按两次 L 才打开。</summary>
    public GameObject PanelRoot => gameObject;

    private bool independent;
    private bool wasOpen;
    private bool pausedByUs;
    private float refreshTimer;

    private bool IsOpen
    {
        get
        {
            if (independent)
                return windowRoot != null ? windowRoot.activeSelf : gameObject.activeSelf;

            return gameObject.activeSelf;
        }
    }

    #region 生命周期

    private void Awake()
    {
        if (bestiary == null)
            bestiary = GetComponent<MonsterBestiary>();

        if (bestiary == null)
            bestiary = FindObjectOfType<MonsterBestiary>();

        if (bestiary == null)
            bestiary = gameObject.AddComponent<MonsterBestiary>();

        if (uiManager == null)
            uiManager = FindObjectOfType<UI>();

        independent = uiManager == null || transform.parent != uiManager.transform;

        if (!independent)
            uiManager.SetBestiaryPanel(PanelRoot);

        // 根节点保持激活，只把显示层关掉
        SetWindowVisible(false);
        wasOpen = false;
        refreshTimer = 0f;
    }

    private void Update()
    {
        bool open = IsOpen;

        if (open != wasOpen)
        {
            wasOpen = open;

            if (open)
            {
                SetWindowVisible(true);
                refreshTimer = 0f;
                Refresh();
            }
            else
            {
                ClosePopups();
            }
        }

        // 开着的时候定期重建列表：在面板外打死怪，计数也能自动刷出来。
        // 不依赖「刚打开」那一瞬的检测，避免根节点被别的逻辑一直开着时只刷新一次。
        if (open)
        {
            refreshTimer -= Time.unscaledDeltaTime;

            if (refreshTimer <= 0f)
            {
                refreshTimer = Mathf.Max(0.1f, autoRefreshInterval);
                Refresh();
            }
        }

        if (!independent)
            return;

        if (Input.GetKeyDown(toggleKey))
            Toggle();

        if (open && Input.GetKeyDown(closeKey))
            ClosePanel();
    }

    private void SetWindowVisible(bool _visible)
    {
        if (windowRoot != null)
            windowRoot.SetActive(_visible);
        else
            gameObject.SetActive(_visible);
    }

    #endregion

    #region 打开 / 关闭

    public void Toggle()
    {
        if (IsOpen)
            ClosePanel();
        else
            OpenPanel();
    }

    public void OpenPanel()
    {
        if (independent)
        {
            SetWindowVisible(true);
            SetPaused(true);
            Refresh();
            return;
        }

        gameObject.SetActive(true);
        SetWindowVisible(true);
        Refresh();
    }

    /// <summary>关闭按钮用。</summary>
    public void ClosePanel()
    {
        ClosePopups();

        if (independent)
        {
            SetWindowVisible(false);
            SetPaused(false);
            return;
        }

        if (uiManager != null)
            uiManager.SwitchWithKeyTo(PanelRoot);
        else
            gameObject.SetActive(false);
    }

    /// <summary>放大详情弹窗的关闭按钮（预制体里已经接好）。</summary>
    public void CloseDetailPopup()
    {
        if (detailPopup != null)
            detailPopup.SetActive(false);
    }

    /// <summary>掉落物弹窗的关闭按钮（预制体里已经接好）。</summary>
    public void CloseDropsPopup()
    {
        if (dropsPopup != null)
            dropsPopup.SetActive(false);
    }

    private void ClosePopups()
    {
        CloseDetailPopup();
        CloseDropsPopup();

        SetPaused(false);
    }

    private void SetPaused(bool _paused)
    {
        if (!independent || GameManager.instance == null)
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

    #region 填充列表

    private void Refresh()
    {
        ClosePopups();

        if (content == null || slotPrefab == null)
        {
            Debug.LogWarning("MonsterBestiaryUI：界面引用不全（Content / Slot Prefab 为空）" +
                             "，请用菜单「Tools/怪物图鉴/生成图鉴预制体」重新生成。", this);
            return;
        }

        ClearChildren(content);

        if (bestiary == null)
            return;

        if (emptyHint != null)
            emptyHint.SetActive(bestiary.MonsterCount == 0);

        for (int i = 0; i < bestiary.MonsterCount; i++)
        {
            MonsterEntry entry = bestiary.GetMonster(i);

            if (entry == null)
                continue;

            MonsterBestiarySlot slot = Instantiate(slotPrefab, content);
            int killCount = bestiary.GetKills(entry);
            bool hidden = entry.hideUntilFirstKill && killCount <= 0;
            MonsterEntry captured = entry;

            slot.Setup(entry, killCount, hidden, MonsterBestiary.GetIcon(entry),
                () => OpenDetail(captured),
                () => OpenDrops(captured));
        }
    }

    private void OpenDetail(MonsterEntry _entry)
    {
        if (detailPopup == null)
            return;

        bool hidden = _entry.hideUntilFirstKill && bestiary.GetKills(_entry) <= 0;

        if (detailName != null)
            detailName.text = hidden ? "？？？" : MonsterBestiary.GetDisplayName(_entry);

        if (detailKills != null)
            detailKills.text = hidden ? "累计击杀：？" : $"累计击杀：{bestiary.GetKills(_entry)}";

        if (detailIcon != null)
        {
            detailIcon.sprite = MonsterBestiary.GetIcon(_entry);
            detailIcon.color = hidden ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }

        if (detailDescription != null)
        {
            if (hidden)
                detailDescription.text = "还没遇到过这种怪物。";
            else if (string.IsNullOrEmpty(_entry.description))
                detailDescription.text = "（还没有写描述）";
            else
                detailDescription.text = _entry.description;
        }

        detailPopup.SetActive(true);
    }

    private void OpenDrops(MonsterEntry _entry)
    {
        if (dropsPopup == null || dropsList == null || dropRowPrefab == null)
            return;

        bool hidden = _entry.hideUntilFirstKill && bestiary.GetKills(_entry) <= 0;

        if (dropsTitle != null)
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
        MonsterBestiaryDropRow row = Instantiate(dropRowPrefab, dropsList);
        row.Setup(_icon, _name, _chance);
    }

    private static void ClearChildren(Transform _parent)
    {
        if (_parent == null)
            return;

        for (int i = _parent.childCount - 1; i >= 0; i--)
        {
            Transform child = _parent.GetChild(i);
            child.SetParent(null, false);   // Destroy 是帧尾生效，先脱离父物体，避免同帧被重复算进去
            Destroy(child.gameObject);
        }
    }

    #endregion
}
