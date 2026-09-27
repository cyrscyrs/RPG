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


    public int lostCurrencyAmount;
    public float lostPositionX;
    public float lostPositionY;

    public Vector3 lastGroundedPos;

    public SerializableDictionary<string, float> volumeSettings;

    public GameData()
    {
        this.lostCurrencyAmount = 0;
        this.lostPositionX = 0;
        this.lostPositionY = 0;
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

        volumeSettings = new SerializableDictionary<string, float>();

    }
}
