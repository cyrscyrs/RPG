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

    /// <summary>
    /// 两个引用必须在 Awake 里拿：OnEnable 比 Start 早，而 OnEnable 已经把 FlipUI / UpdateHP_UI
    /// 挂到事件上了。敌人（比如史莱姆分裂）实例化完会立刻被 Flip 一下，就会在 Start 之前触发回调。
    /// </summary>
    private void Awake()
    {
        myTransform = GetComponent<RectTransform>();
        slider = GetComponentInChildren<Slider>(true);
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
