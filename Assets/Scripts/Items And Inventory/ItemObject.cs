using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemObject : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private ItemData itemData;



    private void SetupVisuals()
    {
        if (itemData == null) return;

        GetComponent<SpriteRenderer>().sprite = itemData.icon;
        gameObject.name = "ItemObject - " + itemData.itemName;
    }


    public void SetupItem(ItemData _itemData, Vector2 _velocity)
    {
        itemData = _itemData;
        rb.velocity = _velocity;

        SetupVisuals();
    }

    public void PickUpItem()
    {
        if (!Inventory.instance.CanAddItem() && itemData.itemType == ItemType.Equipment)
        {
            rb.velocity = new Vector2(0, 7);
            PlayerManager.instance.player.fx.CreatePopUpText("²Ö¿âÒÑÂú");
            return;
        }
        if(itemData.itemType == ItemType.Ability)
        {
            ItemData_Ability item_a = itemData as ItemData_Ability;

            item_a.UnlockAbility();
        }

        AudioManager.instance.PlaySFX(18, transform);
        Inventory.instance.AddItem(itemData);
        Destroy(gameObject);
    }
}
