using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class Inventory : MonoBehaviour, ISaveManager
{

    public static Inventory instance;

    public List<ItemData> startingItems;

    public List<InventoryItem> equipment;
    public Dictionary<ItemData_Equipment, InventoryItem> equipmentDictionary;

    public List<InventoryItem> inventory;
    public Dictionary<ItemData, InventoryItem> inventoryDictionary;

    public List<InventoryItem> stash;
    public Dictionary<ItemData, InventoryItem> stashDictionary;

    [Header("Inventory UI")]
    [SerializeField] private Transform inventorySlotParent;
    [SerializeField] private Transform stashSlotParent;
    [SerializeField] private Transform equipmentSlotParent;
    [SerializeField] private Transform statSlotParent;

    private UI_ItemSlot[] inventoryItemSlot;
    private UI_ItemSlot[] stashItemSlot;
    private UI_EquipmentSlot[] equipmentSlot;
    private UI_StatSlot[] statSlot;

    [Header("Items cooldown")]
    private float lastTimeUsedFlask = float.MinValue;
    private float lastTimeUsedArmor = float.MinValue;

    public float flaskCooldown { get; private set; }
    private float armorCooldown;

    [Header("Data base")]
    public List<ItemData> itemDataBase;
    public List<InventoryItem> loadedItems;
    public List<ItemData_Equipment> loadedEquipments;

    private void Awake()
    {
        if (instance == null)
            instance = this;
        else
            Destroy(gameObject);

        // 读档数据先存在这两个列表里，LoadData 可能早于 Start 被调用，先保证它们不是 null
        if (loadedItems == null)
            loadedItems = new List<InventoryItem>();

        if (loadedEquipments == null)
            loadedEquipments = new List<ItemData_Equipment>();
    }

    // 背包只铺一次：存档数据就绪后铺存档，没有存档才铺初始装备
    private bool inventoryApplied;

    private void Start()
    {
        equipment = new List<InventoryItem>();
        equipmentDictionary = new Dictionary<ItemData_Equipment, InventoryItem>();

        inventory = new List<InventoryItem>();
        inventoryDictionary = new Dictionary<ItemData, InventoryItem>();

        stash = new List<InventoryItem>();
        stashDictionary = new Dictionary<ItemData, InventoryItem>();

        inventoryItemSlot = inventorySlotParent.GetComponentsInChildren<UI_ItemSlot>();
        stashItemSlot = stashSlotParent.GetComponentsInChildren<UI_ItemSlot>();
        equipmentSlot = equipmentSlotParent.GetComponentsInChildren<UI_EquipmentSlot>();
        statSlot = statSlotParent.GetComponentsInChildren<UI_StatSlot>();

        // SaveManager 在它自己的 Start 里调用 LoadData，而 Unity 不保证两个 Start 谁先谁后。
        // 如果 Inventory.Start 比 SaveManager.Start 早，这时的 loadedItems/loadedEquipments 还是空的，
        // 直接 AddStartingItems() 就会把默认初始装备铺进去、把档里的真实背包丢掉（进门换场景时最明显）。
        // 所以等这一帧所有 Start 都跑完再决定：有存档数据就铺存档，没有才铺初始装备。
        StartCoroutine(ApplyInventoryAfterLoad());
    }

    private System.Collections.IEnumerator ApplyInventoryAfterLoad()
    {
        yield return null;

        if (inventoryApplied)
            yield break;

        inventoryApplied = true;
        AddStartingItems();
    }

    private void AddStartingItems()
    {
        foreach (ItemData_Equipment item in loadedEquipments)
        {
            EquipItem(item);
            Debug.Log("equip item");
        }

        if (loadedItems.Count > 0)
        {
            foreach (InventoryItem item in loadedItems)
            {
                for (int i = 0; i < item.stackSize; i++)
                {
                    AddItem(item.data);
                }
            }

            return;
        }

        for (int i = 0; i < startingItems.Count; i++)
        {
            if (startingItems[i] != null)
                AddItem(startingItems[i]);
        }
    }

    public void EquipItem(ItemData _item)
    {
        ItemData_Equipment newEquipment = _item as ItemData_Equipment;
        InventoryItem newItem = new InventoryItem(newEquipment);

        ItemData_Equipment oldItem = null;

        foreach (KeyValuePair<ItemData_Equipment, InventoryItem> item in equipmentDictionary)
        {
            if (item.Key.equipmentType == newEquipment.equipmentType)
                oldItem = item.Key;
        }

        if (oldItem != null)
        {
            UnequipItem(oldItem);
            AddItem(oldItem);
        }

        equipment.Add(newItem);
        equipmentDictionary.Add(newEquipment, newItem);
        newEquipment.AddModifiers();
        RemoveItem(_item);
    }

    public void UnequipItem(ItemData_Equipment itemToRemove)
    {
        if (equipmentDictionary.TryGetValue(itemToRemove, out InventoryItem value))
        {
            equipment.Remove(value);
            equipmentDictionary.Remove(itemToRemove);
            itemToRemove.RemoveModifiers();
        }
    }

    private void UpdateSlotUI()
    {
        for (int i = 0; i < equipmentSlot.Length; i++)
        {
            foreach (KeyValuePair<ItemData_Equipment, InventoryItem> item in equipmentDictionary)
            {
                if (item.Key.equipmentType == equipmentSlot[i].slotType)
                    equipmentSlot[i].UpdateSlot(item.Value);
            }
        }

        for (int i = 0; i < inventoryItemSlot.Length; i++)
        {
            inventoryItemSlot[i].CleanUpSlot();
        }

        for (int i = 0; i < stashItemSlot.Length; i++)
        {
            stashItemSlot[i].CleanUpSlot();
        }

        // 背包 / 仓库的格子是固定数量的池子，物品种类数可能超过格子数，取两者较小值防止越界
        int inventorySlotCount = Mathf.Min(inventory.Count, inventoryItemSlot.Length);

        for (int i = 0; i < inventorySlotCount; i++)
        {
            inventoryItemSlot[i].UpdateSlot(inventory[i]);
        }

        int stashSlotCount = Mathf.Min(stash.Count, stashItemSlot.Length);

        for (int i = 0; i < stashSlotCount; i++)
        {
            stashItemSlot[i].UpdateSlot(stash[i]);
        }

        UpdateStatUI();
    }

    public void UpdateStatUI()
    {
        for (int i = 0; i < statSlot.Length; i++)
        {
            statSlot[i].UpdateStatValueUI();
        }
    }

    public void AddItem(ItemData _item)
    {
        if (CanAddItem() && _item.itemType == ItemType.Equipment)
        {
            AddToInventory(_item);

        }
        else if (_item.itemType == ItemType.Material)
        {
            AddToStash(_item);
        }


        UpdateSlotUI();
    }

    private void AddToStash(ItemData _item)
    {
        if (stashDictionary.TryGetValue(_item, out InventoryItem value))
        {
            value.AddStack();
        }
        else
        {
            InventoryItem newItem = new InventoryItem(_item);
            stash.Add(newItem);
            stashDictionary.Add(_item, newItem);
        }
    }

    private void AddToInventory(ItemData _item)
    {
        if (inventoryDictionary.TryGetValue(_item, out InventoryItem value))
        {
            value.AddStack();
        }
        else
        {
            InventoryItem newItem = new InventoryItem(_item);
            inventory.Add(newItem);
            inventoryDictionary.Add(_item, newItem);
        }
    }

    public void RemoveItem(ItemData _item)
    {
        if (inventoryDictionary.TryGetValue(_item, out InventoryItem value))
        {
            if (value.stackSize <= 1)
            {
                inventory.Remove(value);
                inventoryDictionary.Remove(_item);
            }
            else
                value.RemoveStack();
        }

        if (stashDictionary.TryGetValue(_item, out InventoryItem stashValue))
        {
            if (stashValue.stackSize <= 1)
            {
                stash.Remove(stashValue);
                stashDictionary.Remove(_item);
            }
            else
                stashValue.RemoveStack();
        }

        UpdateSlotUI();
    }

    public bool CanAddItem()
    {
        if (inventory.Count >= inventoryItemSlot.Length)
        {
            //Debug.Log("仓库空间不足");
            return false;
        }

        return true;
    }

    public bool CanCraft(ItemData_Equipment _itemToCraft, List<InventoryItem> _requiredMaterial)
    {
        List<InventoryItem> materialToRemove = new List<InventoryItem>();

        for (int i = 0; i < _requiredMaterial.Count; i++)
        {
            if (stashDictionary.TryGetValue(_requiredMaterial[i].data, out InventoryItem stashValue))
            {
                if (stashValue.stackSize < _requiredMaterial[i].stackSize)
                {
                    Debug.Log("not enough materials");
                    return false;
                }
                else
                {
                    materialToRemove.Add(_requiredMaterial[i]);
                }
            }
            else
            {
                Debug.Log("not enough materials");
                return false;
            }
        }

        for (int i = 0; i < materialToRemove.Count; i++)
        {
            for (int j = 0; j < materialToRemove[i].stackSize; j++)
            {
                RemoveItem(materialToRemove[i].data);
            }
        }

        AddItem(_itemToCraft);
        Debug.Log("Craft" + _itemToCraft.name + "success");

        return true;
    }
    public List<InventoryItem> GetEquipmentList() => equipment;
    public List<InventoryItem> GetStashList() => stash;

    /// <summary>背包 + 仓库里一共持有多少个这个物品（强化扣材料用）。</summary>
    public int GetItemAmount(ItemData _item)
    {
        if (_item == null)
            return 0;

        int amount = 0;

        if (inventoryDictionary != null && inventoryDictionary.TryGetValue(_item, out InventoryItem inBag))
            amount += inBag.stackSize;

        if (stashDictionary != null && stashDictionary.TryGetValue(_item, out InventoryItem inStash))
            amount += inStash.stackSize;

        return amount;
    }
    public ItemData_Equipment GetEquipment(EquipmentType _type)
    {
        ItemData_Equipment equipedItem = null;

        foreach (KeyValuePair<ItemData_Equipment, InventoryItem> item in equipmentDictionary)
        {
            if (item.Key.equipmentType == _type)
                equipedItem = item.Key;
        }

        return equipedItem;
    }

    public void UseFlask()
    {
        ItemData_Equipment currentFlask = GetEquipment(EquipmentType.Flask);

        if (currentFlask == null) return;

        bool canUseFlask = Time.time > lastTimeUsedFlask + flaskCooldown;

        if (canUseFlask)
        {
            // 血瓶 10 级：恢复量 +100%（翻倍）；20 级：冷却减半
            float healMultiplier = EquipmentLevelManager.Instance != null
                ? EquipmentLevelManager.Instance.GetFlaskHealMultiplier(currentFlask)
                : 1f;

            float cooldownMultiplier = EquipmentLevelManager.Instance != null
                ? EquipmentLevelManager.Instance.GetFlaskCooldownMultiplier(currentFlask)
                : 1f;

            flaskCooldown = currentFlask.itemCooldown * cooldownMultiplier;
            currentFlask.Effect(null, healMultiplier);
            lastTimeUsedFlask = Time.time;
        }
        else
        {
            Debug.Log("血瓶冷却中");
        }
    }

    public bool CanUseArmor()
    {
        ItemData_Equipment currentArmor = GetEquipment(EquipmentType.Armor);

        if (currentArmor == null) return false;

        if (Time.time > lastTimeUsedArmor + armorCooldown)
        {
            armorCooldown = currentArmor.itemCooldown;
            lastTimeUsedArmor = Time.time;
            return true;
        }

        Debug.Log("装甲冷却中");
        return false;
    }

    public void LoadData(GameData _data)
    {
        // 先把上一次可能残留的清掉，避免重复读档时叠起来
        loadedItems.Clear();
        loadedEquipments.Clear();

        foreach (KeyValuePair<string, int> pair in _data.inventory)
        {
            foreach (var item in itemDataBase)
            {
                if (item != null && item.itemId == pair.Key)
                {
                    InventoryItem itemToLoad = new InventoryItem(item);
                    itemToLoad.stackSize = pair.Value;

                    loadedItems.Add(itemToLoad);
                }
            }
        }

        foreach (string loadedItemId in _data.equipmentIds)
        {
            foreach (var item in itemDataBase)
            {
                if (item != null && loadedItemId == item.itemId)
                {
                    Debug.Log("load equipment");
                    loadedEquipments.Add(item as ItemData_Equipment);
                }
            }
        }
    }

    public void SaveData(ref GameData _data)
    {
        _data.inventory.Clear();
        _data.equipmentIds.Clear();

        foreach (KeyValuePair<ItemData, InventoryItem> pair in inventoryDictionary)
        {
            _data.inventory.Add(pair.Key.itemId, pair.Value.stackSize);
        }

        foreach (KeyValuePair<ItemData, InventoryItem> pair in stashDictionary)
        {
            _data.inventory.Add(pair.Key.itemId, pair.Value.stackSize);
        }

        foreach (KeyValuePair<ItemData_Equipment, InventoryItem> pair in equipmentDictionary)
        {
            _data.equipmentIds.Add(pair.Key.itemId);
        }
    }


#if UNITY_EDITOR
    [ContextMenu("Fill up item data base")]
    private void FillupItemDataBase()=>itemDataBase = new List<ItemData>(GetItemDataBase());

    private List<ItemData> GetItemDataBase()
    {
        List<ItemData> itemDataBase = new List<ItemData>();
        string[] assetNames = AssetDatabase.FindAssets("", new[] { "Assets/Data/Items" });

        foreach (string SOName in assetNames)
        {
            var SOpath = AssetDatabase.GUIDToAssetPath(SOName);
            var itemData = AssetDatabase.LoadAssetAtPath<ItemData>(SOpath);
            itemDataBase.Add(itemData);
        }

        return itemDataBase;
    }
#endif

}
