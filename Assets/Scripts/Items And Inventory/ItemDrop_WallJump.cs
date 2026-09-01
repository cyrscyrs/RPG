using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemDrop_WallJump : ItemDrop
{
    public override void GenerateDrop()
    {
        if (!PlayerManager.instance.wallJumpUnlocked)
            DropItem(possibleDrop[0]);
    }
}
