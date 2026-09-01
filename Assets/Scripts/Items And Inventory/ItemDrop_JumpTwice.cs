using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemDrop_JumpTwice : ItemDrop
{
    public override void GenerateDrop()
    {
        if (!PlayerManager.instance.jumpTwiceUnlocked)
            DropItem(possibleDrop[0]);
    }
}
