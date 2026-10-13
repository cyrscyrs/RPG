using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Stat
{
    [SerializeField]private int baseValue;

    public List<int> modifiers;

    /// <summary>
    /// 乘法修正（1 = 不变，0.7 = 打七折）。
    /// 百分比类的效果（Boss 的减防 / 减闪避）走这里，和加法修正分开，互不干扰。
    /// 刻意不加序列化：这些修正都是运行中加的，不该被烘进预制体里。
    /// </summary>
    [System.NonSerialized] public List<float> percentModifiers = new List<float>();

    public int GetValue()
    {
        int finalValue = baseValue;
        if (modifiers != null)
        {
            foreach (int modifier in modifiers)
                finalValue += modifier;
        }

        // 乘法修正最后一次性乘上去（不一个个四舍五入，避免多次取整带来的偏差）
        if (percentModifiers != null && percentModifiers.Count > 0)
        {
            float multiplier = 1f;

            foreach (float percentModifier in percentModifiers)
                multiplier *= percentModifier;

            finalValue = Mathf.RoundToInt(finalValue * multiplier);
        }

        return finalValue;
    }

    public void SetDefaultValue(int _value)
    {
        baseValue = _value;
    }

    public void AddModifier(int _modifier)
    {
        modifiers.Add(_modifier);
    }

    public void RemoveModifier(int _modifier)
    {
        modifiers.Remove(_modifier);
    }

    /// <summary>加一个乘法修正（1 = 不变）。同一个值加几次就乘几次，所以挂之前要先确保旧的那份已经撤掉。</summary>
    public void AddPercentModifier(float _percentModifier)
    {
        if (percentModifiers == null)
            percentModifiers = new List<float>();

        percentModifiers.Add(_percentModifier);
    }

    /// <summary>撤掉一个乘法修正（传入当时 AddPercentModifier 的那个值）。</summary>
    public void RemovePercentModifier(float _percentModifier)
    {
        if (percentModifiers == null)
            return;

        percentModifiers.Remove(_percentModifier);
    }
}
