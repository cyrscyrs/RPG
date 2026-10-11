using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GameData
{
    public bool wallJumpUnlocked;
    public bool jumpTwiceUnlocked;

    public int currency;
    public SerializableDictionary<string, bool> skillTree;
    public SerializableDictionary<string, int> inventory;
    public List<string> equipmentIds;


    public SerializableDictionary<string, bool> checkpoints;
    public string closestCheckpointId;

    // 被玩家彻底摧毁的墙体：key = 墙体的 wallId，value = 是否已摧毁
    public SerializableDictionary<string, bool> destroyedWalls;

    // 怪物图鉴的累计击杀数：key = 怪物 ID，value = 击杀次数
    public SerializableDictionary<string, int> monsterKills;

    // NPC 对话：已完成的一次性对话 / 已选过的分支选项
    public SerializableDictionary<string, bool> completedDialogues;
    public SerializableDictionary<string, bool> selectedChoices;

    // 装备等级：key = 物品 ID，value = 等级（0 ~ 20）
    public SerializableDictionary<string, int> equipmentLevels;

    // 赏金任务
    public List<string> bountyOffers;
    public SerializableDictionary<string, bool> bountyAccepted;
    public SerializableDictionary<string, bool> bountySubmitted;
    public SerializableDictionary<string, int> bountyProgress;

    // 宝箱：key = 宝箱 ID，value = 是否已经打开过（决定第一次奖励还给不给）
    public SerializableDictionary<string, bool> openedChests;


    public int lostCurrencyAmount;
    public float lostPositionX;
    public float lostPositionY;

    // 最近一次存档时玩家所在的场景。主菜单的「继续游戏」靠它决定该进哪个场景
    // （进哪个场景 + closestCheckpointId，就是「最新存档的场景 + 存档位置」）。
    // 老存档没有这个字段，读出来是 null，调用方退回默认场景。
    public string lastSceneName;

    public Vector3 lastGroundedPos;

    public SerializableDictionary<string, float> volumeSettings;

    public GameData()
    {
        this.lostCurrencyAmount = 0;
        this.lostPositionX = 0;
        this.lostPositionY = 0;
        lastSceneName = string.Empty;
        lastGroundedPos = new Vector3(0, 0);

        this.currency = 0;
        skillTree = new SerializableDictionary<string, bool>();
        inventory = new SerializableDictionary<string, int>();
        equipmentIds = new List<string>();


        checkpoints = new SerializableDictionary<string, bool>();
        closestCheckpointId= string.Empty;

        destroyedWalls = new SerializableDictionary<string, bool>();
        monsterKills = new SerializableDictionary<string, int>();

        completedDialogues = new SerializableDictionary<string, bool>();
        selectedChoices = new SerializableDictionary<string, bool>();
        equipmentLevels = new SerializableDictionary<string, int>();

        bountyOffers = new List<string>();
        bountyAccepted = new SerializableDictionary<string, bool>();
        bountySubmitted = new SerializableDictionary<string, bool>();
        bountyProgress = new SerializableDictionary<string, int>();

        openedChests = new SerializableDictionary<string, bool>();

        volumeSettings = new SerializableDictionary<string, float>();

    }
}
