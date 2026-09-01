using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using UnityEngine;
using UnityEngine.EventSystems;

public class UI_SkillTreeSlot : MonoBehaviour,IPointerEnterHandler, IPointerExitHandler,ISaveManager
{
    private UI ui;
    private Image skillImage;

    [SerializeField] private int skillCost;
    [SerializeField] private string skillName;
    [SerializeField] private string skillName_CH;
    [TextArea]
    [SerializeField] private string skillDescription;
    [SerializeField] private Color skillLockedColor;

    public bool unlocked;

    [SerializeField] private UI_SkillTreeSlot[] shouldBeUnlocked;
    [SerializeField] private UI_SkillTreeSlot[] shouBeLocked;


    private void OnValidate()
    {
        gameObject.name = "SkillTreeSlot_UI - " + skillName;
    }

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => UnlockSkillSlot());
        
    }

    private void Start()
    {
        skillImage = GetComponent<Image>();
        skillImage.color = skillLockedColor;
        ui=GetComponentInParent<UI>();

        if(unlocked)
            skillImage.color = Color.white;
    }

    public void UnlockSkillSlot()
    {
        for (int i = 0; i < shouldBeUnlocked.Length; i++)
        {
            if (shouldBeUnlocked[i].unlocked == false)
            {
                Debug.Log("无法解锁技能，缺少前置技能");
                return;
            }
        }

        for (int i = 0; i < shouBeLocked.Length; i++)
        {
            if (shouBeLocked[i].unlocked == true)
            {
                Debug.Log("无法解锁技能，与其他技能冲突");
                return;
            }
        }

        if (unlocked == true || PlayerManager.instance.HaveEnoughCurrency(skillCost) == false) return;
        unlocked = true;
        skillImage.color = Color.white;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        ui.skillToolTip.ShowSkillToolTip(skillDescription,skillName_CH,skillCost);

        //弹窗位置跟随鼠标
        /*
        Vector2 mousePosition = Input.mousePosition;

        float xOffset = mousePosition.x > 600 ? -150 : 150;
        float yOffset = mousePosition.y > 320 ? -150 : 150;

        ui.statToolTip.transform.position = new Vector2(mousePosition.x + xOffset, mousePosition.y + yOffset);
        */
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ui.skillToolTip.HideToolTip();
    }

    public void LoadData(GameData _data)
    {
        if(_data.skillTree.TryGetValue(skillName,out bool value))
        {
            unlocked = value;
        }
    }

    public void SaveData(ref GameData _data)
    {
        if(_data.skillTree.TryGetValue(skillName, out bool value))
        {
            _data.skillTree.Remove(skillName);
            _data.skillTree.Add(skillName, unlocked);
        }
        else
        {
            _data.skillTree.Add(skillName, unlocked);
        }
    }
}
