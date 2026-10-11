using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum AbilityType
{
    WallJump,
    JumpTwice
}

[CreateAssetMenu(fileName = "Ability Unlock Data", menuName = "Data/Ability")]

public class ItemData_Ability : ItemData
{
    public AbilityType abilityType;

    public void UnlockAbility()
    {
        switch (abilityType) 
        {
            case AbilityType.WallJump:PlayerManager.instance.wallJumpUnlocked=true; break;
            case AbilityType.JumpTwice:
                PlayerManager.instance.jumpTwiceUnlocked=true;
                PlayerManager.instance.player.SetupJumpLimit(true);
                break;
        }
    }

    /// <summary>给界面用的中文名（itemName 是资产内部用的英文名，不适合直接给玩家看）。</summary>
    public string GetAbilityDisplayName()
        => abilityType == AbilityType.WallJump ? "蹬墙跳" : "二段跳";

    /// <summary>解锁弹窗里的一句话说明。</summary>
    public string GetAbilityDescription()
        => abilityType == AbilityType.WallJump
            ? "贴住墙壁时再按一次空格，可以蹬墙跳起。"
            : "跳到空中后再按一次空格，可以在空中再跳一次。";
}
