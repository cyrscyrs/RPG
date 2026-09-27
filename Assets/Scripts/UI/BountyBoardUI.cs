using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>任务列表里的一行：任务名 / 类型+目标+内容 / 进度 / 奖励 / 接取或提交按钮。</summary>
public class BountyQuestSlot : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI infoText;
    [SerializeField] private TextMeshProUGUI progressText;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button actionButton;
    [SerializeField] private TextMeshProUGUI actionLabel;
    [SerializeField] private Image background;

    [SerializeField] private Color normalColor = new Color(0.16f, 0.18f, 0.24f, 0.98f);
    [SerializeField] private Color readyColor = new Color(0.20f, 0.30f, 0.20f, 0.98f);
    [SerializeField] private Color submittedColor = new Color(0.13f, 0.14f, 0.17f, 0.9f);

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

/// <summary>
/// 赏金任务界面（预制体 Assets/Prefabs/UI/BountyPanel.prefab，带 UI_SelfManagedPanel 标记）。
/// - 赏金猎人对话里选「接取任务」→ 打开任务列表（可接取 / 可提交 / 已完成都能看到）；
/// - 按 T → 打开同一面板，只显示已接取的任务；
/// - 底部可以花金币刷新任务列表（全部完成时免费自动刷新）。
/// </summary>
public class BountyBoardUI : MonoBehaviour
{
    public const string OpenActionId = "open_bounty";

    public static BountyBoardUI Instance;

    [Header("界面引用")]
    [SerializeField] private GameObject windowRoot;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI currencyText;
    [SerializeField] private RectTransform listRoot;
    [SerializeField] private BountyQuestSlot slotPrefab;
    [SerializeField] private GameObject emptyHint;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button refreshButton;
    [SerializeField] private TextMeshProUGUI refreshLabel;

    [Header("奖励弹窗")]
    [SerializeField] private GameObject rewardPopup;
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button rewardCloseButton;

    [Header("按键")]
    [Tooltip("查看已接取任务的按键")]
    [SerializeField] private KeyCode acceptedKey = KeyCode.T;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;

    private bool acceptedOnly;
    private bool pausedByUs;

    public bool IsOpen => windowRoot != null && windowRoot.activeSelf;

    #region 生命周期

    private void Awake()
    {
        if (Instance != null && Instance != this)
            Destroy(gameObject);
        else
            Instance = this;

        DialogueManager.OnAction += HandleAction;
        UI_SelfManagedPanel.OnOtherMenuOpened += CloseIfOpen;

        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (refreshButton != null) refreshButton.onClick.AddListener(OnRefreshClicked);
        if (rewardCloseButton != null) rewardCloseButton.onClick.AddListener(CloseRewardPopup);

        if (windowRoot != null) windowRoot.SetActive(false);
        if (rewardPopup != null) rewardPopup.SetActive(false);
    }

    private void OnDestroy()
    {
        DialogueManager.OnAction -= HandleAction;
        UI_SelfManagedPanel.OnOtherMenuOpened -= CloseIfOpen;

        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (IsOpen)
        {
            if (Input.GetKeyDown(closeKey)) { Close(); return; }
            if (Input.GetKeyDown(acceptedKey)) { Open(true); return; }
        }
        else if (Input.GetKeyDown(acceptedKey))
        {
            Open(true);
        }
    }

    private void HandleAction(string _actionId)
    {
        if (_actionId == OpenActionId)
            Open(false);
    }

    private void CloseIfOpen()
    {
        if (IsOpen) Close();
    }

    #endregion

    #region 打开 / 关闭

    public void Open(bool _acceptedOnly)
    {
        if (windowRoot == null || BountyBoard.Instance == null)
            return;

        acceptedOnly = _acceptedOnly;

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        windowRoot.SetActive(true);
        SetPaused(true);
        Refresh();
    }

    public void Close()
    {
        CloseRewardPopup();

        if (windowRoot != null) windowRoot.SetActive(false);

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

    #region 列表

    public void Refresh()
    {
        BountyBoard board = BountyBoard.Instance;

        if (board == null || listRoot == null || slotPrefab == null)
            return;

        CloseRewardPopup();
        ClearChildren(listRoot);

        if (titleText != null)
            titleText.text = acceptedOnly ? "已接取的任务（T）" : "赏金任务";

        if (currencyText != null)
            currencyText.text = $"金币：{(PlayerManager.instance != null ? PlayerManager.instance.GetCurrency() : 0)}" +
                                $"　已接 {board.AcceptedCount} / {board.MaxAccepted}";

        if (refreshLabel != null)
            refreshLabel.text = board.CanRefreshFree() ? "刷新（免费）" : $"刷新（{board.RefreshCost} 金币）";

        if (refreshButton != null)
            refreshButton.interactable = board.CanRefreshFree() ||
                (PlayerManager.instance != null && PlayerManager.instance.GetCurrency() >= board.RefreshCost);

        int shown = 0;

        foreach (BountyQuest quest in board.Offers)
        {
            if (quest == null)
                continue;

            if (acceptedOnly && !board.IsAccepted(quest))
                continue;

            CreateSlot(board, quest);
            shown++;
        }

        if (emptyHint != null)
            emptyHint.SetActive(shown == 0);
    }

    private void CreateSlot(BountyBoard _board, BountyQuest _quest)
    {
        int progress = Mathf.Min(_board.GetProgress(_quest), Mathf.Max(1, _quest.requiredAmount));
        int required = Mathf.Max(1, _quest.requiredAmount);

        bool submitted = _board.IsSubmitted(_quest);
        bool accepted = _board.IsAccepted(_quest);
        bool ready = _board.CanSubmit(_quest);

        string info = $"【{_quest.GetTypeLabel()}】{_quest.GetObjectiveText()}";
        if (!string.IsNullOrEmpty(_quest.content))
            info += $"\n{_quest.content}";

        string progressText = submitted ? "已完成" : $"进度：{progress} / {required}";

        string reward = "奖励：" + BuildRewardText(_quest);

        string buttonLabel;
        bool buttonEnabled;
        Color color;

        if (submitted)
        {
            buttonLabel = "已提交";
            buttonEnabled = false;
            color = slotPrefab != null ? new Color(0.13f, 0.14f, 0.17f, 0.9f) : Color.gray;
        }
        else if (ready)
        {
            buttonLabel = "提交任务";
            buttonEnabled = true;
            color = new Color(0.20f, 0.30f, 0.20f, 0.98f);
        }
        else if (accepted)
        {
            buttonLabel = "进行中";
            buttonEnabled = false;
            color = new Color(0.16f, 0.18f, 0.24f, 0.98f);
        }
        else
        {
            buttonLabel = "接取任务";
            buttonEnabled = _board.CanAcceptMore;
            color = new Color(0.16f, 0.18f, 0.24f, 0.98f);
        }

        BountyQuestSlot slot = Instantiate(slotPrefab, listRoot);
        BountyQuest captured = _quest;

        slot.Setup(_quest, string.IsNullOrEmpty(_quest.title) ? _quest.GetId() : _quest.title,
            info, progressText, reward, buttonLabel, buttonEnabled, color,
            () => OnSlotClicked(captured));
    }

    private static string BuildRewardText(BountyQuest _quest)
    {
        List<string> parts = new List<string>();

        if (_quest.rewards != null)
        {
            foreach (BountyReward reward in _quest.rewards)
            {
                if (reward == null) continue;

                if (reward.currency > 0)
                    parts.Add($"{reward.currency} 金币");

                if (reward.item != null && reward.amount > 0)
                    parts.Add($"{reward.item.itemName} ×{reward.amount}");
            }
        }

        return parts.Count > 0 ? string.Join("、", parts) : "无";
    }

    #endregion

    #region 按钮

    private void OnSlotClicked(BountyQuest _quest)
    {
        BountyBoard board = BountyBoard.Instance;

        if (board == null || _quest == null)
            return;

        if (board.CanSubmit(_quest))
        {
            string reward = BuildRewardText(_quest);
            board.Submit(_quest);
            ShowRewardPopup($"任务完成！\n{_quest.title}\n\n获得：{reward}");
            Refresh();
            return;
        }

        if (!board.IsAccepted(_quest))
        {
            if (board.Accept(_quest))
                Debug.Log($"已接取任务：{_quest.title}");
            else
                Debug.Log("接取失败（可能已经接了 5 个任务）");

            Refresh();
        }
    }

    private void OnRefreshClicked()
    {
        BountyBoard board = BountyBoard.Instance;

        if (board == null)
            return;

        if (!board.RefreshWithCurrency())
        {
            Debug.Log("刷新失败：金币不够");
            return;
        }

        Refresh();
    }

    private void ShowRewardPopup(string _text)
    {
        if (rewardPopup == null)
            return;

        if (rewardText != null)
            rewardText.text = _text;

        rewardPopup.SetActive(true);
    }

    private void CloseRewardPopup()
    {
        if (rewardPopup != null)
            rewardPopup.SetActive(false);
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
