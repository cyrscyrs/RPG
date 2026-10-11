using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemObject : MonoBehaviour
{
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private ItemData itemData;

    [Header("掉落物底色")]
    [Tooltip("背景（预制体里那个叫 background 的子物体），留空会自动找")]
    [SerializeField] private SpriteRenderer backgroundRenderer;

    [Tooltip("材料：保持默认颜色")]
    [SerializeField] private Color materialColor = new Color(0f, 1f, 0.91673875f, 1f);

    [Tooltip("装备：红色")]
    [SerializeField] private Color equipmentColor = new Color(1f, 0.25f, 0.25f, 1f);

    [Tooltip("解锁能力的道具：金色")]
    [SerializeField] private Color abilityColor = new Color(1f, 0.843f, 0.2f, 1f);



    private void Awake()
    {
        if (backgroundRenderer == null)
        {
            Transform found = transform.Find("background");

            if (found != null)
                backgroundRenderer = found.GetComponent<SpriteRenderer>();
        }

        // 场景里手摆的掉落物也按类型上色
        ApplyBackgroundColor();
    }

    private void SetupVisuals()
    {
        if (itemData == null) return;

        GetComponent<SpriteRenderer>().sprite = itemData.icon;
        gameObject.name = "ItemObject - " + itemData.itemName;

        ApplyBackgroundColor();
    }

    /// <summary>掉落物底色：材料 = 默认色，装备 = 红色，解锁能力 = 金色。</summary>
    private void ApplyBackgroundColor()
    {
        if (backgroundRenderer == null || itemData == null)
            return;

        switch (itemData.itemType)
        {
            case ItemType.Equipment:
                backgroundRenderer.color = equipmentColor;
                break;

            case ItemType.Ability:
                backgroundRenderer.color = abilityColor;
                break;

            default:
                backgroundRenderer.color = materialColor;
                break;
        }
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
            PlayerManager.instance.player.fx.CreatePopUpText("仓库已满");
            return;
        }
        if (itemData.itemType == ItemType.Ability)
        {
            ItemData_Ability item_a = itemData as ItemData_Ability;

            if (item_a != null)
            {
                item_a.UnlockAbility();

                // 解锁蹬墙跳 / 二段跳时暂停游戏弹个提示（场景里没放弹窗就跳过，不影响解锁）
                if (AbilityUnlockUI.Instance != null)
                    AbilityUnlockUI.Instance.Show(item_a);

                // 能力（蹬墙跳 / 二段跳）解锁（45）
                if (AudioManager.instance != null)
                    AudioManager.instance.PlaySFX(45, transform);
            }
        }

        AudioManager.instance.PlaySFX(18, transform);
        Inventory.instance.AddItem(itemData);
        Destroy(gameObject);
    }
}
