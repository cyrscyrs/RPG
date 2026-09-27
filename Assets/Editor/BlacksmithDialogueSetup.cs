using UnityEditor;
using UnityEngine;

/// <summary>
/// 铁匠 NPC 的示例对话 + 一键配置：
/// - 菜单「Tools/对话/生成铁匠示例对话」：生成两段对话资产
///   （一次性剧情 Blacksmith_First、可重复的默认对话 Blacksmith_Default）
/// - 菜单「Tools/对话/给场景里的铁匠 NPC 配置对话」：给场景里的 NPC_Blacksmith 挂上 DialogueNpc 并接好这两段对话
/// </summary>
public static class BlacksmithDialogueSetup
{
    private const string FolderPath = "Assets/Dialogue";
    private const string FirstPath = FolderPath + "/Blacksmith_First.asset";
    private const string DefaultPath = FolderPath + "/Blacksmith_Default.asset";
    private const string NpcObjectName = "NPC_Blacksmith";

    [MenuItem("Tools/对话/生成铁匠示例对话")]
    private static void CreateDialogueAssets()
    {
        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets", "Dialogue");

        CreateFirstDialogue();
        CreateDefaultDialogue();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/对话/给场景里的铁匠 NPC 配置对话")]
    private static void SetupBlacksmithNpc()
    {
        GameObject npc = GameObject.Find(NpcObjectName);

        if (npc == null)
        {
            EditorUtility.DisplayDialog("对话系统", $"当前场景里没找到「{NpcObjectName}」。", "好");
            return;
        }

        NpcDialogue first = AssetDatabase.LoadAssetAtPath<NpcDialogue>(FirstPath);
        NpcDialogue fallback = AssetDatabase.LoadAssetAtPath<NpcDialogue>(DefaultPath);

        if (first == null || fallback == null)
        {
            EditorUtility.DisplayDialog("对话系统", "还没生成对话资产。\n先执行「Tools/对话/生成铁匠示例对话」。", "好");
            return;
        }

        DialogueNpc component = npc.GetComponent<DialogueNpc>();

        if (component == null)
            component = Undo.AddComponent<DialogueNpc>(npc);

        SerializedObject serialized = new SerializedObject(component);
        serialized.FindProperty("npcName").stringValue = "铁匠";
        serialized.FindProperty("interactionRadius").floatValue = 2.5f;
        serialized.FindProperty("promptOffset").vector3Value = new Vector3(0f, 1.9f, 0f);

        SerializedProperty list = serialized.FindProperty("dialogues");
        list.arraySize = 1;
        list.GetArrayElementAtIndex(0).objectReferenceValue = first;

        serialized.FindProperty("defaultDialogue").objectReferenceValue = fallback;
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(npc);
        Selection.activeGameObject = npc;

        Debug.Log($"对话系统：「{NpcObjectName}」已配置好对话（首次触发一次性剧情，之后一直是「你来了啊」）。记得保存场景。");
    }

    #region 生成对话资产

    private static void CreateFirstDialogue()
    {
        if (AssetDatabase.LoadAssetAtPath<NpcDialogue>(FirstPath) != null)
        {
            Debug.Log($"对话系统：{FirstPath} 已经存在，没有覆盖它（想重新生成请先删掉）。");
            return;
        }

        NpcDialogue dialogue = ScriptableObject.CreateInstance<NpcDialogue>();
        dialogue.dialogueId = "blacksmith_first";
        dialogue.onceOnly = true;
        dialogue.hideChosenChoices = true;

        dialogue.nodes = new[]
        {
            Node("n1", "", "新面孔啊，是新来的吗？", "n2"),
            Node("n2", "玩家", "是啊，两眼一睁就到这里了", "n3"),

            // 两个「不冲突」选项：选完一个会回到这个选项界面，两个都选过才结束对话
            ChoiceNode("n3", "", new[]
            {
                Choice("ask_who", "询问对方身份", "n4", true),
                Choice("ask_why", "询问对方为什么在这里", "n5", true)
            }),

            Node("n4", "玩家", "你是什么人，看起来在这里待了很久了，对这里很熟悉的样子", "n4b"),
            Node("n4b", "", "我是这里的铁匠，如果想升级装备随时来找我", ""),

            Node("n5", "", "有些材料只有这里才能获取，那些怪物身上浑身是宝，无论是制作还是强化装备都非常好用", "")
        };

        AssetDatabase.CreateAsset(dialogue, FirstPath);
        Debug.Log($"对话系统：已生成一次性剧情对话 {FirstPath}");
    }

    private static void CreateDefaultDialogue()
    {
        NpcDialogue dialogue = AssetDatabase.LoadAssetAtPath<NpcDialogue>(DefaultPath);
        bool created = dialogue == null;

        if (created)
        {
            dialogue = ScriptableObject.CreateInstance<NpcDialogue>();
            dialogue.dialogueId = "blacksmith_default";
            dialogue.onceOnly = false;
        }

        dialogue.hideChosenChoices = false;   // 强化选项每次都要出现

        // 说完「你来了啊」之后给出「强化装备」选项（选中会触发 open_equipment_upgrade 动作）
        dialogue.nodes = new[]
        {
            Node("n1", "", "你来了啊", "n2"),

            ChoiceNode("n2", "", new[]
            {
                ActionChoice("upgrade", "强化装备", "open_equipment_upgrade"),
                Choice("nothing", "没什么事", "", false)
            })
        };

        if (created)
        {
            AssetDatabase.CreateAsset(dialogue, DefaultPath);
            Debug.Log($"对话系统：已生成默认可重复对话 {DefaultPath}（带「强化装备」选项）");
        }
        else
        {
            EditorUtility.SetDirty(dialogue);
            Debug.Log($"对话系统：已更新 {DefaultPath}（默认对话里加了「强化装备」选项）");
        }
    }

    private static DialogueNode Node(string _id, string _speaker, string _text, string _next)
    {
        return new DialogueNode
        {
            nodeId = _id,
            speaker = _speaker,
            text = _text,
            nextNodeId = _next,
            choices = new DialogueChoice[0]
        };
    }

    private static DialogueNode ChoiceNode(string _id, string _text, DialogueChoice[] _choices)
    {
        return new DialogueNode
        {
            nodeId = _id,
            speaker = "",
            text = _text,
            nextNodeId = "",
            choices = _choices
        };
    }

    /// <summary>带动作的选项：选完会触发 actionId（比如打开强化界面），然后结束对话。</summary>
    private static DialogueChoice ActionChoice(string _id, string _label, string _actionId)
    {
        return new DialogueChoice
        {
            choiceId = _id,
            label = _label,
            targetNodeId = "",
            returnToChoices = false,
            actionId = _actionId
        };
    }

    private static DialogueChoice Choice(string _id, string _label, string _target, bool _returnToChoices)
    {
        return new DialogueChoice
        {
            choiceId = _id,
            label = _label,
            targetNodeId = _target,
            returnToChoices = _returnToChoices
        };
    }

    #endregion
}
