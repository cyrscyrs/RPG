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
            // 热更下来的优先（远端能改成长曲线），其次用 Inspector 上配的，最后用代码默认值
            if (Tuning.EquipmentGrowthHotfix != null)
                return Tuning.EquipmentGrowthHotfix;

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
    /// <summary>这件装备还有哪几个里程碑等级（默认 10 / 20）。</summary>
    public IEnumerable<int> GetMilestones(ItemData_Equipment _item)
    {
        if (_item == null)
            yield break;

        EquipmentGrowthConfig cfg = Config;

        if (cfg.firstMilestone > 0)
            yield return cfg.firstMilestone;

        if (cfg.secondMilestone > 0 && cfg.secondMilestone != cfg.firstMilestone)
            yield return cfg.secondMilestone;
    }

    /// <summary>里程碑效果的短名（列表里用的），比如「剑气」「斩杀」。没有对应效果就返回空串。</summary>
    public string GetMilestoneSummary(ItemData_Equipment _item, int _milestone)
    {
        if (_item == null)
            return string.Empty;

        EquipmentGrowthConfig cfg = Config;
        bool first = _milestone == cfg.firstMilestone;
        bool second = _milestone == cfg.secondMilestone;

        if (!first && !second)
            return string.Empty;

        switch (_item.equipmentType)
        {
            case EquipmentType.Weapon:
                return first ? "剑气" : "斩杀";

            case EquipmentType.Armor:
                return first ? "反弹伤害" : "免死";

            case EquipmentType.Amulet:
                return first
                    ? $"力量/体质/敏捷/智力 各 +{cfg.amuletFlatBonus}"
                    : $"全属性 +{Mathf.RoundToInt(cfg.amuletPercentBonus * 100f)}%";

            case EquipmentType.Flask:
                return first
                    ? $"恢复量 +{Mathf.RoundToInt(cfg.flaskHealBonus * 100f)}%"
                    : $"使用冷却 ×{cfg.flaskCooldownMultiplier:0.##}";
        }

        return string.Empty;
    }

    /// <summary>里程碑效果的完整描述（数值都取自配置），给强化预览用。</summary>
    public string GetMilestoneDescription(ItemData_Equipment _item, int _milestone)
    {
        if (string.IsNullOrEmpty(GetMilestoneSummary(_item, _milestone)))
            return string.Empty;

        EquipmentGrowthConfig cfg = Config;
        bool first = _milestone == cfg.firstMilestone;

        switch (_item.equipmentType)
        {
            case EquipmentType.Weapon:
                return first
                    ? $"剑气：每次攻击向前放出一道剑气，伤害为攻击力的 {Mathf.RoundToInt(cfg.swordAuraDamagePercent * 100f)}%"
                    : $"斩杀：攻击后敌人生命低于 {Mathf.RoundToInt(cfg.executeHealthPercent * 100f)}% 时直接斩杀";

            case EquipmentType.Armor:
                return first
                    ? $"反弹伤害：把受到的 {Mathf.RoundToInt(cfg.armorReflectPercent * 100f)}% 伤害反弹给攻击者"
                    : $"免死：致命伤害免疫一次并留 1 点血，冷却 {Mathf.RoundToInt(cfg.lethalImmunityCooldown)} 秒";

            case EquipmentType.Amulet:
                return first
                    ? $"力量 / 体质 / 敏捷 / 智力 各 +{cfg.amuletFlatBonus}"
                    : $"全属性 +{Mathf.RoundToInt(cfg.amuletPercentBonus * 100f)}%";

            case EquipmentType.Flask:
                return first
                    ? $"恢复量 +{Mathf.RoundToInt(cfg.flaskHealBonus * 100f)}%"
                    : $"使用冷却 ×{cfg.flaskCooldownMultiplier:0.##}（减半）";
        }

        return string.Empty;
    }

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
