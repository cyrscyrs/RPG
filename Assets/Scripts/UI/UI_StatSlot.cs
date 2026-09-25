using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_StatSlot : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
{
    private UI ui;

    [SerializeField] private string statName_EN;
    [SerializeField] private string statName_CH;
    [SerializeField] private StatType statType;
    [SerializeField] private TextMeshProUGUI statValueText;
    [SerializeField] private TextMeshProUGUI statNameText;


    [TextArea]
    [SerializeField] private string statDescription;
    private void OnValidate()
    {
        statName_EN = statType.ToString();
        statName_CH = GetStatName(statType);

        gameObject.name = "Stat - " + statName_EN;

        if (statNameText != null)
            statNameText.text = statName_CH;
    }

    void Start()
    {
        UpdateStatValueUI();

        ui = GetComponentInParent<UI>();
    }

    public void UpdateStatValueUI()
    {
        PlayerStats playerStats= PlayerManager.instance.player.GetComponent<PlayerStats>();

        if(playerStats != null)
        {
            statValueText.text = playerStats.GetStat(statType).GetValue().ToString();

            switch(statType)
            {
                case StatType.health: statValueText.text = playerStats.GetMaxHealthValue().ToString(); break;
                case StatType.damage: statValueText.text = playerStats.GetDamageValue().ToString(); break;
                case StatType.critPower: statValueText.text = playerStats.GetCritPowerValue().ToString(); break;
                case StatType.critChance: statValueText.text = playerStats.GetCrtiChanceValue().ToString(); break;
                case StatType.evasion: statValueText.text = playerStats.GetEvasionValue().ToString(); break;
                case StatType.magicRes: statValueText.text = playerStats.GetMagicResistance().ToString(); break;
                default:break;
            }
        }


    }

    public string GetStatName(StatType _statType)
    {
        return _statType switch
        {
            StatType.strength => "力量",
            StatType.agility => "敏捷",
            StatType.intelegence => "智力",
            StatType.vitality => "体质",
            StatType.damage => "攻击力",
            StatType.critChance => "暴击",
            StatType.critPower => "爆伤",
            StatType.health => "生命",
            StatType.armor => "护甲",
            StatType.evasion => "闪避",
            StatType.magicRes => "魔抗",
            StatType.fireDamage => "火伤",
            StatType.iceDamage => "冰伤",
            StatType.lightningDamage => "雷伤",
            _ => "",
        };
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        ui.statToolTip.ShowStatToolTip(statDescription);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ui.statToolTip.HideStatToolTip();
    }

}
