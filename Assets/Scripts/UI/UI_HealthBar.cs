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

    private void Start()
    {
        myTransform = GetComponent<RectTransform>();
        slider = GetComponentInChildren<Slider>();


        UpdateHP_UI();
    }

    private void OnEnable()
    {
        entity.onFlipped += FlipUI;
        myStats.OnHP_Changed += UpdateHP_UI;
    }

    private void UpdateHP_UI()
    {
        slider.maxValue = myStats.GetMaxHealthValue();
        slider.value = myStats.currentHealth;
    }


    private void FlipUI() => myTransform.Rotate(0, 180, 0);

    private void OnDisable()
    {
        if(entity!= null)
            entity.onFlipped -= FlipUI;
        if(myStats!=null)
            myStats.OnHP_Changed -= UpdateHP_UI;
    }
}
