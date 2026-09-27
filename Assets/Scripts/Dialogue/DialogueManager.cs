using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话系统的运行时逻辑：
/// - 玩家靠近 NPC 时显示「按W对话」，按 W 开始对话；
/// - 鼠标左键 / 空格推进对话；文字还在打字时按一下就立刻全部显示；
/// - 支持选项分支：互斥选项（选完顺着分支走）和不冲突选项（分支说完回到选项界面，全部选完才继续）；
/// - 一次性对话的完成状态和选过的选项都会写进存档（GameData.completedDialogues / selectedChoices）。
///
/// 和界面一起挂在预制体 Assets/Prefabs/UI/DialoguePanel.prefab 上，把预制体丢进 Canvas 就能用。
/// NPC 那边只需要挂 DialogueNpc 组件并配好对话资产。
/// </summary>
public class DialogueManager : MonoBehaviour, ISaveManager
{
    public static DialogueManager instance;

    [Header("引用（预制体里已经接好）")]
    [SerializeField] private DialogueUI ui;

    [Header("交互")]
    [SerializeField] private KeyCode interactKey = KeyCode.W;

    [Tooltip("提示文字里显示的按键名")]
    [SerializeField] private string interactKeyLabel = "W";

    [Tooltip("头顶提示文字的格式，{0} 会替换成按键名")]
    [SerializeField] private string promptFormat = "按 {0} 对话";

    [Header("行为")]
    [Tooltip("对话时暂停游戏（推荐：对话期间玩家和敌人都停住）")]
    [SerializeField] private bool pauseDuringDialogue = true;

    [Tooltip("一次性对话做完后立刻写一次存档")]
    [SerializeField] private bool saveWhenDialogueCompleted = true;

    public bool InDialogue => inDialogue;

    [Tooltip("对话中按这个键可以直接退出对话（保底不会卡在暂停里）")]
    [SerializeField] private KeyCode abortKey = KeyCode.Escape;

    private float stuckTimer;

    /// <summary>对话选项可以触发一个「动作」（比如打开强化界面），订阅它就能接住。</summary>
    public static event System.Action<string> OnAction;

    /// <summary>触发一个对话动作。</summary>
    public static void RaiseAction(string _actionId)
    {
        if (string.IsNullOrEmpty(_actionId))
            return;

        OnAction?.Invoke(_actionId);
    }

    // 存档内容
    private readonly HashSet<string> completedDialogues = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> chosenOptions = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);

    // 不冲突分支说完之后要回到的选项节点（支持嵌套，所以用栈）
    private readonly Stack<DialogueNode> returnStack = new Stack<DialogueNode>();

    private DialogueNpc currentNpc;
    private NpcDialogue currentDialogue;
    private DialogueNode currentNode;

    private bool inDialogue;
    private bool waitingForChoice;
    private bool waitingForRelease;
    private bool pausedByUs;

    private DialogueNpc[] npcs;
    private float nextNpcRefresh;

    private void Awake()
    {
        if (instance != null && instance != this)
            Destroy(instance.gameObject);
        else
            instance = this;

        if (ui == null)
            ui = GetComponent<DialogueUI>();

        if (ui == null)
            ui = FindObjectOfType<DialogueUI>();

        // 玩家按了别的菜单（C/B/K/O/L）时，把正在进行的对话收掉
        UI_SelfManagedPanel.OnOtherMenuOpened += AbortDialogue;
    }

    private void OnDestroy()
    {
        UI_SelfManagedPanel.OnOtherMenuOpened -= AbortDialogue;

        if (instance == this)
            instance = null;
    }

    private void Update()
    {
        if (ui == null)
            return;

        // 安全网 1：对话中按 Esc 直接退出，永远不会卡在暂停里
        if (inDialogue && Input.GetKeyDown(abortKey))
        {
            AbortDialogue();
            return;
        }

        // 安全网 2：对话开着但对话框被别的代码关掉了 → 自动收尾并恢复时间
        if (inDialogue && !ui.IsDialogueVisible)
        {
            stuckTimer += Time.unscaledDeltaTime;

            if (stuckTimer > 1f)
            {
                Debug.LogWarning("对话界面被关掉了，自动结束对话并恢复游戏时间。");
                AbortDialogue();
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        if (inDialogue)
            UpdateDialogueInput();
        else
            UpdateInteraction();
    }

    #region 靠近提示 / 开始对话

    private void UpdateInteraction()
    {
        // 游戏暂停中（菜单开着）就不显示提示、也不能触发对话
        if (Time.timeScale <= 0f)
        {
            ui.HidePrompt();
            return;
        }

        DialogueNpc npc = FindNearestNpc();

        if (npc == null)
        {
            currentNpc = null;
            ui.HidePrompt();
            return;
        }

        NpcDialogue available = npc.GetAvailableDialogue(this);

        if (available == null)
        {
            ui.HidePrompt();
            return;
        }

        currentNpc = npc;
        ui.ShowPrompt(npc.PromptWorldPosition, string.Format(promptFormat, interactKeyLabel));

        if (Input.GetKeyDown(interactKey))
            StartDialogue(npc, available);
    }

    private DialogueNpc FindNearestNpc()
    {
        if (npcs == null || Time.time >= nextNpcRefresh)
        {
            npcs = FindObjectsOfType<DialogueNpc>();
            nextNpcRefresh = Time.time + 2f;
        }

        Transform player = PlayerManager.instance != null && PlayerManager.instance.player != null
            ? PlayerManager.instance.player.transform
            : null;

        if (player == null)
            return null;

        DialogueNpc nearest = null;
        float nearestDistance = float.MaxValue;

        foreach (DialogueNpc npc in npcs)
        {
            if (npc == null || !npc.isActiveAndEnabled)
                continue;

            float distance = Vector2.Distance(player.position, npc.transform.position);

            if (distance > npc.InteractionRadius || distance >= nearestDistance)
                continue;

            nearest = npc;
            nearestDistance = distance;
        }

        return nearest;
    }

    private void StartDialogue(DialogueNpc _npc, NpcDialogue _dialogue)
    {
        if (_dialogue == null)
            return;

        currentNpc = _npc;
        currentDialogue = _dialogue;
        returnStack.Clear();

        inDialogue = true;
        waitingForChoice = false;
        waitingForRelease = true;      // 防止「开始对话」的那次按键顺带推进第一句

        // 可重复触发的对话，每次进来都把选项重新开放
        if (!_dialogue.onceOnly)
            ClearChosenOptions(_dialogue.GetId());

        if (pauseDuringDialogue && GameManager.instance != null && Time.timeScale > 0f)
        {
            GameManager.instance.PauseGame(true);
            pausedByUs = true;
        }

        ui.HidePrompt();
        ShowNode(_dialogue.GetFirstNode());
    }

    #endregion

    #region 推进对话

    private void UpdateDialogueInput()
    {
        if (waitingForChoice)
            return;

        if (waitingForRelease)
        {
            if (!IsAdvanceHeld())
                waitingForRelease = false;

            return;
        }

        if (!IsAdvancePressed())
            return;

        // 文字还在打 → 先一次性显示全部
        if (ui.IsTyping)
        {
            ui.CompleteTyping();
            return;
        }

        if (currentNode != null && currentNode.HasChoices)
        {
            ShowChoices(currentNode);
            return;
        }

        AdvanceTo(currentNode != null ? currentNode.nextNodeId : null);
    }

    private void ShowNode(DialogueNode _node)
    {
        if (_node == null)
        {
            EndDialogue();
            return;
        }

        currentNode = _node;

        // 选项节点：如果已经没有可选选项了就直接往下走
        if (_node.HasChoices && GetVisibleChoices(_node).Count == 0)
        {
            AdvanceTo(_node.nextNodeId);
            return;
        }

        string speaker = string.IsNullOrEmpty(_node.speaker)
            ? (currentNpc != null ? currentNpc.NpcName : string.Empty)
            : _node.speaker;

        ui.ShowNode(speaker, _node.text);

        // 纯选项节点（没有文字）直接把选项弹出来
        if (string.IsNullOrEmpty(_node.text) && _node.HasChoices && !ui.IsTyping)
            ShowChoices(_node);
    }

    private void AdvanceTo(string _nodeId)
    {
        if (!string.IsNullOrEmpty(_nodeId))
        {
            DialogueNode next = currentDialogue.FindNode(_nodeId);

            if (next != null)
            {
                ShowNode(next);
                return;
            }
        }

        // 这段分支到底了：如果是从不冲突选项进来的，就回到那个选项界面
        if (returnStack.Count > 0)
        {
            ShowNode(returnStack.Pop());
            return;
        }

        EndDialogue();
    }

    private void EndDialogue()
    {
        if (currentDialogue != null && currentDialogue.onceOnly)
            MarkCompleted(currentDialogue.GetId());

        inDialogue = false;
        waitingForChoice = false;
        currentNode = null;
        currentDialogue = null;
        returnStack.Clear();

        ui.HideDialogue();

        if (pausedByUs && GameManager.instance != null)
        {
            GameManager.instance.PauseGame(false);
            pausedByUs = false;
        }
    }

    private bool IsAdvancePressed() => Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space);

    /// <summary>中途放弃对话（比如玩家去开了别的菜单）：不算完成，也不存任何记录。</summary>
    public void AbortDialogue()
    {
        if (!inDialogue)
            return;

        inDialogue = false;
        waitingForChoice = false;
        waitingForRelease = false;
        returnStack.Clear();
        currentNode = null;
        currentDialogue = null;

        if (ui != null)
            ui.HideDialogue();

        if (pausedByUs && GameManager.instance != null)
        {
            GameManager.instance.PauseGame(false);
            pausedByUs = false;
        }
    }
    private bool IsAdvanceHeld() => Input.GetMouseButton(0) || Input.GetKey(KeyCode.Space);

    #endregion

    #region 选项

    private void ShowChoices(DialogueNode _node)
    {
        List<DialogueChoice> visible = GetVisibleChoices(_node);

        if (visible.Count == 0)
        {
            AdvanceTo(_node.nextNodeId);
            return;
        }

        waitingForChoice = true;

        ui.ShowChoices(visible, choice => SelectChoice(_node, choice));
    }

    private List<DialogueChoice> GetVisibleChoices(DialogueNode _node)
    {
        List<DialogueChoice> result = new List<DialogueChoice>();

        if (_node == null || _node.choices == null)
            return result;

        bool hideChosen = currentDialogue == null || currentDialogue.hideChosenChoices;

        foreach (DialogueChoice choice in _node.choices)
        {
            if (choice == null)
                continue;

            if (hideChosen && chosenOptions.Contains(ChoiceKey(currentDialogue, choice)))
                continue;

            result.Add(choice);
        }

        return result;
    }

    private void SelectChoice(DialogueNode _choiceNode, DialogueChoice _choice)
    {
        chosenOptions.Add(ChoiceKey(currentDialogue, _choice));

        // 带动作的选项：先结束对话，再触发动作（比如打开强化界面）
        if (!string.IsNullOrEmpty(_choice.actionId))
        {
            string actionId = _choice.actionId;

            EndDialogue();
            RaiseAction(actionId);
            return;
        }

        waitingForChoice = false;
        ui.HideChoices();

        // 不冲突选项：这条分支说完要回到这个选项界面；互斥选项：顺着分支走，不再回来
        if (_choice.returnToChoices && _choiceNode != null)
            returnStack.Push(_choiceNode);

        DialogueNode target = currentDialogue != null ? currentDialogue.FindNode(_choice.targetNodeId) : null;

        if (target != null)
        {
            ShowNode(target);
            return;
        }

        // 选项没配目标节点：当作这条分支到此结束
        if (returnStack.Count > 0)
        {
            ShowNode(returnStack.Pop());
            return;
        }

        EndDialogue();
    }

    private static string ChoiceKey(NpcDialogue _dialogue, DialogueChoice _choice)
    {
        string dialogueId = _dialogue != null ? _dialogue.GetId() : string.Empty;
        string choiceId = _choice != null && !string.IsNullOrEmpty(_choice.choiceId)
            ? _choice.choiceId
            : (_choice != null ? _choice.label : string.Empty);

        return dialogueId + "/" + choiceId;
    }

    private void ClearChosenOptions(string _dialogueId)
    {
        List<string> remove = new List<string>();
        string prefix = _dialogueId + "/";

        foreach (string key in chosenOptions)
        {
            if (key.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                remove.Add(key);
        }

        foreach (string key in remove)
            chosenOptions.Remove(key);
    }

    #endregion

    #region 存档

    /// <summary>这段一次性对话是不是已经做过了。</summary>
    public bool IsDialogueCompleted(string _dialogueId)
        => !string.IsNullOrEmpty(_dialogueId) && completedDialogues.Contains(_dialogueId);

    private void MarkCompleted(string _dialogueId)
    {
        if (string.IsNullOrEmpty(_dialogueId) || !completedDialogues.Add(_dialogueId))
            return;

        if (saveWhenDialogueCompleted && SaveManager.instance != null)
            SaveManager.instance.SaveGame();
    }

    public void LoadData(GameData _data)
    {
        completedDialogues.Clear();
        chosenOptions.Clear();

        if (_data == null)
            return;

        if (_data.completedDialogues != null)
        {
            foreach (KeyValuePair<string, bool> pair in _data.completedDialogues)
            {
                if (pair.Value)
                    completedDialogues.Add(pair.Key);
            }
        }

        if (_data.selectedChoices != null)
        {
            foreach (KeyValuePair<string, bool> pair in _data.selectedChoices)
            {
                if (pair.Value)
                    chosenOptions.Add(pair.Key);
            }
        }
    }

    public void SaveData(ref GameData _data)
    {
        if (_data == null)
            return;

        if (_data.completedDialogues == null)
            _data.completedDialogues = new SerializableDictionary<string, bool>();

        if (_data.selectedChoices == null)
            _data.selectedChoices = new SerializableDictionary<string, bool>();

        _data.completedDialogues.Clear();

        foreach (string id in completedDialogues)
            _data.completedDialogues[id] = true;

        _data.selectedChoices.Clear();

        foreach (string key in chosenOptions)
            _data.selectedChoices[key] = true;
    }

    #endregion
}
