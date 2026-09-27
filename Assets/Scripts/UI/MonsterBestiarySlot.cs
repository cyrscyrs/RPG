using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 图鉴里的一格怪物：图片按钮 + 名字 + 累计击杀数 + 「掉落物」按钮。
/// 这个组件挂在预制体 Assets/Prefabs/UI/MonsterBestiarySlot.prefab 上，
/// 由 MonsterBestiaryUI 实例化后调用 Setup 填数据。样式直接在预制体上改就行。
/// </summary>
public class MonsterBestiarySlot : MonoBehaviour
{
    [SerializeField] private Button iconButton;
    [SerializeField] private Image iconImage;
    [Tooltip("没有图片时显示的占位文字，可以留空")]
    [SerializeField] private GameObject noIconHint;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI killText;
    [SerializeField] private Button dropsButton;

    public void Setup(MonsterEntry _entry, int _killCount, bool _hidden, Sprite _icon,
                      UnityEngine.Events.UnityAction _onClickIcon,
                      UnityEngine.Events.UnityAction _onClickDrops)
    {
        if (nameText != null)
            nameText.text = _hidden ? "？？？" : MonsterBestiary.GetDisplayName(_entry);

        if (killText != null)
            killText.text = _hidden ? "累计击杀：？" : $"累计击杀：{_killCount}";

        if (iconImage != null)
        {
            iconImage.sprite = _icon;
            iconImage.color = _hidden ? new Color(1f, 1f, 1f, 0.35f) : Color.white;
        }

        if (noIconHint != null)
            noIconHint.SetActive(_icon == null);

        if (iconButton != null)
        {
            iconButton.onClick.RemoveAllListeners();
            iconButton.onClick.AddListener(_onClickIcon);
        }

        if (dropsButton != null)
        {
            dropsButton.onClick.RemoveAllListeners();
            dropsButton.onClick.AddListener(_onClickDrops);
        }
    }
}
