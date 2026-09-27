using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UI_ItemToolTip : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI itemNameText;
    [SerializeField] private TextMeshProUGUI itemTypeText;
    [SerializeField] private TextMeshProUGUI itemDescription;

    private float defaultFontSize = 32;
    public void ShowToolTip(ItemData_Equipment item)
    {
        if(item == null) return;
        // 强化过的装备名字带上等级，例如「木剑 +10」
        itemNameText.text = EquipmentLevelManager.Instance != null
            ? EquipmentLevelManager.Instance.GetDisplayName(item)
            : item.itemName;
        itemTypeText.text = item.GetChineseEquipmentType();
        itemDescription.text = item.GetDescription();

        if (itemNameText.text.Length > 12)
            itemNameText.fontSize = itemNameText.fontSize * .7f;
        else
            itemNameText.fontSize = defaultFontSize;

        gameObject.SetActive(true);
    }

    public void HideToolTip()
    {
        itemNameText.fontSize = defaultFontSize;
        gameObject.SetActive(false);
    }
}
