using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum EquipmentType
{
    Weapon,
    Armor,
    Amulet,
    Flask
}

[CreateAssetMenu(fileName = "New Item Data", menuName = "Data/Equipment")]
public class ItemData_Equipment : ItemData
{
    public EquipmentType equipmentType;

    [Header("Unique effect")]
    public float itemCooldown;
    public ItemEffect[] itemEffects;

    [Header("Major stats")]
    [Tooltip("力量，1力量提高1点伤害(攻击力)和1%暴伤")]
    public int strength;
    [Tooltip("敏捷，1敏捷增加1%闪避率和1%暴击率")]
    public int agility;
    [Tooltip("智力，1智力增加1魔法伤害和3点魔抗")]
    public int intelligence;
    [Tooltip("体质，1体质增加4点最大生命值")]
    public int vitality;

    [Header("Offensive stats")]
    [Tooltip("伤害")]
    public int damage;
    [Tooltip("暴击")]
    public int critChance;
    [Tooltip("爆伤")]
    public int critPower;

    [Header("Defensive stats")]
    [Tooltip("生命值")]
    public int health;
    [Tooltip("护甲")]
    public int armor;
    [Tooltip("闪避")]
    public int evasion;
    [Tooltip("魔抗")]
    public int magicResistance;

    [Header("Magic stats")]
    [Tooltip("火伤")]
    public int fireDamage;
    [Tooltip("冰伤")]
    public int iceDamage;
    [Tooltip("雷伤")]
    public int lightningDamage;

    [Header("Craft requirements")]
    public List<InventoryItem> craftingMaterials;

    [Header("升级成长（曲线见 EquipmentGrowthConfig）")]
    [Tooltip("1~9 级：每级给每项属性增加的固定值")]
    public int growthFlatPerLevel = 2;

    [Tooltip("11~19 级：每级给每项属性增加的百分比（0.1 = 每级 +10%）")]
    public float growthPercentPerLevel = 0.1f;

    // 当前实际加了多少（运行时缓存），升级/换装时用来精确移除
    [System.NonSerialized] private readonly List<StatValuePair> appliedModifiers = new List<StatValuePair>();

    private int descriptionLength;

    public void Effect(Transform _enemyPosition)
    {
        Effect(_enemyPosition, 1f);
    }

    /// <summary>带倍率的版本（血瓶升级后恢复量翻倍会用到）。</summary>
    public void Effect(Transform _enemyPosition, float _multiplier)
    {
        foreach (var item in itemEffects)
        {
            if (item != null)
                item.ExecuteEffect(_enemyPosition, _multiplier);
        }
    }

    public string GetChineseEquipmentType()
    {
        return equipmentType switch
        {
            EquipmentType.Weapon => "武器",
            EquipmentType.Armor => "衣服",
            EquipmentType.Amulet => "护符",
            EquipmentType.Flask => "血瓶",
            _ => ""
        };
    }
    public void AddModifiers()
    {
        PlayerStats playerStats = PlayerManager.instance.player.GetComponent<PlayerStats>();

        appliedModifiers.Clear();

        AddStatModifier(playerStats.strength, strength);
        AddStatModifier(playerStats.agility, agility);
        AddStatModifier(playerStats.intelligence, intelligence);
        AddStatModifier(playerStats.vitality, vitality);

        AddStatModifier(playerStats.damage, damage);
        AddStatModifier(playerStats.critChance, critChance);
        AddStatModifier(playerStats.critPower, critPower);

        AddStatModifier(playerStats.maxHealth, health);
        AddStatModifier(playerStats.armor, armor);
        AddStatModifier(playerStats.evasion, evasion);
        AddStatModifier(playerStats.magicResistance, magicResistance);

        AddStatModifier(playerStats.fireDamage, fireDamage);
        AddStatModifier(playerStats.iceDamage, iceDamage);
        AddStatModifier(playerStats.lightningDamage, lightningDamage);

        // 护符的 10 级（基础属性 +20）/ 20 级（全属性 +20%）加成
        if (EquipmentLevelManager.Instance != null)
            EquipmentLevelManager.Instance.ApplyMilestoneStatModifiers(this, playerStats, appliedModifiers);
    }

    public void RemoveModifiers() 
    {
        foreach (StatValuePair pair in appliedModifiers)
        {
            if (pair != null && pair.stat != null)
                pair.stat.RemoveModifier(pair.value);
        }

        appliedModifiers.Clear();
    }

    /// <summary>按装备等级算出实际加多少，再挂到属性上并记录下来。</summary>
    private void AddStatModifier(Stat _stat, int _baseValue)
    {
        if (_stat == null || _baseValue == 0)
            return;

        int value = EquipmentLevelManager.Instance != null
            ? EquipmentLevelManager.Instance.CalculateStatValue(this, _baseValue)
            : _baseValue;

        if (value == 0)
            return;

        _stat.AddModifier(value);
        appliedModifiers.Add(new StatValuePair { stat = _stat, value = value });
    }

    public override string GetDescription()
    {
        sb.Length = 0;
        descriptionLength = 0;

        // 先显示强化等级；下面的属性也会按等级换算后再显示
        if (EquipmentLevelManager.Instance != null)
        {
            int itemLevel = EquipmentLevelManager.Instance.GetLevel(this);

            if (itemLevel > 0)
            {
                sb.AppendLine($"强化等级：+{itemLevel} / {EquipmentLevelManager.Instance.MaxLevel}");
                descriptionLength++;
            }
        }

        AddItemDescription(strength, "力量");
        AddItemDescription(agility, "敏捷");
        AddItemDescription(intelligence, "智力");
        AddItemDescription(vitality, "体质");

        AddItemDescription(damage, "攻击力");
        AddItemDescription(critChance, "暴击");
        AddItemDescription(critPower, "爆伤");

        AddItemDescription(health, "生命");
        AddItemDescription(evasion, "闪避");
        AddItemDescription(armor, "护甲");
        AddItemDescription(magicResistance, "魔抗");

        AddItemDescription(fireDamage, "火伤");
        AddItemDescription(iceDamage, "冰伤");
        AddItemDescription(lightningDamage, "雷伤");





        for (int i = 0; i < itemEffects.Length; i++)
        {
            if (itemEffects[i].effectDescription.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("唯一: " + itemEffects[i].effectDescription);
                descriptionLength++;
            }
        }




        if (descriptionLength < 5)
        {
            for (int i = 0; i < 5 - descriptionLength; i++)
            {
                sb.AppendLine();
                sb.Append("");
            }
        }



        return sb.ToString();
    }



    private void AddItemDescription(int _value, string _name)
    {
        // 按装备等级换算后再显示，否则强化完面板上还是 0 级的数值
        if (EquipmentLevelManager.Instance != null)
            _value = EquipmentLevelManager.Instance.CalculateStatValue(this, _value);

        if (_value != 0)
        {
            if (sb.Length > 0)
                sb.AppendLine();

            if (_value > 0)
                sb.Append("+ " + _value + " " + _name);

            descriptionLength++;
        }
    }
}
