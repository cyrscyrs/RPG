using UnityEngine;

/// <summary>商店里的一件商品。</summary>
[System.Serializable]
public class ShopEntry
{
    public ItemData item;

    [Tooltip("售价")]
    public int price = 100;
}

/// <summary>
/// 挂在商店 NPC 上：定义卖什么，并在对话里选了「进入商店」时打开商店界面。
/// 对话那边只要有一个选项的 actionId = "open_shop" 即可（示例对话见 Shop_Welcome.asset）。
/// </summary>
public class ShopKeeper : MonoBehaviour
{
    [SerializeField] private string shopName = "商店";

    [Tooltip("店里卖的东西：材料和装备都可以")]
    [SerializeField] private ShopEntry[] goods;

    public string ShopName => string.IsNullOrEmpty(shopName) ? name : shopName;
    public ShopEntry[] Goods => goods;

    private void Awake()
    {
        DialogueManager.OnAction += HandleAction;
    }

    private void OnDestroy()
    {
        DialogueManager.OnAction -= HandleAction;
    }

    private void HandleAction(string _actionId)
    {
        if (_actionId != ShopUI.OpenActionId)
            return;

        ShopUI ui = ShopUI.Instance != null ? ShopUI.Instance : FindObjectOfType<ShopUI>();

        if (ui != null)
            ui.Open(this);
    }
}
