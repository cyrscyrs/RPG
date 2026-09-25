using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class UI_SkillToolTip : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI skillDescription;
    [SerializeField] private TextMeshProUGUI skillName;
    [SerializeField] private TextMeshProUGUI skillCost;

    public void ShowSkillToolTip(string _description, string _name, int _cost)
    {
        skillDescription.text = _description;
        skillName.text = _name;
        skillCost.text = "学习成本: " + _cost.ToString();
        gameObject.SetActive(true);
    }

    public void HideToolTip() => gameObject.SetActive(false);
}
