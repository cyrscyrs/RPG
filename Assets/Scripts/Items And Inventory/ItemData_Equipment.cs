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
    public int strength;
    public int agility;
    public int intelligence;
    public int vitality;

    [Header("Offensive stats")]
    public int damage;
    public int critChance;
    public int critPower;

    [Header("Defensive stats")]
    public int health;
    public int armor;
    public int evasion;
    public int magicResistance;

    [Header("Magic stats")]
    public int fireDamage;
    public int iceDamage;
    public int lightningDamage;

    [Header("Craft requirements")]
    public List<InventoryItem> craftingMaterials;

    private int descriptionLength;

    public void Effect(Transform _enemyPosition)
    {
        foreach (var item in itemEffects)
        {
            item.ExecuteEffect(_enemyPosition);
        }
    }

    public string GetChineseEquipmentType()
    {
        return equipmentType switch
        {
            EquipmentType.Weapon => "ÎäÆ÷",
            EquipmentType.Armor => "ÒÂ·þ",
            EquipmentType.Amulet => "»¤·û",
            EquipmentType.Flask => "ÑªÆ¿",
            _ => ""
        };
    }
    public void AddModifiers()
    {
        PlayerStats playerStats = PlayerManager.instance.player.GetComponent<PlayerStats>();

        playerStats.strength.AddModifier(strength);
        playerStats.agility.AddModifier(agility);
        playerStats.intelligence.AddModifier(intelligence);
        playerStats.vitality.AddModifier(vitality);

        playerStats.damage.AddModifier(damage);
        playerStats.critChance.AddModifier(critChance);
        playerStats.armor.AddModifier(armor);

        playerStats.maxHealth.AddModifier(health);
        playerStats.armor.AddModifier(armor);
        playerStats.evasion.AddModifier(evasion);
        playerStats.magicResistance.AddModifier(magicResistance);

        playerStats.fireDamage.AddModifier(fireDamage);
        playerStats.iceDamage.AddModifier(iceDamage);
        playerStats.lightningDamage.AddModifier(lightningDamage);
    }

    public void RemoveModifiers() 
    {
        PlayerStats playerStats = PlayerManager.instance.player.GetComponent<PlayerStats>();

        playerStats.strength.RemoveModifier(strength);
        playerStats.agility.RemoveModifier(agility);
        playerStats.intelligence.RemoveModifier(intelligence);
        playerStats.vitality.RemoveModifier(vitality);

        playerStats.damage.RemoveModifier(damage);
        playerStats.critChance.RemoveModifier(critChance);
        playerStats.armor.RemoveModifier(armor);

        playerStats.maxHealth.RemoveModifier(health);
        playerStats.armor.RemoveModifier(armor);
        playerStats.evasion.RemoveModifier(evasion);
        playerStats.magicResistance.RemoveModifier(magicResistance);

        playerStats.fireDamage.RemoveModifier(fireDamage);
        playerStats.iceDamage.RemoveModifier(iceDamage);
        playerStats.lightningDamage.RemoveModifier(lightningDamage);
    }

    public override string GetDescription()
    {
        sb.Length = 0;
        descriptionLength = 0;

        AddItemDescription(strength, "Á¦Á¿");
        AddItemDescription(agility, "Ãô½Ý");
        AddItemDescription(intelligence, "ÖÇÁ¦");
        AddItemDescription(vitality, "ÌåÖÊ");

        AddItemDescription(damage, "¹¥»÷Á¦");
        AddItemDescription(critChance, "±©»÷");
        AddItemDescription(critPower, "±¬ÉË");

        AddItemDescription(health, "ÉúÃü");
        AddItemDescription(evasion, "ÉÁ±Ü");
        AddItemDescription(armor, "»¤¼×");
        AddItemDescription(magicResistance, "Ä§¿¹");

        AddItemDescription(fireDamage, "»ðÉË");
        AddItemDescription(iceDamage, "±ùÉË");
        AddItemDescription(lightningDamage, "À×ÉË");





        for (int i = 0; i < itemEffects.Length; i++)
        {
            if (itemEffects[i].effectDescription.Length > 0)
            {
                sb.AppendLine();
                sb.AppendLine("Î¨Ò»: " + itemEffects[i].effectDescription);
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
