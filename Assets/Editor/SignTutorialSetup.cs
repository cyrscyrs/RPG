using UnityEditor;
using UnityEngine;

/// <summary>
/// 标牌（告示牌）的示例对话 + 一键配置：
/// - 菜单「Tools/对话/生成标牌教学对话」：生成 Assets/Dialogue/Sign_Tutorial.asset
/// - 菜单「Tools/对话/给场景里的标牌配置对话」：给场景里的 sign 挂上 DialogueNpc 并接好这段对话
///
/// 标牌本身是纯图片（SpriteRenderer），没有碰撞体也没关系：DialogueNpc 是按距离判断的。
/// 靠近后显示「按 W 互动」，按 W 弹出对话框显示教学文字。
/// </summary>
public static class SignTutorialSetup
{
    private const string FolderPath = "Assets/Dialogue";
    private const string TutorialPath = FolderPath + "/Sign_Tutorial.asset";
    private const string SignObjectName = "sign";

    private const string PromptFormat = "按 {0} 互动";
    private const string TutorialText = "按A、D进行左右移动，按空格跳跃";

    [MenuItem("Tools/对话/生成标牌教学对话")]
    private static void CreateDialogueAsset()
    {
        if (!AssetDatabase.IsValidFolder(FolderPath))
            AssetDatabase.CreateFolder("Assets", "Dialogue");

        NpcDialogue dialogue = AssetDatabase.LoadAssetAtPath<NpcDialogue>(TutorialPath);
        bool created = dialogue == null;

        if (created)
            dialogue = ScriptableObject.CreateInstance<NpcDialogue>();

        dialogue.dialogueId = "sign_tutorial_move";
        dialogue.onceOnly = false;              // 教学提示可以反复看
        dialogue.hideChosenChoices = true;

        dialogue.nodes = new[]
        {
            new DialogueNode
            {
                nodeId = "n1",
                speaker = "告示牌",
                text = TutorialText,
                nextNodeId = "",
                choices = new DialogueChoice[0]
            }
        };

        if (created)
        {
            AssetDatabase.CreateAsset(dialogue, TutorialPath);
            Debug.Log($"对话系统：已生成标牌教学对话 {TutorialPath}");
        }
        else
        {
            EditorUtility.SetDirty(dialogue);
            Debug.Log($"对话系统：已更新 {TutorialPath}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    [MenuItem("Tools/对话/给场景里的标牌配置对话")]
    private static void SetupSign()
    {
        GameObject sign = GameObject.Find(SignObjectName);

        if (sign == null)
        {
            EditorUtility.DisplayDialog("对话系统", $"当前场景里没找到「{SignObjectName}」。", "好");
            return;
        }

        NpcDialogue tutorial = AssetDatabase.LoadAssetAtPath<NpcDialogue>(TutorialPath);

        if (tutorial == null)
        {
            EditorUtility.DisplayDialog("对话系统", "还没生成标牌对话资产。\n先执行「Tools/对话/生成标牌教学对话」。", "好");
            return;
        }

        DialogueNpc component = sign.GetComponent<DialogueNpc>();

        if (component == null)
            component = Undo.AddComponent<DialogueNpc>(sign);

        SerializedObject serialized = new SerializedObject(component);
        serialized.FindProperty("npcName").stringValue = "告示牌";
        serialized.FindProperty("interactionRadius").floatValue = 2.8f;
        serialized.FindProperty("promptOffset").vector3Value = new Vector3(0f, 1.6f, 0f);
        serialized.FindProperty("promptFormatOverride").stringValue = PromptFormat;
        serialized.FindProperty("dialogues").arraySize = 0;
        serialized.FindProperty("defaultDialogue").objectReferenceValue = tutorial;
        serialized.ApplyModifiedProperties();

        EditorUtility.SetDirty(sign);
        Selection.activeGameObject = sign;

        Debug.Log($"对话系统：标牌「{SignObjectName}」已配置好教学对话（靠近显示「按 W 互动」，按 W 弹出）。记得保存场景。");
    }
}
