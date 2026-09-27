using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>商店列表里的一件商品：图标 + 名称 + 价格。挂在 ShopSlot.prefab 上。</summary>
public class ShopSlot : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI priceText;
    [SerializeField] private Image background;

    [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.24f, 0.98f);
    [SerializeField] private Color selectedColor = new Color(0.34f, 0.29f, 0.16f, 1f);

    public ShopEntry Entry { get; private set; }

    public void Setup(ShopEntry _entry, UnityAction _onClick)
    {
        Entry = _entry;

        if (iconImage != null)
        {
            iconImage.sprite = _entry != null && _entry.item != null ? _entry.item.icon : null;
            iconImage.enabled = iconImage.sprite != null;
        }

        if (nameText != null)
            nameText.text = _entry != null && _entry.item != null ? _entry.item.itemName : "";

        if (priceText != null)
            priceText.text = _entry != null ? _entry.price.ToString() : "";

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
