using UnityEngine;

public enum BountyObjectiveType
{
    Hunt,      // 猎杀：击杀指定怪物若干只
    Collect    // 收集：收集指定物品若干件
}

/// <summary>任务奖励的一项（金币 / 物品）。</summary>
[System.Serializable]
public class BountyReward
{
    [Tooltip("奖励金币，0 表示这一项不给金币")]
    public int currency;

    [Tooltip("奖励物品（材料或装备都可以），留空表示这一项不给物品")]
    public ItemData item;

    [Tooltip("奖励物品数量")]
    public int amount = 1;
}

/// <summary>
/// 一个赏金任务的定义（右键 Assets → Create → Data → Bounty Quest 创建）。
/// 猎杀型：填 targetMonsterId（和怪物图鉴用同一套 ID 规则，比如 skeleton）；
/// 收集型：填 targetItem（材料或装备）。
/// </summary>
[CreateAssetMenu(fileName = "New Bounty Quest", menuName = "Data/Bounty Quest")]
public class BountyQuest : ScriptableObject
{
    [Tooltip("存档用的唯一 ID，留空 = 用资产名")]
    public string questId;

    public BountyObjectiveType objectiveType = BountyObjectiveType.Hunt;

    [Tooltip("任务名")]
    public string title;

    [Tooltip("任务类型显示名（留空自动用「猎杀」/「收集」）")]
    public string typeLabel;

    [Tooltip("任务目标，例如「击杀 5 只骷髅」")]
    public string objective;

    [TextArea(2, 4)]
    [Tooltip("任务内容描述")]
    public string content;

    [Tooltip("猎杀型：目标怪物的 ID（skeleton / slime / archer …，和怪物图鉴同一套规则）")]
    public string targetMonsterId;

    [Tooltip("收集型：需要收集的物品")]
    public ItemData targetItem;

    [Tooltip("需要完成的数量")]
    public int requiredAmount = 3;

    [Tooltip("额外奖励：金币、装备、材料可以给一种或多种")]
    public BountyReward[] rewards;

    public string GetId() => string.IsNullOrEmpty(questId) ? name : questId;

    public string GetTypeLabel()
    {
        if (!string.IsNullOrEmpty(typeLabel))
            return typeLabel;

        return objectiveType == BountyObjectiveType.Hunt ? "猎杀" : "收集";
    }

    public string GetObjectiveText()
    {
        if (!string.IsNullOrEmpty(objective))
            return objective;

        if (objectiveType == BountyObjectiveType.Hunt)
            return $"击杀 {requiredAmount} 只 {targetMonsterId}";

        return $"收集 {requiredAmount} 个 {(targetItem != null ? targetItem.itemName : "？")}";
    }
}
