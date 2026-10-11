using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class UI_HealthBar : MonoBehaviour
{
    private Entity entity => GetComponentInParent<Entity>();
    private RectTransform myTransform;
    private CharacterStats myStats => GetComponentInParent<CharacterStats>();
    private Slider slider;

    [Header("掉血残影")]
    [Tooltip("扣掉的那段血量先用这个颜色填着，再慢慢缩回去（黄色）")]
    [SerializeField] private Color damageFillColor = new Color(1f, 0.83f, 0.2f, 1f);
    [Tooltip("扣血后先停多久再开始缩（秒）")]
    [SerializeField] private float damageHoldTime = 0.18f;
    [Tooltip("收缩速度：每秒缩掉最大血量的百分之多少")]
    [SerializeField] private float damageDrainSpeed = 0.35f;

    /// <summary>
    /// 两个引用必须在 Awake 里拿：OnEnable 比 Start 早，而 OnEnable 已经把 FlipUI / UpdateHP_UI
    /// 挂到事件上了。敌人（比如史莱姆分裂）实例化完会立刻被 Flip 一下，就会在 Start 之前触发回调。
    /// </summary>
    private void Awake()
    {
        myTransform = GetComponent<RectTransform>();
        slider = GetComponentInChildren<Slider>(true);

        SetupDamageFill();
    }

    /// <summary>
    /// 给血条加一层「掉血残影」：扣血瞬间红条直接变短，黄色留在后面慢慢缩。
    /// 挂在 Slider 那个子物体上，玩家和所有怪物共用同一套逻辑。
    /// </summary>
    private void SetupDamageFill()
    {
        if (slider == null)
            return;

        UI_HealthBarDelay delay = slider.GetComponent<UI_HealthBarDelay>();

        if (delay == null)
            delay = slider.gameObject.AddComponent<UI_HealthBarDelay>();

        delay.Configure(damageFillColor, damageHoldTime, damageDrainSpeed);
    }

    private void Start()
    {
        UpdateHP_UI();
    }

    private void OnEnable()
    {
        entity.onFlipped += FlipUI;
        myStats.OnHP_Changed += UpdateHP_UI;
    }

    private void UpdateHP_UI()
    {
        if (slider == null || myStats == null)
            return;

        slider.maxValue = myStats.GetMaxHealthValue();
        slider.value = myStats.currentHealth;
    }


    private void FlipUI()
    {
        if (myTransform == null)
            return;

        myTransform.Rotate(0, 180, 0);
    }

    private void OnDisable()
    {
        if(entity!= null)
            entity.onFlipped -= FlipUI;
        if(myStats!=null)
            myStats.OnHP_Changed -= UpdateHP_UI;
    }
}
