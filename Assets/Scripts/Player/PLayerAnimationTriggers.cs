using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PLayerAnimationTriggers : MonoBehaviour
{
    private Player player => GetComponentInParent<Player>();

    private void AnimationTrigger()
    {
        player.AnimationTrigger();
    }

    private void AttackTrigger()
    {
        AudioManager.instance.PlaySFX(2,null);

        Collider2D[] colliders = Physics2D.OverlapCircleAll(player.attackCheck.position, player.attackCheckRadius);

        foreach(var hit in colliders)
        {
            if(hit.GetComponent<Enemy>() != null)
            {
                EnemyStats target = hit.GetComponent<EnemyStats>();
                if(target != null)
                    player.stats.DoDamage(target);

                Inventory.instance.GetEquipment(EquipmentType.Weapon)?.Effect(target.transform);
            }
        }
    }

    private void ThrowSword()
    {
        SkillManager.instance.sword.CreateSword();
    }
}
