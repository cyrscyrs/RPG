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

        volumeSettings = new SerializableDictionary<string, float>();

    }
}
