using System.Collections;
using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "New Item Data", menuName = "Data/Item effect")]
public class ItemEffect : ScriptableObject
{
    [TextArea]
    public string effectDescription;
    public virtual void ExecuteEffect(Transform _enemyPosition)
    {

    }

    /// <summary>带倍率的版本：默认忽略倍率，需要支持倍率的效果（比如治疗）重写它。</summary>
    public virtual void ExecuteEffect(Transform _enemyPosition, float _multiplier)
    {
        ExecuteEffect(_enemyPosition);
    }
}
