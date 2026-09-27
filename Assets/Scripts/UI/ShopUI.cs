using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 商店界面（预制体 Assets/Prefabs/UI/ShopPanel.prefab，带 UI_SelfManagedPanel 标记）。
/// 左边是商品列表（图标 + 名称 + 价格），选中后下方显示购买数量 [－] N [＋]（单击 ±1，长按持续增减）和「购买」。
/// 打开方式：商店 NPC 的对话里选「进入商店」（actionId = open_shop）。
/// </summary>
public class ShopUI : MonoBehaviour
{
    public const string OpenActionId = "open_shop";

    public static ShopUI Instance;

    [Header("界面引用")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private ShopSlot slotPrefab;
    [SerializeField] private GameObject emptyHint;
    [SerializeField] private Button closeButton;

    [Header("下方购买栏")]
    [SerializeField] private GameObject buyBarRoot;
    [SerializeField] private Image selectedIcon;
    [SerializeField] private TextMeshProUGUI selectedName;
    [SerializeField] private TextMeshProUGUI selectedPrice;
    [SerializeField] private TextMeshProUGUI quantityText;
    [SerializeField] private Button minusButton;
    [SerializeField] private Button plusButton;
    [SerializeField] private Button buyButton;
    [SerializeField] private TextMeshProUGUI buyButtonLabel;

    [Header("长按加减")]
    [SerializeField] private float holdDelay = 0.35f;
    [SerializeField] private float holdInterval = 0.05f;

    [Header("按键")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private ShopKeeper shop;
    private ShopEntry selected;
    private int quantity = 1;
    private bool pausedByUs;

    private float holdTimer;
    private int holdDirection;

    public bool IsOpen => windowRoot != null && windowRoot.activeSelf;

    #region 生命周期

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;

        UI_SelfManagedPanel.OnOtherMenuOpened += CloseIfOpen;

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (marketplaceSafe(plusButton)) plusButton.onClick.AddListener(() => ChangeQuantity(1));
        if (marketplaceSafe(minusButton)) minusButton.onClick.AddListener(() => ChangeQuantity(-1));
        if (marketplaceSafe(buyButton)) buyButton.onClick.AddListener(Buy);

        if (windowRoot != null) windowRoot.SetActive(false);
    }

    private static bool marketplaceSafe(Button _button) => _button != null;

    private void OnDestroy()
    {
        UI_SelfManagedPanel.OnOtherMenuOpened -= CloseIfOpen;

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (!IsOpen) return;

        if (Input.GetKeyDown(closeKey)) { Close(); return; }

        UpdateHold();
    }

    private void CloseIfOpen()
    {
        if (IsOpen) Close();
    }

    public void Open(ShopKeeper _shop)
    {
        if (windowRoot == null) return;

        shop = _shop;
        selected = null;
        quantity = 1;

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        windowRoot.SetActive(true);
        SetPaused(true);
        Refresh();
    }

    public void Close()
    {
        if (windowRoot == null) return;

        windowRoot.SetActive(false);
        SetPaused(false);
    }

    private void SetPaused(bool _paused)
    {
        if (GameManager.instance == null) return;

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

    #region 列表与选中

    public void Refresh()
    {
        if (listRoot == null || slotPrefab == null) return;

        ClearChildren(listRoot);

        List<ShopEntry> goods = new List<ShopEntry>();

        if (shop != null && shop.Goods != null)
        {
            foreach (ShopEntry entry in shop.Goods)
                if (entry != null && entry.item != null) goods.Add(entry);
        }

        if (emptyHint != null) emptyHint.SetActive(goods.Count == 0);

        if (titleText != null) titleText.text = shop != null ? shop.ShopName : "商店";

        foreach (ShopEntry entry in goods)
        {
            ShopEntry captured = entry;
            ShopSlot slot = Instantiate(slotPrefab, listRoot);
            slot.Setup(entry, () => Select(captured));
        }

        if (selected == null || !goods.Contains(selected))
            selected = goods.Count > 0 ? goods[0] : null;

        UpdateSelectionVisuals();
        UpdateBuyBar();
    }

    private void Select(ShopEntry _entry)
    {
        selected = _entry;
        quantity = 1;
        UpdateSelectionVisuals();
        UpdateBuyBar();
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < listRoot.childCount; i++)
        {
            ShopSlot slot = listRoot.GetChild(i).GetComponent<ShopSlot>();
            if (slot != null) slot.SetSelected(slot.Entry == selected);
        }
    }

    private void UpdateBuyBar()
    {
        bool hasSelection = selected != null && selected.item != null;

        if (buyBarRoot != null) buyBarRoot.SetActive(hasSelection);
        if (currencyText != null) currencyText.text = $"金币：{GetCurrency()}";

        if (!hasSelection) return;

        quantity = Mathf.Max(1, quantity);

        if (selectedIcon != null)
        {
            selectedIcon.sprite = selected.item.icon;
            selectedIcon.enabled = selected.item.icon != null;
        }

        if (selectedName != null) selectedName.text = GetDisplayName(selected.item);
        if (selectedPrice != null) selectedPrice.text = $"单价 {selected.price}";
        if (quantityText != null) quantityText.text = quantity.ToString();

        int total = selected.price * quantity;

        if (buyButtonLabel != null) buyButtonLabel.text = $"购买（{total}）";
        if (buyButton != null) buyButton.interactable = GetCurrency() >= total && total > 0;
    }

    private string GetDisplayName(ItemData _item)
    {
        if (_item is ItemData_Equipment equipment && EquipmentLevelManager.Instance != null)
            return EquipmentLevelManager.Instance.GetDisplayName(equipment);

        return _item.itemName;
    }

    private static int GetCurrency() => PlayerManager.instance != null ? PlayerManager.instance.GetCurrency() : 0;

    #endregion

    #region 数量加减（支持长按）

    private void ChangeQuantity(int _delta)
    {
        quantity = Mathf.Max(1, quantity + _delta);
        UpdateBuyBar();
    }

    private void UpdateHold()
    {
        if (holdDirection == 0) return;

        holdTimer -= Time.unscaledDeltaTime;

        if (holdTimer > 0f) return;

        holdTimer = holdInterval;
        ChangeQuantity(holdDirection * 1);
    }

    /// <summary>按住时开始持续增减（按钮的 PointerDown 里调用思路：按住超过 holdDelay 后每个 holdInterval 变一次）。</summary>
    public void BeginHold(int _direction)
    {
        holdDirection = _direction;
        holdTimer = holdDelay;
    }

    public void EndHold() => holdDirection = 0;

    #endregion

    #region 购买

    public void Buy()
    {
        if (selected == null || selected.item == null) return;

        int total = selected.price * quantity;

        if (total <= 0 || PlayerManager.instance == null || Inventory.instance == null) return;

        if (GetCurrency() < total)
        {
            Debug.Log("金币不够");
            return;
        }

        if (!PlayerManager.instance.HaveEnoughCurrency(total))
            return;

        for (int i = 0; i < quantity; i++)
            Inventory.instance.AddItem(selected.item);

        Debug.Log($"购买成功：{GetDisplayName(selected.item)} ×{quantity}，花费 {total}");

        quantity = 1;
        Refresh();
    }

    private static void ClearChildren(Transform _parent)
    {
        if (_parent == null) return;

        for (int i = _parent.childCount - 1; i >= 0; i--)
        {
            Transform child = _parent.GetChild(i);
            child.SetParent(null, false);
            Destroy(child.gameObject);
        }
    }

    #endregion
}
