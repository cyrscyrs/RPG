using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 装备等级管理：记录每件装备的等级、按通用曲线算属性加成、提供 10 / 20 级特殊效果的查询接口。
///
/// - 等级按「物品 ID」存在存档里（GameData.equipmentLevels），重进地图、重开游戏都保留；
/// - 升级接口：Upgrade(item) / SetLevel(item, level)，装备在身上时会立刻重算属性；
/// - 特殊效果数值全部来自 EquipmentGrowthConfig，改配置就影响所有装备。
/// 升级入口（比如铁匠）只要调用 Upgrade 即可，具体 UI 自己搭。
/// </summary>
public class EquipmentLevelManager : MonoBehaviour, ISaveManager
{
    public static EquipmentLevelManager Instance;

    [SerializeField] private EquipmentGrowthConfig config;

    [Tooltip("升完级立刻写一次存档")]
    [SerializeField] private bool saveImmediatelyOnLevelChange = true;

    private readonly Dictionary<string, int> levels = new Dictionary<string, int>(System.StringComparer.OrdinalIgnoreCase);

    private EquipmentGrowthConfig runtimeConfig;
    private float nextLethalImmunityTime;

    /// <summary>没配资产时用一份默认数值，保证功能可用。</summary>
    public EquipmentGrowthConfig Config
    {
        get
        {
            if (config != null)
                return config;

            if (runtimeConfig == null)
                runtimeConfig = ScriptableObject.CreateInstance<EquipmentGrowthConfig>();

            return runtimeConfig;
        }
    }

    public int MaxLevel => Mathf.Max(1, Config.maxLevel);

    #region 生命周期

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    #endregion

    #region 等级读写

    public int GetLevel(ItemData_Equipment _item) => _item == null ? 0 : GetLevel(_item.itemId);

    public int GetLevel(string _itemId)
    {
        if (string.IsNullOrEmpty(_itemId))
            return 0;

        return levels.TryGetValue(_itemId, out int level) ? level : 0;
    }

    /// <summary>设置等级（自动夹在 0 ~ 最高等级之间）。装备在身上时会立刻重算属性。</summary>
    public bool SetLevel(ItemData_Equipment _item, int _level)
    {
        if (_item == null)
            return false;

        int clamped = Mathf.Clamp(_level, 0, MaxLevel);

        if (clamped == GetLevel(_item))
            return false;

        levels[_item.itemId] = clamped;

        ReapplyIfEquipped(_item);

        if (saveImmediatelyOnLevelChange && SaveManager.instance != null)
            SaveManager.instance.SaveGame();

        return true;
    }

    /// <summary>升一级，满级返回 false。</summary>
    public bool Upgrade(ItemData_Equipment _item)
    {
        if (_item == null || GetLevel(_item) >= MaxLevel)
            return false;

        return SetLevel(_item, GetLevel(_item) + 1);
    }

    /// <summary>某件装备到过这个里程碑等级没有（10 级 / 20 级）。</summary>
    public bool HasMilestone(ItemData_Equipment _item, int _milestone) => _item != null && GetLevel(_item) >= _milestone;

    /// <summary>带等级的显示名，比如「木剑 +10」；0 级就是原名字。</summary>
    public string GetDisplayName(ItemData_Equipment _item)
    {
        if (_item == null)
            return string.Empty;

        int level = GetLevel(_item);

        return level > 0 ? $"{_item.itemName} +{level}" : _item.itemName;
    }

    /// <summary>强化到下一级需要的多种材料（同类型的装备完全相同）。</summary>
    public EquipmentUpgradeMaterial[] GetUpgradeMaterials(ItemData_Equipment _item)
        => _item == null ? null : Config.GetUpgradeMaterials(_item.equipmentType);

    /// <summary>强化到下一级时，某一种材料需要多少个。</summary>
    public int GetUpgradeMaterialAmount(ItemData_Equipment _item, ItemData _material)
        => _item == null ? 0 : Config.GetUpgradeMaterialAmount(_item.equipmentType, _material, GetLevel(_item) + 1);

    public int GetUpgradeCurrencyCost(ItemData_Equipment _item)
        => _item == null ? 0 : Config.GetUpgradeCurrencyCost(_item.equipmentType, GetLevel(_item) + 1);

    /// <summary>现在能不能强化；不能就把原因写进 _reason。</summary>
    public bool CanUpgrade(ItemData_Equipment _item, out string _reason)
    {
        _reason = string.Empty;

        if (_item == null || Inventory.instance == null || PlayerManager.instance == null)
        {
            _reason = "缺少必要的管理器";
            return false;
        }

        if (GetLevel(_item) >= MaxLevel)
        {
            _reason = "已经是最高等级";
            return false;
        }

        int currency = GetUpgradeCurrencyCost(_item);

        if (PlayerManager.instance.GetCurrency() < currency)
        {
            _reason = $"金币不够（需要 {currency}）";
            return false;
        }

        EquipmentUpgradeMaterial[] materials = GetUpgradeMaterials(_item);

        if (materials != null)
        {
            int targetLevel = GetLevel(_item) + 1;

            foreach (EquipmentUpgradeMaterial entry in materials)
            {
                if (entry == null || entry.material == null)
                    continue;

                int need = entry.GetAmount(targetLevel);
                int have = Inventory.instance.GetItemAmount(entry.material);

                if (have < need)
                {
                    _reason = $"{entry.material.itemName} 不够（需要 {need}，现有 {have}）";
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>扣掉金币和全部材料，然后升一级。成功返回 true。</summary>
    public bool TryUpgrade(ItemData_Equipment _item)
    {
        if (!CanUpgrade(_item, out string reason))
        {
            Debug.Log($"强化失败：{reason}");
            return false;
        }

        int targetLevel = GetLevel(_item) + 1;
        int currency = GetUpgradeCurrencyCost(_item);

        if (currency > 0 && !PlayerManager.instance.HaveEnoughCurrency(currency))
            return false;

        EquipmentUpgradeMaterial[] materials = GetUpgradeMaterials(_item);

        if (materials != null)
        {
            foreach (EquipmentUpgradeMaterial entry in materials)
            {
                if (entry == null || entry.material == null)
                    continue;

                int need = entry.GetAmount(targetLevel);

                for (int i = 0; i < need; i++)
                    Inventory.instance.RemoveItem(entry.material);
            }
        }

        return Upgrade(_item);
    }

    private void ReapplyIfEquipped(ItemData_Equipment _item)
    {
        if (Inventory.instance == null)
            return;

        if (Inventory.instance.GetEquipment(_item.equipmentType) != _item)
            return;

        // 等级变了就是加成变了：先精确撤掉旧的，再按新等级加一遍
        _item.RemoveModifiers();
        _item.AddModifiers();

        Inventory.instance.UpdateStatUI();
    }

    #endregion

    #region 成长曲线

    /// <summary>按等级算出某件装备上某个属性的最终值。</summary>
    public int CalculateStatValue(ItemData_Equipment _item, int _baseValue)
    {
        if (_item == null)
            return _baseValue;

        return CalculateStatValue(_baseValue, GetLevel(_item), _item.growthFlatPerLevel, _item.growthPercentPerLevel);
    }

    /// <summary>
    /// 通用曲线：1~9 级每级加固定值，11~19 级每级加固定百分比，10 / 20 级只给特殊效果。
    /// </summary>
    public int CalculateStatValue(int _baseValue, int _level, int _flatPerLevel, float _percentPerLevel)
    {
        EquipmentGrowthConfig cfg = Config;

        if (_baseValue == 0)
            return 0;

        // 1~9 级：每级加固定值
        int flatLevels = Mathf.Clamp(_level, 0, Mathf.Max(0, cfg.firstMilestone - 1));

        // 11~19 级：每级加「装备基础属性」的固定百分比
        int percentLevels = Mathf.Clamp(_level - cfg.firstMilestone, 0,
            Mathf.Max(0, cfg.secondMilestone - cfg.firstMilestone - 1));

        float value = _baseValue + flatLevels * _flatPerLevel + _baseValue * _percentPerLevel * percentLevels;

        return Mathf.RoundToInt(value);
    }

    #endregion

    #region 10 / 20 级的特殊效果数值

    public float SwordAuraDamagePercent => Config.swordAuraDamagePercent;
    public float ExecuteHealthPercent => Config.executeHealthPercent;
    public float ReflectPercent => Config.armorReflectPercent;
    public float LethalImmunityCooldown => Config.lethalImmunityCooldown;
    public int AmuletFlatBonus => Config.amuletFlatBonus;
    public float AmuletPercentBonus => Config.amuletPercentBonus;

    /// <summary>血瓶 10 级：恢复量倍率（1 + 100% = 2 倍）。</summary>
    public float GetFlaskHealMultiplier(ItemData_Equipment _flask)
        => HasMilestone(_flask, Config.firstMilestone) ? 1f + Config.flaskHealBonus : 1f;

    /// <summary>血瓶 20 级：冷却倍率（0.5 = 冷却减半）。</summary>
    public float GetFlaskCooldownMultiplier(ItemData_Equipment _flask)
        => HasMilestone(_flask, Config.secondMilestone) ? Config.flaskCooldownMultiplier : 1f;

    /// <summary>衣服 20 级的免死：冷却好了就返回 true 并开始冷却。</summary>
    public bool TryUseLethalImmunity()
    {
        if (Time.time < nextLethalImmunityTime)
            return false;

        nextLethalImmunityTime = Time.time + LethalImmunityCooldown;
        return true;
    }

    /// <summary>免死还剩多少秒（0 = 已经好了），给 UI 用。</summary>
    public float GetLethalImmunityRemaining() => Mathf.Max(0f, nextLethalImmunityTime - Time.time);

    #endregion

    #region 护符的里程碑属性加成

    /// <summary>
    /// 护符：10 级给力量/体质/敏捷/智力各 +20；20 级给全属性 +20%。
    /// 加进去的每一项都记进 _applied，卸下装备时能精确移除。
    /// </summary>
    public void ApplyMilestoneStatModifiers(ItemData_Equipment _item, PlayerStats _stats, List<StatValuePair> _applied)
    {
        if (_item == null || _stats == null || _item.equipmentType != EquipmentType.Amulet)
            return;

        int level = GetLevel(_item);

        if (level >= Config.firstMilestone && AmuletFlatBonus != 0)
        {
            AddAndRecord(_stats.strength, AmuletFlatBonus, _applied);
            AddAndRecord(_stats.vitality, AmuletFlatBonus, _applied);
            AddAndRecord(_stats.agility, AmuletFlatBonus, _applied);
            AddAndRecord(_stats.intelligence, AmuletFlatBonus, _applied);
        }

        if (level >= Config.secondMilestone && AmuletPercentBonus > 0f)
        {
            // 按「当前面板值」算一次快照，避免组合出的加成互相滚雪球
            foreach (Stat stat in GetAllPlayerStats(_stats))
                AddAndRecord(stat, Mathf.RoundToInt(stat.GetValue() * AmuletPercentBonus), _applied);
        }
    }

    private static void AddAndRecord(Stat _stat, int _value, List<StatValuePair> _applied)
    {
        if (_stat == null || _value == 0)
            return;

        _stat.AddModifier(_value);
        _applied.Add(new StatValuePair { stat = _stat, value = _value });
    }

    private static IEnumerable<Stat> GetAllPlayerStats(PlayerStats _stats)
    {
        yield return _stats.strength;
        yield return _stats.agility;
        yield return _stats.intelligence;
        yield return _stats.vitality;

        yield return _stats.damage;
        yield return _stats.critChance;
        yield return _stats.critPower;

        yield return _stats.maxHealth;
        yield return _stats.armor;
        yield return _stats.evasion;
        yield return _stats.magicResistance;

        yield return _stats.fireDamage;
        yield return _stats.iceDamage;
        yield return _stats.lightningDamage;
    }

    #endregion

    #region 存档

    public void LoadData(GameData _data)
    {
        levels.Clear();

        if (_data == null || _data.equipmentLevels == null)
            return;

        foreach (KeyValuePair<string, int> pair in _data.equipmentLevels)
            levels[pair.Key] = pair.Value;
    }

    public void SaveData(ref GameData _data)
    {
        if (_data == null)
            return;

        if (_data.equipmentLevels == null)
            _data.equipmentLevels = new SerializableDictionary<string, int>();

        _data.equipmentLevels.Clear();

        foreach (KeyValuePair<string, int> pair in levels)
            _data.equipmentLevels[pair.Key] = pair.Value;
    }

    #endregion
}

/// <summary>记录「给某个属性加了多少」，用来精确移除装备加成。</summary>
[System.Serializable]
public class StatValuePair
{
    public Stat stat;
    public int value;
}
