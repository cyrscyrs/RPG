using UnityEngine;

/// <summary>
/// 装备的通用升级成长曲线配置（右键 Assets → Create → Data → Equipment Growth Config 创建）。
///
/// 曲线规则（等级 0 起步，最高 20 级）：
/// - 1~9 级：每级给装备的基础属性加一个「固定值」（每件装备自己的 growthFlatPerLevel）
/// - 10 级：第一次特殊效果
/// - 11~19 级：每级给装备的属性加一个「固定百分比」（每件装备自己的 growthPercentPerLevel）
/// - 20 级：第二次特殊效果（满级）
///
/// 四个部位的特殊效果数值都在这个资产里统一调，改一处就影响所有装备。
/// </summary>
[CreateAssetMenu(fileName = "Equipment Growth Config", menuName = "Data/Equipment Growth Config")]
public class EquipmentGrowthConfig : ScriptableObject
{
    [Header("等级曲线")]
    [Tooltip("最高等级（初始 0 级）")]
    public int maxLevel = 20;

    [Tooltip("第一次给特殊效果的等级")]
    public int firstMilestone = 10;

    [Tooltip("第二次给特殊效果的等级（一般是满级）")]
    public int secondMilestone = 20;

    [Header("武器：10 级剑气 / 20 级斩杀")]
    [Tooltip("剑气伤害 = 玩家攻击力的这个比例（0.5 = 50%）")]
    [Range(0f, 3f)] public float swordAuraDamagePercent = 0.5f;

    [Tooltip("剑气飞行速度")]
    public float swordAuraSpeed = 12f;

    [Tooltip("剑气存在时间（秒）")]
    public float swordAuraLifeTime = 0.6f;

    [Tooltip("剑气的命中半径")]
    public float swordAuraRadius = 1.1f;

    [Tooltip("剑气的特效预制体（留空就只有看不见的判定）。可以拖 Assets/Prefabs/FX 里的特效")]
    public GameObject swordAuraPrefab;

    [Tooltip("斩杀门槛：敌人生命值低于最大生命值的这个比例时直接斩杀（0.1 = 10%）")]
    [Range(0f, 0.5f)] public float executeHealthPercent = 0.1f;

    [Header("衣服：10 级反弹伤害 / 20 级免疫一次致命伤害")]
    [Tooltip("反弹伤害 = 受到伤害的这个比例（0.5 = 50%）")]
    [Range(0f, 2f)] public float armorReflectPercent = 0.5f;

    [Tooltip("免死触发后的冷却（秒）")]
    public float lethalImmunityCooldown = 60f;

    [Header("护符：10 级基础属性 +固定值 / 20 级全属性 +百分比")]
    [Tooltip("10 级时给力量、体质、敏捷、智力各加多少")]
    public int amuletFlatBonus = 20;

    [Tooltip("20 级时全属性加成比例（0.2 = +20%）")]
    [Range(0f, 2f)] public float amuletPercentBonus = 0.2f;

    [Header("血瓶：10 级恢复效果 +100% / 20 级使用 CD 减半")]
    [Tooltip("10 级时恢复效果的额外比例（1 = 恢复量翻倍）")]
    [Range(0f, 5f)] public float flaskHealBonus = 1f;

    [Tooltip("20 级时使用冷却的倍率（0.5 = CD 减半）")]
    [Range(0.1f, 1f)] public float flaskCooldownMultiplier = 0.5f;

    [Header("强化消耗（同类型的装备需求相同）")]
    [Tooltip("每个部位一条：需要的材料与金币按「目标等级」缩放")]
    [SerializeField] private EquipmentUpgradeRequirement[] upgradeRequirements;

    public EquipmentUpgradeRequirement GetUpgradeRequirement(EquipmentType _type)
    {
        if (upgradeRequirements != null)
        {
            foreach (EquipmentUpgradeRequirement requirement in upgradeRequirements)
            {
                if (requirement != null && requirement.equipmentType == _type)
                    return requirement;
            }
        }

        return null;
    }

    /// <summary>这个部位需要哪些材料（同类型相同，可以多种）。</summary>
    public EquipmentUpgradeMaterial[] GetUpgradeMaterials(EquipmentType _type)
    {
        EquipmentUpgradeRequirement requirement = GetUpgradeRequirement(_type);

        return requirement != null ? requirement.materials : null;
    }

    /// <summary>强化到目标等级时，某一种材料需要多少个。</summary>
    public int GetUpgradeMaterialAmount(EquipmentType _type, ItemData _material, int _targetLevel)
    {
        EquipmentUpgradeRequirement requirement = GetUpgradeRequirement(_type);

        return requirement != null ? requirement.GetAmount(_material, _targetLevel) : 0;
    }

    /// <summary>强化到目标等级需要的金币（同类型相同）。</summary>
    public int GetUpgradeCurrencyCost(EquipmentType _type, int _targetLevel)
    {
        EquipmentUpgradeRequirement requirement = GetUpgradeRequirement(_type);

        if (requirement == null)
            return 0;

        return requirement.currencyBase + requirement.currencyPerLevel * Mathf.Max(1, _targetLevel);
    }
}

/// <summary>强化需要的一种材料（同类型装备共用同一套）。</summary>
[System.Serializable]
public class EquipmentUpgradeMaterial
{
    [Tooltip("材料")]
    public ItemData material;

    [Tooltip("基础数量（不随等级变化，可以填 0）")]
    public int baseAmount;

    [Tooltip("每级增加的数量：这个数 × 目标等级")]
    public int amountPerLevel = 1;

    /// <summary>升到目标等级需要多少个。</summary>
    public int GetAmount(int _targetLevel)
        => material == null ? 0 : Mathf.Max(0, baseAmount) + Mathf.Max(0, amountPerLevel) * Mathf.Max(1, _targetLevel);
}

/// <summary>一种装备部位的强化需求（同类型的装备共用）。</summary>
[System.Serializable]
public class EquipmentUpgradeRequirement
{
    public EquipmentType equipmentType;

    [Tooltip("需要哪些材料，每种的数量可以不同")]
    public EquipmentUpgradeMaterial[] materials;

    [Tooltip("基础金币")]
    public int currencyBase = 100;

    [Tooltip("金币随等级增加：这个数 × 目标等级")]
    public int currencyPerLevel = 50;

    /// <summary>某一材料升到目标等级需要多少个。</summary>
    public int GetAmount(ItemData _material, int _targetLevel)
    {
        if (_material == null || materials == null)
            return 0;

        foreach (EquipmentUpgradeMaterial entry in materials)
        {
            if (entry != null && entry.material == _material)
                return entry.GetAmount(_targetLevel);
        }

        return 0;
    }
}
