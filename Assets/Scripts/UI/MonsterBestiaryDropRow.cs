using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 掉落物列表里的一行：图标 + 名字 + 掉率。
/// 挂在预制体 Assets/Prefabs/UI/MonsterBestiaryDropRow.prefab 上，由 MonsterBestiaryUI 填充。
/// </summary>
public class MonsterBestiaryDropRow : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI chanceText;

    public void Setup(Sprite _icon, string _name, string _chance)
    {
        if (iconImage != null)
        {
            iconImage.sprite = _icon;
            iconImage.enabled = _icon != null;
        }

        if (nameText != null)
            nameText.text = _name;

        if (chanceText != null)
            chanceText.text = _chance;
    }
}
