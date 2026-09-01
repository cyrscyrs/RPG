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
}
