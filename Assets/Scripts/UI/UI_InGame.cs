using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UI_InGame : MonoBehaviour
{
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private Slider slider;

    [Header("掉血残影")]
    [Tooltip("扣掉的那段血量先用这个颜色填着，再慢慢缩回去（黄色）")]
    [SerializeField] private Color damageFillColor = new Color(1f, 0.83f, 0.2f, 1f);
    [Tooltip("扣血后先停多久再开始缩（秒）")]
    [SerializeField] private float damageHoldTime = 0.18f;
    [Tooltip("收缩速度：每秒缩掉最大血量的百分之多少")]
    [SerializeField] private float damageDrainSpeed = 0.35f;

    [SerializeField] private Image dashImage;
    [SerializeField] private Image parryImage;
    [SerializeField] private Image crystalImage;
    [SerializeField] private Image swordImage;
    [SerializeField] private Image blackholeImage;
    [SerializeField] private Image flaskImage;

    private SkillManager skills;

    [Header("Souls info")]
    [SerializeField] private TextMeshProUGUI currentSouls;
    [SerializeField] private float soulsAmount;
    [SerializeField] private float increaseRate = 1000;
    void Start()
    {
        if (playerStats != null)
            playerStats.OnHP_Changed += UpdateHealthUI;

        skills = SkillManager.instance;
        SetupHealthBarDamageFill();

    }

    /// <summary>给屏幕上的玩家血条也加上「掉血残影」（黄色慢慢缩）。</summary>
    private void SetupHealthBarDamageFill()
    {
        if (slider == null)
            return;

        UI_HealthBarDelay delay = slider.GetComponent<UI_HealthBarDelay>();

        if (delay == null)
            delay = slider.gameObject.AddComponent<UI_HealthBarDelay>();

        delay.Configure(damageFillColor, damageHoldTime, damageDrainSpeed);
    }


    void Update()
    {
        UpdateSoulUI();

        if (Input.GetKeyDown(KeyCode.LeftShift) && skills.dash.dashUnlocked)
            SetCooldownOf(dashImage);

        if (Input.GetKeyDown(KeyCode.Q) && skills.parry.parryUnlocked)
            SetCooldownOf(parryImage);

        if (Input.GetKeyDown(KeyCode.F) && skills.crystal.crystalUnlocked)
            SetCooldownOf(crystalImage);


        if (Input.GetKeyDown(KeyCode.R) && skills.blackhole.blackholeUnlocked)
            SetCooldownOf(blackholeImage);


        if (Input.GetKeyDown(KeyCode.Alpha1) && Inventory.instance.GetEquipment(EquipmentType.Flask) != null)
            SetCooldownOf(flaskImage);

        CheckCooldownOf(dashImage, skills.dash.cooldown);
        CheckCooldownOf(parryImage, skills.parry.cooldown);
        CheckCooldownOf(crystalImage, skills.crystal.cooldown);
        
        UpdateSwordCooldownUI();
        
        CheckCooldownOf(blackholeImage, skills.blackhole.cooldown);

        CheckCooldownOf(flaskImage, Inventory.instance.flaskCooldown);
    }

    private void UpdateSoulUI()
    {
        if (soulsAmount < PlayerManager.instance.GetCurrency())
            soulsAmount += Time.deltaTime * increaseRate;
        else
            soulsAmount = PlayerManager.instance.GetCurrency();

        currentSouls.text = ((int)soulsAmount).ToString();
    }

    private void UpdateHealthUI()
    {
        slider.maxValue = playerStats.GetMaxHealthValue();
        slider.value = playerStats.currentHealth;
    }

    private void SetCooldownOf(Image _image)
    {
        if(_image.fillAmount <= 0)
            _image.fillAmount = 1;
    }

    private void CheckCooldownOf(Image _image, float _cooldown)
    {
        if(_image.fillAmount > 0)
        {
            _image.fillAmount -= 1 / _cooldown * Time.deltaTime;
        }
    }

    // 剑的冷却图标按当前状态驱动，不再依赖 catchTheSword 事件：
    // 那个事件只在 Start 里订阅、在 OnDisable 里退订，而打开任意菜单都会把 InGame_UI
    // 整个 SetActive(false)，回来时 Start 不会再跑，订阅就永久丢了，图标从此不动。
    // 现在：剑飞在外面 = 满格；剑回到手上 = 跟着 cooldownTimer 逐渐走完。
    private void UpdateSwordCooldownUI()
    {
        if (swordImage == null || skills == null || skills.sword == null)
            return;

        if (skills.sword.sword != null)
        {
            // 剑还在外面飞，这段时间用不了
            swordImage.fillAmount = 1f;
            return;
        }

        float cooldown = Mathf.Max(0.0001f, skills.sword.cooldown);
        swordImage.fillAmount = Mathf.Clamp01(skills.sword.cooldownTimer / cooldown);
    }
}
