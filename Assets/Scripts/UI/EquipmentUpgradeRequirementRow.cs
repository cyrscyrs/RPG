using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 强化需求列表里的一行：材料图标 + 名称 + 「拥有 / 需要」。不够时数字变红。
/// 挂在预制体 Assets/Prefabs/UI/EquipmentUpgradeRequirementRow.prefab 上，由强化界面填充。
/// </summary>
public class EquipmentUpgradeRequirementRow : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI countText;

    [SerializeField] private Color enoughColor = new Color(0.92f, 0.9f, 0.85f, 1f);
    [SerializeField] private Color notEnoughColor = new Color(1f, 0.42f, 0.42f, 1f);

    [Tooltip("文字在「有图标」和「没有图标」时的水平位置")]
    [SerializeField] private float textOffsetWithIcon = 72f;
    [SerializeField] private float textOffsetWithoutIcon = 20f;

    public void Setup(Sprite _icon, string _name, int _have, int _need)
    {
        if (iconImage != null)
        {
            iconImage.sprite = _icon;
            iconImage.enabled = _icon != null;
        }

        if (nameText != null)
        {
            nameText.text = _name;

            Vector2 position = nameText.rectTransform.anchoredPosition;
            position.x = _icon != null ? textOffsetWithIcon : textOffsetWithoutIcon;
            nameText.rectTransform.anchoredPosition = position;
        }

        if (countText != null)
        {
            countText.text = $"{_have} / {_need}";
            countText.color = _have >= _need ? enoughColor : notEnoughColor;
        }
    }
}
