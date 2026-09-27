using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 强化界面左侧列表里的一格装备：图标 + 带等级的装备名 + 等级/是否已装备。
/// 挂在预制体 Assets/Prefabs/UI/EquipmentUpgradeSlot.prefab 上，由 EquipmentUpgradeUI 填充。
/// </summary>
public class EquipmentUpgradeSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Image background;

    [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.24f, 0.98f);
    [SerializeField] private Color selectedColor = new Color(0.34f, 0.29f, 0.16f, 1f);

    public ItemData_Equipment Item { get; private set; }

    public void Setup(ItemData_Equipment _item, string _displayName, int _level, int _maxLevel, bool _equipped, UnityAction _onClick)
    {
        Item = _item;

        if (iconImage != null)
        {
            iconImage.sprite = _item != null ? _item.icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameText != null)
            nameText.text = _displayName;

        if (levelText != null)
            levelText.text = $"Lv.{_level} / {_maxLevel}" + (_equipped ? "　已装备" : "");

        Button button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(_onClick);
        }

        SetSelected(false);
    }

    public void SetSelected(bool _selected)
    {
        if (background != null)
            background.color = _selected ? selectedColor : normalColor;
    }
}
