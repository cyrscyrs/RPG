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

        // 同一次挥击里，一个物体可能被多个 Collider 命中（例如墙体由多块碰撞体拼成），
        // 用这个集合去重，保证一次攻击只结算一次。
        HashSet<GameObject> hitObjects = new HashSet<GameObject>();

        foreach(var hit in colliders)
        {
            if (!hitObjects.Add(hit.gameObject))
                continue;

            // 可破坏物（例如 DestructibleWall）：只累计攻击次数，不走 CharacterStats 的伤害流程
            IHittable hittable = hit.GetComponent<IHittable>();
            if (hittable != null)
                hittable.TakeHit();

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
