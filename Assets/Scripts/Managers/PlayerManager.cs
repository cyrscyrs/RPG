using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerManager : MonoBehaviour,ISaveManager
{
    public static PlayerManager instance;
    public Player player;

    public int currency;
    public Vector3 lastGrounedPos;

    public bool wallJumpUnlocked;
    public bool jumpTwiceUnlocked;

    private void Awake()
    {
        if(instance != null)
            Destroy(instance.gameObject);
        else
            instance = this;
    }


    public bool HaveEnoughCurrency(int _price)
    {
        if(currency < _price)
            return false;

        currency -= _price;
        return true;
    }

    public int GetCurrency() => currency;

    public void LoadData(GameData _data)
    {
        currency = _data.currency;
        lastGrounedPos = _data.lastGroundedPos;
        wallJumpUnlocked = _data.wallJumpUnlocked;
        jumpTwiceUnlocked = _data.jumpTwiceUnlocked;
    }

    public void SaveData(ref GameData _data)
    {
        _data.currency = this.currency;
        _data.lastGroundedPos = this.lastGrounedPos;
        _data.wallJumpUnlocked = this.wallJumpUnlocked;
        _data.jumpTwiceUnlocked = this.jumpTwiceUnlocked;
    }
}
