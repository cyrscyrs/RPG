using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class PopUpTextFX : MonoBehaviour
{
    private TextMeshPro myText;

    [SerializeField] private float speed;
    [SerializeField] private float disappearanceSpeed;
    [SerializeField] private float colorDisappearanceSpeed;

    [SerializeField] private float lifeTime;

    [Header("字号动态变化")]
    [Tooltip("出生时的字号随机倍率（相对预制体上的字号）")]
    [SerializeField] private Vector2 birthSizeScale = new Vector2(0.85f, 1.3f);

    [Tooltip("之后字号的持续起伏幅度（0.15 = 上下浮动 15%）")]
    [SerializeField] private float sizePulseAmplitude = 0.15f;

    [Tooltip("字号起伏的快慢（每秒几个来回）")]
    [SerializeField] private float sizePulseSpeed = 3.5f;

    private float textTimer;

    private float baseFontSize;
    private float sizeScale = 1f;
    private float pulsePhase;

    void Start()
    {
        myText = GetComponent<TextMeshPro>();

        // 记下预制体上的字号，之后都按它乘倍率
        baseFontSize = myText.fontSize;

        // 每个数字出生时大小就不一样，密的时候不至于糊成一团
        sizeScale = Random.Range(birthSizeScale.x, birthSizeScale.y);
        pulsePhase = Random.Range(0f, Mathf.PI * 2f);

        textTimer = lifeTime;
    }

    // Update is called once per frame
    void Update()
    {
        transform.position = Vector2.MoveTowards(transform.position, new Vector2(transform.position.x, transform.position.y+1),speed*Time.deltaTime);
        textTimer -= Time.deltaTime;

        if(textTimer < 0)
        {
            float alpha = myText.color.a - colorDisappearanceSpeed * Time.deltaTime;
            myText.color = new Color(myText.color.r, myText.color.g, myText.color.b, alpha);

            if (myText.color.a < 50)
                speed = disappearanceSpeed;

            if(myText.color.a <= 0)
                Destroy(gameObject);
        }

        // 字号一直小幅起伏：同一只怪连爆几个数字时看起来更有层次，也不容易叠成一团
        float pulse = 1f + sizePulseAmplitude * Mathf.Sin(Time.time * sizePulseSpeed + pulsePhase);
        myText.fontSize = baseFontSize * sizeScale * pulse;
    }
}
