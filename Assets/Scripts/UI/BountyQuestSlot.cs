using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 赏金任务列表里的一行：任务名 / 类型+目标+内容 / 进度 / 奖励 / 接取或提交按钮。
/// 挂在预制体 Assets/Prefabs/UI/BountyQuestSlot.prefab 上，由 BountyBoardUI 填充。
/// </summary>
public class BountyQuestSlot : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI infoText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionLabel;
    [SerializeField] private Image background;

    public BountyQuest Quest { get; private set; }

    public void Setup(BountyQuest _quest, string _title, string _info, string _progress, string _reward,
                      string _buttonLabel, bool _buttonInteractable, Color _background,
                      UnityEngine.Events.UnityAction _onClick)
    {
        Quest = _quest;

        if (titleText != null) titleText.text = _title;
        if (infoText != null) infoText.text = _info;
        if (progressText != null) progressText.text = _progress;
        if (rewardText != null) rewardText.text = _reward;
        if (background != null) background.color = _background;
        if (actionLabel != null) actionLabel.text = _buttonLabel;

        if (actionButton != null)
        {
            actionButton.interactable = _buttonInteractable;
            actionButton.onClick.RemoveAllListeners();
            actionButton.onClick.AddListener(_onClick);
        }
    }
}
