using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 对话界面（预制体 Assets/Prefabs/UI/DialoguePanel.prefab）：
/// 头顶的「按W对话」提示、屏幕上方的对话框、打字机文字、选项按钮。
/// 逻辑在 DialogueManager 里，这里只负责显示。
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [Header("头顶提示")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private TextMeshProUGUI promptText;
    [Tooltip("提示文字挂在哪个 RectTransform 上（世界坐标转屏幕坐标用），留空 = 用提示自己")]
    [SerializeField] private RectTransform promptRect;
    [Tooltip("投影用的相机，留空 = Camera.main")]
    [SerializeField] private Camera worldCamera;

    [Header("对话框（屏幕上方）")]
    [SerializeField] private GameObject boxRoot;
    [SerializeField] private TextMeshProUGUI speakerText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [Tooltip("文字全部显示完之后出现的「继续」提示，可以留空")]
    [SerializeField] private GameObject continueHint;

    [Header("选项")]
    [SerializeField] private RectTransform choiceRoot;
    [SerializeField] private Button choiceButtonPrefab;

    [Header("打字机")]
    [Tooltip("每秒打几个字")]
    [SerializeField] private float charactersPerSecond = 45f;

    /// <summary>文字还在一个字一个字地打</summary>
    public bool IsTyping { get; private set; }

    /// <summary>对话框现在是不是真的显示着（给对话管理器做安全网用）。</summary>
    public bool IsDialogueVisible => boxRoot != null && boxRoot.activeInHierarchy;

    private float visibleCharacters;
    private Vector3 promptWorldPosition;
    private UnityAction<DialogueChoice> choiceCallback;

    private void Awake()
    {
        // 防止预制体/场景实例上存了 0，导致文字一个字都打不出来
        charactersPerSecond = Mathf.Max(1f, charactersPerSecond);

        if (promptRect == null && promptRoot != null)
            promptRect = promptRoot.transform as RectTransform;

        HidePrompt();
        HideDialogue();
    }

    private void Update()
    {
        if (!IsTyping || bodyText == null)
            return;

        // 对话时游戏时间是暂停的（timeScale = 0），所以打字机要用不受影响的时间
        visibleCharacters += charactersPerSecond * Time.unscaledDeltaTime;

        int total = bodyText.textInfo.characterCount;
        int shown = Mathf.Clamp(Mathf.FloorToInt(visibleCharacters), 0, total);

        bodyText.maxVisibleCharacters = shown;

        if (shown >= total)
            FinishTyping();
    }

    private void LateUpdate()
    {
        if (promptRoot != null && promptRoot.activeSelf)
            UpdatePromptPosition();
    }

    #region 头顶提示

    /// <summary>显示「按W对话」并把气泡放到 NPC 头顶。</summary>
    public void ShowPrompt(Vector3 _worldPosition, string _text)
    {
        if (promptRoot == null)
            return;

        promptWorldPosition = _worldPosition;

        if (promptText != null)
            promptText.text = _text;

        promptRoot.SetActive(true);
        UpdatePromptPosition();
    }

    public void HidePrompt()
    {
        if (promptRoot != null)
            promptRoot.SetActive(false);
    }

    private void UpdatePromptPosition()
    {
        if (promptRect == null)
            return;

        Camera cam = worldCamera != null ? worldCamera : Camera.main;

        if (cam == null)
            return;

        Vector3 screen = cam.WorldToScreenPoint(promptWorldPosition);

        if (screen.z < 0f)
        {
            promptRoot.SetActive(false);
            return;
        }

        Canvas canvas = promptRect.GetComponentInParent<Canvas>();

        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            promptRect.position = cam.ScreenToWorldPoint(screen);
        else
            promptRect.position = screen;   // Overlay 画布的坐标就是屏幕像素
    }

    #endregion

    #region 对话框与打字机

    public void ShowNode(string _speaker, string _text)
    {
        HideChoices();

        // 保险：把对话框上面所有父节点都激活（菜单切换可能把中间层关掉），并顶到最上层
        if (boxRoot != null)
        {
            for (Transform parent = boxRoot.transform; parent != null; parent = parent.parent)
            {
                if (!parent.gameObject.activeSelf)
                    parent.gameObject.SetActive(true);
            }

            boxRoot.transform.SetAsLastSibling();
        }

        if (boxRoot != null)
            boxRoot.SetActive(true);

        if (speakerText != null)
        {
            speakerText.text = _speaker;
            speakerText.gameObject.SetActive(!string.IsNullOrEmpty(_speaker));
        }

        if (bodyText == null)
            return;

        bodyText.text = _text ?? string.Empty;
        bodyText.ForceMeshUpdate();
        bodyText.maxVisibleCharacters = 0;

        visibleCharacters = 0f;
        SetContinueHint(false);

        IsTyping = bodyText.textInfo.characterCount > 0;

        // 没有文字（比如纯选项节点）就直接算显示完
        if (!IsTyping)
            FinishTyping();
    }

    /// <summary>把当前这句话直接全部显示出来（玩家在打字过程中点了鼠标/空格）。</summary>
    public void CompleteTyping()
    {
        if (bodyText != null)
            bodyText.maxVisibleCharacters = bodyText.textInfo.characterCount;

        FinishTyping();
    }

    private void FinishTyping()
    {
        IsTyping = false;
        SetContinueHint(true);
    }

    private void SetContinueHint(bool _show)
    {
        if (continueHint != null)
            continueHint.SetActive(_show);
    }

    public void HideDialogue()
    {
        IsTyping = false;
        HideChoices();

        if (boxRoot != null)
            boxRoot.SetActive(false);

        SetContinueHint(false);
    }

    #endregion

    #region 选项

    public void ShowChoices(List<DialogueChoice> _choices, UnityAction<DialogueChoice> _onSelected)
    {
        HideChoices();

        if (choiceRoot == null || choiceButtonPrefab == null)
        {
            Debug.LogWarning("DialogueUI：没有配置选项容器或选项按钮预制体。", this);
            return;
        }

        choiceCallback = _onSelected;
        choiceRoot.gameObject.SetActive(true);

        foreach (DialogueChoice choice in _choices)
        {
            if (choice == null)
                continue;

            Button button = Instantiate(choiceButtonPrefab, choiceRoot);
            TextMeshProUGUI label = button.GetComponentInChildren<TextMeshProUGUI>();

            if (label != null)
                label.text = choice.label;

            DialogueChoice captured = choice;
            button.onClick.AddListener(() => choiceCallback?.Invoke(captured));
        }
    }

    public void HideChoices()
    {
        choiceCallback = null;

        if (choiceRoot == null)
            return;

        for (int i = choiceRoot.childCount - 1; i >= 0; i--)
        {
            Transform child = choiceRoot.GetChild(i);
            child.SetParent(null, false);   // Destroy 要到帧尾才生效，先脱离父物体，否则同一帧里还会被算进选项数
            Destroy(child.gameObject);
        }

        choiceRoot.gameObject.SetActive(false);
    }

    #endregion
}
