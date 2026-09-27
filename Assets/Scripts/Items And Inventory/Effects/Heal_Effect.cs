using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Heal effect", menuName = "Data/Item effect/Heal effect")]
public class Heal_Effect : ItemEffect
{
    [Range(0f, 1f)]
    [SerializeField] private float healPercent;

    public override void ExecuteEffect(Transform _enemyPosition)
    {
        ExecuteEffect(_enemyPosition, 1f);
    }

    /// <summary>血瓶 10 级会把恢复量乘上倍率（1 + 100%）。</summary>
    public override void ExecuteEffect(Transform _enemyPosition, float _multiplier)
    {
        PlayerStats playerStats = PlayerManager.instance.player.GetComponent<PlayerStats>();

        int healAmount = Mathf.RoundToInt(playerStats.GetMaxHealthValue() * healPercent * Mathf.Max(0f, _multiplier));

        playerStats.IncreaseHP_By(healAmount);
    }
}
