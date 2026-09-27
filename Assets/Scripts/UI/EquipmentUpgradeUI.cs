using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 装备强化界面（预制体 Assets/Prefabs/UI/EquipmentUpgradePanel.prefab）：
/// 左边列出「已装备的 + 装备库里的」装备，右边显示强化预览和需要的材料/金币，按「强化」就能升级。
///
/// 打开方式：对话里选到带 actionId = "open_equipment_upgrade" 的选项就会打开（铁匠的默认对话里已经配好）。
/// 强化消耗按「装备部位」统一配置（EquipmentGrowthConfig 里每个部位一条），数量随目标等级增加。
/// </summary>
public class EquipmentUpgradeUI : MonoBehaviour
{
    public const string OpenActionId = "open_equipment_upgrade";

    [Header("界面引用")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private EquipmentUpgradeSlot slotPrefab;
    [Tooltip("一件可强化装备都没有时显示的提示，可以留空")]
    [SerializeField] private GameObject emptyHint;
    [SerializeField] private Button closeButton;

    [Header("右侧详情")]
    [SerializeField] private GameObject detailRoot;
    [SerializeField] private Image detailIcon;
    [SerializeField] private TextMeshProUGUI detailName;
    [SerializeField] private TextMeshProUGUI detailLevel;
    [SerializeField] private TextMeshProUGUI previewText;
    [Tooltip("强化需求列表的标题（不够什么东西也会写在这里）")]
    [SerializeField] private TextMeshProUGUI requirementText;

    [Tooltip("强化需求列表的父节点：每种材料一行，最后一行是金币")]
    [SerializeField] private RectTransform requirementListRoot;
    [SerializeField] private EquipmentUpgradeRequirementRow requirementRowPrefab;
    [SerializeField] private Button upgradeButton;
    [SerializeField] private TextMeshProUGUI upgradeButtonLabel;

    [Header("按键")]
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private ItemData_Equipment selected;
    private bool pausedByUs;

    private EquipmentLevelManager Manager => EquipmentLevelManager.Instance;

    public bool IsOpen => windowRoot != null && windowRoot.activeSelf;

    #region 生命周期

    private void Awake()
    {
        DialogueManager.OnAction += HandleDialogueAction;

        // 玩家去开别的菜单时把自己收起来
        UI_SelfManagedPanel.OnOtherMenuOpened += CloseIfOpen;

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (upgradeButton != null)
            upgradeButton.onClick.AddListener(OnUpgradeClicked);

        if (windowRoot != null)
            windowRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        DialogueManager.OnAction -= HandleDialogueAction;
        UI_SelfManagedPanel.OnOtherMenuOpened -= CloseIfOpen;
    }

    private void CloseIfOpen()
    {
        if (IsOpen)
            Close();
    }

    private void Update()
    {
        if (IsOpen && Input.GetKeyDown(closeKey))
            Close();
    }

    private void HandleDialogueAction(string _actionId)
    {
        if (_actionId == OpenActionId)
            Open();
    }

    public void Open()
    {
        if (windowRoot == null)
            return;

        // 万一被别的代码关掉了根节点，这里顺手恢复（挂了 UI_SelfManagedPanel 就不会发生）
        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        windowRoot.SetActive(true);
        SetPaused(true);
        Refresh();
    }

    public void Close()
    {
        if (windowRoot == null)
            return;

        windowRoot.SetActive(false);
        SetPaused(false);
    }

    private void SetPaused(bool _paused)
    {
        if (GameManager.instance == null)
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

    #region 列表

    public void Refresh()
    {
        if (listRoot == null || slotPrefab == null || Manager == null)
        {
            Debug.LogWarning("EquipmentUpgradeUI：界面引用不全，或者场景里没有 EquipmentLevelManager。", this);
            return;
        }

        ClearChildren(listRoot);

        List<ItemData_Equipment> items = CollectItems();

        if (emptyHint != null)
            emptyHint.SetActive(items.Count == 0);

        foreach (ItemData_Equipment item in items)
        {
            EquipmentUpgradeSlot slot = Instantiate(slotPrefab, listRoot);
            ItemData_Equipment captured = item;

            slot.Setup(item, Manager.GetDisplayName(item), Manager.GetLevel(item), Manager.MaxLevel,
                IsEquipped(item), () => SelectItem(captured));
        }

        if (selected == null || !items.Contains(selected))
            selected = items.Count > 0 ? items[0] : null;

        UpdateSelectionVisuals();
        UpdateDetail();
    }

    /// <summary>已装备的 + 装备库里（背包中的）装备。</summary>
    private List<ItemData_Equipment> CollectItems()
    {
        List<ItemData_Equipment> result = new List<ItemData_Equipment>();

        if (Inventory.instance == null)
            return result;

        foreach (InventoryItem entry in Inventory.instance.GetEquipmentList())
        {
            if (entry != null && entry.data is ItemData_Equipment equipment && !result.Contains(equipment))
                result.Add(equipment);
        }

        foreach (InventoryItem entry in Inventory.instance.inventory)
        {
            if (entry != null && entry.data is ItemData_Equipment equipment && !result.Contains(equipment))
                result.Add(equipment);
        }

        return result;
    }

    private bool IsEquipped(ItemData_Equipment _item)
        => Inventory.instance != null && _item != null && Inventory.instance.GetEquipment(_item.equipmentType) == _item;

    private void SelectItem(ItemData_Equipment _item)
    {
        selected = _item;
        UpdateSelectionVisuals();
        UpdateDetail();
    }

    private void UpdateSelectionVisuals()
    {
        for (int i = 0; i < listRoot.childCount; i++)
        {
            EquipmentUpgradeSlot slot = listRoot.GetChild(i).GetComponent<EquipmentUpgradeSlot>();

            if (slot != null)
                slot.SetSelected(slot.Item == selected);
        }
    }

    #endregion

    #region 详情 / 预览 / 强化

    private void UpdateDetail()
    {
        bool hasSelection = selected != null && Manager != null;

        if (detailRoot != null)
            detailRoot.SetActive(hasSelection);

        if (!hasSelection)
            return;

        int level = Manager.GetLevel(selected);
        bool maxed = level >= Manager.MaxLevel;

        if (detailName != null)
            detailName.text = Manager.GetDisplayName(selected);

        if (detailLevel != null)
            detailLevel.text = $"等级 {level} / {Manager.MaxLevel}" + (IsEquipped(selected) ? "　（已装备）" : "");

        if (detailIcon != null)
        {
            detailIcon.sprite = selected.icon;
            detailIcon.enabled = selected.icon != null;
        }

        if (previewText != null)
            previewText.text = maxed ? "已经是最高等级了。" : BuildPreview(selected, level);

        UpdateRequirement(maxed);
    }

    /// <summary>列出这件装备提供的属性：当前值 → 下一级值。</summary>
    private string BuildPreview(ItemData_Equipment _item, int _level)
    {
        List<string> lines = new List<string>();

        AddPreviewLine(lines, "力量", _item.strength, _item, _level);
        AddPreviewLine(lines, "敏捷", _item.agility, _item, _level);
        AddPreviewLine(lines, "智力", _item.intelligence, _item, _level);
        AddPreviewLine(lines, "体质", _item.vitality, _item, _level);

        AddPreviewLine(lines, "攻击力", _item.damage, _item, _level);
        AddPreviewLine(lines, "暴击", _item.critChance, _item, _level);
        AddPreviewLine(lines, "爆伤", _item.critPower, _item, _level);

        AddPreviewLine(lines, "生命", _item.health, _item, _level);
        AddPreviewLine(lines, "护甲", _item.armor, _item, _level);
        AddPreviewLine(lines, "闪避", _item.evasion, _item, _level);
        AddPreviewLine(lines, "魔抗", _item.magicResistance, _item, _level);

        AddPreviewLine(lines, "火伤", _item.fireDamage, _item, _level);
        AddPreviewLine(lines, "冰伤", _item.iceDamage, _item, _level);
        AddPreviewLine(lines, "雷伤", _item.lightningDamage, _item, _level);

        return lines.Count > 0 ? string.Join("\n", lines) : "这件装备没有属性成长。";
    }

    private void AddPreviewLine(List<string> _lines, string _label, int _baseValue, ItemData_Equipment _item, int _level)
    {
        if (_baseValue == 0)
            return;

        int now = Manager.CalculateStatValue(_baseValue, _level, _item.growthFlatPerLevel, _item.growthPercentPerLevel);
        int next = Manager.CalculateStatValue(_baseValue, _level + 1, _item.growthFlatPerLevel, _item.growthPercentPerLevel);

        _lines.Add($"{_label}  {now} → {next}");
    }

    private void UpdateRequirement(bool _maxed)
    {
        if (requirementListRoot != null)
            ClearChildren(requirementListRoot);

        EquipmentUpgradeMaterial[] materials = Manager.GetUpgradeMaterials(selected);
        int targetLevel = Manager.GetLevel(selected) + 1;

        // 每种材料一行，最后加一行金币
        if (!_maxed && requirementListRoot != null && requirementRowPrefab != null)
        {
            if (materials != null)
            {
                foreach (EquipmentUpgradeMaterial entry in materials)
                {
                    if (entry == null || entry.material == null)
                        continue;

                    int need = entry.GetAmount(targetLevel);
                    int have = Inventory.instance != null ? Inventory.instance.GetItemAmount(entry.material) : 0;

                    EquipmentUpgradeRequirementRow row = Instantiate(requirementRowPrefab, requirementListRoot);
                    row.Setup(entry.material.icon, entry.material.itemName, have, need);
                }
            }

            int needCurrency = Manager.GetUpgradeCurrencyCost(selected);
            int haveCurrency = PlayerManager.instance != null ? PlayerManager.instance.GetCurrency() : 0;

            EquipmentUpgradeRequirementRow currencyRow = Instantiate(requirementRowPrefab, requirementListRoot);
            currencyRow.Setup(null, "金币", haveCurrency, needCurrency);
        }

        string reason = string.Empty;
        bool canUpgrade = !_maxed && Manager.CanUpgrade(selected, out reason);

        if (requirementText != null)
            requirementText.text = _maxed ? "已满级，不需要材料。" : (canUpgrade ? "强化需要：" : $"强化需要：（{reason}）");

        if (upgradeButton != null)
            upgradeButton.interactable = canUpgrade;

        if (upgradeButtonLabel != null)
            upgradeButtonLabel.text = _maxed ? "已满级" : "强化";
    }

    private static string Colorize(string _text, bool _enough)
        => _enough ? _text : "<color=#ff6b6b>" + _text + "</color>";

    private void OnUpgradeClicked()
    {
        if (selected == null || Manager == null)
            return;

        // 检查资源、扣材料与金币、升级都放在 EquipmentLevelManager 里
        if (Manager.TryUpgrade(selected))
            Debug.Log($"强化成功：{Manager.GetDisplayName(selected)}");

        Refresh();
    }

    private static void ClearChildren(Transform _parent)
    {
        if (_parent == null)
            return;

        for (int i = _parent.childCount - 1; i >= 0; i--)
        {
            Transform child = _parent.GetChild(i);
            child.SetParent(null, false);   // Destroy 是帧尾生效，先脱离父物体
            Destroy(child.gameObject);
        }
    }

    #endregion
}
