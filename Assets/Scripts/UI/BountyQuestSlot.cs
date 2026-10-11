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
    [Tooltip("「放弃任务」按钮，预制体里接好；只有进行中的任务会显示它")]
    [SerializeField] private Button abandonButton;
    [SerializeField] private TextMeshProUGUI abandonLabel;
    [SerializeField] private Image background;

    public BountyQuest Quest { get; private set; }

    public void Setup(BountyQuest _quest, string _title, string _info, string _progress, string _reward,
                      string _buttonLabel, bool _buttonInteractable, Color _background,
                      UnityEngine.Events.UnityAction _onClick,
                      UnityEngine.Events.UnityAction _onAbandon)
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

        // 传了回调才显示「放弃任务」：只有进行中（接了但没完成）的任务能放弃
        if (abandonButton != null)
        {
            abandonButton.gameObject.SetActive(_onAbandon != null);
            abandonButton.onClick.RemoveAllListeners();

            if (_onAbandon != null)
                abandonButton.onClick.AddListener(_onAbandon);
        }
    }
}
