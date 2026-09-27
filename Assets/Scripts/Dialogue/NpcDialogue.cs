using UnityEngine;

/// <summary>
/// 一段 NPC 对话：由若干「节点」组成，节点之间用 nextNodeId 串起来，也可以在节点上挂选项分支。
/// 右键 Assets → Create → Data → NPC Dialogue 创建，或者用菜单「Tools/对话/生成铁匠示例对话」生成。
/// </summary>
[CreateAssetMenu(fileName = "New Npc Dialogue", menuName = "Data/NPC Dialogue")]
public class NpcDialogue : ScriptableObject
{
    [Tooltip("存档用的唯一 ID。留空 = 用资产名，改了这个相当于换了一段对话")]
    public string dialogueId;

    [Tooltip("勾上 = 只触发一次（触发过会写进存档，重进地图也不会再触发）；不勾 = 可以反复触发")]
    public bool onceOnly;

    [Tooltip("选项界面是否只显示还没选过的选项（不冲突分支用）")]
    public bool hideChosenChoices = true;

    [Tooltip("对话节点，第一个是入口")]
    public DialogueNode[] nodes;

    public string GetId() => string.IsNullOrEmpty(dialogueId) ? name : dialogueId;

    public DialogueNode GetFirstNode()
    {
        if (nodes == null || nodes.Length == 0)
            return null;

        return nodes[0];
    }

    public DialogueNode FindNode(string _nodeId)
    {
        if (nodes == null || string.IsNullOrEmpty(_nodeId))
            return null;

        foreach (DialogueNode node in nodes)
        {
            if (node != null && node.nodeId == _nodeId)
                return node;
        }

        return null;
    }
}

/// <summary>一句话（或一个选项节点）。</summary>
[System.Serializable]
public class DialogueNode
{
    [Tooltip("节点 ID，选项靠它跳转")]
    public string nodeId;

    [Tooltip("说话人。留空 = 用 NPC 的名字；想让玩家说话就填玩家的名字")]
    public string speaker;

    [TextArea(2, 6)]
    public string text;

    [Tooltip("这句话说完跳到哪个节点。留空 = 结束（不冲突分支则回到上一层的选项界面）")]
    public string nextNodeId;

    [Tooltip("挂了选项时，文字打完会弹出选项；选项优先于 nextNodeId")]
    public DialogueChoice[] choices;

    public bool HasChoices => choices != null && choices.Length > 0;
}

/// <summary>一个分支选项。</summary>
[System.Serializable]
public class DialogueChoice
{
    [Tooltip("选项 ID，用来记录选过没有")]
    public string choiceId;

    [Tooltip("选项动作 ID（可选）：填了就会在选完这个选项后触发对应动作，比如 open_equipment_upgrade 就是打开强化界面")]
    public string actionId;

    [Tooltip("按钮上显示的文字")]
    public string label;

    [Tooltip("选了这个之后先播放的节点（这个节点的 nextNodeId 可以继续往下串）")]
    public string targetNodeId;

    [Tooltip("勾上 = 不冲突选项：这条分支说完回到选项界面，所有选项都选过才继续；\n不勾 = 互斥选项：选完就顺着这条分支走下去，不再回到选项界面")]
    public bool returnToChoices;
}
