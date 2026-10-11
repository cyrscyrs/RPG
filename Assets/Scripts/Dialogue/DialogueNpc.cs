using UnityEngine;

/// <summary>
/// 挂在一个 NPC 上：定义它的名字、可交互距离，以及它有哪些对话。
///
/// 「一次性 / 可重复」的规则：
/// - dialogues 列表里只挑「勾了 onceOnly 且还没触发过」的对话，按顺序取第一个；
/// - 没有可用的一次性对话时，就播放 defaultDialogue（可重复的默认对话）。
/// 所以玩家第一次靠近触发剧情对话，剧情对话做完之后再互动就一直是默认对话。
/// </summary>
public class DialogueNpc : MonoBehaviour
{
    [SerializeField] private string npcName = "NPC";

    [Tooltip("玩家离多近才会出现「按W对话」")]
    [SerializeField] private float interactionRadius = 2.5f;

    [Tooltip("提示文字挂在头顶的偏移（相对 NPC 自身）")]
    [SerializeField] private Vector3 promptOffset = new Vector3(0f, 1.8f, 0f);

    [Tooltip("这个可交互物自己的提示格式，{0} 会替换成按键名。留空 = 用 DialogueManager 上的默认格式")]
    [SerializeField] private string promptFormatOverride = "";

    [Tooltip("和这个 NPC 开始对话时播的音效在 AudioManager 里的下标，-1 = 不播（赏金猎人 50 / 商人 54 / 铁匠 55）")]
    [SerializeField] private int talkSfxIndex = -1;

    [Tooltip("按顺序检查：第一个「还没触发过的一次性对话」会被用上")]
    [SerializeField] private NpcDialogue[] dialogues;

    [Tooltip("没有可用的一次性对话时使用的默认对话（建议做成可重复的）")]
    [SerializeField] private NpcDialogue defaultDialogue;

    public string NpcName => string.IsNullOrEmpty(npcName) ? name : npcName;
    public float InteractionRadius => interactionRadius;
    public Vector3 PromptWorldPosition => transform.position + promptOffset;

    /// <summary>开始对话时的招呼声下标（-1 = 不播）。</summary>
    public int TalkSfxIndex => talkSfxIndex;

    /// <summary>这个可交互物想用的提示格式（标牌用「按 W 互动」）；留空表示用 DialogueManager 的默认格式。</summary>
    public string PromptFormatOverride => promptFormatOverride;

    /// <summary>取现在该播放哪段对话。</summary>
    public NpcDialogue GetAvailableDialogue(DialogueManager _manager)
    {
        if (dialogues != null)
        {
            foreach (NpcDialogue dialogue in dialogues)
            {
                if (dialogue == null || !dialogue.onceOnly)
                    continue;

                if (_manager == null || !_manager.IsDialogueCompleted(dialogue.GetId()))
                    return dialogue;
            }
        }

        return defaultDialogue;
    }

    /// <summary>在编辑器里选中这个 NPC 时可以看见交互范围。</summary>
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.35f, 0.85f, 1f, 1f);
        Gizmos.DrawWireSphere(transform.position, interactionRadius);

        Gizmos.DrawLine(transform.position, PromptWorldPosition);
        Gizmos.DrawWireSphere(PromptWorldPosition, 0.12f);
    }
}
